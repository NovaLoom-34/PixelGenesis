# Replays a small, balanced human/elf starting cohort and checks survival plus births per species.
# Run during final Unity validation: Tools/founder_soak.ps1 -Seeds @(2024,7,99,11,23,42) -Years 100
param(
    [int[]]$Seeds = @(2024, 7, 99, 11, 23, 42),
    [ValidateRange(100,10000)][int]$Years = 100,
    [ValidateRange(60,7200)][int]$TimeoutSec = 1200
)
$ErrorActionPreference = 'Stop'
if ($Seeds.Count -lt 6) { throw 'The founder cohort acceptance requires at least six seeds.' }
if (($Seeds | Select-Object -Unique).Count -ne $Seeds.Count) { throw 'Seed values must be unique.' }
$root = Split-Path $PSScriptRoot -Parent
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($seed in $Seeds) {
    $tag = "civ-founders-$Years-$seed"
    $metricsPath = Join-Path $root "Logs\metrics-$tag.json"
    $logPath = Join-Path $root "Logs\$tag-CivSoak.log"
    $runError = $null
    try {
        & "$PSScriptRoot\civ_ab.ps1" -Scenario founders -Years $Years -Seeds @($seed) -TimeoutSec $TimeoutSec
    }
    catch { $runError = $_.Exception.Message }

    if (-not (Test-Path -LiteralPath $metricsPath)) {
        $failures.Add("seed ${seed}: missing founder metrics ($runError)")
        continue
    }
    $report = Get-Content -LiteralPath $metricsPath -Raw | ConvertFrom-Json
    if ($report.Samples.Count -lt $Years) { $failures.Add("seed ${seed}: only $($report.Samples.Count)/$Years annual samples") }
    foreach ($speciesId in @('sp.human', 'sp.elf')) {
        $initial = @($report.InitialPopulationBySpecies | Where-Object Species -eq $speciesId | Select-Object -First 1)
        if ($initial.Count -eq 0 -or [int]$initial[0].Population -ne 7 -or [int]$initial[0].Female -ne 4 -or
            [int]$initial[0].Male -ne 3 -or [int]$initial[0].Adults -ne 7 -or [int]$initial[0].Juveniles -ne 0) {
            $failures.Add("seed ${seed}: $speciesId did not start with the expected 7 adults (4 female, 3 male)")
        }
    }

    $humanBirths = 0
    $elfBirths = 0
    $humanDeathsByCause = @{}
    $elfDeathsByCause = @{}
    $humanMinPopulation = [int]::MaxValue
    $humanMinYear = 0
    $elfMinPopulation = [int]::MaxValue
    $elfMinYear = 0
    foreach ($sample in $report.Samples) {
        $human = @($sample.PopulationBySpecies | Where-Object Species -eq 'sp.human' | Select-Object -First 1)
        $elf = @($sample.PopulationBySpecies | Where-Object Species -eq 'sp.elf' | Select-Object -First 1)
        $humanPopulation = if ($human.Count -gt 0) { [int]$human[0].Population } else { 0 }
        $elfPopulation = if ($elf.Count -gt 0) { [int]$elf[0].Population } else { 0 }
        if ($humanPopulation -lt $humanMinPopulation) { $humanMinPopulation = $humanPopulation; $humanMinYear = [int]$sample.Year }
        if ($elfPopulation -lt $elfMinPopulation) { $elfMinPopulation = $elfPopulation; $elfMinYear = [int]$sample.Year }

        foreach ($species in $sample.PopulationBySpecies) {
            if ($species.Species -eq 'sp.human') {
                $humanBirths += [int]$species.Births
                foreach ($cause in @($species.DeathsByCause)) {
                    if (-not $cause -or [string]::IsNullOrWhiteSpace([string]$cause.Cause)) { continue }
                    $humanDeathsByCause[$cause.Cause] = [int]$humanDeathsByCause[$cause.Cause] + [int]$cause.Count
                }
            }
            elseif ($species.Species -eq 'sp.elf') {
                $elfBirths += [int]$species.Births
                foreach ($cause in @($species.DeathsByCause)) {
                    if (-not $cause -or [string]::IsNullOrWhiteSpace([string]$cause.Cause)) { continue }
                    $elfDeathsByCause[$cause.Cause] = [int]$elfDeathsByCause[$cause.Cause] + [int]$cause.Count
                }
            }
        }
    }
    $lastSample = $report.Samples | Sort-Object Year | Select-Object -Last 1
    $finalHumans = 0
    $finalElves = 0
    foreach ($species in $lastSample.PopulationBySpecies) {
        if ($species.Species -eq 'sp.human') { $finalHumans = [int]$species.Population }
        elseif ($species.Species -eq 'sp.elf') { $finalElves = [int]$species.Population }
    }
    if ($humanBirths -le 0) { $failures.Add("seed ${seed}: no human births in $Years years") }
    if ($elfBirths -le 0) { $failures.Add("seed ${seed}: no elf births in $Years years") }
    if ($finalHumans -lt 7) { $failures.Add("seed ${seed}: humans fell below the initial cohort of 7 (final $finalHumans)") }
    if ($finalElves -lt 7) { $failures.Add("seed ${seed}: elves fell below the initial cohort of 7 (final $finalElves)") }
    if ([int]$lastSample.Population -le 0) { $failures.Add("seed ${seed}: civilization population is zero in year $Years (wildlife must not mask civilization collapse)") }
    if ([int]$lastSample.Cities -lt 1) { $failures.Add("seed ${seed}: no living city remains in year $Years") }
    if ([int]$lastSample.LivingKingdoms -lt 1) { $failures.Add("seed ${seed}: no living kingdom remains in year $Years") }
    if (-not (Test-Path -LiteralPath $logPath) -or -not (Select-String -LiteralPath $logPath -Pattern '\[Civ\] ALL OK' -Quiet)) {
        $failures.Add("seed ${seed}: CivSoak reported an acceptance failure ($runError)")
    }
    "seed {0}: human births {1}, final {2} (min 7); elf births {3}, final {4} (min 7); civ {5}, cities {6}, kingdoms {7}; report {8}" -f `
        $seed, $humanBirths, $finalHumans, $elfBirths, $finalElves, $lastSample.Population, $lastSample.Cities, $lastSample.LivingKingdoms, $metricsPath
    $humanTopDeaths = @($humanDeathsByCause.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 3 | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
    $elfTopDeaths = @($elfDeathsByCause.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 3 | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
    "seed {0}: minimum annual humans {1} (year {2}); leading death causes: {3}" -f $seed, $humanMinPopulation, $humanMinYear, $(if ($humanTopDeaths) { $humanTopDeaths } else { 'none recorded' })
    "seed {0}: minimum annual elves {1} (year {2}); leading death causes: {3}" -f $seed, $elfMinPopulation, $elfMinYear, $(if ($elfTopDeaths) { $elfTopDeaths } else { 'none recorded' })
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Output "FAIL: $_" }
    throw "Founder cohort acceptance failed with $($failures.Count) finding(s). Review the per-seed metrics and logs."
}
"Founder cohort acceptance metrics passed for $($Seeds.Count) seeds × $Years years."
