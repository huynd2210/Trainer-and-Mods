# Builds and runs the offline patch check on the Windows .NET Framework CLR (C# 5 compiler that ships with Windows).
$here   = $PSScriptRoot
$root   = Resolve-Path (Join-Path $here '..\..')
$core   = Join-Path $root 'vendor\BepInEx\BepInEx\core'
$csc    = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outDir = Join-Path $here 'bin'
New-Item -ItemType Directory -Force $outDir | Out-Null
$refs = @('BepInEx.dll', '0Harmony.dll') | ForEach-Object { '/r:' + (Join-Path $core $_) }
$exe = Join-Path $outDir 'PatchCheck.exe'
$src = Join-Path $here 'PatchCheck.cs'
& $csc /nologo /target:exe "/out:$exe" $refs $src
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }
# BepInEx and Harmony (and their MonoMod/Cecil dependencies) must sit next to the exe: Main's own JIT needs them
Copy-Item (Join-Path $core '*.dll') $outDir -Force
& $exe
exit $LASTEXITCODE
