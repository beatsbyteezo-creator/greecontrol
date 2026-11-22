#define ProductName "Gree Control"
#define ProductExe "GreeControl.exe"
#define ProductVersion GetEnv("APP_VERSION")

[Setup]
AppId={{6387EC06-24FA-4308-97A5-DAB586C8F463}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} {#ProductVersion}
AppPublisher=GreeControl
DefaultDirName={localappdata}\Programs\GreeControl
DefaultGroupName=Gree Control
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
OutputDir=..\installer-output
OutputBaseFilename=GreeControl-Setup-x64
SetupIconFile=..\gree-control.ico
UninstallDisplayIcon={app}\gree-control.ico
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
VersionInfoCompany=Gree Control
VersionInfoProductName=Gree Control
VersionInfoProductVersion={#ProductVersion}
VersionInfoVersion={#ProductVersion}.0

[Tasks]
Name: "autostart"; Description: "Iniciar o Gree Control com o Windows (na bandeja)"; GroupDescription: "Preferências:"; Flags: checkedonce

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\installer-output\MicrosoftEdgeWebView2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Gree Control"; Filename: "{app}\{#ProductExe}"; WorkingDir: "{app}"; IconFilename: "{app}\gree-control.ico"
Name: "{group}\Desinstalar Gree Control"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GreeControl"; ValueData: """{app}\{#ProductExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{tmp}\MicrosoftEdgeWebView2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Instalando o componente WebView2..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#ProductExe}"; Description: "Abrir Gree Control"; Flags: nowait

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\GreeControl\Updates"
