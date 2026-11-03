#define AppVersion GetEnv("APP_VERSION")
#define ArtifactsDir GetEnv("ARTIFACTS_STAGING_DIR")

[Setup]
AppId={{B6EE8423-FDF3-4C84-BA15-2BB7F7A05042}
AppName=Files Community Build
AppVersion={#AppVersion}
AppPublisher=Unofficial community fork
DefaultDirName={autopf}\WindowsApps
CreateAppDir=yes
DisableDirPage=no
DirExistsWarning=no
UsePreviousAppDir=no
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Languages\Unofficial\ChineseSimplified.isl"

[CustomMessages]
english.AdditionalOptions=Additional options:
english.CreateDesktopIcon=Create a desktop shortcut
english.InvalidPackageLocation=Choose a Windows app package location ending in "WindowsApps", for example C:\Program Files\WindowsApps or D:\WindowsApps.
english.InstallationFailed=Files could not be installed. Details were saved to:
english.InstallingFiles=Installing Files...
english.ScriptStartFailed=The Files installation could not be started.
chinesesimp.AdditionalOptions=其他选项:
chinesesimp.CreateDesktopIcon=创建桌面快捷方式
chinesesimp.InvalidPackageLocation=安装位置必须以 WindowsApps 结尾，例如 C:\Program Files\WindowsApps 或 D:\WindowsApps。
chinesesimp.InstallationFailed=Files 安装失败。详细信息已保存到：
chinesesimp.InstallingFiles=正在安装 Files...
chinesesimp.ScriptStartFailed=无法启动 Files 安装过程。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: checkedonce

[Files]
Source: "{#ArtifactsDir}\AppxPackages\*"; DestDir: "{tmp}\FilesInstaller\AppxPackages"; Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall
Source: "{#ArtifactsDir}\Install-Files.ps1"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall
Source: "{#ArtifactsDir}\FilesApp_SelfSigned.cer"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall
Source: "{#ArtifactsDir}\WindowsAppRuntimeInstall-x64.exe"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall

[Icons]
Name: "{autodesktop}\Files"; Filename: "{sys}\explorer.exe"; Parameters: "shell:AppsFolder\FilesDev_ykqwq8d6ps0ag!App"; AppUserModelID: "FilesDev_ykqwq8d6ps0ag!App"; Tasks: desktopicon

[Code]
function NextButtonClick(CurPageID: Integer): Boolean;
var
  SelectedPath: String;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    SelectedPath := WizardDirValue;
    while (Length(SelectedPath) > 3) and
      (SelectedPath[Length(SelectedPath)] = '\') do
      Delete(SelectedPath, Length(SelectedPath), 1);

    if (Length(SelectedPath) < 14) or
      (SelectedPath[2] <> ':') or
      (SelectedPath[3] <> '\') or
      (CompareText(ExtractFileName(SelectedPath), 'WindowsApps') <> 0) then
    begin
      MsgBox(
        ExpandConstant('{cm:InvalidPackageLocation}'),
        mbError,
        MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  PowerShellPath: String;
  ScriptPath: String;
  PackageVolumePath: String;
  LogPath: String;
  Parameters: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingFiles}');
    WizardForm.Update;
    PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    ScriptPath := ExpandConstant('{tmp}\FilesInstaller\Install-Files.ps1');
    PackageVolumePath := WizardDirValue;
    LogPath := ExpandConstant('{%TEMP}\FilesCommunityInstall-{#AppVersion}.log');
    Parameters :=
      '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass ' +
      '-File "' + ScriptPath + '" ' +
      '-PackageVolumePath "' + PackageVolumePath + '" ' +
      '-LogPath "' + LogPath + '"';
    if not Exec(
      PowerShellPath,
      Parameters,
      ExpandConstant('{tmp}\FilesInstaller'),
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode) then
      RaiseException(ExpandConstant('{cm:ScriptStartFailed}'));
    if ResultCode <> 0 then
      RaiseException(
        ExpandConstant('{cm:InstallationFailed}') + #13#10 + LogPath);
  end;
end;
