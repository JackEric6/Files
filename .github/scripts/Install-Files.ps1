$ErrorActionPreference = "Stop"

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
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

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$trustedCertificate = Get-ChildItem Cert:\LocalMachine\TrustedPeople |
    Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } |
    Select-Object -First 1
if (-not $trustedCertificate) {
    Import-Certificate `
        -FilePath $certificatePath `
        -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}
$certificate.Dispose()

$sdkVersion = (Get-Content $sdkVersionPath -Raw).Trim()
if ($sdkVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Invalid Windows App SDK version: $sdkVersion"
}
$sdkMajorMinor = ($sdkVersion -split '\.')[0..1] -join '.'
$runtimeInstaller = Join-Path $env:TEMP "windowsappruntimeinstall-x64.exe"
$runtimeUri = "https://aka.ms/windowsappsdk/$sdkMajorMinor/$sdkVersion/windowsappruntimeinstall-x64.exe"
Invoke-WebRequest -Uri $runtimeUri -OutFile $runtimeInstaller
$runtimeProcess = Start-Process `
    -FilePath $runtimeInstaller `
    -ArgumentList "--quiet", "--force" `
    -Wait `
    -PassThru
if ($runtimeProcess.ExitCode -notin @(0, 3010)) {
    throw "Windows App SDK runtime installation failed with exit code $($runtimeProcess.ExitCode)."
}

$dependencies = @(
    Get-ChildItem -Path $packagesRoot -Recurse -File -Include *.appx, *.msix |
        Where-Object { $_.FullName -match '[\\/]Dependencies[\\/]x64[\\/]' } |
        ForEach-Object { $_.FullName }
)
if ($dependencies.Count -gt 0) {
    Add-AppxPackage -Path $bundle.FullName -DependencyPath $dependencies
} else {
    Add-AppxPackage -Path $bundle.FullName
}

Write-Host "Files was installed successfully."
