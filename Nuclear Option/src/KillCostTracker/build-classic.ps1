param(
    [string]$OutputPath = "$PSScriptRoot\NuclearOption.KillCostTracker.dll"
)

$ErrorActionPreference = "Stop"
$game = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$managed = Join-Path $game "NuclearOption_Data\Managed"
$core = Join-Path $game "BepInEx\core"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $PSScriptRoot "KillCostTrackerPlugin.cs"

$references = @(
    "/reference:$core\BepInEx.dll",
    "/reference:$core\0Harmony.dll",
    "/reference:$managed\Assembly-CSharp.dll",
    "/reference:$managed\Mirage.dll",
    "/reference:$managed\Rewired_Core.dll",
    "/reference:$managed\netstandard.dll",
    "/reference:$managed\Unity.InputSystem.dll",
    "/reference:$managed\UnityEngine.dll",
    "/reference:$managed\UnityEngine.CoreModule.dll",
    "/reference:$managed\UnityEngine.IMGUIModule.dll",
    "/reference:$managed\UnityEngine.InputLegacyModule.dll",
    "/reference:$managed\UnityEngine.TextRenderingModule.dll",
    "/reference:$managed\UnityEngine.UIModule.dll",
    "/reference:$managed\UnityEngine.UI.dll",
    "/reference:System.Runtime.Serialization.dll"
)

& $compiler /nologo /target:library "/out:$OutputPath" @references $source
if ($LASTEXITCODE -ne 0) {
    throw "Classic BepInEx build failed with exit code $LASTEXITCODE."
}

Get-Item -LiteralPath $OutputPath
