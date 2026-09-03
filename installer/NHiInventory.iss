; ============================================================================
;  NHI Inventory Management System - Windows Installer (Inno Setup 6)
; ----------------------------------------------------------------------------
;  Build:
;     1. Publish the app (self-contained, win-x64) into  installer\publish\
;        dotnet publish NHi.Inventory\NHi.Inventory.csproj -c Release -r win-x64 ^
;          --self-contained true -p:PublishSingleFile=false -p:DebugType=none ^
;          -p:DebugSymbols=false -o installer\publish
;     2. Compile this script:
;        "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\NHiInventory.iss
;     3. Result:  installer\Output\NHI-Inventory-Setup.exe
;
;  User data (SQLite database + settings) lives in
;     %LOCALAPPDATA%\NHiInventory\
;  which this installer never touches, so upgrades and uninstalls keep it safe.
; ============================================================================

#define AppName        "NHI Inventory Management System"
#define AppShortName   "NHI Inventory"
; Version can be overridden from the command line:  ISCC /DAppVersion=1.1.0 NHiInventory.iss
#ifndef AppVersion
  #define AppVersion   "1.0.0"
#endif
#define AppPublisher   "NHI"
#define AppExeName     "NHi.Inventory.exe"
#define AppId          "{{9E4B1C7A-2F86-4D53-A1E9-7C0B5D6F8A24}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Copyright (C) NHI 2026

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

; Modern wizard look, with the user able to pick the install folder.
WizardStyle=modern
SetupIconFile=..\NHi.Inventory\Resources\AppIcon.ico
OutputDir=Output
OutputBaseFilename=NHI-Inventory-Setup
Compression=lzma2/max
SolidCompression=yes

; 64-bit only (the app is published for win-x64).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Needs admin to write to Program Files; user may switch to a per-user
; install from the wizard or the command line (/CURRENTUSER).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline

; If the app is running during an upgrade, close it gracefully via the
; Windows Restart Manager instead of failing on locked files.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Everything produced by "dotnet publish" (exe, DLLs, native libs, runtimes\...).
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; NOTE: the per-user data folder %LOCALAPPDATA%\NHiInventory\ (SQLite database +
; settings) is created by the application itself on first run and is never
; written to or deleted by this installer, so it survives upgrades/uninstalls.

[InstallDelete]
; Clear the versioned native/runtime folder on upgrade so stale files never linger.
; The per-user database folder is deliberately NOT listed anywhere in this section.
Type: filesandordirs; Name: "{app}\runtimes"

[Icons]
; Start Menu: launch + uninstall entries.
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Open {#AppName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
; Desktop shortcut (checkbox on the wizard, ticked by default).
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Offer to start the app right after a successful install.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove only the installed program folder. User data under
; %LOCALAPPDATA%\NHiInventory\ is intentionally left untouched.
Type: filesandordirs; Name: "{app}"
