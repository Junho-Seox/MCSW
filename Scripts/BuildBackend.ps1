# Build a Windows --onedir Python tracker from an already tested Python venv.
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$svc = Join-Path $repo "TrackingService"
$py = Join-Path $svc ".venv\Scripts\python.exe"
$models = Join-Path $svc "models"
foreach ($name in @("pose_landmarker_full.task", "hand_landmarker.task", "face_landmarker.task")) {
    if (-not (Test-Path -LiteralPath (Join-Path $models $name))) {
        throw "Missing model file: $name in $models"
    }
}
if (-not (Test-Path -LiteralPath $py)) {
    throw "Python venv missing. First create TrackingService/.venv and install requirements: $py"
}
Push-Location $svc
try {
    & $py -m PyInstaller --noconfirm --clean --onedir --console `
        --name VCS_Tracker --collect-all mediapipe `
        --add-data "$models;models" main.py
    if ($LASTEXITCODE -ne 0) { throw "PyInstaller failed with code $LASTEXITCODE" }
} finally { Pop-Location }
Write-Host "Backend ready: $svc\dist\VCS_Tracker\VCS_Tracker.exe"
