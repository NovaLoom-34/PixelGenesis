# Runs matched variable-climate and fixed-climate CivSoak cases for each seed.
# Unity execution is intentionally explicit: run this script during the final user test pass.
param(
    [int[]]$Seeds = @(2024, 7, 99, 11, 23, 42),
    [ValidateRange(1,10000)][int]$Years = 100,
    [ValidateRange(60,7200)][int]$TimeoutSec = 1800
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$results = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

function Invoke-ClimateCase([int]$seed, [bool]$fixedClimate) {
    $kind = if ($fixedClimate) { 'fixed' } else { 'variable' }
    $tag = "climate-$kind-$Years-$seed-$runId"
    $metricsPath = Join-Path $root "Logs\metrics-$tag.json"
    $logPath = Join-Path $root "Logs\$tag-CivSoak.log"
    $startedUtc = [DateTime]::UtcNow
    $env:PG_CIV_SEED = "$seed"
    $env:PG_CIV_SCENARIO = 'single'
    $env:PG_CIV_YEARS = "$Years"
    $env:PG_CIV_TAG = $tag
    $env:PG_CIV_CLIMATE_CONTROL = if ($fixedClimate) { '1' } else { '0' }
    & "$PSScriptRoot\probe_unity.ps1" -Mode CivSoak -Tag $tag -Marker '\[Civ\] (ALL OK|\d+ failure)' -TimeoutSec $TimeoutSec

    if (-not (Test-Path -LiteralPath $logPath) -or -not (Select-String -LiteralPath $logPath -Pattern '\[Civ\] ALL OK' -Quiet)) {
        throw "CivSoak did not report successful completion for $tag; inspect $logPath"
    }
    if (-not (Test-Path -LiteralPath $metricsPath) -or (Get-Item -LiteralPath $metricsPath).LastWriteTimeUtc -lt $startedUtc) {
        throw "CivSoak metrics were not freshly written for ${tag}: $metricsPath"
    }
    $report = Get-Content -LiteralPath $metricsPath -Raw | ConvertFrom-Json
    if ($report.RequestedYears -ne $Years -or $report.Samples.Count -ne $Years) { throw "CivSoak report metadata is invalid: $metricsPath" }
    $expectedScenario = if ($fixedClimate) { 'single-fixed-climate' } else { 'single-variable-climate' }
    if ($report.Scenario -ne $expectedScenario) { throw "Expected scenario $expectedScenario in $metricsPath, got $($report.Scenario)" }
    $last = $report.Samples[-1]
    [pscustomobject]@{
        Seed = $seed
        FixedClimate = $fixedClimate
        FamineEvents = [int]$report.Summary.FamineEvents
        ClimateLinkedFamineEvents = [int]$report.Summary.ClimateLinkedFamineEvents
        ClimateMillisecondsPerMonth = [double]$report.Summary.ClimateMillisecondsPerMonth
        FinalCivilizationPopulation = [int]$last.Population
        MetricsPath = $metricsPath
    }
}

function Get-PopulationStandardDeviation([object[]]$values) {
    if ($values.Count -le 1) { return 0.0 }
    $mean = [double](($values | Measure-Object -Average).Average)
    $sumSquares = 0.0
    foreach ($value in $values) { $difference = [double]$value - $mean; $sumSquares += $difference * $difference }
    return [Math]::Sqrt($sumSquares / $values.Count)
}

try {
    foreach ($seed in $Seeds) {
        $variable = Invoke-ClimateCase $seed $false
        $fixed = Invoke-ClimateCase $seed $true
        $results.Add([pscustomobject]@{ Seed = $seed; Variable = $variable; Fixed = $fixed })
        "seed {0}: famines variable/fixed {1}/{2}, climate-linked {3}/{4}, climate ms/month {5:N3}/{6:N3}, final civ population {7}/{8}" -f `
            $seed, $variable.FamineEvents, $fixed.FamineEvents, $variable.ClimateLinkedFamineEvents, $fixed.ClimateLinkedFamineEvents, `
            $variable.ClimateMillisecondsPerMonth, $fixed.ClimateMillisecondsPerMonth, $variable.FinalCivilizationPopulation, $fixed.FinalCivilizationPopulation
        if ($variable.FinalCivilizationPopulation -lt 200) { $failures.Add("seed ${seed}: variable-climate final civilization population is below 200") }
        if ($variable.ClimateMillisecondsPerMonth -gt 0.3) { $failures.Add("seed ${seed}: climate systems exceeded 0.3 ms/month") }
    }

    $variableTotalFamines = ($results | ForEach-Object { $_.Variable.FamineEvents } | Measure-Object -Sum).Sum
    $variableClimateFamines = ($results | ForEach-Object { $_.Variable.ClimateLinkedFamineEvents } | Measure-Object -Sum).Sum
    $linkedRatio = if ($variableTotalFamines -gt 0) { [double]$variableClimateFamines / $variableTotalFamines } else { 0.0 }
    $variableFamineCounts = @($results | ForEach-Object { $_.Variable.FamineEvents })
    $fixedFamineCounts = @($results | ForEach-Object { $_.Fixed.FamineEvents })
    $variableVariance = Get-PopulationStandardDeviation -values $variableFamineCounts
    $fixedVariance = Get-PopulationStandardDeviation -values $fixedFamineCounts
    "aggregate: climate-linked famines {0}/{1} ({2:P1}); famine-count standard deviation variable/fixed {3:N2}/{4:N2}" -f `
        $variableClimateFamines, $variableTotalFamines, $linkedRatio, $variableVariance, $fixedVariance
    if ($linkedRatio -lt 0.5) { $failures.Add("only $($linkedRatio.ToString('P1')) of variable-climate famines were linked to drought/cold/flood crop loss (target 50%)") }
    if ($variableVariance -le $fixedVariance) { $failures.Add('famine counts were not more variable across seeds under variable climate than the fixed-climate control') }
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root "Logs\climate-ab-$runId.json") -Encoding UTF8
}
finally {
    Remove-Item Env:PG_CIV_SEED, Env:PG_CIV_SCENARIO, Env:PG_CIV_YEARS, Env:PG_CIV_TAG, Env:PG_CIV_CLIMATE_CONTROL -ErrorAction SilentlyContinue
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "Climate acceptance failed: $($failures.Count) issue(s) across $($Seeds.Count) seed(s)."
}
