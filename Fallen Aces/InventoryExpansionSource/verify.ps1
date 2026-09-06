$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe "/out:$PSScriptRoot\Verify.exe" "$PSScriptRoot\InventoryLogic.cs" "$PSScriptRoot\Verify.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$PSScriptRoot\Verify.exe"
if ($LASTEXITCODE -ne 0) { throw 'Scroll verification failed.' }
