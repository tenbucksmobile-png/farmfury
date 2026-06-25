# FarmFury - Unity automation runner
# Usage:  .\Run-Unity.ps1 <command>
#
# Commands:
#   levels          Generate all LevelData ScriptableObjects
#   check           Compile check (errors exit with code 1)
#   build           Windows 64-bit build  -> unity/Builds/Windows/
#   build-webgl     WebGL build           -> unity/Builds/WebGL/
#   build-android   Android APK           -> unity/Builds/Android/

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("levels","setup","check","build","build-webgl","build-android")]
    [string]$Command
)

$UnityExe  = "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe"
$Project   = "$PSScriptRoot\unity"
$LogFile   = "$PSScriptRoot\unity\Logs\batch_$Command.log"

$MethodMap = @{
    "levels"        = "BuildScript.GenerateLevels"
    "setup"         = "BuildScript.WireScene"
    "check"         = "BuildScript.CompileCheck"
    "build"         = "BuildScript.BuildWindows"
    "build-webgl"   = "BuildScript.BuildWebGL"
    "build-android" = "BuildScript.BuildAndroid"
}

$Method = $MethodMap[$Command]

Write-Host ""
Write-Host "FarmFury Unity Automation" -ForegroundColor Cyan
Write-Host "  Command : $Command"
Write-Host "  Method  : $Method"
Write-Host "  Project : $Project"
Write-Host "  Log     : $LogFile"
Write-Host ""

New-Item -ItemType Directory -Path "$Project\Logs" -Force | Out-Null

$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $Project,
    "-executeMethod", $Method,
    "-logFile", $LogFile,
    "-quit"
)

Write-Host "Running Unity (this may take a minute on first run)..." -ForegroundColor Yellow

$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -Wait -PassThru
$exitCode = $proc.ExitCode

if (Test-Path $LogFile) {
    Write-Host ""
    Write-Host "--- Filtered Unity Log ---" -ForegroundColor DarkGray
    $lines = Get-Content $LogFile
    foreach ($line in $lines) {
        $lower = $line.ToLower()
        $relevant = $line -match "\[FarmFury" `
            -or $lower -match "error" `
            -or $lower -match "exception" `
            -or $lower -match "warning" `
            -or $line -match "Build succeeded" `
            -or $line -match "Build FAILED" `
            -or $line -match "Compile check"

        if ($relevant) {
            $color = "White"
            if ($lower -match "error" -or $lower -match "exception" -or $line -match "FAILED") {
                $color = "Red"
            } elseif ($lower -match "warning") {
                $color = "Yellow"
            } elseif ($line -match "succeeded" -or $line -match "\[FarmFury") {
                $color = "Green"
            }
            Write-Host $line -ForegroundColor $color
        }
    }
    Write-Host "--------------------------" -ForegroundColor DarkGray
}

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "DONE (exit 0)" -ForegroundColor Green
} else {
    Write-Host "FAILED (exit $exitCode)" -ForegroundColor Red
    Write-Host "Full log: $LogFile"
    exit $exitCode
}
