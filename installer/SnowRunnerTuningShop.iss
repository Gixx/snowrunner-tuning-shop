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
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Code]
function UiCultureFromInnoLanguage(const InnoName: String): String;
begin
  if InnoName = 'german' then Result := 'de'
  else if InnoName = 'french' then Result := 'fr'
  else if InnoName = 'spanish' then Result := 'es'
  else if InnoName = 'portuguese' then Result := 'pt'
  else if InnoName = 'brazilianportuguese' then Result := 'pt-BR'
  else if InnoName = 'polish' then Result := 'pl'
  else if InnoName = 'russian' then Result := 'ru'
  else if InnoName = 'ukrainian' then Result := 'uk'
  else Result := 'en';
end;

procedure WriteInstallLanguageSeed;
var
  AppDataDir, Path, Json: String;
  UiCulture: String;
begin
  UiCulture := UiCultureFromInnoLanguage(ActiveLanguage);
  AppDataDir := ExpandConstant('{localappdata}\SnowRunnerTuningShop');
  Path := AppDataDir + '\install-language.json';
  if not DirExists(AppDataDir) then
    ForceDirectories(AppDataDir);
  Json := '{ "uiCulture": "' + UiCulture + '" }' + #13#10;
  SaveStringToFile(Path, Json, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    WriteInstallLanguageSeed;
end;

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
