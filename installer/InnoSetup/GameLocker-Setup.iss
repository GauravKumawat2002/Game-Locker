; GameLocker Installer Script for Inno Setup
; Requires Inno Setup 6.0 or later: https://jrsoftware.org/isdl.php

#define MyAppName "GameLocker"
#define MyAppVersion "1.3.0"
#define MyAppPublisher "GauravKumawat2002"
#define MyAppURL "https://github.com/GauravKumawat2002/Game-Locker"
#define MyAppServiceExeName "GameLocker.Service.exe"
#define MyAppConfigExeName "GameLocker.exe"
#define MyAppRecoveryExeName "GameLocker.Recovery.exe"
#ifndef GameLockerPublishDirectory
  #define GameLockerPublishDirectory "..\..\installer-package\publish"
#endif

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{8F7A3B2C-9D4E-4F1A-B8C7-6E5D4C3B2A10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\
OutputBaseFilename=GameLocker-Installer-v{#MyAppVersion}
SetupIconFile=..\Icons\gamelocker.ico
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\ConfigUI\GameLocker.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startservice"; Description: "Start GameLocker service after installation"; GroupDescription: "Service Options:"; Flags: checkedonce

[Files]
; Service files
Source: "{#GameLockerPublishDirectory}\service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs createallsubdirs
; Config UI files
Source: "{#GameLockerPublishDirectory}\configui\*"; DestDir: "{app}\ConfigUI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#GameLockerPublishDirectory}\recovery\*"; DestDir: "{app}\Recovery"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[InstallDelete]
Type: files; Name: "{app}\ConfigUI\GameLocker.ConfigUI.exe"
Type: files; Name: "{app}\ConfigUI\GameLocker.ConfigUI.dll"
Type: files; Name: "{app}\ConfigUI\GameLocker.ConfigUI.deps.json"
Type: files; Name: "{app}\ConfigUI\GameLocker.ConfigUI.runtimeconfig.json"
Type: files; Name: "{app}\ConfigUI\GameLocker.ConfigUI.pdb"

[Dirs]
; These deliberately carry NO "Permissions: users-full".
;
; That grant made %ProgramData%\GameLocker writable by every account on the machine. The service
; runs as LocalSystem and polls that directory for a command file, so any unprivileged user could
; make SYSTEM operate on an arbitrary path - a local privilege escalation - and could delete
; folder_key.dat, permanently destroying every encrypted folder.
; See ARCHITECTURE-REVIEW.md findings [V-1], [V-2], [H-3] and [V-5].
;
; The explicit DACL is applied by the icacls steps in [Run] below.
Name: "{app}\Logs"
Name: "{commonappdata}\{#MyAppName}"

[Icons]
Name: "{group}\{#MyAppName} Configuration"; Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; IconFilename: "{app}\ConfigUI\GameLocker.ico"
Name: "{group}\{#MyAppName} Recovery"; Filename: "{app}\Recovery\{#MyAppRecoveryExeName}"; IconFilename: "{app}\ConfigUI\GameLocker.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\ConfigUI\GameLocker.ico"
Name: "{autodesktop}\{#MyAppName} Configuration"; Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; IconFilename: "{app}\ConfigUI\GameLocker.ico"; Tasks: desktopicon

[Run]
; Launch Config UI with Setup's elevated token.
Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')} Configuration}"; Flags: nowait postinstall runascurrentuser skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: dirifempty; Name: "{commonappdata}\{#MyAppName}"

[Code]
function HasCanonicalService(): Boolean;
begin
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\GameLockerService');
end;

function StopGameLockerServices(): Boolean;
var
  ExitCode: Integer;
  StopCommand: String;
begin
  StopCommand := '-NoProfile -NonInteractive -Command "$ErrorActionPreference = ''Stop''; ' +
    'foreach ($name in @(''GameLockerService'', ''GameLocker Service'')) { ' +
    '$service = Get-Service -Name $name -ErrorAction SilentlyContinue; ' +
    'if ($service -and $service.Status -ne ''Stopped'') { ' +
    'if ($service.Status -ne ''StopPending'') { Stop-Service -InputObject $service }; ' +
    '$service.WaitForStatus(''Stopped'', [TimeSpan]::FromSeconds(30)) } }"';
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    StopCommand, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
end;

procedure CurUninstallStepChanged(Step: TUninstallStep);
var
  ExitCode: Integer;
  RecoveryPath: String;
begin
  if Step <> usUninstall then
    Exit;

  if not StopGameLockerServices() then
  begin
    SuppressibleMsgBox('GameLocker could not stop its service. Uninstall was cancelled.', mbError, MB_OK, IDOK);
    Abort;
  end;

  RecoveryPath := ExpandConstant('{app}\Recovery\{#MyAppRecoveryExeName}');
  if not Exec(RecoveryPath, 'restore-all', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
  begin
    SuppressibleMsgBox('GameLocker could not verify that all game folders were restored. ' +
      'Uninstall was cancelled. Run "' + RecoveryPath + '" restore-all from an administrator terminal.',
      mbError, MB_OK, IDOK);
    Abort;
  end;

  if HasCanonicalService() then
  begin
    if not Exec(ExpandConstant('{sys}\sc.exe'), 'delete GameLockerService', '',
      SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
    begin
      SuppressibleMsgBox('Game folders were restored. The service could not be removed, so the installed files were kept.',
        mbError, MB_OK, IDOK);
      Abort;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ErrorCode: Integer;
  ServiceKey: String;
begin
  Result := '';
  ServiceKey := 'SYSTEM\CurrentControlSet\Services\';
  if RegKeyExists(HKEY_LOCAL_MACHINE, ServiceKey + 'GameLockerService') or
     RegKeyExists(HKEY_LOCAL_MACHINE, ServiceKey + 'GameLocker Service') then
  begin
    if not StopGameLockerServices() then
    begin
      Result := 'GameLocker could not stop its service. Upgrade was cancelled.';
      Exit;
    end;

    if RegKeyExists(HKEY_LOCAL_MACHINE, ServiceKey + 'GameLocker Service') then
      if not Exec(ExpandConstant('{sys}\sc.exe'), 'delete "GameLocker Service"',
        '', SW_HIDE, ewWaitUntilTerminated, ErrorCode) or (ErrorCode <> 0) then
        Result := 'GameLocker could not migrate its legacy service. Upgrade was cancelled.';
  end;
end;

procedure RunInstallCommand(FileName, Arguments, FailureMessage: String);
var
  ExitCode: Integer;
begin
  if not Exec(ExpandConstant('{sys}\' + FileName), Arguments, '', SW_HIDE,
    ewWaitUntilTerminated, ExitCode) then
    RaiseException(FailureMessage + ' ' + SysErrorMessage(ExitCode));
  if ExitCode <> 0 then
    RaiseException(FailureMessage + ' Exit code ' + IntToStr(ExitCode) + '.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigData: AnsiString;
  ConfigFile, Operation, ServicePath: String;
begin
  if CurStep = ssPostInstall then
  begin
    // Secure application data before the LocalSystem service can read it.
    RunInstallCommand('icacls.exe',
      '"' + ExpandConstant('{commonappdata}\{#MyAppName}') +
      '" /inheritance:r /grant *S-1-5-18:(OI)(CI)F /grant *S-1-5-32-544:(OI)(CI)F',
      'GameLocker could not secure its data directory.');
    RunInstallCommand('icacls.exe',
      '"' + ExpandConstant('{app}\Logs') +
      '" /inheritance:r /grant *S-1-5-18:(OI)(CI)F /grant *S-1-5-32-544:(OI)(CI)F',
      'GameLocker could not secure its log directory.');

    if HasCanonicalService() then
      Operation := 'config'
    else
      Operation := 'create';
    ServicePath := ExpandConstant('{app}\Service\{#MyAppServiceExeName}');
    RunInstallCommand('sc.exe', Operation + ' GameLockerService binPath= "\"' +
      ServicePath + '\"" start= auto DisplayName= "GameLocker Service"',
      'GameLocker could not register its Windows service.');
    RunInstallCommand('sc.exe',
      'description GameLockerService "Automatically locks and unlocks game folders based on configured schedules."',
      'GameLocker could not set its Windows service description.');
    RunInstallCommand('sc.exe', 'failure GameLockerService reset= 30 actions= restart/5000/restart/10000/restart/15000',
      'GameLocker could not configure its Windows service recovery.');

    // Create service configuration file
    ConfigFile := ExpandConstant('{commonappdata}\{#MyAppName}\service-config.json');
    ConfigData := '{'#13#10 +
      '  "ServiceName": "GameLockerService",'#13#10 +
      '  "InstallPath": "' + ExpandConstant('{app}') + '",'#13#10 +
      '  "DataPath": "' + ExpandConstant('{commonappdata}\{#MyAppName}') + '",'#13#10 +
      '  "LogPath": "' + ExpandConstant('{app}\Logs') + '",'#13#10 +
      '  "ConfigUIPath": "' + ExpandConstant('{app}\ConfigUI\{#MyAppConfigExeName}') + '",'#13#10 +
      '  "Version": "{#MyAppVersion}",'#13#10 +
      '  "InstallDate": "' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + '"'#13#10 +
      '}';
    
    ForceDirectories(ExtractFileDir(ConfigFile));
    SaveStringToFile(ConfigFile, ConfigData, False);

    if WizardIsTaskSelected('startservice') then
      RunInstallCommand('sc.exe', 'start GameLockerService', 'GameLocker could not start its Windows service.');
  end;
end;
