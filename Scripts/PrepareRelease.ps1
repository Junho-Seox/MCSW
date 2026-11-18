# Usage: powershell -ExecutionPolicy Bypass -File .\Scripts\PrepareRelease.ps1 -UnityBuildDir '.\Builds\Windows'
param([Parameter(Mandatory=$true)][string]$UnityBuildDir)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$unity = (Resolve-Path $UnityBuildDir).Path
$tracker = Join-Path $repo "TrackingService\dist\VCS_Tracker"
if (-not (Test-Path (Join-Path $tracker "VCS_Tracker.exe"))) { throw "Build backend first via Scripts/BuildBackend.ps1" }
if (-not (Get-ChildItem -Path $unity -Filter "*.exe" -File)) { throw "No Unity .exe found in $unity" }
$releaseRoot = Join-Path $repo "Releases"
$release = Join-Path $releaseRoot "VCS-Windows"
if (Test-Path $release) { Remove-Item -Recurse -Force $release }
New-Item -ItemType Directory -Path $release -Force | Out-Null
Copy-Item -Path (Join-Path $unity '*') -Destination $release -Recurse -Force
$backend = Join-Path $release 'Backend'
New-Item -ItemType Directory -Path $backend -Force | Out-Null
Copy-Item -LiteralPath $tracker -Destination $backend -Recurse -Force
$zip = Join-Path $releaseRoot 'VCS-Windows.zip'
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $release '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Share this file in GitHub Releases: $zip"
