param(
    [string]$Apk = 'Builds/Android/PixelGenesis.apk',
    [string]$Adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
    [string]$PackageId = $(if ($env:PG_ANDROID_APP_ID) { $env:PG_ANDROID_APP_ID } else { 'com.DefaultCompany.PixelGenesis' }),
    [switch]$NoLaunch,
    [int]$LogLines = 60
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$apkPath = Join-Path $projectRoot $Apk
if (-not (Test-Path $apkPath)) { throw "APK not found: $apkPath (run Tools/run_unity.ps1 -Mode AndroidBuild first)" }
if (-not (Test-Path $Adb)) { throw "adb not found: $Adb" }

& $Adb devices
& $Adb install -r -d $apkPath
if ($LASTEXITCODE -ne 0) { throw "adb install failed with exit code $LASTEXITCODE" }
Write-Output "Installed $apkPath"

if (-not $NoLaunch) {
    & $Adb logcat -c
    & $Adb shell monkey -p $PackageId -c android.intent.category.LAUNCHER 1 | Out-Null
    Start-Sleep -Seconds 6
    Write-Output "---- logcat (Unity tag, last $LogLines lines) ----"
    & $Adb logcat -d -t $LogLines -s Unity Unity-IL2CPP CRASH
}
