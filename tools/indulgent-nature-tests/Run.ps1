# Runs production dispatch/power code with a small isolated combat test double.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testBuild = Join-Path $projectRoot '.godot/verification/indulgent-nature'
New-Item -ItemType Directory -Force -Path $testBuild | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Tests.csproj.template') -Destination (Join-Path $testBuild 'Tests.csproj')
dotnet run --project (Join-Path $testBuild 'Tests.csproj') -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Indulgent Nature checks failed.' }
