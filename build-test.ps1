# Build + self test for the Esports Mixer (PeaceWpf).
# Everything printed by this script is ASCII English on purpose: cmd.exe
# codepages turn non-ASCII text into garbage. dotnet's own messages are forced
# to English too (DOTNET_CLI_UI_LANGUAGE).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File build-test.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File build-test.ps1 -Run
param(
    [switch]$Run,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest
)

$ErrorActionPreference = 'Continue'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:VSLANG = '1033'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Set-Location -LiteralPath $PSScriptRoot

$shouldRun = $Run -or ($null -ne $Rest -and $Rest.Count -gt 0)

# Log files may be UTF-8 or ANSI depending on how dotnet wrote them; decode
# defensively so nothing shows up as question marks.
function Show-Log([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $full = (Resolve-Path -LiteralPath $Path).Path
    $bytes = [System.IO.File]::ReadAllBytes($full)

    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.IndexOf([char]0xFFFD) -ge 0) {
        $text = [System.Text.Encoding]::Default.GetString($bytes)
    }

    foreach ($line in ($text -split "`r?`n")) {
        Write-Host $line
    }
}

# ---- locate dotnet ---------------------------------------------------------
$dotnet = 'dotnet'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $fallback = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $fallback) {
        $dotnet = $fallback
    }
    else {
        Write-Host 'dotnet not found. Install the .NET 10 SDK first.' -ForegroundColor Red
        exit 1
    }
}

Write-Host "using: $dotnet"
& $dotnet --version

# ---- 1/3 restore -----------------------------------------------------------
Write-Host ''
Write-Host '[1/3] restore'
& $dotnet restore PeaceWpf.slnx --verbosity minimal *> restore.log
if ($LASTEXITCODE -ne 0) {
    Write-Host 'restore FAILED' -ForegroundColor Red
    Show-Log 'restore.log'
    exit 1
}

# ---- 2/3 build -------------------------------------------------------------
Write-Host '[2/3] build'
& $dotnet build 'src\PeaceWpf\PeaceWpf.csproj' -c Debug --no-restore --nologo -v minimal *> build.log
$buildCode = $LASTEXITCODE

if ($buildCode -eq 0) {
    & $dotnet build 'tests\PeaceWpf.Core.SelfTest\PeaceWpf.Core.SelfTest.csproj' -c Debug --no-restore --nologo -v minimal *>> build.log
    $buildCode = $LASTEXITCODE
}

if ($buildCode -ne 0) {
    Write-Host 'build FAILED' -ForegroundColor Red
    Show-Log 'build.log'
    exit 1
}

# ---- 3/3 self test ---------------------------------------------------------
Write-Host '[3/3] self test'
& $dotnet run --project 'tests\PeaceWpf.Core.SelfTest\PeaceWpf.Core.SelfTest.csproj' -c Debug --no-build *> test.log
$testCode = $LASTEXITCODE

Write-Host ''
Write-Host '---- build log ----'
Show-Log 'build.log'
Write-Host ''
Write-Host '---- self test ----'
Show-Log 'test.log'

if ($testCode -ne 0) {
    Write-Host 'self test FAILED' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host 'OK. Everything built and the self test passed.'

if ($shouldRun) {
    Write-Host 'launching the app...'
    & (Join-Path $PSScriptRoot 'run.cmd')
}

exit 0
