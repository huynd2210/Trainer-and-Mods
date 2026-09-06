param([switch]$NoInstall)
$ErrorActionPreference = 'Stop'
$gameRoot = Split-Path $PSScriptRoot -Parent
$managed = Join-Path $gameRoot 'Fallen Aces_Data\Managed'
$core = Join-Path $gameRoot 'BepInEx\core'
$sdkLine = (& dotnet --list-sdks | Select-Object -Last 1)
if ($sdkLine -notmatch '^(\S+) \[(.+)\]') { throw 'A .NET SDK is required.' }
$compiler = Join-Path $Matches[2] "$($Matches[1])\Roslyn\bincore\csc.dll"
$references = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName })
$references += "/reference:$core\BepInEx.dll", "/reference:$core\0Harmony.dll"
$output = Join-Path $PSScriptRoot 'FallenAces.Trainer.dll'
& dotnet $compiler /nologo /nostdlib+ /target:library /optimize+ /langversion:latest "/out:$output" @references `
    "$PSScriptRoot\TrainerModel.cs" "$PSScriptRoot\TrainerCheats.cs" "$PSScriptRoot\TrainerPlugin.cs"
if ($LASTEXITCODE -ne 0) { throw 'Trainer compilation failed.' }
if (!$NoInstall) {
    Copy-Item -LiteralPath $output -Destination (Join-Path $gameRoot 'BepInEx\plugins\FallenAces.Trainer.dll') -Force
}
Write-Host "Built $output"
