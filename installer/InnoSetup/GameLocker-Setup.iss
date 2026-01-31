; GameLocker Installer Script for Inno Setup
; Requires Inno Setup 6.0 or later: https://jrsoftware.org/isdl.php

#define MyAppName "GameLocker"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "GauravKumawat2002"
#define MyAppURL "https://github.com/GauravKumawat2002/Game-Locker"
#define MyAppServiceExeName "GameLocker.Service.exe"
#define MyAppConfigExeName "GameLocker.ConfigUI.exe"

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
Source: "..\..\installer-package\publish\service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs createallsubdirs
; Config UI files
Source: "..\..\installer-package\publish\configui\*"; DestDir: "{app}\ConfigUI"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Dirs]
Name: "{app}\Logs"; Permissions: users-full
Name: "{commonappdata}\{#MyAppName}"; Permissions: users-full

[Icons]
Name: "{group}\{#MyAppName} Configuration"; Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName} Configuration"; Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; Tasks: desktopicon

[Run]
; Install and start the Windows Service
Filename: "sc.exe"; Parameters: "create GameLockerService binPath= ""{app}\Service\{#MyAppServiceExeName}"" start= auto DisplayName= ""GameLocker Service"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "description GameLockerService ""Automatically locks and unlocks game folders based on configured schedules."""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "failure GameLockerService reset= 30 actions= restart/5000/restart/10000/restart/15000"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start GameLockerService"; Flags: runhidden waituntilterminated; Tasks: startservice
; Launch Config UI after install
Filename: "{app}\ConfigUI\{#MyAppConfigExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')} Configuration}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop and remove the Windows Service
Filename: "sc.exe"; Parameters: "stop GameLockerService"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete GameLockerService"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: dirifempty; Name: "{commonappdata}\{#MyAppName}"

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  NetFrameworkInstalled: Boolean;
begin
  Result := True;
  
  // Check if .NET 8.0 Runtime is installed (not needed for self-contained builds)
  // This is just a placeholder - we're using self-contained publish
  
  // Check if service already exists
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\GameLockerService') then
  begin
    if MsgBox('GameLocker service is already installed. Do you want to upgrade?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
    
    // Stop existing service
    Exec('sc.exe', 'stop GameLockerService', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
    Sleep(2000);
    
    // Delete existing service
    Exec('sc.exe', 'delete GameLockerService', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
    Sleep(2000);
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
