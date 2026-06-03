; ============================================================================
; DNS Manager Pro — Inno Setup Installer Script
; ============================================================================
; Requires: Inno Setup 6.x (https://jrsoftware.org/isdl.php)
; Usage:    Compile this file with Inno Setup Compiler (Compil32.exe)
; ============================================================================

#define AppName        "DNS Manager Pro"
#define AppVersion     "1.0.0"
#define AppPublisher   "RoOt㉿zErO"
#define AppURL         "https://mrrezanemati.ir"
#define AppExeName     "DNSManagerGUI.exe"
#define AppCopyright   "Copyright (c) 2025 RoOt㉿zErO"
#define AppDescription "Modern DNS management application for Windows"

[Setup]
; --- App Info ---
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppCopyright={#AppCopyright}

; --- Output ---
OutputBaseFilename=DNSManagerPro-Setup-{#AppVersion}
OutputDir=..\publish-output
Compression=lzma2/ultra64
SolidCompression=yes

; --- Architecture ---
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; --- Privileges ---
; DNS changes require admin — installer must run elevated
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; --- Directories ---
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

; --- License ---
LicenseFile=..\LICENSE

; --- Wizard ---
WizardStyle=modern
WizardSizePercent=100

; --- Windows ---
MinVersion=10.0.19041
CloseApplications=force

; --- Uninstall ---
UninstallDisplayName={#AppName}

; --- Misc ---
DisableProgramGroupPage=yes
DisableWelcomePage=no

; ============================================================================
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "Create Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

; ============================================================================
[Files]
; --- Main executable (single-file publish output) ---
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion sign
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

; ============================================================================
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startmenuicon
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

; ============================================================================
[Run]
; Launch app after installation
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; ============================================================================
[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; ============================================================================
[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  // Check if .NET 8 runtime is available (optional since we're self-contained)
  // Self-contained publish includes the runtime, so this is just informational
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Installation complete — nothing extra needed
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Optionally ask user if they want to delete profile data from Registry
    if MsgBox('Do you want to delete saved DNS profiles from the Registry?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\DnsManager');
    end;
  end;
end;
