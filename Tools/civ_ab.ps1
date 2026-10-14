# Runs repeatable civilisation soaks and verifies that every run wrote machine-readable metrics.
# Example: Tools/civ_ab.ps1 -Scenario multi -Years 200 -Seeds @(11,23,42)
param(
    [int[]]$Seeds = @(2024, 7, 99),
    [ValidateSet('single','war','multi','founders')][string]$Scenario = 'single',
    [ValidateRange(1,10000)][int]$Years = 100,
    [ValidateRange(60,7200)][int]$TimeoutSec = 900,
    # Run every seed even if one fails, then throw one summary (so report-reading checks see all seeds).
    [switch]$ContinueOnFailure
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$failures = @()
try {
    foreach ($seed in $Seeds) {
        try {
            $tag = "civ-$Scenario-$Years-$seed"
            $env:PG_CIV_SEED = "$seed"
            $env:PG_CIV_SCENARIO = $Scenario
            $env:PG_CIV_YEARS = "$Years"
            $env:PG_CIV_TAG = $tag
            $metrics = Join-Path $root "Logs\metrics-$tag.json"
            if (Test-Path -LiteralPath $metrics) { Remove-Item -LiteralPath $metrics -Force }
            & "$PSScriptRoot\probe_unity.ps1" -Mode CivSoak -Tag $tag -Marker '\[Civ\] (ALL OK|\d+ failure)' -TimeoutSec $TimeoutSec
            if (-not (Test-Path -LiteralPath $metrics)) { throw "CivSoak did not write metrics for seed $seed ($Scenario, $Years years): $metrics" }
            $log = Join-Path $root "Logs\$tag-CivSoak.log"
            if (-not (Select-String -LiteralPath $log -Pattern '\[Civ\] ALL OK' -Quiet)) { throw "CivSoak did not report a clean completion for seed $seed; inspect $log" }
            $report = Get-Content -LiteralPath $metrics -Raw | ConvertFrom-Json
            "seed {0} ({1} years, {2}): cities {3}, wars {4}, peace {5}, famine {6}, crafted items {7}, avg tick {8:N3} ms; metrics {9}" -f `
                $seed, $Years, $Scenario, $report.Summary.FoundedCities, $report.Summary.DeclaredWars, $report.Summary.PeaceEvents, `
                $report.Summary.FamineEvents, $report.Summary.CraftedItems, $report.Summary.TotalMillisecondsPerTick, $metrics
        }
        catch {
            if (-not $ContinueOnFailure) { throw }
            $failures += "seed ${seed}: $($_.Exception.Message)"
            Write-Warning "seed $seed failed: $($_.Exception.Message)"
        }
    }
    if ($failures.Count -gt 0) { throw ("{0} of {1} seeds failed:`n{2}" -f $failures.Count, @($Seeds).Count, ($failures -join "`n")) }
}
finally {
    Remove-Item Env:PG_CIV_SEED, Env:PG_CIV_SCENARIO, Env:PG_CIV_YEARS, Env:PG_CIV_TAG -ErrorAction SilentlyContinue
}
