param([string]$ArtifactName = 'card-rewrite')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if ($ArtifactName -notin @('card-rewrite', 'lust-survival')) { throw 'Unknown verified package.' }
$stage = Join-Path $projectRoot ('artifacts/' + $ArtifactName)
$gameMods = 'D:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/mods'
$backup = Join-Path $stage ('game-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$files = @(
    'ArabellaMod/ArabellaMod.dll',
    'ArabellaMod/ArabellaMod.json',
    'ArabellaMod/ArabellaMod.pck',
    'STS2-RitsuLib/STS2-RitsuLib.dll',
    'STS2-RitsuLib/mod_manifest.json',
    'STS2-RitsuLib/STS2-RitsuLib.xml'
)

# Validate the complete package before changing any installed file.
$mod = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $stage 'ArabellaMod/ArabellaMod.json') | ConvertFrom-Json
$lib = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $stage 'STS2-RitsuLib/mod_manifest.json') | ConvertFrom-Json
$required = ($mod.dependencies | Where-Object id -EQ 'STS2-RitsuLib').version
if ($required -ne $lib.version) { throw "RitsuLib version mismatch: $required / $($lib.version)" }
$hashes = @{}
foreach ($relative in $files) {
    $source = Join-Path $stage $relative
    if (!(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-Item -LiteralPath $source).Length -eq 0) {
        throw "Missing staged file: $relative"
    }
    $hashes[$relative] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
}
foreach ($relative in $files) {
    $installed = Join-Path $gameMods $relative
    if (Test-Path -LiteralPath $installed) {
        $saved = Join-Path $backup $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $saved) | Out-Null
        Copy-Item -LiteralPath $installed -Destination $saved
        if ((Get-FileHash -LiteralPath $saved).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) {
            throw "Backup verification failed: $relative"
        }
    }
}

foreach ($relative in $files) {
    $dest = Join-Path $gameMods $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
    Copy-Item -LiteralPath (Join-Path $stage $relative) -Destination $dest -Force
}
$results = foreach ($relative in $files) {
    $dest = Join-Path $gameMods $relative
    $actual = (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash
    if ($actual -ne $hashes[$relative]) { throw "Deployment hash mismatch: $relative" }
    [pscustomobject]@{ File = $relative; SHA256 = $actual; Bytes = (Get-Item -LiteralPath $dest).Length }
}
[pscustomobject]@{
    DeployedAt = (Get-Date).ToString('o')
    GameMods = $gameMods
    Backup = $backup
    RitsuLibVersion = $lib.version
    Files = @($results)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'deployment.json') -Encoding UTF8
$results | Format-Table -AutoSize
"Backup: $backup"
"PASS: All six installed files match the verified package; RitsuLib $($lib.version)."
