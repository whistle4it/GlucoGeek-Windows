#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist-installer"
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\installer-output"
#endif

[Setup]
AppId={{D54ACB18-B234-45D9-9284-9E7594C6EF02}
AppName=Librewin
AppVersion={#AppVersion}
AppPublisher=whistle4it
AppPublisherURL=https://github.com/whistle4it/Librewin
AppSupportURL=https://github.com/whistle4it/Librewin/issues
DefaultDirName={localappdata}\Programs\Librewin
DisableDirPage=yes
UsePreviousAppDir=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#InstallerOutputDir}
OutputBaseFilename=Librewin-v{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Librewin.exe
AppMutex=Local\Librewin
CloseApplications=yes
RestartApplications=no
UsePreviousTasks=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Librewin Windows installer

[Tasks]
Name: "startup"; Description: "Start Librewin when I sign in to Windows"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Librewin"; Filename: "{app}\Librewin.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Librewin"; Filename: "{app}\Librewin.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Librewin"; ValueData: """{app}\Librewin.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Librewin"; Flags: deletevalue; Tasks: not startup

[InstallDelete]
; Remove only the old, specifically named startup shortcut to avoid two launches.
Type: files; Name: "{userstartup}\Librewin.lnk"

[Run]
Filename: "{app}\Librewin.exe"; WorkingDir: "{app}"; Description: "Launch Librewin"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard;
begin
  { Preserve the app's current startup preference for interactive upgrades.
    Silent deployments choose explicitly with /TASKS="startup". }
  if not WizardSilent then begin
    if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Librewin') or
      FileExists(ExpandConstant('{userstartup}\Librewin.lnk')) then
      WizardSelectTasks('startup');
  end;
end;
