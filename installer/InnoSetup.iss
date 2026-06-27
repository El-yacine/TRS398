; TRS-398 Pro Installer Script for Inno Setup
; This creates a professional single-file Windows installer (.exe)
; Download Inno Setup from: https://jrsoftware.org/isinfo.php

#define MyAppName "TRS-398 Pro"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Medical Physics"
#define MyAppURL "http://localhost:8000"
#define MyAppExeName "TRS398Pro.bat"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=..\dist\installer
OutputBaseFilename=TRS398Pro-Setup-{#MyAppVersion}
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Server files
Source: "..\server\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Detector library
Source: "..\detector_library.json"; DestDir: "{app}"; Flags: ignoreversion
; Launcher script
Source: "TRS398Pro.bat"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[Code]
var
  DotNetPage: TOutputMsgWizardPage;
  DotNetInstalled: Boolean;

function IsDotNetInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--version', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure InitializeWizard();
begin
  DotNetInstalled := IsDotNetInstalled();
  
  if not DotNetInstalled then
  begin
    DotNetPage := CreateOutputMsgPage(wpWelcome,
      '.NET Runtime Required',
      'TRS-398 Pro requires .NET 8.0 Runtime',
      'The .NET 8.0 Runtime is not installed on your system.' + #13#10 + #13#10 +
      'Please download and install it from:' + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10 + #13#10 +
      'After installing .NET, run this installer again.');
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  if (CurPageID = DotNetPage.ID) and not DotNetInstalled then
  begin
    // Open .NET download page
    ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

