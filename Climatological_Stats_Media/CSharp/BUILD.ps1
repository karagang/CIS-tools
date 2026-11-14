param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Source\IceClimatology.csproj'
$publish = Join-Path $PSScriptRoot 'publish'
& $DotNet publish $project -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'C# publishing failed.' }
Copy-Item -LiteralPath (Join-Path $publish 'IceClimatology.exe') -Destination (Join-Path $PSScriptRoot 'IceClimatology.exe') -Force
Write-Output "Executable ready: $(Join-Path $PSScriptRoot 'IceClimatology.exe')"
