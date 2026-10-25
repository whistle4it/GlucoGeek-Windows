#ifndef AppVersion
  #define AppVersion "0.4.1"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist-installer"
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\installer-output"
#endif

[Setup]
AppId={{D54ACB18-B234-45D9-9284-9E7594C6EF02}
AppName=GlucoGeek
AppVersion={#AppVersion}
AppPublisher=whistle4it
AppPublisherURL=https://github.com/whistle4it/GlucoGeek-Windows
AppSupportURL=https://github.com/whistle4it/GlucoGeek-Windows/issues
; Keep the original location and AppId so this upgrades the existing installation.
DefaultDirName={localappdata}\Programs\Librewin
DisableDirPage=yes
UsePreviousAppDir=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#InstallerOutputDir}
OutputBaseFilename=GlucoGeek-v{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\Resources\AppIcon.ico
UninstallDisplayIcon={app}\GlucoGeek.exe
AppMutex=Local\Librewin
CloseApplications=yes
RestartApplications=no
UsePreviousTasks=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoDescription=GlucoGeek Windows installer

[Tasks]
Name: "startup"; Description: "Start GlucoGeek when I sign in to Windows"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\GlucoGeek"; Filename: "{app}\GlucoGeek.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\GlucoGeek"; Filename: "{app}\GlucoGeek.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Librewin"; Flags: deletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GlucoGeek"; ValueData: """{app}\GlucoGeek.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "GlucoGeek"; Flags: deletevalue; Tasks: not startup

[InstallDelete]
; Remove only the old, specifically named startup shortcut to avoid two launches.
Type: files; Name: "{userstartup}\GlucoGeek.lnk"
Type: files; Name: "{userstartup}\Librewin.lnk"
Type: files; Name: "{userprograms}\Librewin.lnk"
Type: files; Name: "{userdesktop}\Librewin.lnk"
Type: files; Name: "{app}\Librewin.exe"
Type: files; Name: "{app}\Librewin.dll"
Type: files; Name: "{app}\Librewin.deps.json"
Type: files; Name: "{app}\Librewin.runtimeconfig.json"
Type: files; Name: "{app}\Librewin.pdb"

[Run]
Filename: "{app}\GlucoGeek.exe"; WorkingDir: "{app}"; Description: "Launch GlucoGeek"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard;
begin
  { Preserve the app's current startup preference for interactive upgrades.
    Silent deployments choose explicitly with /TASKS="startup". }
  if not WizardSilent then begin
    if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GlucoGeek') or
      RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Librewin') or
      FileExists(ExpandConstant('{userstartup}\Librewin.lnk')) or
      FileExists(ExpandConstant('{userstartup}\GlucoGeek.lnk')) then
      WizardSelectTasks('startup');
  end;
end;
