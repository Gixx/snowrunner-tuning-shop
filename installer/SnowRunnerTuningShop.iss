; Inno Setup 6 script — compiled by the Release GitHub Actions workflow.
; Pass /DMyAppVersion=1.2.3 (and optionally MyAppTag / MyAppSourceDir / MyAppOutputDir).

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyAppTag
  #define MyAppTag "v" + MyAppVersion
#endif
#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\publish\SnowRunnerTuningShop-win-x64"
#endif
#ifndef MyAppOutputDir
  #define MyAppOutputDir "..\publish"
#endif

#define MyAppName "SnowRunner Tuning Shop"
#define MyAppPublisher "Gixx"
#define MyAppURL "https://gixx.github.io/snowrunner-tuning-shop/"
#define MyAppExeName "SnowRunnerTuningShop.exe"

[Setup]
AppId={{A7C3E8F1-4B2D-4E9A-9C1F-6D8B5A0E2F47}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL=https://github.com/Gixx/snowrunner-tuning-shop/issues
AppUpdatesURL=https://github.com/Gixx/snowrunner-tuning-shop/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir={#MyAppOutputDir}
OutputBaseFilename=SnowRunnerTuningShop-{#MyAppTag}-win-x64-Setup
SetupIconFile=..\src\SnowRunnerTuningShop\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
