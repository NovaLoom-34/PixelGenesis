param([string]$Tag = 'web')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$rootPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

function Get-ProjectChild([string]$relativePath, [string]$label) {
    $full = [IO.Path]::GetFullPath((Join-Path $projectRoot $relativePath))
    if (-not $full.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use $label outside the project: $full"
    }
    return $full
}

$safeTag = ($Tag -replace '[^A-Za-z0-9._-]', '-').Trim('-', '.')
if ([string]::IsNullOrWhiteSpace($safeTag)) { $safeTag = 'web' }
$runId = '{0}-{1}' -f $safeTag, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
# Unity names WebGL files after the output folder, so the staging folder must be called PixelGenesis.
$buildStageRoot = Get-ProjectChild "Builds/Web/candidate-$runId" 'WebGL staging root'
$buildStage = Join-Path $buildStageRoot 'PixelGenesis'
$siteStage = Get-ProjectChild "Builds/WebSite.candidate-$runId" 'hosting staging output'
$buildFinal = Get-ProjectChild 'Builds/Web/PixelGenesis' 'WebGL output'
$siteFinal = Get-ProjectChild 'Builds/WebSite' 'hosting output'
$buildBackup = Get-ProjectChild "Builds/Web/PixelGenesis.previous-$runId" 'previous WebGL backup'
$siteBackup = Get-ProjectChild "Builds/WebSite.previous-$runId" 'previous hosting backup'
$archiveStage = Get-ProjectChild "Builds/PixelGenesis-hosting-ready-$runId.candidate.zip" 'hosting archive candidate'
$archiveFinal = Get-ProjectChild "Builds/PixelGenesis-hosting-ready-$runId.zip" 'hosting archive output'

foreach ($path in @($buildStageRoot, $siteStage, $buildBackup, $siteBackup, $archiveStage, $archiveFinal)) {
    if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite existing staging/backup path: $path" }
}

# 1) Build into a unique candidate. The existing WebGL output and hosting folder stay intact.
$hadPreviousBuildPath = Test-Path Env:PG_WEB_BUILD_PATH
$previousBuildPath = $env:PG_WEB_BUILD_PATH
try {
    $env:PG_WEB_BUILD_PATH = $buildStage
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run_unity.ps1') -Mode WebBuild -Tag $runId
    $unityExitCode = $LASTEXITCODE
}
finally {
    if ($hadPreviousBuildPath) { $env:PG_WEB_BUILD_PATH = $previousBuildPath }
    else { Remove-Item Env:PG_WEB_BUILD_PATH -ErrorAction SilentlyContinue }
}
if ($unityExitCode -ne 0) { throw "WebBuild failed (exit $unityExitCode). Candidate, if any, kept at $buildStage" }

# 2) Assemble and verify a complete candidate before changing either published folder.
foreach ($directory in 'Build', 'StreamingAssets', 'TemplateData') {
    if (-not (Test-Path -LiteralPath (Join-Path $buildStage $directory) -PathType Container)) {
        throw "WebBuild output missing: $directory (candidate kept at $buildStage)"
    }
}
New-Item -ItemType Directory -Path $siteStage | Out-Null
foreach ($directory in 'Build', 'StreamingAssets', 'TemplateData') {
    Copy-Item -LiteralPath (Join-Path $buildStage $directory) -Destination $siteStage -Recurse
}
$websiteSource = Join-Path $projectRoot 'WebSite'
foreach ($entry in Get-ChildItem -LiteralPath $websiteSource -Force) {
    if ($entry.Name -eq 'tests') { continue }
    Copy-Item -LiteralPath $entry.FullName -Destination $siteStage -Recurse -Force
}
# Cache-bust the Unity build files: browsers otherwise pair a cached old framework.js with the new wasm.
$gamePage = Join-Path $siteStage 'oyun.html'
$gameHtml = [IO.File]::ReadAllText($gamePage, [Text.Encoding]::UTF8)
if (-not $gameHtml.Contains('__PG_BUILD_VERSION__')) { throw 'oyun.html is missing the __PG_BUILD_VERSION__ placeholder.' }
[IO.File]::WriteAllText($gamePage, $gameHtml.Replace('__PG_BUILD_VERSION__', $runId), (New-Object Text.UTF8Encoding($false)))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($siteStage, $archiveStage, [IO.Compression.CompressionLevel]::Optimal, $false)
& python (Join-Path $PSScriptRoot 'verify_web_bundle.py') --bundle-path $siteStage --archive-path $archiveStage
if ($LASTEXITCODE -ne 0) { throw "Staged web bundle verification failed (exit $LASTEXITCODE). Candidate kept at $siteStage" }

# 3) Promote both candidates with rollback. Prior deliverables are retained as dated backups.
$buildBackedUp = $false
$siteBackedUp = $false
$buildPromoted = $false
$sitePromoted = $false
$archivePromoted = $false
try {
    if (Test-Path -LiteralPath $buildFinal) {
        Move-Item -LiteralPath $buildFinal -Destination $buildBackup
        $buildBackedUp = $true
    }
    Move-Item -LiteralPath $buildStage -Destination $buildFinal
    $buildPromoted = $true

    if (Test-Path -LiteralPath $siteFinal) {
        Move-Item -LiteralPath $siteFinal -Destination $siteBackup
        $siteBackedUp = $true
    }
    Move-Item -LiteralPath $siteStage -Destination $siteFinal
    $sitePromoted = $true

    Move-Item -LiteralPath $archiveStage -Destination $archiveFinal
    $archivePromoted = $true
}
catch {
    if ($archivePromoted -and (Test-Path -LiteralPath $archiveFinal)) { Move-Item -LiteralPath $archiveFinal -Destination $archiveStage }
    if ($sitePromoted -and (Test-Path -LiteralPath $siteFinal)) { Move-Item -LiteralPath $siteFinal -Destination $siteStage }
    if ($siteBackedUp -and (Test-Path -LiteralPath $siteBackup)) { Move-Item -LiteralPath $siteBackup -Destination $siteFinal }
    if ($buildPromoted -and (Test-Path -LiteralPath $buildFinal)) { Move-Item -LiteralPath $buildFinal -Destination $buildStage }
    if ($buildBackedUp -and (Test-Path -LiteralPath $buildBackup)) { Move-Item -LiteralPath $buildBackup -Destination $buildFinal }
    throw
}

if (-not (Get-ChildItem -LiteralPath $buildStageRoot -Force)) { Remove-Item -LiteralPath $buildStageRoot -Force }
Write-Output "Verified hosting bundle: $siteFinal"
Write-Output "Verified hosting archive: $archiveFinal"
if ($siteBackedUp) { Write-Output "Previous hosting bundle retained: $siteBackup" }
if ($buildBackedUp) { Write-Output "Previous WebGL output retained: $buildBackup" }
