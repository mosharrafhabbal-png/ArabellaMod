$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testBuild = Join-Path $projectRoot '.godot/verification/lust-survival'
New-Item -ItemType Directory -Force -Path $testBuild | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Tests.csproj.template') -Destination (Join-Path $testBuild 'Tests.csproj')
dotnet run --project (Join-Path $testBuild 'Tests.csproj') -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Lust survival regression checks failed.' }
