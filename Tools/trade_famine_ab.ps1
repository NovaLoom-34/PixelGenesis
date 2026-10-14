param(
    [ulong[]]$Seeds = @(2024, 7, 99, 17, 41, 73),
    [int]$Years = 100,
    [int]$TimeoutSec = 1800
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$logs = Join-Path $root 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

$uniqueSeeds = @($Seeds | Select-Object -Unique)
if ($uniqueSeeds.Count -lt 6) { throw 'At least six distinct seeds are required.' }
if ($Years -lt 1) { throw 'Years must be positive.' }

$old = @{}
foreach ($name in @('PG_CIV_SCENARIO','PG_CIV_YEARS','PG_CIV_SEED','PG_CIV_TAG','PG_CIV_TRADE_ENABLED')) {
    $old[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$results = @{}

function Get-FamineRate($report) {
    $years = @($report.Samples | Sort-Object Year)
    $previous = 0
    $famineEvents = 0
    $civilizationYears = 0
    foreach ($sample in $years) {
        $current = [int]$sample.FamineEvents
        $famineEvents += [Math]::Max(0, $current - $previous)
        $previous = $current
        $civilizationYears += [Math]::Max(0, [int]$sample.Population)
    }
    return [pscustomobject]@{
        FamineEvents = $famineEvents
        CivilizationYears = $civilizationYears
        RatePer1000CivilizationYears = if ($civilizationYears -gt 0) { 1000.0 * $famineEvents / $civilizationYears } else { [double]::PositiveInfinity }
        Deliveries = [int]$report.Summary.TradeDeliveries
        TradeAmount = [int]$report.Summary.TradeAmount
        FinalPopulation = if ($years.Count -gt 0) { [int]$years[-1].Population } else { 0 }
    }
}

try {
    foreach ($seed in $uniqueSeeds) {
        foreach ($mode in @('off','on')) {
            $enabled = if ($mode -eq 'on') { '1' } else { '0' }
            $tag = "trade-famine-$mode-$Years-$seed"
            $metricPath = Join-Path $logs "metrics-$tag.json"
            $runLog = Join-Path $logs "$tag-CivSoak.log"
            Remove-Item $metricPath, $runLog -Force -ErrorAction SilentlyContinue
            $env:PG_CIV_SCENARIO = 'multi'
            $env:PG_CIV_YEARS = [string]$Years
            $env:PG_CIV_SEED = [string]$seed
            $env:PG_CIV_TAG = $tag
            $env:PG_CIV_TRADE_ENABLED = $enabled
            & (Join-Path $PSScriptRoot 'probe_unity.ps1') -Mode CivSoak -Tag $tag -Marker '\[Civ\] metrics:' -TimeoutSec $TimeoutSec | Out-Host
            if (-not (Test-Path $metricPath)) { throw "Missing metrics report for seed ${seed} trade-${mode}: $metricPath" }
            $report = Get-Content -LiteralPath $metricPath -Raw | ConvertFrom-Json
            if ([int]$report.RequestedYears -ne $Years -or [string]$report.Seed -ne [string]$seed) {
                throw "Metrics metadata mismatch for seed $seed trade-$mode."
            }
            $results["$seed-$mode"] = Get-FamineRate $report
        }
    }
}
finally {
    foreach ($name in $old.Keys) { [Environment]::SetEnvironmentVariable($name, $old[$name], 'Process') }
}

$pairedSeeds = @($uniqueSeeds | Where-Object { $results["${_}-on"].Deliveries -gt 0 })
$allOnDeliveries = ($uniqueSeeds | ForEach-Object { $results["${_}-on"].Deliveries } | Measure-Object -Sum).Sum
$onEvents = 0; $onYears = 0; $offEvents = 0; $offYears = 0
foreach ($seed in $pairedSeeds) {
    $on = $results["${seed}-on"]; $off = $results["${seed}-off"]
    $onEvents += $on.FamineEvents; $onYears += $on.CivilizationYears
    $offEvents += $off.FamineEvents; $offYears += $off.CivilizationYears
}
$onRate = if ($onYears -gt 0) { 1000.0 * $onEvents / $onYears } else { [double]::PositiveInfinity }
$offRate = if ($offYears -gt 0) { 1000.0 * $offEvents / $offYears } else { [double]::PositiveInfinity }
$comparison = [pscustomobject]@{
    Years = $Years; Seeds = $uniqueSeeds; EligiblePairedSeeds = $pairedSeeds;
    TradeOnDeliveries = $allOnDeliveries;
    TradeOnFamineEvents = $onEvents; TradeOnCivilizationYears = $onYears; TradeOnRatePer1000CivilizationYears = $onRate;
    TradeOffFamineEvents = $offEvents; TradeOffCivilizationYears = $offYears; TradeOffRatePer1000CivilizationYears = $offRate;
    Result = if ($pairedSeeds.Count -ge 3 -and $allOnDeliveries -gt 0 -and $onRate -lt $offRate) { 'PASS' } else { 'INCONCLUSIVE_OR_FAIL' }
}
$outPath = Join-Path $logs "trade-famine-ab-$Years-years.json"
$comparison | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPath -Encoding UTF8
$comparison | Format-List
Write-Output "Comparison report: $outPath"
if ($comparison.Result -ne 'PASS') { exit 1 }
