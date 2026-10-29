# Writes the version into src\PeaceWpf\PeaceWpf.csproj.
# Kept in its own file so release.cmd never has to pass < > through cmd:
# cmd treats them as redirection even inside quotes.
#
# This file must stay UTF-8 with BOM: Windows PowerShell 5.1 reads a BOM-less
# file as ANSI, which corrupts the non-ASCII comments in the csproj on write-back.
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^[0-9]+(\.[0-9]+){1,3}$') {
    Write-Host "Invalid version: $Version"
    exit 3
}

$path = Join-Path $PSScriptRoot '..\src\PeaceWpf\PeaceWpf.csproj'
if (-not (Test-Path -LiteralPath $path)) {
    Write-Host "csproj not found: $path"
    exit 2
}

# Read and write as UTF-8 explicitly; the csproj contains Chinese comments.
$text = Get-Content -Raw -LiteralPath $path -Encoding UTF8
$pattern = '<Version>[^<]*</Version>'
if ($text -notmatch $pattern) {
    Write-Host 'No Version tag found in the csproj.'
    exit 4
}

$text = $text -replace $pattern, ('<Version>' + $Version + '</Version>')
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Version set to $Version"
exit 0
