$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$testArguments = @('/nologo', '/target:exe', '/utf8output',
    ('/out:' + (Join-Path $PSScriptRoot 'Test-Analysis.exe')),
    ('/reference:' + (Join-Path $PSScriptRoot 'ArcticOverlayCharts.exe')),
    '/reference:System.Core.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    (Join-Path $PSScriptRoot 'Test-Analysis.cs'))
& $compilerPath @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $PSScriptRoot 'Test-Analysis.exe')
if ($LASTEXITCODE -ne 0) { throw 'Analysis tests failed.' }
