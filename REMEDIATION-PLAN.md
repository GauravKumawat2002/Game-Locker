# GameLocker — Remediation Plan

**Companion document:** [`ARCHITECTURE-REVIEW.md`](ARCHITECTURE-REVIEW.md). Every bracketed ID below (`[D-1]`, `[P-2]`, …) is a finding in that review.

**Status:** Phases 0, 1 and 2 applied (not yet compiled or tested — see [Verification](#verification)). Phases 3-8 not started.

---

## Why this plan looks the way it does

The review found **17 Critical, 23 High, 21 Medium, 12 Low** defects. But the defect count is not the real problem. The real problem is that **the mechanism is disproportionate to the goal.**

The objective is to stop a person from launching a game outside chosen hours. The v1.2.0 implementation achieves that by performing bulk in-place AES encryption with deletion of originals across hundreds of gigabytes of irreplaceable data, twice a day, unattended, as SYSTEM, with no journal, no integrity check, and no verification. Even a *flawless* implementation of that design carries a permanent tail risk that dwarfs the benefit.

A folder that cannot be opened is already unusable to a game. NTFS denial achieves the entire product goal in roughly 200ms regardless of folder size, is perfectly reversible, and cannot corrupt a single byte. Encryption adds real value only against an *offline* attacker — a threat model that does not apply to a user restraining themselves, and which the current key storage does not defend against anyway (`DpapiHelper.cs:12` uses `DataProtectionScope.LocalMachine` with a hardcoded entropy string compiled into a shipped binary, so any process at any integrity level can recover the master key — `[X-3]`).

### Decisions

| Decision | Choice |
|---|---|
| Default lock mechanism | **ACL-only.** Files are never rewritten. |
| Encryption | Demoted to a narrow, opt-in "save vault" mode, allowlist-only, gated behind a verified key backup. |
| Legacy `.enc` data | **None exists.** No migration path required; the format can change freely. |
| Sequencing | **Emergency patch first**, then rebuild against a hardened baseline. |

### Target behaviour

```
DEFAULT (ACL-only)
  lock:    snapshot DACL+owner+protection -> apply protected DACL -> verify
  unlock:  restore snapshot verbatim -> verify
  files:   NEVER touched. 0 bytes rewritten.
  time:    ~200ms on a 120GB folder

OPT-IN (save vault)
  scope:   user-picked allowlist only; hard denylist that no UI action overrides
  cap:     refuse files over size cap, refuse over count cap, require free space
  gate:    disabled until the user has exported and confirmed a key backup
  format:  magic + version + key ID + per-file nonce + AES-GCM tag
```

---

## Phase 0 — Emergency patch

Targeted edits, no new abstractions. Goal: **no live exploit and no destructive code path left in the repository.**

| # | Location | Change | Fixes |
|---|---|---|---|
| 0.1 | `Common/Security/AclHelper.cs` | Deny a **reduced right set** instead of `FullControl`, and drop the `authenticatedUsers` and current-user Deny rules | `[P-1]` `[P-2]` |
| 0.2 | `Common/Security/AclHelper.cs` | `AllowAccess` no longer grants `Users`/`AuthenticatedUsers` FullControl — it only removes deny entries | `[P-3]` |
| 0.3 | `Common/Services/FolderLocker.cs` | Delete the "`.enc` exists → delete the original" branch; throw instead | `[D-2]` |
| 0.4 | `ForceDecrypt/Program.cs` | Delete the "raw recovery" branch; verify plaintext via temp-file round-trip before replacing; never delete an unverified `.enc`; drop the `keys.dat` fallback | `[D-5]` |
| 0.5 | `Service/GameLockerService.cs` | Canonicalise the command path and reject anything not in `GameFolderPaths` (`IsManagedFolder`) | `[V-1]` `[V-4]` |
| 0.6 | `installer/InnoSetup/GameLocker-Setup.iss` | Remove both `Permissions: users-full`; apply an explicit SYSTEM+Administrators DACL via `icacls` before the service starts | `[H-3]` `[V-2]` `[V-5]` |
| 0.7 | `installer/PowerShell/GameLocker-Setup.ps1` | Remove the `Remove-Item $DataPath` block from the generated uninstaller; warn instead | `[H-2]` |
| 0.8 | `Service/GameLockerService.cs` | Return success counts; only flip `_wasWithinAllowedTime` when every folder reached the desired state; wrap notifications in try/catch | `[S-1]` `[S-6]` |
| 0.9 | `Common/Models/GameLockerConfig.cs` | Replace the day-then-time check with absolute window intervals (`GetWindows`) | `[S-2]` |
| 0.10 | `Common/Services/FileExtensionScanner.cs` | Move `.dat` out of `Safe`; add `NeverEncryptExtensions`, enforced in both `ShouldEncryptFile` implementations so no selection can override it | `[G-7]` |
| 0.11 | `UnlockTool/`, `ForceDecrypt/` | Require an explicit path argument; remove the `G:\Hogwarts Legacy` default; stop claiming success on partial failure; keep the lock marker when files remain encrypted | `[T-7]` `[F-1]` |
| 0.12 | `ConfigUI/Form1.cs` | Remove `TriggerImmediateFolderProcessingAsync` — it commanded a lock for a folder not yet in the saved configuration and reported success without any acknowledgement | `[S-9]` `[V-1]` |
| 0.13 | `Common/Services/FolderLocker.cs` | Apply `NeverEncryptExtensions` in `EncryptFolderContentsAsync` — the path the service actually runs — and replace the culture-sensitive full-path `EndsWith` filters with an extension comparison | `[D-1]` `[D-14]` |

### Note on 0.13

0.10 alone would not have prevented the Spider-Man 2 loss. The denylist lives in `ShouldEncryptFile`, which is only reached from `LockFolderWithCustomExtensionsAsync` — a method with **zero callers**. The service calls `LockFolderAsync`, which encrypts everything through `EncryptFolderContentsAsync` without consulting any setting. Until that wiring is rebuilt in Phases 1-4, the filter added in 0.13 is the only thing preventing a scheduled lock from encrypting `.exe`, `.dll` and asset archives.

### Note on 0.1

The original plan was to delete the current-user Deny rule alone. That is **not sufficient**: a LocalSystem token is itself a member of `BUILTIN\Users`, so the remaining `Users` Deny would still have locked the service out.

The fix instead narrows *which rights* are denied. `LockedDenyRights` covers read, write, execute, append, extended attributes and delete — but deliberately excludes `ChangePermissions` (WRITE_DAC), `TakeOwnership` (WRITE_OWNER) and `ReadPermissions` (READ_CONTROL). Content becomes unreadable and unexecutable, so a game cannot launch, while the ACL stays repairable by both SYSTEM and any Administrator. The permanent-lockout class is eliminated rather than narrowed.

`IsAccessDenied` was updated to match on `ReadData` rather than `FullControl`, since a FullControl test would no longer ever fire.

### Behaviour change

Adding a folder in the ConfigUI no longer locks it immediately. The folder is not in the saved configuration at that point, and the service now refuses commands for folders it does not manage. Clicking **Save** writes the configuration, signals the service, and the correct schedule state is applied.

**Gate:** Phase 0 must compile and pass the Phase 0 verification below before any Phase 1 work begins.

---

## Phase 1 — ACL engine rewrite ✅ applied

**No Deny ACEs at all.** Deny precedence is the root of the entire lockout class — an explicit Deny matching any group in a token defeats every Allow, including the one granted to `Administrators`, and `FullControl` includes `WRITE_DAC`, so the deny blocks the very operation needed to undo it.

Locking now installs a **protected DACL granting only SYSTEM and Administrators**. Under UAC an administrator's ordinary logon token carries the Administrators group as deny-only, so Explorer, the game, and every non-elevated process are refused, while the service, an elevated process, and the recovery tool retain full access. The lock is real friction; recovery never requires taking ownership. Fixes `[P-1]` `[P-2]` `[P-3]` `[P-4]`.

| File | Role |
|---|---|
| `Common/Security/FolderAclManager.cs` | `Capture` / `ApplyLock` / `Restore` / `ResetToInherited` / `VerifyLocked` / `MatchesSnapshot`. Captures **explicit** entries only, so inherited entries are not written back literally and the folder does not drift on each cycle. |
| `Common/Security/AclSnapshot.cs` | The captured DACL (SDDL), owner SID, and protection flag. |
| `Common/Security/PathGuard.cs` | Canonicalisation plus refusal of drive roots, `%SystemRoot%`, `%ProgramData%`, the Program Files and Users containers, reparse points, and GameLocker's own data directory `[D-8]` `[V-4]`. |
| `Common/State/FolderStateStore.cs` | One record per folder at `%ProgramData%\GameLocker\state\<sha256>.json`, restricted to SYSTEM + Administrators, written atomically (temp → `Flush(true)` → replace) `[F-2]`. |
| `Common/State/FolderStateRecord.cs` | Mode, timestamp, `AclApplied`, the snapshot, the vault manifest, and the last error. Replaces the `.gamelocker` marker `[F-1]` `[F-4]`. |
| `src/GameLocker.Recovery/` | Standalone elevated tool: `list`, `restore <folder>`, `restore-all`. Prints the `takeown` / `icacls /reset` fallback when a snapshot is missing. Added to `GameLocker.sln`. |

**Ordering guarantee:** the snapshot is written to the state store *before* the new DACL is applied, so an interruption between the two still leaves a folder that can be restored.

### `LockMode` — the default is now ACL-only

`GameLockerConfig.LockMode` defaults to `LockMode.AclOnly`, and configurations written before the setting existed deserialise to that default. Upgrading therefore stops the bulk encryption that previously ran on every scheduled lock.

**`EncryptFolderContentsAsync` and `CreateLockMarkerAsync` were deleted.** The former was the "encrypt everything, delete every original" path the scheduled lock actually ran, and the direct cause of the destroyed game install `[D-1]`. There is no longer any entry point that encrypts a folder wholesale — encryption happens only through `EncryptFolderWithCustomExtensionsAsync`, which requires an explicit selection, enforces `NeverEncryptExtensions`, and returns a manifest of exactly what it changed.

`GetFolderState` now derives state from the store and a live ACL probe, and returns `Unknown` when the record and the filesystem disagree, instead of asserting a state it cannot know `[F-1]`.

### Tests added

`ScheduleWindowTests`, `PathGuardTests`, `FolderStateStoreTests` — all `Category=Basic`, none require elevation. These cover the Phase 0 midnight-crossing fix and the Phase 1 guards and store.

### Deferred to later phases

Owner restoration is captured but not re-applied; effective-access verification by impersonation is Phase 7; the `AclHelper.DenyAccess` legacy path remains for the existing test suite and is no longer used in production.

---

## Phase 2 — Scheduler rewrite ✅ applied

`GameLockerService.ExecuteAsync` is now a **convergence loop, not a state machine**. Every 15 seconds it asks the same two questions of every folder — what should it be, and what is it — and acts on the difference.

The critical property: **the result of an action is never trusted.** After locking or unlocking, the folder is re-observed with `GetFolderState`, and only a folder that genuinely reached the required state counts as done. That removes the entire "reported success while nothing happened" class `[S-1]` `[S-4]`.

| Concern | Resolution |
|---|---|
| Failure recorded as success | Gone. There is no `_wasWithinAllowedTime` gate; a folder in the wrong state is retried every cycle until it converges `[S-1]` |
| No retry until the next boundary | `Common/Services/RetryPolicy.cs` — per-folder exponential backoff, 15s doubling to a 15min ceiling, so a permanently failing folder neither stalls nor floods the log |
| No reconciliation after a crash | Every cycle is a full reconciliation; there is no other code path `[S-4]` |
| Warnings that never fire | `Common/Services/LockWarningTracker.cs` — anchored to the window's end, each threshold fires once as it is crossed, at any sampling rate. Crossing several at once (service was stopped, machine asleep) shows only the most urgent `[S-3]` |
| Sleep, DST, clock tampering | `Common/Services/ClockDriftDetector.cs` — compares wall-clock delta against a monotonic `Stopwatch`; a divergence over 30s means sleep, resume, DST, or a deliberate clock change, and resets backoff, warnings and the announcement latch `[S-7]` |
| Silent stale config | `ConfigLoadResult` distinguishes `NotFound` from `Unreadable`. A damaged config is never treated as a first run; it is logged (throttled to once per 10 minutes) and the last known config keeps being enforced `[S-5]` |
| Non-atomic config write | `ConfigManager.SaveConfigAsync` writes to a temp file, `Flush(true)`, then `File.Replace` with `config.dat.bak` as the backup `[F-2]` |
| `Environment.Exit(1)` | Removed. Init failures propagate so the host stops cleanly and the SCM restart actions apply; a bad cycle is caught and the next one re-derives everything `[S-8]` |
| `Console.WriteLine` as the error channel | `ILogger` throughout `FolderLocker` and `FileExtensionScanner` (via `Microsoft.Extensions.Logging.Abstractions`), plus `Service/Logging/FileLoggerProvider.cs` — a dependency-free size-rotating file logger alongside the Event Log `[T-9]` |
| Service name mismatch | Reconciled to `GameLockerService` in `Program.cs` and `ServiceManager.cs`, matching every installer `[T-10]` |

### Deviation from the original plan

The plan called for `SystemEvents.TimeChanged` / `PowerModeChanged`. Those depend on a window receiving `WM_TIMECHANGE` and `WM_POWERBROADCAST`, and a service runs in session 0 with no message pump, so they are not dependable there. Comparing a wall clock against a monotonic clock always is, and it catches sleep, resume, DST and deliberate clock changes with one mechanism.

### Behaviour changes

- A manual `lock`/`unlock` command no longer persists — the next cycle restores whatever the schedule requires. The schedule is the authority.
- The command channel now also accepts a folder that has a state-store record but is no longer in the configuration, so a folder being removed can still be unlocked. Both sources are admin-only.
- "Service started" fires on the first successful config load rather than before one exists.

### Tests added

`RetryPolicyTests`, `LockWarningTrackerTests`, `ClockDriftDetectorTests` — all `Category=Basic`, no elevation. The scheduler's decision logic was extracted into these three types specifically so it could be tested without installing a service and waiting for a schedule boundary.

### Still open

The 15s tick is hardcoded; `PollingIntervalMinutes` remains dead and is removed from the UI in Phase 5. Effective-access verification and crash-injection tests are Phase 7.

---

## Phase 3 — Privilege and IPC hardening

- Replace the file-based command channel with a **named pipe** (`\\.\pipe\GameLocker`) whose `PipeSecurity` grants only `BUILTIN\Administrators` and `SYSTEM`. Rewrite `Common/Services/ServiceManager.cs` (keep the `ServiceController` wrapper; replace `SendImmediateActionCommandAsync` / `SendConfigReloadCommandAsync`) and `GameLockerService.ProcessImmediateCommandsAsync`. Adds the acknowledgement the file channel never had `[S-9]`.
- Add `app.manifest` with `requestedExecutionLevel level="requireAdministrator"` and `longPathAware` to `GameLocker.ConfigUI.csproj` `[G-1]` `[D-12]`.
- Harden the data-directory DACL at service start (idempotent), not only at install time.
- Reconcile the service name across all layers — `Program.cs:10`, `ServiceManager.cs:10`, `.iss:61`, `ServiceManager.ps1:10` `[T-10]` `[V-7]`.

---

## Phase 4 — Encryption demoted to an opt-in save vault

Only after Phases 1-3 are green. **The default remains ACL-only; encryption is never the default.**

- **New container format** in `AesEncryptionHelper`: magic + format version + key ID + **per-file random nonce** + original size + original attributes + **AES-GCM tag**. Fixes fixed-IV reuse `[X-1]`, the absent format `[X-2]`, and silent garbage-decryption `[D-6]` `[D-4]` `[D-7]` in one change. Chunked GCM so large files stream. Keep the existing `EncryptStream` / `DecryptStream` signatures; replace the bodies.
- **Write protocol:** journal intent → encrypt to `<name>.gltmp` → `Flush(true)` → verify by decrypting and comparing hash → `File.Replace` → commit journal. A crash at any point is resolvable from the journal `[D-2]` `[D-3]`.
- **Scope guards:** opt-in allowlist only; hard denylist no UI action overrides; per-file size cap; total-file-count cap; free-space precondition `[D-10]`.
- **Key backup gate:** encryption stays disabled until the user has exported and confirmed a key bundle. Add key backup / restore / rotation `[X-4]`. Reconsider `LocalMachine` scope and the hardcoded entropy `[X-3]`.
- **Manifest:** every encrypted file recorded in the state store, so a partial failure is always enumerable `[F-1]`.
- Rewrite `ForceDecrypt` as verify-only: it may report and it may decrypt, but it may never delete a ciphertext it could not verify `[D-5]`.

---

## Phase 5 — UI truth

`src/GameLocker.ConfigUI/Form1.cs` (818 lines, all handlers inline — extract a view-model in passing).

- Per-folder state column: Unlocked / Locked / **Partially locked** / Error, refreshed from the service `[G-5]`.
- Never remove a folder from config unless the unlock verifiably succeeded; on partial failure keep the entry and show what remains. Today the removal block after `ShowDialog` runs unconditionally and reports success in green even when every file failed `[G-2]`.
- Wrap `btnTestLock_Click` in `try/finally` plus a `ProcessExit` / `FormClosing` safety net, so a crash mid-dialog cannot leave a folder denied `[G-3]`.
- Config load failure must block editing and offer restore-from-backup — never present a blank form the user can save over their real folder list `[G-4]`.
- Move folder scanning off the UI thread; drop `Application.DoEvents()`; debounce `SelectedIndexChanged`; cache scan results `[G-6]`.
- Pre-lock confirmation stating exactly what will happen — *"deny access to 41,382 files — no files will be modified"* `[G-9]`.
- Remove the dead `PollingIntervalMinutes` control `[T-11]`.

---

## Phase 6 — Installer and uninstaller

- **Single installer.** Keep Inno; delete `installer/NSIS/` and `installer/WiX/`; reduce PowerShell to a dev helper `[H-5]`.
- **Uninstall must unlock first:** enumerate the state store, restore every ACL, decrypt every vault file, verify, and only then remove the service and binaries. Refuse to proceed, with a clear message, if any folder cannot be restored `[H-1]`.
- Never delete the data directory while any folder is locked; if the user opts to remove data, force a key export first `[H-2]`.
- Admin-only DACL on the data directory; drop the `{app}\Logs` `users-full` grant `[H-3]` `[V-5]`.
- Upgrade path: unlock everything → upgrade → re-lock. Never stop the service mid-operation `[H-4]`.
- Fix stale absolute paths in `Build-Installer.ps1:17-18`, `ServiceManager.ps1:11`, `BUILD-COMMANDS.txt:9`; derive the version from a single source `[H-6]`.
- Add Authenticode signing to the release pipeline `[V-6]`.

---

## Phase 7 — Tests and CI

- Add `tests/GameLocker.Tests` **to `GameLocker.sln`**, along with the tools `[T-5]`.
- Replace the eight `if (!IsRunningAsAdmin()) return;` early-exits — an early return is a **pass** in xUnit, so eight of fourteen tests currently report green without executing a single assertion `[T-2]`.
- Rewrite the ACL tests to assert **effective access** (open a handle / impersonate), not merely the presence of an ACE. The four existing "invariant" tests are exactly the ones that should have caught the SYSTEM self-deny, and are written in a way that cannot `[T-3]`.
- New suites: schedule-window generation (midnight crossing, DST, multi-day, 24h duration); ACL snapshot/restore round-trip; crash injection on lock/unlock; vault format round-trip and tamper detection; **LPE regression** — an unprivileged write to the command channel must be rejected.
- GitHub Actions on `windows-latest`: build, non-elevated tests, analyzers, `TreatWarningsAsErrors` `[T-4]`.
- Delete or gut the demo projects that hardcode `C:\Windows\System32` `[T-6]`.

---

## Phase 8 — Documentation honesty

- Rewrite the README security table. State plainly: **a self-discipline speed bump, not a security boundary.** Document the real bypasses — change the system clock; `sc stop`; run the shipped `UnlockTool`; recover the key in four lines of code `[V-3]`.
- Remove all claims that selective encryption is active until Phase 4 lands `[T-8]`.
- Document the recovery procedure (`GameLocker.Recovery.exe`, plus the `takeown` + `icacls /reset` fallback).
- Delete stale root artefacts: `FIXES-SUMMARY.md`, `BUILD-COMMANDS.txt`, `TestConfig.cs` (orphaned — belongs to no project) `[T-12]`.
- Update `CLAUDE.md` — several documented invariants change in Phase 1.

---

## Verification

> **Environment blocker.** No .NET SDK is installed on the development machine (`dotnet --version` fails with *"No .NET SDKs were found"*). Install the .NET 8 SDK before any of this can be built or tested. Nothing in the review was verified by execution; findings marked *(inferred)* in `ARCHITECTURE-REVIEW.md` still need runtime confirmation.

> **Test on a throwaway VM or spare drive only.** Never point this at a real game folder until Phase 7 is green.

| Phase | Verification |
|---|---|
| 0 | **LPE regression:** as an unprivileged user, write `unlock\|C:\Windows\|…` to the command file; the service must reject and log it. `ForceDecrypt` against a deliberately corrupted `.enc` must leave the ciphertext intact and exit non-zero. |
| 1 | `dotnet test tests/GameLocker.Tests/GameLocker.Tests.csproj --filter "Category=Basic"` — the new suites need no elevation. Then, elevated: `icacls <folder> /save before.txt`, lock, confirm a non-elevated Explorer is refused and `psexec -s cmd` can still list the folder, unlock, `icacls <folder> /save after.txt`, and diff. Confirm no file's timestamp or size changed. Kill the process between snapshot write and `ApplyLock`, then run `GameLocker.Recovery restore-all`. |
| 2 | `dotnet test tests/GameLocker.Tests/GameLocker.Tests.csproj --filter "Category=Basic"` covers the window model and all three scheduler primitives. Then, on a VM: kill the service mid-lock and confirm the next cycle converges; `icacls` the folder open by hand while locked and confirm it is re-locked within 15s; sleep the machine across a boundary and confirm the drift warning appears in `%ProgramData%\GameLocker\logs\service.log` followed by convergence; wind the clock back an hour and confirm the same. |
| 4 | Round-trip a fixture containing a small text file, a 5 GB binary, a read-only file, a hidden file, a long path, and a file legitimately named `foo.enc`; hash before and after. Flip one ciphertext bit — decryption must **fail loudly** and leave both files intact. Crash-inject at each journal step and confirm recovery. |
| 6 | Install → lock → run the uninstaller → confirm every folder is restored and accessible **before** the service is removed. |

**Acceptance criterion.** A full lock/unlock cycle on a 100 GB fixture, repeated 50 times with random process kills, ends with a byte-identical folder and an SDDL-identical ACL — every time.
