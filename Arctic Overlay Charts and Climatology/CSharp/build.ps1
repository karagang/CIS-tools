$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'The Windows .NET Framework C# compiler is required.' }
$frameworkPath = Split-Path -Parent $compilerPath
$compilerArguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output',
    ('/out:' + (Join-Path $PSScriptRoot 'ArcticOverlayCharts.exe')),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
foreach ($assemblyName in @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'Microsoft.VisualBasic.dll')) {
    $compilerArguments += '/reference:' + (Join-Path $frameworkPath $assemblyName)
}
foreach ($sourceName in @('Engine.cs', 'Charts.cs', 'MainForm.cs', 'Program.cs')) {
    $compilerArguments += Join-Path $PSScriptRoot $sourceName
}
& $compilerPath @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
Write-Output ('Built ' + (Join-Path $PSScriptRoot 'ArcticOverlayCharts.exe'))
