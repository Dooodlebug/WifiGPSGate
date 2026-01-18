; WifiGPSGate Inno Setup Script
; GNSS Data Bridge for Windows

#define MyAppName "WifiGPSGate"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WifiGPSGate"
#define MyAppURL "https://github.com/wifigpsgate"
#define MyAppExeName "WifiGPSGate.App.exe"

[Setup]
; Application identity
AppId={{8A3F9C2E-4B5D-6E7F-8A9B-0C1D2E3F4A5B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=..\installer\output
OutputBaseFilename=WifiGPSGate-{#MyAppVersion}-Setup
SetupIconFile=..\src\WifiGPSGate.App\icon.ico
Compression=lzma
SolidCompression=yes

; Windows version requirements
MinVersion=10.0.17763

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Appearance
WizardStyle=modern
WizardSizePercent=120

; License
LicenseFile=License.txt

; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Application files
Source: "..\src\WifiGPSGate.App\bin\Release\net9.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; License
Source: "License.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check for .NET 9 Runtime
function IsDotNet9Installed: Boolean;
var
  ResultCode: Integer;
begin
  // Try to run dotnet --list-runtimes and check for .NET 9
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  // Check if .NET 9 runtime is available
  if not IsDotNet9Installed then
  begin
    if MsgBox('WifiGPSGate requires .NET 9.0 Desktop Runtime.' + #13#10 + #13#10 +
              'Would you like to download it now?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/9.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask user if they want to remove settings
    SettingsDir := ExpandConstant('{localappdata}\WifiGPSGate');
    if DirExists(SettingsDir) then
    begin
      if MsgBox('Do you want to remove WifiGPSGate settings and logs?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(SettingsDir, True, True, True);
      end;
    end;
  end;
end;
