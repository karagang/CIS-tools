$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is required.' }
$framework = Split-Path -Parent $compiler
$distribution = $sourceRoot
$arguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output',
    ('/out:' + (Join-Path $distribution 'AnomalyCharts.exe')),
    ('/win32manifest:' + (Join-Path $sourceRoot 'app.manifest')))
foreach ($assembly in @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'Microsoft.VisualBasic.dll')) {
    $arguments += '/reference:' + (Join-Path $framework $assembly)
}
$arguments += Join-Path $sourceRoot 'AnomalyCharts.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
Write-Output ('Built ' + (Join-Path $distribution 'AnomalyCharts.exe'))
