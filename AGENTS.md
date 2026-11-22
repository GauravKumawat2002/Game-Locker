# Repository Guidelines

## Project Structure

The four application projects live under `src/` and build through `GameLocker.sln`. `GameLocker.Common` owns shared configuration, scheduling, locking, and recovery state. `GameLocker.Service` runs the Windows service, `GameLocker.ConfigUI` is the WinForms configuration app, and `GameLocker.Recovery` restores locked folders. UI artwork lives in `src/GameLocker.ConfigUI/Assets/`, and the app icon lives in `installer/Icons/`. Find packaging sources in `installer/`, xUnit tests in `tests/GameLocker.Tests/`, and contributor documentation in `docs/`.

## Build and Run

Use Windows 10 or 11 with the .NET 8 SDK. Run these commands from the repository root.

- `dotnet build GameLocker.sln -c Release` restores dependencies and builds the application projects.
- `dotnet run --project src/GameLocker.ConfigUI` opens the configuration app from an elevated PowerShell session.
- `.\Build-Installer.ps1 -BuildType ZIP` builds a self-contained Windows x64 package. Use `-BuildType Both` with Inno Setup for ZIP and EXE packages.

## Coding Style

Use four spaces, file-scoped namespaces, and `System` imports before project imports. Use `PascalCase` for types and public members, `camelCase` for locals and parameters, and `_camelCase` for private fields. Suffix asynchronous methods with `Async` and add XML documentation for public APIs. Match nearby code and keep the build free of new warnings.

## Testing

Run `dotnet test tests/GameLocker.Tests/GameLocker.Tests.csproj -c Release` directly. Group xUnit tests by component in `*Tests.cs` files and use descriptive method names such as `GetNextUnlockTime_FindsTheNextWindowStart`. Add `--collect "XPlat Code Coverage"` for optional Coverlet coverage. Set `GAMELOCKER_TEST_INSTALLER` to the built ZIP or EXE only on a fresh, disposable Windows machine running an elevated shell. Use disposable folders for manual lock and recovery checks.

## Commits and Pull Requests

Check the working tree, remote, and target branch before editing. Make, verify, and commit repository changes in the local checkout, then publish with `git push`. Stop and report the exact issue if a safe push is blocked.

Use short Conventional Commit subjects such as `fix(config): restore Print Screen and branding` or `docs: update contributor setup`. Base pull requests on `master`. Summarize the change, list the checks you ran, link related issues when available, and include screenshots for UI changes.
