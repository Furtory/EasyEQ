$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$okFlag = Join-Path $root 'sdk-ok.txt'
$failFlag = Join-Path $root 'sdk-fail.txt'
Remove-Item $okFlag, $failFlag -ErrorAction SilentlyContinue

try {
    $installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    $installer = Join-Path $root 'dotnet-install.ps1'
    if (-not (Test-Path $installer)) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    }

    & $installer -Channel '10.0' -InstallDir $installDir -NoPath

    $dotnet = Join-Path $installDir 'dotnet.exe'
    if (-not (Test-Path $dotnet)) {
        throw 'dotnet.exe not found after install'
    }

    $sdks = & $dotnet --list-sdks
    if (-not $sdks) {
        throw 'installer finished but no SDK is listed'
    }

    Set-Content -Path $okFlag -Value 'ok' -Encoding ASCII
}
catch {
    $message = $_.Exception.Message
    if (-not $message) { $message = 'unknown error' }
    Set-Content -Path $failFlag -Value $message -Encoding ASCII
}
