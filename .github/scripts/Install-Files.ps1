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
    $sdkVersionPath = Join-Path $artifactRoot "windows-app-sdk-version.txt"

    if (-not (Test-Path $certificatePath)) {
        throw "FilesApp_SelfSigned.cer was not found beside this script."
    }
    if (-not (Test-Path $sdkVersionPath)) {
        throw "windows-app-sdk-version.txt was not found beside this script."
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

    $sdkVersion = (Get-Content $sdkVersionPath -Raw).Trim()
    if ($sdkVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
        throw "Invalid Windows App SDK version: $sdkVersion"
    }
    $normalizedSdkVersion = if ($sdkVersion.Split('.').Count -eq 3) {
        "$sdkVersion.0"
    }
    else {
        $sdkVersion
    }
    $minimumRuntimeVersion = [version]$normalizedSdkVersion
    $runtimePackage = Get-AppxPackage -AllUsers -Name "Microsoft.WindowsAppRuntime.2" `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Status -eq "Ok" -and
            $_.Architecture -eq "X64" -and
            [version]$_.Version -ge $minimumRuntimeVersion
        } |
        Sort-Object { [version]$_.Version } -Descending |
        Select-Object -First 1

    if (-not $runtimePackage) {
        $sdkMajorMinor = ($sdkVersion -split '\.')[0..1] -join '.'
        $runtimeUri = "https://aka.ms/windowsappsdk/$sdkMajorMinor/$sdkVersion/windowsappruntimeinstall-x64.exe"
        $runtimeInstaller = Join-Path $env:TEMP "WindowsAppRuntimeInstall-x64-$PID.exe"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $downloaded = $false

        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Invoke-WebRequest `
                    -Uri $runtimeUri `
                    -OutFile $runtimeInstaller `
                    -UseBasicParsing `
                    -TimeoutSec 600 `
                    -ErrorAction Stop

                if (-not (Test-Path -Path $runtimeInstaller -PathType Leaf) -or
                    (Get-Item $runtimeInstaller).Length -lt 1MB) {
                    throw "The downloaded Windows App SDK runtime installer is missing or incomplete."
                }

                $runtimeSignature = Get-AuthenticodeSignature -FilePath $runtimeInstaller
                if ($runtimeSignature.Status -ne "Valid" -or
                    $runtimeSignature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)O=Microsoft Corporation(,|$)') {
                    throw "The Windows App SDK runtime installer does not have a valid Microsoft signature."
                }

                $downloaded = $true
                break
            }
            catch {
                Remove-Item $runtimeInstaller -Force -ErrorAction SilentlyContinue
                if ($attempt -eq 3) {
                    throw "Could not download and verify the Windows App SDK $sdkVersion runtime after 3 attempts: $($_.Exception.Message)"
                }
                Start-Sleep -Seconds (5 * $attempt)
            }
        }

        if (-not $downloaded) {
            throw "The Windows App SDK runtime download did not complete."
        }

        try {
            $runtimeProcess = Start-Process `
                -FilePath $runtimeInstaller `
                -ArgumentList @("--quiet", "--force") `
                -WindowStyle Hidden `
                -Wait `
                -PassThru
            if ($runtimeProcess.ExitCode -notin @(0, 3010)) {
                throw "Windows App SDK runtime installation failed with exit code $($runtimeProcess.ExitCode)."
            }
        }
        finally {
            Remove-Item $runtimeInstaller -Force -ErrorAction SilentlyContinue
        }

        $runtimePackage = Get-AppxPackage -AllUsers -Name "Microsoft.WindowsAppRuntime.2" `
            -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Status -eq "Ok" -and
                $_.Architecture -eq "X64" -and
                [version]$_.Version -ge $minimumRuntimeVersion
            } |
            Sort-Object { [version]$_.Version } -Descending |
            Select-Object -First 1
        if (-not $runtimePackage) {
            throw "Windows App SDK runtime $sdkVersion or newer was not registered after installation."
        }
    }

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

    Add-AppxPackage @packageArguments

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
