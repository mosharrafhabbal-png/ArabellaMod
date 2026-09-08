$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testBuild = Join-Path $projectRoot '.godot/verification/card-rewrite'
New-Item -ItemType Directory -Force -Path $testBuild | Out-Null
$names = @('ArabellaSerpentKissChain','ArabellaLustfireVeil','ArabellaBacklashStrangle','ArabellaLingeringWarmth','ArabellaLavishRecovery','ArabellaCoiledBladeRecovery')
$sources = Get-ChildItem (Join-Path $projectRoot 'ArabellaModCode/Cards') -Filter '*.cs' | ForEach-Object { Get-Content -Raw -Encoding UTF8 $_.FullName }
$all = $sources -join "`n"
$usings = @('ArabellaMod.Characters','ArabellaMod.Mechanics','ArabellaMod.Powers','MegaCrit.Sts2.Core.CardSelection','MegaCrit.Sts2.Core.Commands','MegaCrit.Sts2.Core.Entities.Cards','MegaCrit.Sts2.Core.Extensions','MegaCrit.Sts2.Core.GameActions.Multiplayer','MegaCrit.Sts2.Core.HoverTips','MegaCrit.Sts2.Core.Localization.DynamicVars','MegaCrit.Sts2.Core.Models','MegaCrit.Sts2.Core.Models.Powers','MegaCrit.Sts2.Core.ValueProps','STS2RitsuLib.Combat.SecondaryResources')
$generated = ($usings | ForEach-Object { "using $_;" }) -join "`n"
$generated += "`nnamespace ArabellaMod.Cards;`n"
foreach ($name in $names) {
    $matchesForClass = [regex]::Matches($all, "(?ms)^public sealed class $name\b.*?^}")
    if ($matchesForClass.Count -ne 1) { throw "Expected one production class: $name" }
    $generated += $matchesForClass[0].Value + "`n"
}
[IO.File]::WriteAllText((Join-Path $testBuild 'Cards.cs'), $generated, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Tests.csproj.template') -Destination (Join-Path $testBuild 'Tests.csproj')
dotnet run --project (Join-Path $testBuild 'Tests.csproj') -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Card rewrite regression checks failed.' }
