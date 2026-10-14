# Final multi-kingdom acceptance gate. This is intentionally not run during implementation;
# it launches Unity soaks and should be executed during the user's final gameplay validation.
param(
    [int[]]$Seeds = @(2024, 7, 99, 17, 41, 73),
    [ValidateRange(300,10000)][int]$Years = 300,
    [ValidateRange(60,7200)][int]$TimeoutSec = 3600
)
$ErrorActionPreference = 'Stop'
if ($Seeds.Count -lt 6) { throw 'The E acceptance gate requires at least six distinct seeds.' }
if (($Seeds | Select-Object -Unique).Count -ne $Seeds.Count) { throw 'Seed values must be unique.' }
$root = Split-Path $PSScriptRoot -Parent
$failures = [System.Collections.Generic.List[string]]::new()
$reports = [System.Collections.Generic.List[object]]::new()

& "$PSScriptRoot\civ_ab.ps1" -Scenario multi -Years $Years -Seeds $Seeds -TimeoutSec $TimeoutSec
foreach ($seed in $Seeds) {
    $tag = "civ-multi-$Years-$seed"
    $path = Join-Path $root "Logs\metrics-$tag.json"
    if (-not (Test-Path -LiteralPath $path)) { $failures.Add("seed ${seed}: missing metrics $path"); continue }
    $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $summary = $report.Summary
    $reports.Add($report)
    if ($report.Samples.Count -lt $Years) { $failures.Add("seed ${seed}: only $($report.Samples.Count)/$Years annual samples") }
    $orderedSamples = @($report.Samples | Sort-Object Year)
    if ($orderedSamples.Count -eq 0) { $failures.Add("seed ${seed}: report has no annual samples"); continue }
    $peakSample = $orderedSamples | Sort-Object @{Expression='Population';Descending=$true}, Year | Select-Object -First 1
    $civilizationPeak = [int]$peakSample.Population
    $collapseStreak = 0
    $longestCollapse = 0
    foreach ($sample in $orderedSamples) {
        if ([int]$sample.Year -gt [int]$peakSample.Year -and $civilizationPeak -ge 20 -and [int]$sample.Population -lt ($civilizationPeak * 0.3)) {
            $collapseStreak++
            $longestCollapse = [Math]::Max($longestCollapse, $collapseStreak)
        } else { $collapseStreak = 0 }
    }
    $lastSample = $orderedSamples | Select-Object -Last 1
    if ([int]$lastSample.Population -eq 0) { $failures.Add("seed ${seed}: no civilized units remain in year $($lastSample.Year)") }
    if ($longestCollapse -gt 20) { $failures.Add("seed ${seed}: civilization population remained below 30% of its overall peak for $longestCollapse consecutive years (peak $civilizationPeak)") }
    "seed {0}: civilization peak {1} (year {2}), final {3}, longest collapse {4} years" -f $seed, $civilizationPeak, $peakSample.Year, $lastSample.Population, $longestCollapse
    if ($summary.MaxLivingKingdoms -lt 4) { $failures.Add("seed ${seed}: fewer than four concurrent kingdoms (max $($summary.MaxLivingKingdoms))") }
    if ($summary.MaxContinuousWarPairYears -gt 50) { $failures.Add("seed ${seed}: an active war exceeded 50 years ($($summary.MaxContinuousWarPairYears))") }
    if ($summary.PeaceEvents -lt 1) { $failures.Add("seed ${seed}: no peace event") }
    if ($summary.AcceptedTreaties -lt 1) { $failures.Add("seed ${seed}: no accepted treaty") }
    if ($summary.AlliancesFormed -lt 1) { $failures.Add("seed ${seed}: no alliance") }
    if ($summary.TradeDeliveries -lt 1) { $failures.Add("seed ${seed}: no completed trade delivery") }
    if ($summary.MetaMillisecondsPerTick -gt 0.3) { $failures.Add("seed ${seed}: meta cost $([double]$summary.MetaMillisecondsPerTick) ms/tick exceeds 0.3") }
    foreach ($kingdom in $lastSample.KingdomPopulations) {
        if ($kingdom.WarsParticipated -lt 1) { $failures.Add("seed ${seed}: kingdom $($kingdom.KingdomId) has no recorded war participation") }
    }

    $kingdomIds = @($report.Samples | ForEach-Object { $_.KingdomPopulations } | ForEach-Object { $_.KingdomId } | Sort-Object -Unique)
    foreach ($kingdomId in $kingdomIds) {
        $streak = 0
        $maxStreak = 0
        $fell = $false
        foreach ($sample in ($report.Samples | Sort-Object Year)) {
            $kingdom = $sample.KingdomPopulations | Where-Object KingdomId -eq $kingdomId | Select-Object -First 1
            if ($kingdom -and $kingdom.Alive -and $kingdom.PopulationShare -gt 0.8 -and $kingdom.Population -gt 0) {
                $streak++
                $maxStreak = [Math]::Max($maxStreak, $streak)
            } else { $streak = 0 }
            if ($kingdom -and $kingdom.Fell) { $fell = $true }
        }
        if ($maxStreak -gt 50 -and -not $fell) { $failures.Add("seed ${seed}: kingdom $kingdomId held over 80% for $maxStreak years without a recorded kingdom fall") }
    }
}
if (-not ($reports | Where-Object { $_.Summary.VassalagesFormed -gt 0 -or $_.Summary.Rebellions -gt 0 })) {
    $failures.Add('Across six seeds, neither vassalage nor rebellion occurred.')
}
if ($failures.Count) {
    $failures | ForEach-Object { Write-Output "FAIL: $_" }
    throw "E acceptance failed with $($failures.Count) finding(s). Review per-seed JSON/logs before changing balance values."
}
"E acceptance metrics passed for $($reports.Count) seeds × $Years years. Review trade/famine comparisons and history-based collapse evidence manually before accepting Phase E."
