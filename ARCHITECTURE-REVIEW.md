# GameLocker — Architectural Review & Defect Audit

**Date:** 2026-07-26
**Reviewed version:** v1.2.0 (commit `bdf4e36`)
**Method:** Full static read of all C#, PowerShell, Inno Setup, NSIS and WiX sources. No .NET SDK was available in the review environment, so **nothing here was verified by execution** — every finding is traced from source and cites file:line. Findings marked *(inferred)* depend on runtime behaviour that should be confirmed on a test machine.

---

## Verdict

**This project must not be installed on a machine containing game data the user cannot afford to lose.**

GameLocker's core design is *encrypt the user's real files in place and delete the originals*. That is the highest-risk file operation a desktop application can perform, and it is implemented here without any of the safeguards that make such an operation survivable: no authenticated encryption, no file format header, no crash journal, no atomic rename, no integrity verification, no key backup, no dry-run, no reconciliation, and no tested recovery path. Several code paths **actively destroy the only remaining copy of user data**, including one tool named "emergency recovery" that overwrites plaintext with ciphertext and then deletes the ciphertext.

Separately, the NTFS permission logic contains a defect that causes **the service to deny access to its own service account**, producing exactly the "not even Administrator can get in" symptom reported. And the scheduler evaluates its state machine only on boolean *transitions*, recording a failed lock as if it had succeeded — which is exactly the "sometimes it works, sometimes it doesn't" symptom reported.

The three symptoms the maintainer reported are not mysteries. They each have a specific, identifiable line of code.

| Reported symptom | Root cause | Finding |
|---|---|---|
| Lock/unlock timer unreliable | Transition-only state machine; failure recorded as success; midnight-crossing windows evaluated against the wrong day | [S-1], [S-2], [S-3] |
| Folder permanently inaccessible even to Administrator | `DenyAccess` writes a Deny ACE for the *calling process's own SID*, which is SYSTEM when called from the service | [P-1], [P-2] |
| Game files corrupted (Marvel's Spider-Man 2) | Service encrypts **every** file including engine archives; `.dat` is classified "Safe"; no integrity check; partial-encryption states destroy originals | [D-1], [D-2], [D-6], [X-1] |

Counts: **17 Critical, 23 High, 21 Medium, 12 Low.**

---

## Contents

- [A. Data destruction](#a-data-destruction-critical)
- [B. Permanent lockout](#b-permanent-lockout)
- [C. Scheduler unreliability](#c-scheduler-unreliability)
- [D. Cryptographic design](#d-cryptographic-design)
- [E. Security vulnerabilities](#e-security-vulnerabilities)
- [F. State consistency](#f-state-consistency)
- [G. UI defects](#g-ui-defects)
- [H. Install / uninstall](#h-install--uninstall)
- [I. Process, testing, documentation](#i-process-testing-documentation)
- [Appendix: Spider-Man 2 post-mortem](#appendix-reconstructing-the-spider-man-2-loss)

---

## A. Data destruction (Critical)

### [D-1] CRITICAL — The service encrypts every file in the folder, including executables and engine archives
`src/GameLocker.Service/GameLockerService.cs:176` · `src/GameLocker.Common/Services/FolderLocker.cs:221`

`LockAllFoldersAsync` calls `_folderLocker.LockFolderAsync(folderPath)`. That method's file selector is:

```csharp
var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
    .Where(f => !f.EndsWith(".gamelocker") && !f.EndsWith(".enc"))
```

Every `.exe`, `.dll`, `.pak`, `.toc`, `.dat`, shader cache and asset archive is encrypted and the original deleted. `FolderLocker.LockFolderWithCustomExtensionsAsync` (line 421) implements the safe, extension-filtered path — **and has zero callers anywhere in the repository**. The entire per-folder extension selection UI, `FileExtensionScanner`, `FolderEncryptionSettings`, and the risk-rating system are decorative. The README, CHANGELOG and `docs/` all describe this feature as shipped and active.

**Failure scenario:** User adds `D:\Steam\steamapps\common\Spider-Man 2`, carefully ticks only `.sav` in the UI, saves. At the schedule boundary the service encrypts all ~40,000 files. Game is destroyed.

---

### [D-2] CRITICAL — A pre-existing `.enc` file causes the plaintext original to be deleted without verification
`src/GameLocker.Common/Services/FolderLocker.cs:286-292`

```csharp
// Check if encrypted version already exists
if (File.Exists(encryptedPath))
{
    // File is already encrypted, just delete original if it exists
    if (File.Exists(filePath))
        File.Delete(filePath);
    return;
}
```

The existence of a file named `<name>.enc` is treated as proof that a valid ciphertext exists. It is not verified — not by length, not by checksum, not by trial decryption.

**Failure scenario:** Lock run #1 is interrupted (power loss, service stop, disk full, BSOD) after `FileStream` created `game.pak.enc` but before the data was fully written and the original deleted. Disk now holds a truncated `game.pak.enc` **and** the intact `game.pak`. Lock run #2 hits this branch, sees the `.enc`, and **deletes the intact original**. The only surviving copy is a truncated ciphertext that will never decrypt. Permanent, silent, unrecoverable loss.

This branch converts every interrupted lock into guaranteed data loss on the next lock.

---

### [D-3] CRITICAL — Encrypt is not atomic: a crash window exists between ciphertext write and original delete
`src/GameLocker.Common/Services/FolderLocker.cs:305-315`

```csharp
using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
using (var destStream = new FileStream(encryptedPath, FileMode.Create, FileAccess.Write, FileShare.None))
{
    AesEncryptionHelper.EncryptStream(sourceStream, destStream, _masterKey, _masterIV);
}
File.Delete(filePath);
File.SetAttributes(encryptedPath, FileAttributes.Hidden);
```

There is no `FlushAsync`/`Flush(true)` to force the ciphertext to stable storage before the original is deleted. On a power loss, NTFS may have journalled the metadata (file exists, original deleted) while the data extents are still in the write cache. Result: a zero-length or partial `.enc` and no original. Combined with [D-2], the next run finishes the job.

There is also no `File.Replace`/`MoveFile` atomic swap and no temp-file-then-rename discipline anywhere in the codebase.

---

### [D-4] CRITICAL — Decrypt overwrites the original filename before verifying the plaintext is correct
`src/GameLocker.Common/Services/FolderLocker.cs:378-385`

```csharp
using (var sourceStream = new FileStream(encryptedPath, ...))
using (var destStream = new FileStream(originalPath, FileMode.Create, FileAccess.Write, FileShare.None))
{
    AesEncryptionHelper.DecryptStream(sourceStream, destStream, _masterKey, _masterIV);
}
File.Delete(encryptedPath);
```

`FileMode.Create` truncates any existing file at `originalPath`. Because AES-CBC with PKCS7 has **no authentication** ([D-6]), decryption with a wrong key, wrong IV, or a corrupted ciphertext frequently *succeeds* — it emits garbage. That garbage is written over the original filename and the ciphertext is then deleted. There is no way back.

---

### [D-5] CRITICAL — `ForceDecrypt` destroys data when decryption fails, by design
`ForceDecrypt/Program.cs:84-103`

```csharp
catch (Exception decryptEx)
{
    Console.WriteLine($"  ❌ Decrypt failed: {decryptEx.Message}");
    Console.WriteLine($"  🔧 Attempting raw file recovery...");
    if (!File.Exists(originalFile))
    {
        // Copy the encrypted file without .enc extension as last resort
        File.Copy(encFile, originalFile, true);
    }
    File.Delete(encFile);   // <-- deletes the only ciphertext
    failCount++;
    continue;
}
```

When decryption fails — i.e. exactly when the data is still fully recoverable given the right key — the tool copies the **ciphertext** to the plaintext filename and then **deletes the ciphertext**. The user is left with an unusable file and no ciphertext to retry against. The program then prints:

> "⚠️ Some files failed to decrypt but have been recovered where possible. Try launching the game - it may still work with these files."

This is false. Nothing was recovered. The tool named "EMERGENCY FILE RECOVERY" is the most destructive code in the repository.

Compounding it: lines 33-40 fall back to `keys.dat`/`iv.dat` (the **configuration** key pair) when `folder_key.dat` is missing. Those keys will never decrypt file data, so every file fails, and the destructive branch above then runs on **every file in the folder**.

---

### [D-6] CRITICAL — No integrity protection: corrupted ciphertext decrypts to garbage instead of failing
`src/GameLocker.Common/Encryption/AesEncryptionHelper.cs:150-211`

The format is raw AES-256-CBC + PKCS7. There is no MAC, no AEAD mode, no checksum, no length field, no magic bytes, no version byte, no key identifier. Consequences:

- A truncated or bit-rotted ciphertext has roughly a 1-in-256 chance of producing valid PKCS7 padding on its final block, in which case decryption returns **no error** and emits corrupt plaintext, which is then written over the original ([D-4]).
- Even when padding fails, CBC decrypts and emits all preceding blocks before throwing — `DecryptStream` uses `cs.CopyTo(output)`, so the destination file has already been partially written with garbage when the exception fires. The catch handler at line 405 deletes it, but the original is already gone.
- There is no way to distinguish "this is a GameLocker file" from "this is some other `.enc` file". See [D-7].
- Padding-oracle-style issues are irrelevant here, but the absence of authentication is what makes silent corruption possible.

`docs/05-troubleshooting.md` has an entire section titled *"Padding is Invalid" Error* — the project already knows this happens routinely.

---

### [D-7] CRITICAL — Any file the game legitimately ships named `*.enc` will be destroyed on unlock
`src/GameLocker.Common/Services/FolderLocker.cs:248` and `:355`

```csharp
var encryptedFiles = Directory.GetFiles(folderPath, "*.enc", SearchOption.AllDirectories);
...
var originalPath = encryptedPath.Substring(0, encryptedPath.Length - 4);
```

Unlock globs `*.enc` with no ownership check. Many games ship encrypted asset files with a `.enc` extension. Those files:
1. are correctly *skipped* during encryption (line 222), then
2. are **decrypted** on unlock with GameLocker's key, producing garbage,
3. written to the name with `.enc` stripped (creating a junk file), and
4. the game's real file is deleted.

Also note `Substring(len-4)` is a blind truncation with no `.enc` suffix check in this path — unlike `AesEncryptionHelper.DecryptFileAsync:132` which does validate. The `FolderLocker` path does not.

---

### [D-8] CRITICAL — Directory traversal follows junctions and symlinks, encrypting files outside the target folder
`src/GameLocker.Common/Services/FolderLocker.cs:221`, `248`

`Directory.GetFiles(path, "*", SearchOption.AllDirectories)` follows NTFS junctions and directory symlinks. Game installs commonly contain junctions (Steam library moves, `Program Files` redirects, OneDrive/mklink setups, Epic manifests). A junction inside the target folder pointing at `C:\` or at another game directory causes GameLocker to encrypt and delete files far outside the folder the user selected. There is no `FileAttributes.ReparsePoint` check anywhere in the codebase.

---

### [D-9] HIGH — Files locked by a running process are skipped silently, leaving a half-encrypted install
`src/GameLocker.Common/Services/FolderLocker.cs:227-236`

```csharp
catch (Exception ex)
{
    // Log but continue with other files
    Console.WriteLine($"Failed to encrypt {filePath}: {ex.Message}");
}
```

`FileShare.None` guarantees failure for any file the game (or an anti-cheat driver, or Steam, or Windows Defender) currently holds open. Those failures are written to `Console` — which for a Windows Service goes nowhere — and the loop continues. The folder is then marked `Locked` and ACL-denied. The install is now a mixture of encrypted and plaintext files: unbootable, and its true state recorded nowhere.

The same swallow-and-continue exists in the decrypt loop (line 253-261), so a partially-failed unlock also reports success.

---

### [D-10] HIGH — No free-space check before encryption
`src/GameLocker.Common/Services/FolderLocker.cs:268`

Encryption is per-file (write `.enc`, delete original), so peak overhead is roughly the size of the largest single file — but on a nearly-full drive holding a 120 GB game with a 40 GB archive, this fails mid-write. The result is a partial `.enc` plus an intact original, which is precisely the state [D-2] converts into permanent loss.

---

### [D-11] HIGH — Read-only, system, and hidden attributes cause silent skips
`src/GameLocker.Common/Services/FolderLocker.cs:312`

`File.Delete` throws `UnauthorizedAccessException` on a read-only file. That exception is caught by the generic handler at line 333, which deletes the freshly written `.enc` and rethrows — so the ciphertext is discarded and the original survives. Acceptable in isolation, but the caller ([D-9]) swallows the rethrow, so the folder is still marked fully locked.

---

### [D-12] HIGH — Path length limits are not handled
Game installs routinely exceed 260 characters, and appending `.enc` adds four more. Without `<LongPathsEnabled>` in an app manifest (there is no manifest — see [G-1]) or `\\?\` prefixing, `PathTooLongException` is thrown per file and swallowed by [D-9].

---

### [D-13] MEDIUM — `File.SetAttributes(path, FileAttributes.Hidden)` discards all other attributes
`src/GameLocker.Common/Services/FolderLocker.cs:315`, `:199`

This *replaces* the attribute set rather than OR-ing into it. `Archive`, `ReadOnly`, `NotContentIndexed`, `Compressed` flags are dropped. Backup software that keys on the Archive bit will silently stop backing these files up. Attributes are never restored on decrypt.

---

### [D-14] MEDIUM — `EndsWith` filters run against the full path and are culture-sensitive
`src/GameLocker.Common/Services/FolderLocker.cs:222`, `477`

`!f.EndsWith(".gamelocker") && !f.EndsWith(".enc")` uses the culture-sensitive overload and matches on the full path string. A file named `mysave.gamelocker` deep in the tree is silently exempt from encryption; a folder path ending in `.enc` behaves unexpectedly. Should be `Path.GetExtension(...).Equals(..., StringComparison.OrdinalIgnoreCase)`.

---

### [D-15] MEDIUM — The lock marker is written before any encryption occurs
`src/GameLocker.Common/Services/FolderLocker.cs:81-84`

`CreateLockMarkerAsync` runs first, then `EncryptFolderContentsAsync`. If encryption throws wholesale (permissions, disk full), the folder is marked `Locked` while remaining entirely plaintext, and `AclHelper.DenyAccess` at line 87 is never reached — so it is neither encrypted nor access-restricted, but reports as locked. False security.

---

### [D-16] LOW — Unreachable dead code in both retry loops
`src/GameLocker.Common/Services/FolderLocker.cs:328`, `:398`

```csharp
catch (IOException ioEx) when (attempt < maxRetries)
{
    ...
    if (attempt == maxRetries)   // unreachable: filter already excluded this
        throw new Exception(...);
}
```

The exception filter `when (attempt < maxRetries)` makes the inner `attempt == maxRetries` check impossible. Harmless today (the final attempt falls through to the generic `catch` which does rethrow), but it signals the retry logic was never traced.

---

### [D-17] LOW — `GC.Collect()` / `WaitForPendingFinalizers()` used as a file-handle workaround
`src/GameLocker.Common/Services/FolderLocker.cs:300-301`, `:373-374`

Forcing a full blocking GC between retries to "release handles" is cargo-cult; the handles belong to other processes. On a large folder this runs thousands of times and will dominate lock duration.

---

## B. Permanent lockout

### [P-1] CRITICAL — `DenyAccess` denies the service's own account, permanently locking the folder away from the only process that can unlock it
`src/GameLocker.Common/Security/AclHelper.cs:25-27`, `:41-47`, `:76-86`

```csharp
var currentUser = WindowsIdentity.GetCurrent();
var userSid = currentUser.User;
...
// FIRST: Add explicit ALLOW rule for SYSTEM so it can still manage the folder
var systemAllowRule = new FileSystemAccessRule(systemSid, FullControl, ..., AccessControlType.Allow);
security.SetAccessRule(systemAllowRule);
...
// Deny access to current user specifically
new FileSystemAccessRule(userSid, FullControl, ..., AccessControlType.Deny)
...
foreach (var rule in denyRules) security.SetAccessRule(rule);
```

The Windows Service runs as **LocalSystem**. Therefore `WindowsIdentity.GetCurrent().User` is `S-1-5-18` — the SYSTEM SID. The method adds `Allow SYSTEM FullControl` at line 47 and then, thirty lines later, adds `Deny SYSTEM FullControl` at line 86. `SetAccessRule` replaces rules of the *same* `AccessControlType`, so the Allow and the Deny both persist in the DACL. **Deny takes precedence.**

The comment on lines 31-33 states the requirement correctly:

> `// CRITICAL: Get SYSTEM account SID - the service runs as SYSTEM and must retain access`

…and the code four blocks later violates it.

**Failure scenario (matches the reported symptom exactly):**
1. Service locks `D:\Games\X`. DACL now contains `Deny S-1-5-18 FullControl`.
2. Schedule boundary arrives. Service calls `UnlockFolderAsync` → `AclHelper.AllowAccess(folderPath)`.
3. `AllowAccess` line 103 calls `directoryInfo.GetAccessControl()`, which requires `READ_CONTROL` — part of the FullControl the service just denied itself. It throws.
4. That throw is caught and logged as a *warning* (`FolderLocker.cs:127-131`) and execution continues.
5. `DecryptFolderContentsAsync` then calls `Directory.GetFiles`, which throws `UnauthorizedAccessException`, propagating to the outer catch at `FolderLocker.cs:145`. `info.State` stays `Unknown`.
6. Service logs "Failed to unlock folder". The folder remains encrypted and denied. Every subsequent attempt fails identically.

The folder is now beyond the reach of the service, of the interactive user, and of anyone in `BUILTIN\Administrators` (see [P-2]). Recovery requires `takeown` + `icacls /reset` — knowledge the tool never surfaces.

---

### [P-2] CRITICAL — The `Administrators` Allow rule is defeated by the `Authenticated Users` Deny rule
`src/GameLocker.Common/Security/AclHelper.cs:50-56`, `:68-74`

```csharp
var adminsAllowRule = new FileSystemAccessRule(adminsSid, FullControl, ..., Allow);
security.SetAccessRule(adminsAllowRule);
...
new FileSystemAccessRule(authenticatedUsers, FullControl, ..., Deny)
```

Every interactive account — including every Administrator — is a member of both `BUILTIN\Administrators` **and** `NT AUTHORITY\Authenticated Users` **and** `BUILTIN\Users`. In Windows DACL evaluation, an explicit Deny ACE matching *any* group in the token defeats an Allow ACE. The `Administrators` Allow rule is therefore inert. The four unit tests in `tests/GameLocker.Tests/AclHelperTests.cs:218-248` assert that the Allow ACE *is present in the DACL* — they never assert that access is actually granted, so they pass while the protection they claim to verify does not exist.

Because the denied right is `FullControl`, it includes `WRITE_DAC` — the permission required to edit the ACL. An administrator opening the folder's Security tab in Explorer will be refused. Recovery is only possible because the *owner* of a file retains an implicit right to rewrite the DACL, so `takeown /f "<path>" /r /d y` followed by `icacls "<path>" /reset /T` does work — but nothing in the product, the README, or the installer tells the user this, and on a 40,000-file game folder the reset takes a long time.

---

### [P-3] HIGH — `AllowAccess` grants `Users` and `Authenticated Users` FullControl rather than restoring the previous ACL
`src/GameLocker.Common/Security/AclHelper.cs:172-188`

```csharp
var allowRuleUsers = new FileSystemAccessRule(usersGroup, FullControl, ..., Allow);
security.SetAccessRule(allowRuleUsers);
var allowRuleAuth = new FileSystemAccessRule(authenticatedUsers, FullControl, ..., Allow);
security.SetAccessRule(allowRuleAuth);
```

Unlocking does not restore the original ACL — no snapshot is ever taken. It grants every standard user FullControl, with `ContainerInherit | ObjectInherit`, permanently.

**Security consequence:** if the game is installed under `C:\Program Files\...`, one lock/unlock cycle makes that directory tree writable by every non-administrative account on the machine. Any user can now overwrite the game's executables and DLLs. When the game is next launched by an administrator, that is a straightforward local privilege escalation. GameLocker permanently weakens the security of every folder it manages.

---

### [P-4] HIGH — No ACL snapshot, no rollback, no post-condition verification
`src/GameLocker.Common/Security/AclHelper.cs` (entire file)

`DenyAccess` and `AllowAccess` each perform one `SetAccessControl` and never verify the result. There is no SDDL capture before modification, no `.acl.bak` sidecar, no reconciliation, and no way to answer "what did this folder's permissions look like before GameLocker touched it?" `GetAccessControlInfo` (line 236) can produce SDDL but is never called by any production code path.

---

### [P-5] MEDIUM — Only the root folder's ACL is set; children with broken inheritance are unaffected
`src/GameLocker.Common/Security/AclHelper.cs:90`

Protection relies entirely on `ContainerInherit | ObjectInherit` propagation. Any subdirectory or file whose inheritance was disabled (common in game installs, mod managers, and anything restored from a backup) keeps its old ACL and remains fully accessible. `AclHelperTests.cs:349-371` acknowledges this and tests nothing about it:

> `// Note: Subfolders inherit ACL, but IsAccessDenied only checks explicit rules`

---

### [P-6] MEDIUM — `IsAccessDenied` reports lock state from a single ACE and is used as ground truth
`src/GameLocker.Common/Security/AclHelper.cs:199-229`

It returns true only if a `BuiltinUsers` + `Deny` + `FullControl` ACE exists. A partially applied lock, an inherited deny, or a deny with different rights all report "not denied". The `catch` at line 224 returns `false` on any error — so a folder the process cannot even read its ACL for is reported as *accessible*.

---

### [P-7] MEDIUM — Owner and `SeTakeOwnershipPrivilege` are never handled
Nothing in the codebase calls `SetOwner`, enables `SeTakeOwnershipPrivilege`/`SeRestorePrivilege`, or handles the case where the folder's owner is a different account. On a folder GameLocker has locked itself out of ([P-1]), this is the only in-band route back, and it does not exist.

---

## C. Scheduler unreliability

### [S-1] CRITICAL — A failed lock is recorded as a successful lock and is never retried
`src/GameLocker.Service/GameLockerService.cs:143-158`

```csharp
if (_isFirstRun || isWithinAllowedTime != _wasWithinAllowedTime)
{
    if (isWithinAllowedTime) await UnlockAllFoldersAsync();
    else                     await LockAllFoldersAsync();

    _wasWithinAllowedTime = isWithinAllowedTime;   // set unconditionally
    _isFirstRun = false;
}
```

`LockAllFoldersAsync`/`UnlockAllFoldersAsync` return `void` and swallow per-folder failures (lines 183-192, 219-228). The state variable is updated whether or not a single folder actually changed state. Because the outer condition only fires on a *transition*, once `_wasWithinAllowedTime` has flipped there will be **no further attempt until the next boundary**, up to a week later.

This is the "sometimes it works, sometimes it doesn't" bug. Any transient condition at the exact boundary minute — game still running, file locked by Steam, antivirus scan, disk busy — causes the lock to be skipped entirely and silently for the whole period.

---

### [S-2] CRITICAL — A gaming window that crosses midnight locks the folder at 00:00
`src/GameLocker.Common/Models/GameLockerConfig.cs:65-85`

```csharp
public bool IsWithinAllowedTime(DateTime currentTime)
{
    if (!AllowedDays.Contains(currentTime.DayOfWeek))
        return false;                                    // <-- evaluated on today's date

    var currentTimeOnly = TimeOnly.FromDateTime(currentTime);
    var endTime = StartTime.AddHours(DurationHours);

    if (endTime < StartTime)                             // window crosses midnight
        return currentTimeOnly >= StartTime || currentTimeOnly < endTime;

    return currentTimeOnly >= StartTime && currentTimeOnly < endTime;
}
```

The day check runs first and unconditionally. Configure "Saturday, 22:00, 4 hours" — the intended window is Sat 22:00 → Sun 02:00. At Sunday 00:01 the day check sees `Sunday`, which is not in `AllowedDays`, and returns `false`. The service locks the folder **while the user is playing**, two hours early. The midnight-crossing branch on line 80 is dead code for exactly the case it was written to handle.

`GetNextLockTime` (line 121) has the mirror-image defect: it computes `currentTime.Date.Add(endTime)` and adds a day only if `endTime < StartTime`, so the warning notifications are timed against the wrong instant for these windows.

---

### [S-3] HIGH — The schedule is evaluated once per minute at best, and the warning windows are one minute wide
`src/GameLocker.Service/GameLockerService.cs:59-90`, `:253-263`

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    ...
    await ProcessScheduleAsync();          // once per outer iteration

    for (int i = 0; i < 12; i++)           // 12 × 5s = 60s
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        await ProcessImmediateCommandsAsync();
        await ProcessReloadSignalAsync();
    }
    await LoadConfigurationAsync();
}
```

`ProcessScheduleAsync` is called **once per 60-second outer iteration**, and only after the previous iteration's work has completed — `LockAllFoldersAsync` on a large folder can take minutes or hours, so the real interval is unbounded. Meanwhile the warning check is:

```csharp
if (timeRemaining.TotalMinutes <= 15 && timeRemaining.TotalMinutes > 14 && ...)
```

A one-minute-wide window sampled by a loop whose period drifts with the duration of the previous encryption pass. The 15- and 5-minute warnings will frequently never fire. `GameLockerConfig.PollingIntervalMinutes` is presented in the UI, persisted to config, and **never read by the service** — the interval is hardcoded.

---

### [S-4] HIGH — The service never reconciles desired state against actual state
`src/GameLocker.Service/GameLockerService.cs:129-164`

The design is purely edge-triggered. There is no periodic "is every configured folder in the state the schedule requires?" pass. After a crash mid-lock, a failed lock ([S-1]), a manual `icacls` by the user, or a folder deleted and restored, the service's in-memory booleans and reality diverge permanently and nothing ever corrects it.

---

### [S-5] HIGH — A stale configuration is retained forever when the config fails to load
`src/GameLocker.Service/GameLockerService.cs:118-127` · `src/GameLocker.Common/Configuration/ConfigManager.cs:138-142`

```csharp
var newConfig = await _configManager.LoadConfigAsync();
if (newConfig != null) { _config = newConfig; }        // silent no-op on null
```

`LoadConfigAsync` catches **every** exception and returns `null` — corrupt file, missing key, DPAPI failure, transient sharing violation while the UI is writing, all indistinguishable. The service keeps its previous config forever, with no log entry, no notification, no retry backoff. The user's new schedule is silently ignored. On first start with an unreadable config, `_config` stays `null` and `ProcessScheduleAsync` returns at line 133 — the service runs indefinitely doing nothing while reporting "Running".

---

### [S-6] MEDIUM — Toast notification failure blocks the state transition and causes an infinite retry loop
`src/GameLocker.Service/GameLockerService.cs:195-199`, `:143-158`

`NotificationHelper.ShowLockNotification` is called at the end of `LockAllFoldersAsync`, **outside** any try/catch, and `_wasWithinAllowedTime` is assigned only after that call returns. `Microsoft.Toolkit.Uwp.Notifications` requires a registered AUMID and a user session; a LocalSystem service in session 0 has neither. *(inferred)* If `.Show()` throws, the exception unwinds past the state assignment into the loop's catch at line 72, and the next iteration re-attempts the whole lock — every 60 seconds, forever.

Note also `NotificationHelper.AppId` (line 10) is declared and never used, so no AUMID is registered anywhere.

---

### [S-7] MEDIUM — Wall-clock time is used with no DST, sleep, hibernate, or clock-change handling
`GameLockerConfig` uses `DateTime.Now` throughout. During the autumn DST fall-back the hour 01:00-02:00 occurs twice, producing duplicate transitions; during the spring-forward it never occurs, so a window contained in it is skipped. `Task.Delay` does not account for machine sleep, so a laptop closed at 21:55 and opened at 23:30 evaluates the schedule late. There is no `SystemEvents.TimeChanged`/`PowerModeChanged` subscription, and no monotonic clock use.

---

### [S-8] MEDIUM — `Environment.Exit(1)` inside the fatal handler bypasses graceful shutdown
`src/GameLocker.Service/GameLockerService.cs:102`

Called from within `ExecuteAsync`, this terminates the process immediately — potentially mid-encryption, producing exactly the partial `.enc` state that [D-2] later converts into data loss. A service that fails while holding user data hostage should drain and unlock, not abort.

---

### [S-9] MEDIUM — Command files are consumed before the action is performed
`src/GameLocker.Service/GameLockerService.cs:319-348`

```csharp
var commandText = await File.ReadAllTextAsync(commandFile);
File.Delete(commandFile);          // deleted first
... switch (action) { case "lock": await LockSingleFolderAsync(folderPath); ... }
```

If the action fails or the service is stopped between the delete and the action, the command is lost with no acknowledgement channel back to the UI. The UI meanwhile displays "Command sent to service" (`Form1.cs:799`) as though it had been executed.

`ServiceManager.SendImmediateActionCommandAsync` (`ServiceManager.cs:154-171`) writes with `File.WriteAllTextAsync` — not atomic — so the service can read a half-written command. A short read yields `parts.Length < 3` and is discarded in silence. Only one command can be in flight; a second write clobbers the first.

---

## D. Cryptographic design

### [X-1] CRITICAL — One key and one IV are reused for every file, forever
`src/GameLocker.Common/Services/FolderLocker.cs:31-49`, `:308`

A single `folder_key.dat`/`folder_iv.dat` pair is generated once and used for every file in every folder for the lifetime of the installation. In CBC mode a fixed IV means any two files with identical leading blocks produce identical leading ciphertext. Across a game install with thousands of files sharing headers, archive magic numbers, and zero-padding, this leaks structure freely and permits trivial identification of known files. It is a textbook misuse. A per-file random IV stored in the file header costs 16 bytes.

---

### [X-2] CRITICAL — There is no file format: no magic, no version, no key ID, no length, no MAC
`src/GameLocker.Common/Encryption/AesEncryptionHelper.cs:150-176`

The `.enc` file is raw ciphertext with nothing else. This single omission causes [D-2], [D-4], [D-6] and [D-7], and makes it impossible to ever change the crypto without breaking every existing file. A minimum viable header is ~64 bytes: magic, format version, key ID, nonce, original size, original attributes, and an AEAD tag.

---

### [X-3] HIGH — The DPAPI "additional entropy" is a constant compiled into a shipped binary
`src/GameLocker.Common/Encryption/DpapiHelper.cs:12-13`

```csharp
private static readonly byte[] AdditionalEntropy =
    "GameLocker.Security.Entropy.v1"u8.ToArray();
```

`DataProtectionScope.LocalMachine` means any process on the machine can call `ProtectedData.Unprotect`. The entropy parameter would normally raise the bar, but it is a hardcoded ASCII string inside a publicly distributed executable. Therefore **any process, at any integrity level, including a non-administrative one, can recover the master key** with four lines of code. The README's claim that keys are protected is not accurate.

---

### [X-4] HIGH — No key backup, escrow, rotation, or recovery mechanism
`src/GameLocker.Common/Services/FolderLocker.cs:29-50`

If `folder_key.dat` is lost — reinstalled Windows, restored from image, disk moved, ProgramData cleaned, uninstaller run ([H-3]) — every encrypted file on the machine is permanently unrecoverable. There is no recovery code, no exportable key bundle, no warning at setup time, and no check at lock time that a backup exists. `DpapiHelper.ProtectToFileAsync` (line 50) also writes non-atomically, so a power loss during first-run key generation corrupts the key file and destroys everything encrypted thereafter.

---

### [X-5] MEDIUM — Keys are decrypted into managed heap memory and never zeroed
`_masterKey` / `_masterIV` (`FolderLocker.cs:14-15`) are plain `byte[]` living for the lifetime of the process, subject to GC copying and pagefile spill. No `CryptographicOperations.ZeroMemory`, no `SecureString`, no `ProtectedMemory`. Minor relative to [X-3], but it is a stated security product.

---

### [X-6] MEDIUM — Config encryption and file encryption use separate key pairs with no relationship, and one is silently regenerated
`src/GameLocker.Common/Configuration/ConfigManager.cs:89-104`

If `keys.dat`/`iv.dat` go missing, `SaveConfigAsync` generates a fresh pair. The existing `config.dat` then fails to decrypt, `LoadConfigAsync` returns `null`, and the UI presents a blank configuration — see [G-4] for how that becomes data loss.

---

### [X-7] LOW — `AesEncryptionHelper.Encrypt` buffers whole files in memory
Lines 44-70 and 79-105 are used for the `.gamelocker` marker only today, but `EncryptFileAsync`/`DecryptFileAsync` (114-140) read entire files with `File.ReadAllBytesAsync` and are public API. Called on a 40 GB archive they throw `OutOfMemoryException`.

---

## E. Security vulnerabilities

### [V-1] CRITICAL — Local privilege escalation via the world-writable command channel
`installer/InnoSetup/GameLocker-Setup.iss:52` · `src/GameLocker.Service/GameLockerService.cs:312-355` · `src/GameLocker.Common/Security/AclHelper.cs:172-188`

The installer explicitly grants every user full control of the data directory:

```
[Dirs]
Name: "{commonappdata}\{#MyAppName}"; Permissions: users-full
```

The service, running as **LocalSystem**, polls that directory every five seconds for a file named `immediate_action`, parses it as `action|folderPath|timestamp`, and executes the action **on an arbitrary attacker-supplied path with no validation, no allowlist check against the configured folders, and no authentication of the writer**.

**Exploit — full LPE, no admin required:**

```powershell
# As any unprivileged user:
"unlock|C:\Windows|$(Get-Date -Format o)" |
    Out-File "$env:ProgramData\GameLocker\immediate_action" -Encoding utf8
# Wait <= 5 seconds.
```

The service calls `UnlockFolderAsync("C:\Windows")` → `AclHelper.AllowAccess` → `SetAccessRule(BuiltinUsers, FullControl, ContainerInherit|ObjectInherit, Allow)` on `C:\Windows`. Every user now has write access to the entire Windows directory. Replace any DLL loaded by a SYSTEM process; escalate at leisure.

The destructive variant is equally available:

```powershell
"lock|C:\Windows\System32|$(Get-Date -Format o)" | Out-File "$env:ProgramData\GameLocker\immediate_action"
```

which instructs a SYSTEM service to AES-encrypt and delete System32. This is, functionally, a ransomware primitive exposed to unprivileged local users.

Note that even without the installer's `users-full`, the default `C:\ProgramData` ACL grants `Users` create-file rights that are inherited by subdirectories created with `Directory.CreateDirectory` (`ServiceManager.cs:138`, `FolderLocker.cs:42`), so the channel is likely writable regardless. *(inferred — confirm with `icacls "%ProgramData%\GameLocker"` on a real install.)*

---

### [V-2] CRITICAL — Any user can destroy every encrypted game by deleting one file
`installer/InnoSetup/GameLocker-Setup.iss:52`

The same `users-full` grant makes `folder_key.dat` deletable by any unprivileged account. One `del` command permanently destroys every encrypted game folder on the machine. There is no backup ([X-4]) and no integrity monitoring.

---

### [V-3] HIGH — The security model does not survive contact with its own threat model
`README.md:251-265`

The README presents this table:

| Attack Vector | Protection Level |
|---|---|
| Administrative Bypass | ⚠️ Deterrent |
| Boot from External Media | 🔒 **Protected** |
| Key Extraction | ⚠️ Advanced |

The tool's stated purpose is restraining its own operator, who is by requirement a local Administrator. Actual bypasses, in ascending order of effort:

1. **Change the system clock.** Enforcement is `DateTime.Now`. Zero tooling. ~5 seconds.
2. `sc stop GameLockerService` — the service has no watchdog, no recovery peer, no protected-process registration. The `sc failure` restart actions configured at `GameLocker-Setup.iss:63` do not apply to a clean manual stop.
3. Delete `%ProgramData%\GameLocker\config.dat` — the service loads `null`, keeps stale config, and after a restart does nothing at all ([S-5]).
4. Run the shipped `UnlockTool.exe` against the folder. It is distributed in the repository and requires no authorisation.
5. Extract the master key: `ProtectedData.Unprotect(File.ReadAllBytes(keyPath), Encoding.UTF8.GetBytes("GameLocker.Security.Entropy.v1"), DataProtectionScope.LocalMachine)` — works from an unprivileged process ([X-3]).

"Boot from External Media: Protected" is the inverse of the truth: offline access is where the encryption *does* apply, but it is irrelevant because online access is unrestricted.

The honest positioning is **a speed bump against your own impulse**, not a security boundary. The current framing invites users to trust it with irreplaceable data.

---

### [V-4] HIGH — Command channel has no path validation even against the tool's own configuration
`src/GameLocker.Service/GameLockerService.cs:326-347`

`folderPath` from the command file is passed directly to `LockFolderAsync`/`UnlockFolderAsync`. It is never checked against `_config.GameFolderPaths`, never canonicalised, never checked for reparse points, never checked against a denylist of system paths (`C:\Windows`, `C:\Program Files`, the drive root, `%ProgramData%`). Even absent [V-1], a bug or a typo in the UI can point the encryptor at the OS.

---

### [V-5] MEDIUM — `{app}\Logs` is world-writable inside Program Files
`installer/InnoSetup/GameLocker-Setup.iss:51`

`Name: "{app}\Logs"; Permissions: users-full` creates a user-writable directory inside `C:\Program Files\GameLocker`. Combined with any service-side path handling that walks up from it, this is a binary-planting foothold. There is no reason for it — nothing in the service writes logs to disk.

---

### [V-6] MEDIUM — No code signing on a tool that requires Administrator and encrypts user data
No signing step exists in `Build-Installer.ps1`, the `.iss`, or any CI. Users are asked to click through SmartScreen to grant Administrator to an unsigned binary that will encrypt their drive.

---

### [V-7] LOW — Two differently named services can be installed simultaneously
`installer/NSIS/GameLocker.nsi:64` registers `"GameLocker Service"`; `installer/InnoSetup/GameLocker-Setup.iss:61` and `ServiceManager.ps1:10` register `GameLockerService`. Running the NSIS installer after the Inno one yields two independent LocalSystem services fighting over the same folders, the same key files, and the same single-slot command file.

---

## F. State consistency

### [F-1] CRITICAL — `GetFolderState` infers state from a marker file that carries no information about actual state
`src/GameLocker.Common/Services/FolderLocker.cs:158-173`

```csharp
public FolderState GetFolderState(string folderPath)
{
    if (!Directory.Exists(folderPath)) return FolderState.Unknown;
    return HasLockMarker(folderPath) ? FolderState.Locked : FolderState.Unlocked;
}
```

The marker records nothing but a timestamp. State is never derived from what is actually on disk. A folder can simultaneously be: marked locked but fully plaintext ([D-15]); marked unlocked but full of `.enc` files (marker deleted by the user, or `btnTestLock` crash [G-5]); ACL-denied with no marker; half-encrypted with a marker ([D-9]). In every one of these cases the tool confidently reports a state that is false, and the service acts on it.

There is no manifest of which files were encrypted, no per-file record, no transaction log. After any partial failure there is literally no way — in the product or by hand — to determine what was done.

---

### [F-2] HIGH — Config writes are not atomic; a corrupt config is indistinguishable from no config
`src/GameLocker.Common/Configuration/ConfigManager.cs:110`

```csharp
await File.WriteAllBytesAsync(_configFilePath, encryptedConfig);
```

No temp-file-plus-`File.Replace`, no backup, no schema version field, no checksum. A power loss during save truncates `config.dat`; the next `LoadConfigAsync` throws, catches, and returns `null` — which every caller interprets as "first run". See [G-4] for the resulting loss of the folder list.

---

### [F-3] MEDIUM — No schema version in the persisted configuration
`src/GameLocker.Common/Models/GameLockerConfig.cs`

There is no version field. Any future change to `FolderEncryptionSettings` or `SelectiveEncryptionSettings` will deserialise silently into defaults against old configs, changing which files get encrypted without telling anyone.

---

### [F-4] MEDIUM — The lock marker is encrypted but never validated
`src/GameLocker.Common/Services/FolderLocker.cs:187-200`, `:169-173`

`CreateLockMarkerAsync` AES-encrypts `"GameLocker|Locked|{timestamp}"`, but `HasLockMarker` only calls `File.Exists`. The contents are never read, decrypted, or checked. `type nul > .gamelocker` is enough to convince the tool a folder is locked; deleting the file is enough to convince it the folder is clean while 40,000 `.enc` files sit inside.

---

## G. UI defects

### [G-1] HIGH — No application manifest: the ConfigUI does not request elevation
`src/GameLocker.ConfigUI/GameLocker.ConfigUI.csproj` (no `<ApplicationManifest>`; no `app.manifest` file exists in the repository)

`Form1_Load` merely shows a message box saying admin is "recommended" (`Form1.cs:29-37`) and then proceeds. Every ACL operation subsequently fails with `UnauthorizedAccessException`, which `btnTestLock_Click` reports in a status label (line 728) and `DecryptAndRemoveFolderAsync` reports as a transient "ACL warning" label that is immediately overwritten (line 294). The absence of a manifest also forfeits long-path awareness ([D-12]) and DPI/compat declarations.

---

### [G-2] CRITICAL — Folder removal deletes the folder from configuration even when decryption failed
`src/GameLocker.ConfigUI/Form1.cs:329-409`

```csharp
catch (Exception ex) { failed++; Console.WriteLine($"Failed to decrypt {encFile}: {ex.Message}"); }
...
progressForm.ShowDialog(this);
progressForm.Dispose();

_folderSettings.Remove(folderToRemove);
if (lstFolders.Items.Contains(folderToRemove))
    lstFolders.Items.Remove(folderToRemove);
...
ShowStatus($"Folder removed and files decrypted: {folderToRemove}", false);
```

The removal block after `ShowDialog` runs unconditionally. `failed` is not consulted. If 39,000 of 40,000 files failed to decrypt — because the service denied SYSTEM ([P-1]), or the user was not elevated ([G-1]), or the key was wrong — the folder is still removed from the configuration and the user is told, in green text, that the files were decrypted.

The folder is now encrypted, ACL-denied, and no longer known to GameLocker. There is no list of folders GameLocker has ever touched, so the user cannot even find out which folders are affected.

---

### [G-3] HIGH — `Test Lock` can leave a folder permanently denied
`src/GameLocker.ConfigUI/Form1.cs:710-732`

```csharp
AclHelper.DenyAccess(folder);
ShowStatus(...);
MessageBox.Show("Folder locked successfully! ... Click OK to unlock the folder.");
AclHelper.AllowAccess(folder);
```

Between `DenyAccess` and `AllowAccess` sits a blocking modal dialog. If the process is killed, crashes, the machine loses power, or the user ends the task while that dialog is open, the folder remains ACL-denied **with no `.gamelocker` marker**. `GetFolderState` therefore reports `Unlocked` ([F-1]) and the service will never unlock it. There is no `try/finally` — the recovery `AllowAccess` at line 731 only runs on an exception from `DenyAccess` itself, not on process death.

---

### [G-4] CRITICAL — A failed config load leads the user to overwrite their real configuration with an empty one
`src/GameLocker.ConfigUI/Form1.cs:43-71` + `:602-647` · `ConfigManager.cs:117-143`

`LoadConfigAsync` returns `null` for *any* failure. The UI's else-branch (line 62-65) shows "No existing configuration found. Configure your settings." and leaves `_config` as the empty instance from the constructor. The form renders with an empty folder list. The user re-adds one folder and clicks Save. `PopulateConfigFromForm` (line 119) calls `_config.GameFolderPaths.Clear()` and repopulates from the now-nearly-empty ListBox, and `SaveConfigAsync` overwrites `config.dat`.

Every previously configured folder is now gone from the configuration — while still encrypted and ACL-denied on disk. Combined with [F-1] (no other record of what was locked) this is unrecoverable without manually walking the filesystem for `.gamelocker` markers.

---

### [G-5] HIGH — The UI never displays actual lock state
`src/GameLocker.ConfigUI/Form1.cs` (entire file)

`FolderLocker.GetFolderState` is never called from the UI. There is no column, icon, or indicator showing whether a listed folder is currently locked, encrypted, partially encrypted, or ACL-denied. The user's only feedback is a status label describing the last button press.

---

### [G-6] HIGH — The extension scan runs synchronously on the UI thread with `Application.DoEvents()`
`src/GameLocker.ConfigUI/Form1.cs:429-484`

```csharp
lblExtensionInfo.Text = "Scanning folder...";
Application.DoEvents();
var result = _extensionScanner.ScanFolderExtensions(folderPath);
```

`ScanFolderExtensions` performs `Directory.GetFiles(..., AllDirectories)` and constructs a `FileInfo` per file (`FileExtensionScanner.cs:38-48`). On a 120 GB, 40,000-file install this blocks the message pump for minutes; Windows marks the window "Not Responding". `Application.DoEvents()` re-enters the message loop and permits the user to click Remove or Save mid-scan, producing reentrancy on `_folderSettings`.

This fires on every `SelectedIndexChanged` (line 419) — merely arrow-keying down the folder list triggers a full recursive scan per keystroke.

---

### [G-7] CRITICAL — `.dat` is classified as a "Safe" save-file extension
`src/GameLocker.Common/Services/FileExtensionScanner.cs:103`, `:126` · `src/GameLocker.Common/Models/SelectiveEncryptionSettings.cs:80`

```csharp
".sav" or ".save" or ".savegame" or ".dat" or ".progress" or ".slot" or ".checkpoint" => RiskLevel.Safe,
```

`.dat` is one of the most common **primary asset archive** extensions in commercial games — including Insomniac's engine, which ships its Spider-Man asset data as `.toc` + `.dat` pairs. `.xml`, `.json`, `.ini` and `.cfg` are likewise classified Safe (line 127) while being load-bearing engine configuration in most titles. `.toc`, `.arc`, `.bank`, `.sarc`, `.forge`, `.rpf`, `.cpk` are unclassified and fall to `RiskLevel.Unknown`.

Worse, the UI **auto-selects every Safe extension** when a folder has no saved selection:

```csharp
bool shouldCheck = settings.SelectedExtensions.Contains(ext.Extension) ||
    (settings.SelectedExtensions.Count == 0 && ext.RiskLevel == RiskLevel.Safe);
```
`Form1.cs:469-470`

So a user who opens the panel and immediately saves has silently opted in to encrypting every `.dat` in the game. The risk classification is an allowlist built by guessing at extensions, applied by default, to files the tool then deletes. Even if [D-1] were fixed and the selective path were wired up, **this table alone would have destroyed Spider-Man 2.**

---

### [G-8] MEDIUM — Dangerous-extension confirmation is trivially defeated by "Select Safe"
`src/GameLocker.ConfigUI/Form1.cs:500-519` vs `:544-564`

`clbExtensions_ItemCheck` prompts before ticking a `Dangerous` extension, but `btnSelectSafe_Click` calls `SetItemChecked` in a loop, which raises `ItemCheck` programmatically — and the confirmation prompt fires for each one, or the risk classification is simply wrong ([G-7]) so nothing prompts at all. The guard protects against the wrong thing.

---

### [G-9] MEDIUM — No preview, no dry run, no confirmation of scope before a destructive operation
Nothing in the UI shows "this will encrypt 41,382 files totalling 118 GB, and delete the originals" before the first lock. The `Save` confirmation (line 628-636) reports folder counts and service status only.

---

### [G-10] LOW — `async void` event handlers with no top-level exception handling
`Form1.cs:26`, `:136`, `:174`, `:602`. An exception escaping any of these terminates the process rather than surfacing an error. `btnAddFolder_Click` in particular awaits `TriggerImmediateFolderProcessingAsync`, which touches the service control manager.

---

## H. Install / uninstall

### [H-1] CRITICAL — Uninstalling does not unlock or decrypt anything
`installer/InnoSetup/GameLocker-Setup.iss:68-75`

```
[UninstallRun]
Filename: "sc.exe"; Parameters: "stop GameLockerService"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete GameLockerService"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: dirifempty; Name: "{commonappdata}\{#MyAppName}"
```

The uninstaller stops the service, deletes it, and removes the binaries — while every managed folder is still encrypted and ACL-denied. The only software capable of reversing that state has just been removed. The user is left with encrypted games, denied folders, and no tool.

A correct uninstaller must refuse to proceed, or must fully unlock and decrypt every configured folder and verify the result, before removing anything.

---

### [H-2] CRITICAL — The uninstaller offers to delete the encryption keys, with no warning about the consequence
`installer/PowerShell/GameLocker-Setup.ps1:317-325` (inside the generated `GameLocker-Uninstall.ps1`)

```powershell
$keepData = Read-Host "Keep configuration and data files? (Y/n)"
if ($keepData -notmatch '^[Yy]?$') {
    if (Test-Path $DataPath) {
        Remove-Item -Path $DataPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
```

`$DataPath` is `%ProgramData%\GameLocker`, which contains `folder_key.dat`. Answering "n" to a question phrased as being about *configuration files* permanently destroys every encrypted game on the machine. There is no second confirmation, no enumeration of what will be lost, and no key export offered.

---

### [H-3] CRITICAL — `[Dirs] ... Permissions: users-full` on the data directory
`installer/InnoSetup/GameLocker-Setup.iss:52`

Root cause of [V-1] and [V-2]. Restated here because it is an installer decision, not a code decision, and is fixed in the installer.

---

### [H-4] HIGH — Upgrade path deletes the running service without unlocking first
`installer/InnoSetup/GameLocker-Setup.iss:88-104`

`InitializeSetup` stops and deletes the service, sleeps 2 seconds, and proceeds — with no check for in-progress encryption. Stopping the service mid-lock is precisely the interruption that produces the partial `.enc` states of [D-2] and [D-3]. A 120 GB folder will still be encrypting long after the 2-second sleep.

---

### [H-5] HIGH — Four parallel installer implementations, three of them stale, disagreeing on fundamentals
`installer/PowerShell/`, `installer/InnoSetup/`, `installer/NSIS/`, `installer/WiX/`

They disagree on the service name ([V-7]), on data-directory permissions, on whether an uninstall script is generated, and on version number. `Build-Installer.ps1` drives only two of them. There is no single source of truth for what an installed GameLocker looks like.

---

### [H-6] HIGH — Build scripts cannot run in this repository
`Build-Installer.ps1:17-18` · `ServiceManager.ps1:11` · `BUILD-COMMANDS.txt:9`

```powershell
$projectRoot = "h:\Work\10xProductivity\game_locker\code"
$ServicePath = "h:\Work\10xProductivity\game_locker\code\src\...\GameLocker.Service.exe"
```

Absolute paths from a different machine. The release process is not reproducible, which is why the version is still pinned at `1.1.0` (`Build-Installer.ps1:63`, `GameLocker-Setup.iss:5`) three releases in.

---

### [H-7] MEDIUM — The service is installed with no least-privilege consideration
It runs as LocalSystem with unrestricted filesystem access, driven by a world-writable command file. Nothing constrains it to the configured folders. `sc failure` restart actions are configured (`.iss:63`) but no `sc sidtype`, no `RequiredPrivileges`, no service SID, no write-restricted token.

---

### [H-8] LOW — The `[Files]` source paths depend on an untracked build output directory
`installer/InnoSetup/GameLocker-Setup.iss:45-47` reads from `..\..\installer-package\publish\...`, which `.gitignore:65` excludes. Compiling the installer without first running the publish step silently produces an installer missing its payload. *(inferred — Inno usually errors on missing sources; verify.)*

---

## I. Process, testing, documentation

### [T-1] HIGH — There is no test coverage for anything that can destroy data
`tests/GameLocker.Tests/` contains 14 tests, all in `AclHelperTests.cs`, plus an empty `UnitTest1.cs`. There is **zero** coverage of `FolderLocker`, `AesEncryptionHelper`, `DpapiHelper`, `ConfigManager`, `GameLockerConfig.IsWithinAllowedTime`, the service loop, or the command channel. Every Critical finding in sections A, C, D and F is in untested code.

---

### [T-2] HIGH — Admin-gated tests report success when they did not run
`tests/GameLocker.Tests/AclHelperTests.cs:149-151` (and 7 more occurrences)

```csharp
if (!AclHelper.IsRunningAsAdmin())
{
    return; // Skip test - requires admin
}
```

An early `return` is a **pass** in xUnit. Eight of fourteen tests report green from a non-elevated shell without executing a single assertion. Correct mechanism is `Assert.Skip` (xUnit v3) or a `SkippableFact`/custom trait with a discoverer.

---

### [T-3] HIGH — The ACL tests assert the presence of an ACE, not the effective access
`AclHelperTests.cs:184-248`

`DenyAccess_PreservesSystemAccess` walks the DACL looking for an `Allow` ACE for SYSTEM and passes when it finds one. It never checks whether SYSTEM can actually open the folder — which, per [P-1], it cannot, because a Deny ACE for the same SID sits alongside it. The test suite's four "invariant" tests are exactly the tests that would have caught the permanent-lockout bug, and they are written in a way that cannot.

---

### [T-4] MEDIUM — No CI, no build verification, no static analysis
No `.github/workflows`, no `Directory.Build.props`, no `TreatWarningsAsErrors`, no nullable warnings escalated, no analyzers.

---

### [T-5] MEDIUM — The test project and all tools are outside the solution
`GameLocker.sln` contains only the three `src/GameLocker.*` projects. `tests/`, `UnlockTool/`, `ForceDecrypt/`, `CheckConfig/`, and five `src/Test*` demo projects build only when named explicitly. `dotnet build` is green while any of them is broken; `dotnet test` from the repository root finds no tests at all.

---

### [T-6] MEDIUM — Demo projects hardcode `C:\Windows\System32` as a target folder
`src/TestCompleteWorkflow/Program.cs:17` · `src/TestExtensionScanning/Program.cs:16`

```csharp
var gamePath = @"C:\Windows\System32"; // Using System32 as test folder
```

Both currently only *scan*, which is the only reason this has not destroyed a machine. `FullGameLockerDemo` and `TestGameFolderDemo` hardcode `G:\games\TestGame`. One copy-paste of `LockFolderAsync` into any of these files ends the operating system.

---

### [T-7] MEDIUM — Recovery tools default to a hardcoded personal game path
`UnlockTool/Program.cs:17` · `ForceDecrypt/Program.cs:18`

```csharp
string folderPath = args.Length > 0 ? args[0] : @"G:\Hogwarts Legacy";
```

Running the "emergency recovery" tool with no arguments points its destructive logic ([D-5]) at whatever happens to live at `G:\Hogwarts Legacy` on the user's machine. `UnlockTool` closes by printing "Hogwarts Legacy folder is now fully restored" regardless of the path it was given.

---

### [T-8] MEDIUM — Documentation describes features that do not work and security that does not exist
- `README.md:48`, `:374-379`, `CHANGELOG.md:86-90`, `docs/01-architecture.md:218-229` all describe selective encryption as an active, shipped feature. It has no callers ([D-1]).
- `README.md:251-265` presents a security-evaluation table that overstates every row ([V-3]).
- `docs/01-architecture.md:298-306` claims "User deletes .gamelocker → Files stay encrypted → Useless without keys" as a *protection*. It is the data-loss scenario of [F-1] described as a feature.
- `README.md:416` says "Never delete `C:\ProgramData\GameLocker\folder_*.dat`" — while the shipped uninstaller offers to do exactly that ([H-2]).

---

### [T-9] LOW — `Console.WriteLine` is the error channel for a Windows Service
`FolderLocker.cs:130`, `:234`, `:259`, `:483-526`. Output from a LocalSystem service goes to no console, no Event Log, and no file. Every per-file encryption failure in production is invisible.

---

### [T-10] LOW — The service name differs across layers
`Program.cs:10` and `ServiceManager.cs:10` use `"GameLocker Service"`; the Inno, PowerShell and `ServiceManager.ps1` installers register `GameLockerService`. `ServiceController` resolves display names in some cases, so this may work by accident. *(inferred — verify `ServiceManager.IsServiceInstalled()` against an Inno-installed service.)*

---

### [T-11] LOW — `PollingIntervalMinutes` is a setting that does nothing
`GameLockerConfig.cs:39` is written by the UI (`Form1.cs:126`) and read by nothing. Users adjust a control that has no effect.

---

### [T-12] LOW — Stale one-off documents committed at repository root
`FIXES-SUMMARY.md`, `BUILD-COMMANDS.txt`, `TestSuite.ps1`, `TestConfig.cs` (orphaned — belongs to no project) are working notes from a previous machine, containing paths and version numbers that no longer apply.

---

## Appendix: reconstructing the Spider-Man 2 loss

Every step below is a cited defect above; no speculation is required to explain the outcome.

1. The user added the game folder and used the extension panel to select safe types. That selection was persisted to `FolderEncryptionSettings` and to `config.dat`.
2. At the schedule boundary, `GameLockerService.LockAllFoldersAsync` called `LockFolderAsync` — **not** `LockFolderWithCustomExtensionsAsync`. The selection was never consulted. **[D-1]**
3. `EncryptFolderContentsAsync` enumerated every file recursively and encrypted all of them, including the engine's `.dat`/`.toc` asset archives, every `.exe` and every `.dll`. **[D-1]**
4. Files held open by Steam, the anti-cheat, or Windows Defender failed with sharing violations. Those failures were printed to a console that does not exist for a service and skipped. The install became a mixture of encrypted and plaintext files. **[D-9], [T-9]**
5. The folder was marked `Locked` and `DenyAccess` was applied — which, running as LocalSystem, wrote a `Deny FullControl` ACE for SYSTEM itself. **[P-1]**
6. At the unlock boundary, `AllowAccess` threw because SYSTEM could no longer read the DACL; the throw was logged as a warning and swallowed; decryption then failed on every file; `_wasWithinAllowedTime` had already been flipped, so no retry ever occurred. **[P-1], [S-1]**
7. The user attempted recovery. If `ForceDecrypt` was used, every file that failed to decrypt had its ciphertext copied over the plaintext filename and the ciphertext deleted. **[D-5]**
8. If the folder was removed via the ConfigUI, it was dropped from the configuration regardless of the decryption failure count, and the UI reported success in green. **[G-2]**
9. With no manifest of what was encrypted, no integrity headers on the `.enc` files, and no ACL snapshot, there was no path back. **[F-1], [X-2], [P-4]**

Even had step 2 been fixed, `.dat` is classified `Safe` and auto-ticked by default — so the asset archives would have been encrypted anyway. **[G-7]**

---

## Closing assessment

The engineering problem here is not a list of bugs; it is that **the mechanism is disproportionate to the goal.** The objective is to stop a person from launching a game outside chosen hours. The chosen implementation performs bulk in-place AES encryption with deletion of originals across hundreds of gigabytes of irreplaceable data, twice a day, unattended, as SYSTEM, with no journal and no verification. Even a flawless implementation of that design carries a permanent tail risk that dwarfs the benefit.

A folder that cannot be opened is already unusable to a game. ACL denial alone achieves the entire product goal, is instantaneous regardless of folder size, is perfectly reversible, and cannot corrupt a single byte. Encryption adds meaningful value only against an offline attacker — a threat model that does not apply to the user restraining themselves, and that the current key storage does not defend against anyway ([X-3]).

The remediation plan in `REMEDIATION-PLAN.md` therefore inverts the default: **ACL-only locking as the shipped mechanism, with encryption demoted to a narrow, opt-in, allowlist-only mode for save files, gated behind a verified key backup.**
