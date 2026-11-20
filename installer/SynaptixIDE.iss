; =====================================================================
; SynaptiX IDE - Official Inno Setup 6 Script
; Version: 2.15.4 (64-bit)
; Architecture: win-x64 Self-Contained
; Publisher: SynaptiX Corporation
; Standard: Microsoft Visual Studio Code & Git for Windows Reference
; =====================================================================

#define MyAppName "SynaptiX IDE"
#ifndef MyAppVersion
#define MyAppVersion "2.15.5"
#endif
#define MyAppPublisher "SynaptiX Corporation"
#define MyAppURL "https://SynaptiXCompany.github.io/SynaptiX-IDE-Releases/"
#define MyAppExeName "SynaptixIDE.exe"

[Setup]
AppId={{A38D4F32-8D19-4F4B-9E32-E84C1C45E368}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableWelcomePage=no
DisableProgramGroupPage=yes
LicenseFile=EULA.txt
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=dist
OutputBaseFilename=SynaptiX_IDE_Setup_v{#MyAppVersion}
SetupIconFile=..\SimplePLC.Studio\Assets\app.ico
UninstallDisplayIcon={app}\app.ico
UninstallDisplayName={#MyAppName}
Uninstallable=yes
CreateUninstallRegKey=yes
WizardStyle=modern
WizardImageFile=wizard_large.bmp
WizardSmallImageFile=wizard_small.bmp
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
SetupAppTitle=Setup - SynaptiX IDE
SetupWindowTitle=Setup - SynaptiX IDE
WelcomeLabel1=Welcome to the SynaptiX IDE Setup Wizard
WelcomeLabel2=This will install SynaptiX IDE version {#MyAppVersion} on your computer.%n%nSynaptiX IDE provides an integrated engineering platform for industrial automation logic, device telemetry, and edge connectivity across the SynaptiX ecosystem.%n%nIt is recommended that you close all other applications before continuing.
SelectDirDesc=Where should SynaptiX IDE be installed?
SelectDirLabel3=Setup will install SynaptiX IDE into the following folder.
SelectTasksDesc=Which additional shortcuts should be created?
FinishedHeadingLabel=Completing the SynaptiX IDE Setup Wizard
FinishedLabel=Setup has finished installing SynaptiX IDE on your computer.%n%nThe application may be launched by selecting the installed shortcut.
ButtonBack=Back
ButtonNext=Next
ButtonCancel=Cancel

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "dist\staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\SimplePLC.Studio\Assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\app.ico"
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Tools\SynaptiX MCU Emulator"; Filename: "{app}\Tools\SimplePLC.McuEmulator.exe"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
Root: HKA; Subkey: "Software\Classes\.synx"; ValueType: string; ValueName: ""; ValueData: "SynaptixProject"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\SynaptixProject"; ValueType: string; ValueName: ""; ValueData: "SynaptiX Project File"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SynaptixProject\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\app.ico"
Root: HKA; Subkey: "Software\Classes\SynaptixProject\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SynaptiX IDE"; Flags: postinstall nowait skipifsilent

[Code]
procedure AdjustButtons(IsFinished: Boolean);
var
  BtnWidth, BtnHeight, BtnGap, RightMargin: Integer;
begin
  BtnWidth := ScaleX(76);
  BtnHeight := ScaleY(24);
  BtnGap := ScaleX(8);
  RightMargin := ScaleX(12);

  WizardForm.CancelButton.Width := BtnWidth;
  WizardForm.CancelButton.Height := BtnHeight;
  WizardForm.NextButton.Width := BtnWidth;
  WizardForm.NextButton.Height := BtnHeight;
  WizardForm.BackButton.Width := BtnWidth;
  WizardForm.BackButton.Height := BtnHeight;

  WizardForm.CancelButton.Left := WizardForm.ClientWidth - RightMargin - BtnWidth;

  if IsFinished then
  begin
    WizardForm.NextButton.Left := WizardForm.CancelButton.Left;
  end
  else
  begin
    WizardForm.NextButton.Left := WizardForm.CancelButton.Left - BtnGap - BtnWidth;
    WizardForm.BackButton.Left := WizardForm.NextButton.Left - BtnGap - BtnWidth;
  end;
end;

procedure InitializeWizard();
begin
  // Set modern uniform typography across all controls
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.LicenseMemo.Font.Name := 'Segoe UI';
  WizardForm.LicenseAcceptedRadio.Font.Name := 'Segoe UI';
  WizardForm.LicenseNotAcceptedRadio.Font.Name := 'Segoe UI';

  // Uniform button spacing and layout
  AdjustButtons(False);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  // Ensure buttons stay uniformly spaced on every page transition
  AdjustButtons(CurPageID = wpFinished);

  if CurPageID = wpLicense then
  begin
    // Force LicenseMemo caret and scroll position to absolute top
    WizardForm.LicenseMemo.SelStart := 0;
    WizardForm.LicenseMemo.SelLength := 0;
  end;
end;
