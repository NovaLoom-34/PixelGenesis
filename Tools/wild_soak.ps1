# Runs the full naturally spawned wildlife community for several deterministic seeds.
# The separate Unity menu probe remains available for a focused sheep/wolf diagnostic.
# Example: Tools/wild_soak.ps1 -Years 200 -Seeds @(2024,7,99,11,23,42)
param(
    [int[]]$Seeds = @(2024, 7, 99, 11, 23, 42),
    [ValidateRange(1,10000)][int]$Years = 200,
    [ValidateRange(60,7200)][int]$TimeoutSec = 3600
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$acceptanceFailures = [System.Collections.Generic.List[string]]::new()
function Get-NormalizedRange($samples, [string]$property) {
    $values = @($samples | ForEach-Object { [double]$_.$property })
    if ($values.Count -eq 0) { return 0.0 }
    $mean = ($values | Measure-Object -Average).Average
    $maximum = ($values | Measure-Object -Maximum).Maximum
    $minimum = ($values | Measure-Object -Minimum).Minimum
    return ($maximum - $minimum) / [Math]::Max(1.0, $mean)
}
function Get-CollapseReboundSummary($samples, [double]$initialPopulation) {
    # Diagnostic thresholds: collapse at <=25% of the preceding 20-year peak;
    # rebound at >=75% of that peak within 20 years. A 50-year low streak fails the soak.
    $recentPopulations = [System.Collections.Generic.List[double]]::new()
    $lowStreak = 0
    $longestLowStreak = 0
    $collapseEpisodes = 0
    $reboundedEpisodes = 0
    $unrecoveredEpisodes = 0
    $pendingPeak = 0.0
    $pendingYears = 0
    foreach ($sample in $samples) {
        $population = [double]$sample.WildPopulation
        $referencePeak = if ($recentPopulations.Count -eq 0) { $initialPopulation } else { 0.0 }
        foreach ($previousPopulation in $recentPopulations) {
            if ($previousPopulation -gt $referencePeak) { $referencePeak = $previousPopulation }
        }
        $collapsed = $referencePeak -gt 0 -and $population -le (0.25 * $referencePeak)
        if ($collapsed) {
            $lowStreak++
            if ($lowStreak -gt $longestLowStreak) { $longestLowStreak = $lowStreak }
        }
        else { $lowStreak = 0 }

        if ($pendingYears -gt 0) {
            $pendingYears++
            if ($population -ge (0.75 * $pendingPeak)) {
                $reboundedEpisodes++
                $pendingYears = 0
                $pendingPeak = 0.0
            }
            elseif ($pendingYears -ge 20) {
                $unrecoveredEpisodes++
                $pendingYears = 0
                $pendingPeak = 0.0
            }
        }
        elseif ($collapsed) {
            $collapseEpisodes++
            $pendingPeak = $referencePeak
            $pendingYears = 1
        }

        $recentPopulations.Add($population)
        if ($recentPopulations.Count -gt 20) { $recentPopulations.RemoveAt(0) }
    }
    if ($pendingYears -gt 0) { $unrecoveredEpisodes++ }
    return [pscustomobject]@{
        LongestLowPopulationStreak = $longestLowStreak
        CollapseEpisodes = $collapseEpisodes
        ReboundsWithin20Years = $reboundedEpisodes
        UnrecoveredCollapseEpisodes = $unrecoveredEpisodes
    }
}
try {
    foreach ($seed in $Seeds) {
        $tag = "wild-community-$Years-$seed"
        $env:PG_WILD_SEED = "$seed"
        $env:PG_WILD_YEARS = "$Years"
        $metrics = Join-Path $root "Logs\metrics-wild-community-$seed-${Years}y.json"
        $startedUtc = [DateTime]::UtcNow
        & "$PSScriptRoot\probe_unity.ps1" -Mode WildEcosystemSoak -Tag $tag -Marker '\[WildSoak\] COMPLETE' -TimeoutSec $TimeoutSec

        $log = Join-Path $root "Logs\$tag-WildEcosystemSoak.log"
        if (-not (Test-Path -LiteralPath $log) -or -not (Select-String -LiteralPath $log -Pattern '\[WildSoak\] COMPLETE' -Quiet)) {
            throw "WildEcosystemSoak did not report completion for seed $seed; inspect $log"
        }
        if (-not (Test-Path -LiteralPath $metrics)) { throw "WildEcosystemSoak did not write metrics for seed $seed ($Years years): $metrics" }
        if ((Get-Item -LiteralPath $metrics).LastWriteTimeUtc -lt $startedUtc) { throw "Metrics were not refreshed by this run: $metrics" }

        $report = Get-Content -LiteralPath $metrics -Raw | ConvertFrom-Json
        if ([string]$report.Seed -ne [string]$seed -or $report.RequestedYears -ne $Years -or $report.Samples.Count -ne $Years) {
            throw "Metrics metadata does not match seed $seed / $Years years: $metrics"
        }
        if (-not $report.InitialPopulationBySpecies -or $report.InitialPopulationBySpecies.Count -eq 0) {
            throw "Initial species census is missing from $metrics"
        }
        $last = $report.Samples[-1]
        $livingSpecies = @($last.PopulationBySpecies | Where-Object { -not $_.IsCiv -and $_.Population -gt 0 }).Count
        $initialSpecies = @($report.InitialPopulationBySpecies | Where-Object { -not $_.IsCiv -and $_.Population -gt 0 })
        $survivingInitialSpecies = @($initialSpecies | Where-Object {
            $speciesId = $_.Species
            @($last.PopulationBySpecies | Where-Object { -not $_.IsCiv -and $_.Species -eq $speciesId -and $_.Population -gt 0 }).Count -gt 0
        }).Count
        $survivalPercent = if ($initialSpecies.Count -gt 0) { 100.0 * $survivingInitialSpecies / $initialSpecies.Count } else { 0.0 }
        if ($survivalPercent -lt 85.0) { $acceptanceFailures.Add("seed ${seed}: species survival $($survivalPercent.ToString('N1'))% is below 85%") }

        $initialCoverage = [double]$report.InitialVegetationCoveragePercent
        $finalCoverage = [double]$last.VegetationCoveragePercent
        $vegetationDelta = if ($initialCoverage -gt 0) { 100.0 * [Math]::Abs($finalCoverage - $initialCoverage) / $initialCoverage } else { [double]::PositiveInfinity }
        if ($vegetationDelta -gt 25.0) { $acceptanceFailures.Add("seed ${seed}: vegetation coverage changed $($vegetationDelta.ToString('N1'))% (limit 25%)") }

        $initialWildTotal = [double](($initialSpecies | Measure-Object -Property Population -Sum).Sum)
        if ($initialWildTotal -le 0) { throw "Initial wild population is empty in $metrics" }
        $shareFailure = $false
        foreach ($species in $initialSpecies) {
            $initialShare = [double]$species.Population / $initialWildTotal
            $consecutiveAbove = 0
            foreach ($sample in $report.Samples) {
                $current = @($sample.PopulationBySpecies | Where-Object { -not $_.IsCiv -and $_.Species -eq $species.Species } | Select-Object -First 1)
                $currentPopulation = if ($current.Count -gt 0) { [double]$current[0].Population } else { 0.0 }
                $total = [double](($sample.PopulationBySpecies | Where-Object { -not $_.IsCiv -and $_.Population -gt 0 } | Measure-Object -Property Population -Sum).Sum)
                $share = if ($total -gt 0) { $currentPopulation / $total } else { 0.0 }
                if ($share -gt (3.0 * $initialShare)) { $consecutiveAbove++ } else { $consecutiveAbove = 0 }
                if ($consecutiveAbove -ge 50) { $shareFailure = $true; break }
            }
            if ($shareFailure) { break }
        }
        if ($shareFailure) { $acceptanceFailures.Add("seed ${seed}: a species held more than 3x its initial population share for 50 consecutive years") }

        $collapse = Get-CollapseReboundSummary $report.Samples $initialWildTotal
        if ($collapse.LongestLowPopulationStreak -ge 50) {
            $acceptanceFailures.Add("seed ${seed}: wild population remained at or below 25% of its trailing peak for 50 consecutive years")
        }
        "  collapse diagnostic: longest low-population streak {0}y; collapse episodes {1}; recovered within 20y {2}; unrecovered {3}" -f `
            $collapse.LongestLowPopulationStreak, $collapse.CollapseEpisodes, $collapse.ReboundsWithin20Years, $collapse.UnrecoveredCollapseEpisodes

        $firstBand = @($report.Samples | Select-Object -First 50)
        $lastBand = @($report.Samples | Select-Object -Last 50)
        $predatorStartRange = Get-NormalizedRange $firstBand 'PredatorPopulation'
        $predatorEndRange = Get-NormalizedRange $lastBand 'PredatorPopulation'
        $preyStartRange = Get-NormalizedRange $firstBand 'PreyPopulation'
        $preyEndRange = Get-NormalizedRange $lastBand 'PreyPopulation'
        $dampingProxy = ($predatorEndRange -lt $predatorStartRange) -and ($preyEndRange -lt $preyStartRange)

        $top = @($last.PopulationBySpecies | Where-Object { -not $_.IsCiv } | Sort-Object Population -Descending | Select-Object -First 5 | ForEach-Object { "$($_.Species)=$($_.Population)" }) -join ', '
        "seed {0}: year {1}, wild {2}, predators/prey {3}/{4}, 50y normalized range predator {5:N2}->{6:N2}, prey {7:N2}->{8:N2} (damping proxy {9}), living species {10}, initial species surviving {11}/{12} ({13:N1}%), vegetation {14:N2}% -> {15:N2}% (delta {16:N1}%); last-year births/deaths {17}/{18}; top: {19}; metrics {20}" -f `
            $seed, $last.Year, $last.WildPopulation, $last.PredatorPopulation, $last.PreyPopulation, $predatorStartRange, $predatorEndRange, $preyStartRange, $preyEndRange, $dampingProxy, `
            $livingSpecies, $survivingInitialSpecies, $initialSpecies.Count, $survivalPercent, $initialCoverage, $finalCoverage, $vegetationDelta, `
            (($last.PopulationBySpecies | Measure-Object -Property Births -Sum).Sum), `
            (($last.PopulationBySpecies | Measure-Object -Property Deaths -Sum).Sum), $top, $metrics
    }

    $perfTag = "wild-unitperf-$Years"
    $perfPath = Join-Path $root 'Logs\metrics-unit-perf-5000.json'
    $perfStartedUtc = [DateTime]::UtcNow
    & "$PSScriptRoot\probe_unity.ps1" -Mode UnitPerf -Tag $perfTag -Marker '\[UnitPerf\] PASS|\[UnitPerf\] FAIL' -TimeoutSec $TimeoutSec
    $perfLog = Join-Path $root "Logs\$perfTag-UnitPerf.log"
    if (-not (Test-Path -LiteralPath $perfLog) -or -not (Select-String -LiteralPath $perfLog -Pattern '\[UnitPerf\] (PASS|FAIL)' -Quiet)) {
        throw "UnitPerf did not report completion; inspect $perfLog"
    }
    if (-not (Test-Path -LiteralPath $perfPath) -or (Get-Item -LiteralPath $perfPath).LastWriteTimeUtc -lt $perfStartedUtc) {
        throw "UnitPerf report was not refreshed: $perfPath"
    }
    $perf = Get-Content -LiteralPath $perfPath -Raw | ConvertFrom-Json
    if ($perf.TargetPopulation -ne 5000 -or $perf.MeasuredTicks -ne 600) { throw "UnitPerf report metadata is invalid: $perfPath" }
    "unit perf: average {0:N3} ms/tick, worst {1:N3} ms, population {2}-{3}, limit {4:N3} ms, passed {5}; report {6}" -f `
        $perf.AverageUnitSystemsMillisecondsPerTick, $perf.WorstUnitSystemsMillisecondsPerTick, $perf.MinimumMeasuredPopulation, `
        $perf.MaximumMeasuredPopulation, $perf.LimitMillisecondsPerTick, $perf.Passed, $perfPath
    if (-not $perf.Passed) { $acceptanceFailures.Add("5,000-unit unit-system performance exceeded 0.5 ms/tick or population fell below 95% of target") }
}
finally {
    Remove-Item Env:PG_WILD_SEED, Env:PG_WILD_YEARS -ErrorAction SilentlyContinue
}
if ($acceptanceFailures.Count -gt 0) {
    $acceptanceFailures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "Wildlife ecosystem acceptance failed: $($acceptanceFailures.Count) issue(s) across $($Seeds.Count) seed(s)."
}
