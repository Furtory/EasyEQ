# Checks that the published exe really reports the requested version.
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,
    [Parameter(Mandatory = $true, Position = 1)]
    [string]$ExePath
)

if (-not (Test-Path -LiteralPath $ExePath)) {
    Write-Host "exe not found: $ExePath"
    exit 6
}

$actual = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath).FileVersion
if (-not $actual.StartsWith($Version)) {
    Write-Host "actual file version: $actual"
    exit 5
}

Write-Host "file version: $actual"
exit 0
