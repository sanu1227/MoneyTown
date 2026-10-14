#ifndef AppVersion
  #error AppVersion is required. Run Build-Installer.ps1.
#endif

[Setup]
; Keep this identity and install scope unchanged across updates.
AppId=MoneyTown.Unity.Windows
AppName=MoneyTown
AppVersion={#AppVersion}
AppPublisher=MoneyTown
DefaultDirName={localappdata}\Programs\MoneyTown
DefaultGroupName=MoneyTown
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=MoneyTown-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\MoneyTown.exe
InfoBeforeFile=install-guide.txt

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: checkedonce

[Files]
Source: "..\dist\unity\Windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Replace only Unity-generated payload directories, never {app} or user data.
Type: filesandordirs; Name: "{app}\MoneyTown_Data"
Type: filesandordirs; Name: "{app}\MonoBleedingEdge"
Type: filesandordirs; Name: "{app}\D3D12"
Type: files; Name: "{app}\WinPixEventRuntime.dll"

[Icons]
Name: "{group}\MoneyTown"; Filename: "{app}\MoneyTown.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\MoneyTown"; Filename: "{app}\MoneyTown.exe"; WorkingDir: "{app}"; Tasks: desktopicon

; Saves and PlayerPrefs live outside {app}. Never delete them on update/uninstall.
[Run]
Filename: "{app}\MoneyTown.exe"; Description: "{cm:LaunchProgram,MoneyTown}"; Flags: nowait postinstall skipifsilent
