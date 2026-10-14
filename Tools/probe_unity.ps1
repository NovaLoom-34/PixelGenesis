# Runs a run_unity.ps1 mode and stops Unity as soon as a marker line shows up in its log (batch methods that
# print their result but keep the editor open would otherwise hang). Usage:
#   Tools/probe_unity.ps1 -Mode CrowdProbe -Tag crowd -Marker '\[Crowd\] DONE' -Crowd battle
param([string]$Mode, [string]$Tag, [string]$Marker, [string]$Crowd = '', [int]$TimeoutSec = 420)
$root = Split-Path $PSScriptRoot -Parent
$env:PG_CROWD = $Crowd
$log = Join-Path $root "Logs\$Tag-$Mode.log"
if (Test-Path $log) { Remove-Item $log -Force -ErrorAction SilentlyContinue }
$p = Start-Process powershell -ArgumentList '-NoProfile', '-File', "$PSScriptRoot\run_unity.ps1", '-Mode', $Mode, '-Tag', $Tag -WindowStyle Hidden -PassThru
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
    Start-Sleep -Seconds 3
    if ($p.HasExited) { break }
    if ($Marker -and (Test-Path $log) -and (Select-String -Path $log -Pattern ($Marker + "|error CS") -Quiet -ErrorAction SilentlyContinue)) { Start-Sleep -Seconds 3; break }
}
Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match [regex]::Escape("$Tag-$Mode.log") } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
"finished in $([int]$sw.Elapsed.TotalSeconds)s"
