param(
    [ValidateSet('EditMode','Graphics','PlaySmoke','Diagnostics','VisualBenchmark','GroundPreview','UnitPreview','ProfessionPreview','ArchitecturePreview','DepthPreview','ForestPreview','CrowdProbe','CrowdPreview','Perf','CivSoak','WildEcosystemSoak','UnitPerf','Build','AndroidBuild','AndroidReleaseBuild','WebBuild')][string]$Mode = 'EditMode',
    [string]$Tag = 'continue'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + (Join-Path $projectRoot "Logs/$Tag-$Mode.log") + '"'))
if ($Mode -eq 'EditMode') {
    $arguments += @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + (Join-Path $projectRoot "Logs/$Tag-EditMode.xml") + '"'))
} elseif ($Mode -eq 'Graphics') {
    $arguments += @('-runTests', '-testPlatform', 'EditMode', '-testCategory', 'Graphics', '-testResults', ('"' + (Join-Path $projectRoot "Logs/$Tag-Graphics.xml") + '"'))
} elseif ($Mode -eq 'PlaySmoke') {
    $arguments += @('-executeMethod', 'PG.EditorTools.PlaySmoke.Run')
} elseif ($Mode -eq 'Build') {
    $arguments += @('-executeMethod', 'PG.EditorTools.BuildGame.Windows')
} elseif ($Mode -eq 'AndroidBuild') {
    $arguments += @('-executeMethod', 'PG.EditorTools.BuildGame.Android')
} elseif ($Mode -eq 'AndroidReleaseBuild') {
    $arguments += @('-executeMethod', 'PG.EditorTools.BuildGame.AndroidRelease')
} elseif ($Mode -eq 'WebBuild') {
    $arguments += @('-executeMethod', 'PG.EditorTools.BuildGame.Web')
} elseif ($Mode -eq 'VisualBenchmark') {
    $arguments += @('-executeMethod', 'PG.EditorTools.VisualBenchmark.Run')
} elseif ($Mode -eq 'GroundPreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.HousePreview.RunGroundMaterials')
} elseif ($Mode -eq 'UnitPreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.HousePreview.RunFourRaces')
} elseif ($Mode -eq 'DepthPreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.DepthPreview.Run')
} elseif ($Mode -eq 'ForestPreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.DepthPreview.RunForest')
} elseif ($Mode -eq 'CrowdProbe') {
    $arguments += @('-nographics', '-executeMethod', 'PG.EditorTools.CrowdProbe.Run')
} elseif ($Mode -eq 'CrowdPreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.DepthPreview.RunCrowd')
} elseif ($Mode -eq 'Perf') {
    $arguments += @('-nographics', '-executeMethod', 'PG.EditorTools.PerfBench.Run')
} elseif ($Mode -eq 'CivSoak') {
    $arguments += @('-nographics', '-executeMethod', 'PG.EditorTools.CivSoak.Run')
} elseif ($Mode -eq 'WildEcosystemSoak') {
    $arguments += @('-nographics', '-executeMethod', 'PG.EditorTools.NatureSoak.RunWildEcosystem')
} elseif ($Mode -eq 'UnitPerf') {
    $arguments += @('-nographics', '-executeMethod', 'PG.EditorTools.NatureSoak.RunUnitPerformance')
} elseif ($Mode -eq 'ProfessionPreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.HousePreview.RunProfessions')
} elseif ($Mode -eq 'ArchitecturePreview') {
    $arguments += @('-executeMethod', 'PG.EditorTools.HousePreview.RunArchitectureStates')
} else {
    $arguments += @('-nographics', '-executeMethod', 'PG.EditorTools.DiagnosticsMenu.Run')
}
$process = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
Write-Output "Unity $Mode exit: $($process.ExitCode)"
exit $process.ExitCode
