; Inno Setup script for Weight Verification & QR Label System.
; Requires Inno Setup 6 (https://jrsoftware.org/isinfo.php) on the build machine.
;
; Build steps:
;   1. Publish the app first (see installer\publish.bat), which produces a
;      self-contained folder at publish\WeightVerificationQR.
;   2. Open this file in Inno Setup Compiler (or run ISCC.exe WeightVerificationQR.iss)
;      to produce WeightVerificationQR_Setup.exe.

#define MyAppName "Weight Verification & QR Label System"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"
#define MyAppExeName "WeightVerificationQR.exe"
#define PublishDir "..\publish\WeightVerificationQR"

[Setup]
AppId={{8F1B7E2E-6B5B-4F1A-9C1D-WVQR00000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\WeightVerificationQR
DefaultGroupName=Weight Verification QR
DisableProgramGroupPage=yes
OutputBaseFilename=WeightVerificationQR_Setup
OutputDir=..\publish
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\WeightVerificationQR.App\Resources\app.ico
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StripInvalidChars(MyAppName)}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Keep the operator's database and backups on uninstall unless they explicitly delete them -
; production records should never disappear just because the app was reinstalled/upgraded.
Type: files; Name: "{app}\*.log"
