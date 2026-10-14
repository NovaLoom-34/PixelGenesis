param(
    [string]$Tag = 'gate',
    [string]$Python = 'python',
    [string]$UnityPowerShell = 'powershell.exe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$thresholds = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'gate_thresholds.json') -Raw | ConvertFrom-Json
if ([int]$thresholds.editModeMinimumTests -lt 204 -or [int]$thresholds.graphicsMinimumTests -lt 3) {
    throw 'EditMode and Graphics thresholds must preserve the documented 207-case baseline (204 + 3 graphics cases).'
}
if ([int]$thresholds.founderYears -lt 100 -or @($thresholds.founderSeeds).Count -lt [int]$thresholds.minimumFounderSeeds) {
    throw 'Founder cohort gate must run at least the configured six seeds for 100 years.'
}
$logs = Join-Path $root 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Invoke-Child([string]$name, [string]$exe, [string]$arguments) {
    Write-Host "[gate] $name"
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Gate step '$name' failed with exit code $($process.ExitCode)." }
}

function Invoke-Unity([string]$mode, [string]$stepTag) {
    $script = Join-Path $PSScriptRoot 'run_unity.ps1'
    Invoke-Child "Unity $mode" $UnityPowerShell ('-NoProfile -ExecutionPolicy Bypass -File "{0}" -Mode {1} -Tag "{2}"' -f $script, $mode, $stepTag)
}

Push-Location $root
try {
    Invoke-Child 'localization audit' $Python ('"{0}"' -f (Join-Path $PSScriptRoot 'loc_audit.py'))
    Invoke-Child 'offline compile' $Python ('"{0}"' -f (Join-Path $PSScriptRoot 'compile_check.py'))
    Invoke-Child 'soak metrics comparison tests' $Python ('"{0}"' -f (Join-Path $PSScriptRoot 'test_metrics_compare.py'))

    $editTag = "$Tag-editmode"
    $editXml = Join-Path $logs "$editTag-EditMode.xml"
    if (Test-Path -LiteralPath $editXml) { Remove-Item -LiteralPath $editXml -Force }
    Invoke-Unity 'EditMode' $editTag
    if (-not (Test-Path -LiteralPath $editXml)) { throw "EditMode produced no result XML: $editXml" }
    [xml]$editResults = Get-Content -LiteralPath $editXml -Raw
    $editRun = $editResults.SelectSingleNode('/test-run')
    if (-not $editRun -or [int]$editRun.total -lt [int]$thresholds.editModeMinimumTests -or [int]$editRun.failed -gt 0) {
        throw "EditMode result is incomplete or has failures; inspect $editXml"
    }

    $graphicsTag = "$Tag-graphics"
    $graphicsXml = Join-Path $logs "$graphicsTag-Graphics.xml"
    if (Test-Path -LiteralPath $graphicsXml) { Remove-Item -LiteralPath $graphicsXml -Force }
    Invoke-Unity 'Graphics' $graphicsTag
    if (-not (Test-Path -LiteralPath $graphicsXml)) { throw "Graphics tests produced no result XML: $graphicsXml" }
    [xml]$graphicsResults = Get-Content -LiteralPath $graphicsXml -Raw
    $graphicsRun = $graphicsResults.SelectSingleNode('/test-run')
    if (-not $graphicsRun -or [int]$graphicsRun.total -lt [int]$thresholds.graphicsMinimumTests -or [int]$graphicsRun.failed -gt 0) {
        throw "Graphics tests are incomplete or have failures; inspect $graphicsXml"
    }

    Invoke-Child 'source-to-NUnit test inventory' $Python ('"{0}" --root "{1}" --editmode-xml "{2}" --graphics-xml "{3}" --editmode-floor {4} --graphics-floor {5}' -f `
        (Join-Path $PSScriptRoot 'verify_test_inventory.py'), $root, $editXml, $graphicsXml, [int]$thresholds.editModeMinimumTests, [int]$thresholds.graphicsMinimumTests)

    $diagTag = "$Tag-diagnostics"
    Invoke-Unity 'Diagnostics' $diagTag
    $diagLog = Join-Path $logs "$diagTag-Diagnostics.log"
    if (-not (Select-String -LiteralPath $diagLog -Pattern '\[Diagnostics\] ALL OK' -Quiet)) { throw "Diagnostics did not report ALL OK; inspect $diagLog" }

    $smokeTag = "$Tag-smoke"
    Invoke-Unity 'PlaySmoke' $smokeTag
    $smokeLog = Join-Path $logs "$smokeTag-PlaySmoke.log"
    if (-not (Select-String -LiteralPath $smokeLog -Pattern '\[PlaySmoke\] OK:' -Quiet)) { throw "PlaySmoke did not report a clean run; inspect $smokeLog" }

    $crowdTag = "$Tag-crowd"
    Invoke-Unity 'CrowdProbe' $crowdTag
    $crowdLog = Join-Path $logs "$crowdTag-CrowdProbe.log"
    foreach ($year in @(0, 2, 8, 20, 40)) {
        if (-not (Select-String -LiteralPath $crowdLog -Pattern ("^\[Crowd\] year {0}:" -f $year) -Quiet)) {
            throw "CrowdProbe report is missing year $year; inspect $crowdLog"
        }
    }

    $soakScript = Join-Path $PSScriptRoot 'civ_ab.ps1'
    Write-Host '[gate] multi-kingdom soak and JSON reports'
    # Call in this PowerShell process so the configured seed array stays an int[];
    # `powershell -File ... -Seeds @(1,2,3)` passes that expression as literal text.
    & $soakScript -Scenario multi -Years ([int]$thresholds.multikingdomYears) -Seeds ([int[]]$thresholds.multikingdomSeeds)
    foreach ($seed in $thresholds.multikingdomSeeds) {
        $metricPath = Join-Path $logs "metrics-civ-multi-$($thresholds.multikingdomYears)-$seed.json"
        if (-not (Test-Path -LiteralPath $metricPath)) { throw "Missing soak metrics: $metricPath" }
        $report = Get-Content -LiteralPath $metricPath -Raw | ConvertFrom-Json
        if ($report.Samples.Count -lt [int]$thresholds.minimumMetricsSamples) { throw "Insufficient yearly metrics in $metricPath" }
        if ($report.Samples[0].LivingKingdoms -lt [int]$thresholds.minimumInitialLivingKingdoms) { throw "Multi scenario did not begin with enough kingdoms: $metricPath" }
        $civilizedSpeciesCount = @($report.Samples[0].PopulationBySpecies | Where-Object { $_.IsCiv -and $_.Population -gt 0 }).Count
        if ($civilizedSpeciesCount -lt [int]$thresholds.minimumCivilizedSpecies) { throw "Multi scenario did not retain all expected civilized species through year one: $metricPath" }
    }

    $founderScript = Join-Path $PSScriptRoot 'founder_soak.ps1'
    Write-Host '[gate] balanced human/elf founder survival and population floor'
    # Keep the configured seed list typed as an integer array in this PowerShell process.
    & $founderScript -Years ([int]$thresholds.founderYears) -Seeds ([int[]]$thresholds.founderSeeds)

    $economy = $thresholds.civilizationEconomy
    if ([int]$economy.years -lt 100 -or @($economy.seeds).Count -lt 6) {
        throw 'Civilisation economy acceptance must cover at least six seeds for 100 years.'
    }
    Write-Host '[gate] single-kingdom economy acceptance soak'
    & $soakScript -Scenario single -Years ([int]$economy.years) -Seeds ([int[]]$economy.seeds) -ContinueOnFailure
    Invoke-Child 'civilisation economy acceptance' $Python ('"{0}" --root "{1}" --years {2} --seeds {3}' -f `
        (Join-Path $PSScriptRoot 'civ_economy_gate.py'), $root, [int]$economy.years, (($economy.seeds | ForEach-Object { [string]$_ }) -join ' '))

    $perfProfile = $thresholds.performanceProfiles.editorBatch
    if (-not $perfProfile) { throw 'Missing performanceProfiles.editorBatch in gate_thresholds.json.' }
    $env:PG_PERF_SIZE = [string]$perfProfile.size
    $env:PG_PERF_TARGET_UNITS = [string]$perfProfile.targetUnits
    $env:PG_PERF_BUDGET_MS = ([double]$perfProfile.budgetMsPerTick).ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:PG_PERF_MAX_GC_BYTES_PER_TICK = [string]$perfProfile.maxGcBytesPerTick
    $env:PG_PERF_WARMUP = [string]$perfProfile.warmupYears
    $env:PG_PERF_YEARS = [string]$perfProfile.measureYears
    try {
        $perfTag = "$Tag-perf"
        Invoke-Unity 'Perf' $perfTag
        $perfLog = Join-Path $logs "$perfTag-Perf.log"
        $match = Select-String -LiteralPath $perfLog -Pattern '([0-9]+(?:\.[0-9]+)?) ms/tick avg'
        if (-not $match) { throw "Performance average missing from $perfLog" }
        $average = [double]::Parse($match.Matches[0].Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
        if ($average -gt [double]$perfProfile.budgetMsPerTick) { throw "Average tick $average ms exceeded editor-batch threshold $($perfProfile.budgetMsPerTick) ms/tick." }
        $gcMatch = Select-String -LiteralPath $perfLog -Pattern 'gc-worst=([0-9]+) B/tick'
        if (-not $gcMatch) { throw "Worst per-tick GC allocation missing from $perfLog" }
        $gcWorst = [long]::Parse($gcMatch.Matches[0].Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
        if ($gcWorst -gt [long]$perfProfile.maxGcBytesPerTick) { throw "Worst tick allocated $gcWorst GC bytes; threshold is $($perfProfile.maxGcBytesPerTick) B/tick." }
    }
    finally {
        Remove-Item Env:PG_PERF_SIZE, Env:PG_PERF_TARGET_UNITS, Env:PG_PERF_BUDGET_MS, Env:PG_PERF_MAX_GC_BYTES_PER_TICK, Env:PG_PERF_WARMUP, Env:PG_PERF_YEARS -ErrorAction SilentlyContinue
    }

    Write-Host "[gate] ALL OK - EditMode $($editRun.passed), Graphics $($graphicsRun.passed), $($thresholds.multikingdomSeeds.Count) multi-kingdom seeds, $($thresholds.founderSeeds.Count) founder seeds, editor-batch Perf $average ms/tick"
}
finally {
    Pop-Location
}
