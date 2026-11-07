# Builds Mercenaries.sml and the three zips. Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File ModShardLauncher\ModSources\Mercenaries\package.ps1
# Expects this folder at ModShardLauncher\ModSources\Mercenaries and the packer at ModSources\_tools\mslpack
# (both come in Mercenaries-Source.zip). Needs the .NET 8 SDK.
# Output goes OUTSIDE the mod folder (the packer would otherwise sweep old zips into the next .sml):
#   <game dir>\_release\Mercenaries\{Mercenaries.zip, Mercenaries-Pack.zip, Mercenaries-Source.zip}
# and the fresh .sml is also copied into ModShardLauncher\Mods.
$ErrorActionPreference = 'Stop'
$Mod = 'Mercenaries'
$Src = $PSScriptRoot
$Msl = (Resolve-Path "$Src\..\..").Path
$Tool = "$Msl\ModSources\_tools\mslpack"
$Out = Join-Path (Split-Path $Msl -Parent) "_release\$Mod"
if (-not (Test-Path "$Msl\ModShardLauncher.dll")) { throw "ModShardLauncher.dll not found in $Msl" }
New-Item -ItemType Directory -Force "$Out\sml" | Out-Null

# 1. Packer: no build servers left behind.
dotnet build "$Tool\mslpack.csproj" -c Release -nologo -v q -nodeReuse:false /p:UseSharedCompilation=false
if ($LASTEXITCODE) { throw "packer build failed" }
$exe = "$Tool\bin\Release\net8.0-windows\mslpack.exe"

# 2. .sml, with a hard timeout.
$p = Start-Process $exe -ArgumentList @('pack', "`"$Src`"", "`"$Out\sml`"") -NoNewWindow -PassThru
if (-not $p.WaitForExit(180000)) { $p.Kill(); throw "packer timed out" }
$sml = "$Out\sml\$Mod.sml"
if (-not (Test-Path $sml)) { throw "no $sml" }
Copy-Item $sml "$Msl\Mods\$Mod.sml" -Force

# 3. Zips, written entry by entry (no staging copy of the 190 MB launcher).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
function New-Zip([string]$Path, $Entries) {
    if (Test-Path $Path) { Move-Item $Path "$Path.old" -Force }
    $z = [IO.Compression.ZipFile]::Open($Path, 'Create')
    try {
        foreach ($e in $Entries) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $e.From, ($e.As -replace '\\', '/'), 'Optimal')
        }
    } finally { $z.Dispose() }
    if (Test-Path "$Path.old") { Remove-Item "$Path.old" }   # only our own previous build
    "{0}  {1:N1} MB" -f $Path, ((Get-Item $Path).Length / 1MB)
}
function Files([string]$Root, [string]$Prefix, [string[]]$Skip = @()) {
    Get-ChildItem $Root -Recurse -File | Where-Object {
        $rel = $_.FullName.Substring($Root.Length + 1)
        -not ($rel -match '^(bin|obj)\\' -or ($Skip | Where-Object { $rel -like $_ }))
    } | ForEach-Object {
        $rel = $_.FullName.Substring($Root.Length + 1)
        @{ From = $_.FullName; As = $(if ($Prefix) { "$Prefix\$rel" } else { $rel }) }
    }
}

# Mod alone: extract into an existing ModShardLauncher folder.
New-Zip "$Out\$Mod.zip" @(
    @{ From = $sml; As = "Mods\$Mod.sml" },
    @{ From = "$Src\README.md"; As = "$Mod-README.md" })

# Source: extract into ModShardLauncher\ModSources.
New-Zip "$Out\$Mod-Source.zip" (@(Files $Src $Mod) + @(Files $Tool '_tools\mslpack'))

# Pack: ModShardLauncher + this mod only (other mods and every mod source left out).
$install = "$Out\INSTALL.txt"
@"
Mercenaries for Stoneshard 0.9.4.24 - bundled with ModShardLauncher 0.13.2

1. Extract this zip anywhere (for example next to the game folder).
2. Back up the game's data.win.
3. Run ModShardLauncher.exe, open the game's data.win, tick Mercenaries, and save the patched game.
4. Start Stoneshard. Talk to a tavern mercenary and choose "Looking for work?".

Right-click your mercenary for Wait here / Follow me. See Mercenaries-README.md for the rest.
"@ | Set-Content $install -Encoding ascii
$launcher = Files $Msl '' @('ModSources\*', 'Mods\*', '*.log', 'logs\*')
New-Zip "$Out\$Mod-Pack.zip" (@($launcher) + @(
    @{ From = $sml; As = "Mods\$Mod.sml" },
    @{ From = "$Src\README.md"; As = "$Mod-README.md" },
    @{ From = $install; As = 'INSTALL.txt' }))
