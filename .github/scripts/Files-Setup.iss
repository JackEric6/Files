#define AppVersion GetEnv("APP_VERSION")
#define ArtifactsDir GetEnv("ARTIFACTS_STAGING_DIR")

[Setup]
AppId={{B6EE8423-FDF3-4C84-BA15-2BB7F7A05042}
AppName=Files Community Unofficial Build
AppVersion={#AppVersion}
AppPublisher=Unofficial community fork
DefaultDirName={tmp}\FilesCommunitySetup
CreateAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
Uninstallable=no
CreateUninstallRegKey=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir="{#ArtifactsDir}"
OutputBaseFilename=Files-{#AppVersion}-Windows-x64-Setup
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
WizardStyle=modern

[Files]
Source: "{#ArtifactsDir}\AppxPackages\*"; DestDir: "{tmp}\FilesInstaller\AppxPackages"; Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall
Source: "{#ArtifactsDir}\Install-Files.ps1"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall
Source: "{#ArtifactsDir}\FilesApp_SelfSigned.cer"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall
Source: "{#ArtifactsDir}\windows-app-sdk-version.txt"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  PowerShellPath: String;
  ScriptPath: String;
  Parameters: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    ScriptPath := ExpandConstant('{tmp}\FilesInstaller\Install-Files.ps1');
    Parameters := '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + ScriptPath + '"';
    if not Exec(
      PowerShellPath,
      Parameters,
      ExpandConstant('{tmp}\FilesInstaller'),
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode) then
      RaiseException('Could not start the Files installation script.');
    if ResultCode <> 0 then
      RaiseException(Format('Files installation failed (PowerShell exit code %d).', [ResultCode]));
  end;
end;
