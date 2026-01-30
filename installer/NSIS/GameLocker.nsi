; GameLocker Installer
; Created with NSIS

;--------------------------------
; Modern UI
!include "MUI2.nsh"

;--------------------------------
; General
Name "GameLocker"
OutFile "GameLockerSetup.exe"
InstallDir "$PROGRAMFILES\GameLocker"
RequestExecutionLevel admin

;--------------------------------
; Variables
Var StartMenuFolder

;--------------------------------
; Interface Settings
!define MUI_ABORTWARNING
;!define MUI_ICON "icon.ico"  ; Comment out icon for now
!define MUI_WELCOMEPAGE_TITLE "GameLocker Installer"
!define MUI_WELCOMEPAGE_TEXT "This installer will install GameLocker, a Windows service that controls access to game folders based on your schedule.$\r$\n$\r$\nGameLocker will encrypt your game files when access is not allowed and decrypt them during allowed gaming hours."

;--------------------------------
; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "license.txt"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_STARTMENU Application $StartMenuFolder
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

;--------------------------------
; Languages
!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Sections

Section "GameLocker" SecMain
    SectionIn RO
    
    ; Set output path to the installation directory
    SetOutPath $INSTDIR
    
    ; Copy service files
    File /r "..\..\publish\*.*"
    
    ; Create data directory
    CreateDirectory "$APPDATA\GameLocker"
    CreateDirectory "$PROGRAMFILES\GameLocker\Data"
    
    ; Register event log source
    nsExec::ExecToLog 'powershell.exe -Command "New-EventLog -LogName Application -Source ''GameLocker Service'' -ErrorAction SilentlyContinue"'
    
    ; Install the service
    nsExec::ExecToLog 'sc.exe create "GameLocker Service" binpath= "$INSTDIR\GameLocker.Service.exe" start= auto'
    
    ; Start the service
    nsExec::ExecToLog 'sc.exe start "GameLocker Service"'
    
    ; Create shortcuts
    !insertmacro MUI_STARTMENU_WRITE_BEGIN Application
        CreateDirectory "$SMPROGRAMS\$StartMenuFolder"
        CreateShortcut "$SMPROGRAMS\$StartMenuFolder\GameLocker Configuration.lnk" "$INSTDIR\GameLocker.ConfigUI.exe"
        CreateShortcut "$SMPROGRAMS\$StartMenuFolder\Uninstall GameLocker.lnk" "$INSTDIR\Uninstall.exe"
    !insertmacro MUI_STARTMENU_WRITE_END
    
    ; Desktop shortcut
    CreateShortcut "$DESKTOP\GameLocker Configuration.lnk" "$INSTDIR\GameLocker.ConfigUI.exe"
    
    ; Write uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Registry entries
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker" "DisplayName" "GameLocker"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker" "Publisher" "GameLocker Inc"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker" "DisplayVersion" "1.0.0"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker" "InstallLocation" "$INSTDIR"
    
    ; Launch configuration UI after installation
    Exec '"$INSTDIR\GameLocker.ConfigUI.exe"'
    
SectionEnd

;--------------------------------
; Uninstaller Section

Section "Uninstall"
    
    ; Stop and remove service
    nsExec::ExecToLog 'sc.exe stop "GameLocker Service"'
    Sleep 2000
    nsExec::ExecToLog 'sc.exe delete "GameLocker Service"'
    
    ; Remove files
    Delete "$INSTDIR\*.*"
    RMDir /r "$INSTDIR"
    
    ; Remove shortcuts
    !insertmacro MUI_STARTMENU_GETFOLDER Application $StartMenuFolder
    Delete "$SMPROGRAMS\$StartMenuFolder\*.*"
    RMDir "$SMPROGRAMS\$StartMenuFolder"
    Delete "$DESKTOP\GameLocker Configuration.lnk"
    
    ; Remove registry entries
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker"
    
    ; Optional: Remove data directory (ask user)
    MessageBox MB_YESNO "Do you want to remove all GameLocker configuration and data files?" IDNO +3
    RMDir /r "$PROGRAMFILES\GameLocker\Data"
    RMDir /r "$APPDATA\GameLocker"
    
SectionEnd

;--------------------------------
; Functions

Function .onInit
    ; Check if already installed
    ReadRegStr $R0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GameLocker" "UninstallString"
    StrCmp $R0 "" done
    
    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "GameLocker is already installed. $\n$\nClick OK to remove the previous version or Cancel to cancel this upgrade." IDOK uninst
    Abort
    
    uninst:
        ClearErrors
        ExecWait '$R0 /S _?=$INSTDIR'
        
        IfErrors no_remove_uninstaller done
        no_remove_uninstaller:
    
    done:
FunctionEnd