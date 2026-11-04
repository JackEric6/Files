param(
    [Parameter(Mandatory = $true)]
    [string]$PackageVolumePath,

    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = "Stop"
$transcriptStarted = $false

try {
    Start-Transcript -Path $LogPath -Force | Out-Null
    $transcriptStarted = $true

    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this installer as an administrator."
    }

    $artifactRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packagesRoot = Join-Path $artifactRoot "AppxPackages"
    $certificatePath = Join-Path $artifactRoot "FilesApp_SelfSigned.cer"

    if (-not (Test-Path $certificatePath)) {
        throw "FilesApp_SelfSigned.cer was not found beside this script."
    }

    $bundle = Get-ChildItem -Path $packagesRoot -Recurse -File -Filter "*.msixbundle" |
        Select-Object -First 1
    if (-not $bundle) {
        throw "No .msixbundle was found under AppxPackages."
    }

    $PackageVolumePath = [System.IO.Path]::GetFullPath($PackageVolumePath).TrimEnd("\")
    if ((Split-Path -Leaf $PackageVolumePath) -ne "WindowsApps") {
        throw "The package location must end with the WindowsApps directory."
    }

    $volume = Get-AppxVolume |
        Where-Object {
            [System.IO.Path]::GetFullPath($_.PackageStorePath).TrimEnd("\") -ieq $PackageVolumePath
        } |
        Select-Object -First 1
    if (-not $volume) {
        if ($PackageVolumePath -notmatch '^[A-Za-z]:\\WindowsApps$') {
            throw "A new package volume must use a drive root such as D:\WindowsApps."
        }
        $volume = Add-AppxVolume -Path $PackageVolumePath
    }

    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
    $trustedPeopleStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        "TrustedPeople",
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
    )
    try {
        $trustedPeopleStore.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite
        )
        $certificateIsTrusted = $false
        foreach ($trustedCertificate in $trustedPeopleStore.Certificates) {
            if ($trustedCertificate.Thumbprint -eq $certificate.Thumbprint) {
                $certificateIsTrusted = $true
                break
            }
        }
        if (-not $certificateIsTrusted) {
            $trustedPeopleStore.Add($certificate)
        }
    }
    finally {
        $trustedPeopleStore.Close()
        $trustedPeopleStore.Dispose()
        $certificate.Dispose()
    }

    Write-Output "Registering Files with Windows."
    $dependencies = @(
        Get-ChildItem -Path $packagesRoot -Recurse -File -Include *.appx, *.msix |
            Where-Object { $_.FullName -match '[\\/]Dependencies[\\/]x64[\\/]' } |
            ForEach-Object { $_.FullName }
    )
    $packageArguments = @{
        Path                     = $bundle.FullName
        Volume                   = $volume
        ForceUpdateFromAnyVersion = $true
        ErrorAction              = "Stop"
    }
    if ($dependencies.Count -gt 0) {
        $packageArguments.DependencyPath = $dependencies
    }

    $existingPackage = Get-AppxPackage -Name "FilesDev" |
        Where-Object { $_.PackageFamilyName -eq "FilesDev_ykqwq8d6ps0ag" } |
        Select-Object -First 1
    if ($existingPackage) {
        $installerSessionId = (Get-Process -Id $PID).SessionId
        $installerUserName = $identity.Name
        Write-Output "Requesting running Files instances in the current session to close before updating."
        $runningFilesProcesses = @(
            Get-Process -Name "Files" -IncludeUserName -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.SessionId -eq $installerSessionId -and
                    $_.UserName -eq $installerUserName
                }
        )
        foreach ($process in $runningFilesProcesses) {
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                [void]$process.CloseMainWindow()
            }
        }

        $closeDeadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            $runningFilesProcesses = @(
                Get-Process -Name "Files" -IncludeUserName -ErrorAction SilentlyContinue |
                    Where-Object {
                        $_.SessionId -eq $installerSessionId -and
                        $_.UserName -eq $installerUserName
                    }
            )
            if ($runningFilesProcesses.Count -eq 0) {
                break
            }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $closeDeadline)
        if ($runningFilesProcesses.Count -gt 0) {
            throw "Close Files and retry the installation. The running app did not exit within 30 seconds."
        }
        Write-Output "No Files instances are running in the current session."

        $existingLocation = [System.IO.Path]::GetFullPath($existingPackage.InstallLocation)
        if (-not $existingLocation.StartsWith(
            "$PackageVolumePath\",
            [System.StringComparison]::OrdinalIgnoreCase
        )) {
            Move-AppxPackage `
                -Package $existingPackage.PackageFullName `
                -Volume $volume
        }
    }

    Write-Output "Deploying the Files package."
    Add-AppxPackage @packageArguments

    Write-Output "Verifying the Files package registration."
    $installedPackage = Get-AppxPackage -Name "FilesDev" |
        Where-Object { $_.PackageFamilyName -eq "FilesDev_ykqwq8d6ps0ag" } |
        Select-Object -First 1
    if (-not $installedPackage -or $installedPackage.Status -ne "Ok") {
        throw "Windows did not register the Files package successfully."
    }
    $installedLocation = [System.IO.Path]::GetFullPath($installedPackage.InstallLocation)
    if (-not $installedLocation.StartsWith(
        "$PackageVolumePath\",
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Files was not installed to the selected package location."
    }

    Write-Output "Files was installed successfully."
    Stop-Transcript | Out-Null
    $transcriptStarted = $false
    exit 0
}
catch {
    Write-Output ($_ | Out-String)
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
        $transcriptStarted = $false
    }
    exit 1
}
