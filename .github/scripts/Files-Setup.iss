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
english.InvalidPackageLocation=Choose the registered package volume C:\Program Files\WindowsApps or a drive-root volume such as D:\WindowsApps. A new volume cannot be nested under another folder.
english.InstallationFailed=Files could not be installed. Details were saved to:
english.InstallingFiles=Installing Files. The first install may take a few minutes...
english.ScriptStartFailed=The Files installation could not be started.
english.LaunchFiles=Launch Files
chinesesimp.AdditionalOptions=其他选项:
chinesesimp.CreateDesktopIcon=创建桌面快捷方式
chinesesimp.InvalidPackageLocation=请选择已注册的应用包卷（例如 C:\Program Files\WindowsApps、D:\WindowsApps），或磁盘根目录下的新卷（例如 E:\WindowsApps）。新卷不能放在其他文件夹内。
chinesesimp.InstallationFailed=Files 安装失败。详细信息已保存到：
chinesesimp.InstallingFiles=正在安装 Files。首次安装可能需要几分钟...
chinesesimp.ScriptStartFailed=无法启动 Files 安装过程。
chinesesimp.LaunchFiles=启动 Files

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: checkedonce

[Files]
Source: "{#ArtifactsDir}\AppxPackages\*"; DestDir: "{tmp}\FilesInstaller\AppxPackages"; Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall
Source: "{#ArtifactsDir}\Install-Files.ps1"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall
Source: "{#ArtifactsDir}\FilesApp_SelfSigned.cer"; DestDir: "{tmp}\FilesInstaller"; Flags: ignoreversion deleteafterinstall; AfterInstall: InstallFilesPackage

[Icons]
Name: "{autodesktop}\Files"; Filename: "{win}\explorer.exe"; Parameters: "shell:AppsFolder\FilesDev_ykqwq8d6ps0ag!App"; AppUserModelID: "FilesDev_ykqwq8d6ps0ag!App"; Tasks: desktopicon

[Run]
Filename: "{win}\explorer.exe"; Parameters: "shell:AppsFolder\FilesDev_ykqwq8d6ps0ag!App"; Description: "{cm:LaunchFiles}"; Flags: postinstall nowait skipifsilent runasoriginaluser

[Code]
function NextButtonClick(CurPageID: Integer): Boolean;
var
  SelectedPath: String;
  IsSupportedPackageVolume: Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    SelectedPath := WizardDirValue;
    while (Length(SelectedPath) > 3) and
      (SelectedPath[Length(SelectedPath)] = '\') do
      Delete(SelectedPath, Length(SelectedPath), 1);

    IsSupportedPackageVolume :=
      CompareText(
        SelectedPath,
        ExpandConstant('{autopf}\WindowsApps')) = 0;
    if Length(SelectedPath) = 14 then
      IsSupportedPackageVolume :=
        IsSupportedPackageVolume or
        ((SelectedPath[2] = ':') and
          (SelectedPath[3] = '\') and
          (CompareText(Copy(SelectedPath, 4, Length(SelectedPath)), 'WindowsApps') = 0));

    if not IsSupportedPackageVolume then
    begin
      MsgBox(
        ExpandConstant('{cm:InvalidPackageLocation}'),
        mbError,
        MB_OK);
      Result := False;
    end;
  end;
end;

procedure OnInstallOutput(const S: String; const Error, FirstLine: Boolean);
var
  StatusText: String;
begin
  Log(S);
  StatusText := Trim(S);
  if StatusText <> '' then
  begin
    if Length(StatusText) > 110 then
      StatusText := Copy(StatusText, 1, 110);
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.Update;
  end;
end;

procedure InstallFilesPackage();
var
  PowerShellPath: String;
  ScriptPath: String;
  PackageVolumePath: String;
  LogPath: String;
  Parameters: String;
  ResultCode: Integer;
begin
  WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingFiles}');
  WizardForm.ProgressGauge.Style := npbstMarquee;
  WizardForm.Update;
  try
    PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    ScriptPath := ExpandConstant('{tmp}\FilesInstaller\Install-Files.ps1');
    PackageVolumePath := WizardDirValue;
    LogPath := ExpandConstant('{%TEMP}\FilesCommunityInstall-{#AppVersion}.log');
    Parameters :=
      '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass ' +
      '-File "' + ScriptPath + '" ' +
      '-PackageVolumePath "' + PackageVolumePath + '" ' +
      '-LogPath "' + LogPath + '"';
    if not ExecAndLogOutput(
      PowerShellPath,
      Parameters,
      ExpandConstant('{tmp}\FilesInstaller'),
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode,
      @OnInstallOutput) then
      RaiseException(ExpandConstant('{cm:ScriptStartFailed}'));
    if ResultCode <> 0 then
      RaiseException(
        ExpandConstant('{cm:InstallationFailed}') + #13#10 + LogPath);
  finally
    WizardForm.ProgressGauge.Style := npbstNormal;
    WizardForm.Update;
  end;
end;
