# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

GameLocker is a Windows-only .NET 8 app that enforces a gaming schedule by **denying NTFS access to game folders** outside allowed hours. A Windows Service does the enforcement; a WinForms app configures it. All projects target `net8.0-windows10.0.17763.0`.

Read [`ARCHITECTURE-REVIEW.md`](ARCHITECTURE-REVIEW.md) and [`REMEDIATION-PLAN.md`](REMEDIATION-PLAN.md) before changing anything in `Security/`, `State/`, `Services/FolderLocker.cs`, or the service loop. v1.2.0 destroyed a user's game install and could permanently lock folders away from Administrators; Phases 0 and 1 of the plan are applied, Phases 2-8 are not. **Nothing has been compiled or run** — there is no .NET SDK on the development machine.

Default locking is `LockMode.AclOnly`: permissions change, file contents never do. Encryption is opt-in (`LockMode.AclAndVault`), allowlist-only, and still carries the unfixed Phase 4 defects (no MAC, no header, one fixed IV for every file) — do not point it at data that matters. Test with throwaway folders only.

## Commands

```powershell
# Build (builds GameLocker.Common / .ConfigUI / .Service / .Recovery — see "Solution layout" below)
dotnet build GameLocker.sln
dotnet build -c Release

# Run (as Administrator for ACL operations to work)
dotnet run --project src/GameLocker.ConfigUI
dotnet run --project src/GameLocker.Service     # service runs as a console app in dev

# Repair: works with no service installed, requires elevation
dotnet run --project src/GameLocker.Recovery -- list
dotnet run --project src/GameLocker.Recovery -- restore "D:\Games\Title"
dotnet run --project src/GameLocker.Recovery -- restore-all

# Tests — the test project is NOT in the .sln, so the path is required
dotnet test tests/GameLocker.Tests/GameLocker.Tests.csproj
dotnet test tests/GameLocker.Tests/GameLocker.Tests.csproj --filter "Category=Basic"          # no admin needed
dotnet test tests/GameLocker.Tests/GameLocker.Tests.csproj --filter "FullyQualifiedName~DenyAccess_SetsAccessDenied"   # single test

# Installer / release build
.\Build-Installer.ps1 -BuildType Both    # ZIP + Inno Setup EXE; see caveats below
```

`Category=RequiresAdmin` tests **return early and report as passing** when not elevated (`if (!AclHelper.IsRunningAsAdmin()) return;`). A green run from a non-elevated shell proves nothing about ACL behavior — re-run elevated when touching `AclHelper`.

### Solution layout gotcha

`GameLocker.sln` contains four projects: `GameLocker.Common`, `.ConfigUI`, `.Service`, `.Recovery`. `tests/GameLocker.Tests`, `UnlockTool`, `ForceDecrypt`, `CheckConfig`, and the `src/Test*` / `src/FullGameLockerDemo` demo projects each have their own `.csproj` outside the solution — they are only built when named explicitly (`dotnet build <path>`, `dotnet run --project <path>`). A change that breaks them will not show up in a normal `dotnet build`. Phase 7 adds the test project to the solution.

## Architecture

Three components, all business logic in the shared library:

- `src/GameLocker.Common` — everything real: `Services/FolderLocker` (lock/unlock), `Security/FolderAclManager` (the ACL engine), `Security/PathGuard` (what may be locked), `State/FolderStateStore` (the source of truth), `Encryption/` (AES-256 streaming + DPAPI), `Configuration/ConfigManager`, `Services/ServiceManager` (UI→service commands), `Services/FileExtensionScanner`.
- `src/GameLocker.Service` — `BackgroundService` (`GameLockerService.cs`) hosting the schedule loop; registered via `AddWindowsService`.
- `src/GameLocker.ConfigUI` — single-form WinForms app (`Form1.cs`, ~800 lines, all handlers inline).
- `src/GameLocker.Recovery` — standalone elevated repair tool. Does not need the service, and is the documented way out of a stuck folder.

### `C:\ProgramData\GameLocker\` is the shared contract

Both processes coordinate exclusively through this directory — there is no RPC, pipe, or socket.

| File | Written by | Purpose |
|---|---|---|
| `config.dat`, `keys.dat`, `iv.dat` | `ConfigManager` | AES-encrypted config + its DPAPI-protected key/IV |
| `folder_key.dat`, `folder_iv.dat` | `FolderLocker` | **Separate** DPAPI-protected key/IV used for game files |
| `state\<sha256>.json` | `FolderStateStore` | one record per managed folder: mode, ACL snapshot, vault manifest. **The source of truth** — restricted to SYSTEM + Administrators, written atomically |
| `reload_signal` | ConfigUI (on Save) | service reloads config and immediately re-applies lock state |
| `immediate_action` | ConfigUI | one command, format `action|folderPath|timestamp`, actions `lock`/`unlock`/`remove`. The service now rejects any path not already in `GameFolderPaths` — this channel was an unprivileged LPE (`[V-1]`). Phase 3 replaces it with an ACL'd named pipe |

The two key pairs are independent: deleting `folder_key.dat` makes every `.enc` file unrecoverable, regardless of `keys.dat`. Both are DPAPI `LocalMachine` scope — keys do not survive a machine change, so encrypted folders cannot be moved between machines.

The service consumes-and-deletes the signal files, so a command is lost if the service isn't running. `GameLockerConfig.PollingIntervalMinutes` is edited in the UI and persisted but **never read by the service**; it is removed from the UI in Phase 5.

### The service is a convergence loop, not a state machine

`GameLockerService.ReconcileAsync` runs every 15 s (hardcoded) and, for every configured folder, compares the state the schedule requires against `FolderLocker.GetFolderState`. It acts only on the difference, and — this is the important part — **re-observes afterwards rather than trusting the return value.** Only a folder that genuinely reached the required state counts as done; anything else is retried with per-folder exponential backoff.

Do not reintroduce a "did the schedule just flip?" flag. That design recorded a failed lock as a success and never retried it until the next boundary, which is the reported "sometimes it works, sometimes it doesn't" (`[S-1]`, `[S-4]`).

Three pure helpers in `Common/Services/` hold the decision logic so it is testable without installing a service: `RetryPolicy`, `LockWarningTracker`, `ClockDriftDetector`. `ClockDriftDetector` compares wall-clock delta against a monotonic `Stopwatch` — used instead of `SystemEvents.TimeChanged`/`PowerModeChanged`, which need a message pump a session-0 service does not have. A jump over 30 s resets backoff, warnings, and the announcement latch.

A manual `lock`/`unlock` command does not survive the next cycle. The schedule is the authority.

### Logging

`ILogger` everywhere; `Console.WriteLine` reaches nothing from a service. Output goes to the Event Log **and** to `%ProgramData%\GameLocker\logs\service.log` via `Service/Logging/FileLoggerProvider.cs` (size-rotating, dependency-free). `FolderLocker` and `FileExtensionScanner` take an optional `ILogger` and default to `NullLogger`.

### What "locked" means

`FolderLocker.LockFolderAsync(path, mode, vaultSettings)`:

1. `PathGuard.TryValidate` — canonicalise, and refuse drive roots, `%SystemRoot%`, `%ProgramData%`, the Program Files / Users containers, reparse points, and GameLocker's own data directory.
2. `FolderAclManager.Capture` the current DACL, owner and protection flag, and **persist the record to the state store before touching anything**. An interruption after this point is always recoverable.
3. Only when `mode == AclAndVault`: encrypt the selected files and record a manifest.
4. `FolderAclManager.ApplyLock` — install a **protected DACL granting only SYSTEM and Administrators** — then `VerifyLocked`.

Unlock decrypts any manifest entries first (the process still holds access), restores the DACL from the snapshot **in a `finally`**, and only deletes the record when nothing is outstanding. A partial failure keeps the record, keeps the error, and reports `Locked` rather than claiming success.

### ACL invariant (do not break this)

**Never write a Deny ACE.** Deny beats Allow for any group in the token, `FullControl` includes `WRITE_DAC`, and a LocalSystem token is a member of `BUILTIN\Users` — so the old `DenyAccess` locked the service out of the folders it was responsible for unlocking, and no Allow rule could rescue it. That is the "not even Administrator can get in" bug (`[P-1]`, `[P-2]`).

`FolderAclManager.ApplyLock` instead sets a protected DACL with two Allow entries, SYSTEM and Administrators. Under UAC an admin's ordinary token carries Administrators as deny-only, so Explorer and the game are refused while elevated recovery always works. `Capture` stores **explicit entries only** — writing inherited entries back literally would duplicate them on every cycle.

`AclHelper.DenyAccess`/`AllowAccess` still exist for the legacy test suite and for reopening folders locked by older builds. They are not used in production; don't reintroduce them.

### There is no "encrypt everything" code path

`EncryptFolderContentsAsync` and `CreateLockMarkerAsync` were deleted in Phase 1. Encryption is reachable only through `EncryptFolderWithCustomExtensionsAsync`, which requires an explicit selection, enforces `FileExtensionScanner.NeverEncryptExtensions` (a hard denylist no setting can override — `.dat`, `.toc`, `.pak`, `.exe`, `.dll`, …), and returns a manifest. Do not add a bulk path back.

`GetFolderState` reads the state store plus a live ACL probe. `Unknown` now genuinely means the record and the filesystem disagree.

Folder removal in ConfigUI does **not** go through the service: `Form1.DecryptAndRemoveFolderAsync` decrypts directly in-process with a modal progress dialog (see CHANGELOG 1.2.0 — service-mediated decryption had timing issues). It still has finding `[G-2]` open: it removes the folder from config even when decryption failed.

## Naming and path landmines

- **Service name is `GameLockerService` everywhere** — `Program.cs`, `ServiceManager.cs`, both installers and `ServiceManager.ps1`. `"GameLocker Service"` (with a space) is the *display* name only. These disagreed before Phase 2; keep them in sync.
- **Build scripts contain stale absolute paths** from a previous machine: `Build-Installer.ps1`, `ServiceManager.ps1` (`$ServicePath`), `BUILD-COMMANDS.txt`, and `FIXES-SUMMARY.md` all hardcode `h:\Work\10xProductivity\game_locker\code`. They will not run in this repo without editing. They also hardcode version `1.1.0` (as does `installer/InnoSetup/GameLocker-Setup.iss`) while the repo is at v1.2.0 — bump these when releasing.
- **`UnlockTool` and `ForceDecrypt` default to `G:\Hogwarts Legacy`** when run without an argument. Always pass an explicit path.
- Service and ConfigUI must be published `--self-contained true`; framework-dependent builds fail on end-user machines with a ".NET 8.0 required" error (see `FIXES-SUMMARY.md`).

## Conventions

Conventional commits (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`). File-scoped namespaces, `_camelCase` private fields, XML doc comments on public APIs, `Async` suffix on async methods (`async void` only for WinForms event handlers). Nullable reference types enabled everywhere.

Detailed developer docs live in `docs/` (`01-architecture.md`, `02-getting-started.md`, `03-api-reference.md`, `04-contributing.md`, `05-troubleshooting.md`); `05-troubleshooting.md` is the best reference for recovery scenarios (padding errors, stuck ACLs, missing keys).
