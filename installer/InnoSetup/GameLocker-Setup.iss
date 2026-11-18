; GameLocker Installer Script for Inno Setup
; Requires Inno Setup 6.0 or later: https://jrsoftware.org/isdl.php

#define MyAppName "GameLocker"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "GauravKumawat2002"
#define MyAppURL "https://github.com/GauravKumawat2002/Game-Locker"
#define MyAppServiceExeName "GameLocker.Service.exe"
#define MyAppConfigExeName "GameLocker.ConfigUI.exe"
#define MyAppRecoveryExeName "GameLocker.Recovery.exe"
#ifndef GameLockerPublishDirectory
  #define GameLockerPublishDirectory "..\..\installer-package\publish"
#endif

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{8F7A3B2C-9D4E-4F1A-B8C7-6E5D4C3B2A10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
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
; SetupIconFile=..\Icons\gamelocker.ico
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\ConfigUI\{#MyAppConfigExeName}

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
Name: "{group}\{#MyAppName} Configuration"; Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"
Name: "{group}\{#MyAppName} Recovery"; Filename: "{app}\Recovery\{#MyAppRecoveryExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName} Configuration"; Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; Tasks: desktopicon

[Run]
; Lock down the data directory BEFORE the service starts. Well-known SIDs are used so this is
; independent of the display language: S-1-5-18 = LocalSystem, S-1-5-32-544 = Administrators.
; /inheritance:r removes the inherited grants that would otherwise let standard users write here.
Filename: "icacls.exe"; Parameters: """{commonappdata}\{#MyAppName}"" /inheritance:r /grant *S-1-5-18:(OI)(CI)F /grant *S-1-5-32-544:(OI)(CI)F"; Flags: runhidden waituntilterminated
Filename: "icacls.exe"; Parameters: """{app}\Logs"" /inheritance:r /grant *S-1-5-18:(OI)(CI)F /grant *S-1-5-32-544:(OI)(CI)F"; Flags: runhidden waituntilterminated

; Install and start the Windows Service
Filename: "sc.exe"; Parameters: "create GameLockerService binPath= ""{app}\Service\{#MyAppServiceExeName}"" start= auto DisplayName= ""GameLocker Service"""; Flags: runhidden waituntilterminated; Check: not HasCanonicalService
Filename: "sc.exe"; Parameters: "config GameLockerService binPath= ""{app}\Service\{#MyAppServiceExeName}"" start= auto DisplayName= ""GameLocker Service"""; Flags: runhidden waituntilterminated; Check: HasCanonicalService
Filename: "sc.exe"; Parameters: "description GameLockerService ""Automatically locks and unlocks game folders based on configured schedules."""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "failure GameLockerService reset= 30 actions= restart/5000/restart/10000/restart/15000"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start GameLockerService"; Flags: runhidden waituntilterminated; Tasks: startservice
; Launch Config UI after install
Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')} Configuration}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; InitializeUninstall has stopped the service and verified recovery.
Filename: "sc.exe"; Parameters: "delete GameLockerService"; Flags: runhidden

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

function InitializeUninstall(): Boolean;
var
  ExitCode: Integer;
  RecoveryPath: String;
begin
  Result := False;
  if not StopGameLockerServices() then
  begin
    SuppressibleMsgBox('GameLocker could not stop its service. Uninstall was cancelled.', mbError, MB_OK, IDOK);
    Exit;
  end;

  RecoveryPath := ExpandConstant('{app}\Recovery\{#MyAppRecoveryExeName}');
  if not Exec(RecoveryPath, 'restore-all', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
  begin
    SuppressibleMsgBox('GameLocker could not verify that all game folders were restored. ' +
      'Uninstall was cancelled. Run "' + RecoveryPath + '" restore-all from an administrator terminal.',
      mbError, MB_OK, IDOK);
    Exit;
  end;

  Result := True;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  ServiceKey: String;
begin
  Result := True;
  ServiceKey := 'SYSTEM\CurrentControlSet\Services\';
  if RegKeyExists(HKEY_LOCAL_MACHINE, ServiceKey + 'GameLockerService') or
     RegKeyExists(HKEY_LOCAL_MACHINE, ServiceKey + 'GameLocker Service') then
  begin
    if SuppressibleMsgBox('GameLocker service is already installed. Do you want to upgrade?',
      mbConfirmation, MB_YESNO, IDYES) = IDNO then
    begin
      Result := False;
      Exit;
    end;

    Result := StopGameLockerServices();
    if Result and RegKeyExists(HKEY_LOCAL_MACHINE, ServiceKey + 'GameLocker Service') then
      Result := Exec(ExpandConstant('{sys}\sc.exe'), 'delete "GameLocker Service"',
        '', SW_HIDE, ewWaitUntilTerminated, ErrorCode) and (ErrorCode = 0);

    if not Result then
      SuppressibleMsgBox('GameLocker could not stop or migrate its service. Upgrade was cancelled.',
        mbError, MB_OK, IDOK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigData: AnsiString;
  ConfigFile: String;
begin
  if CurStep = ssPostInstall then
  begin
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
  end;
end;
