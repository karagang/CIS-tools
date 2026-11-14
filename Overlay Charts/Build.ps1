$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$framework = Split-Path -Parent $compiler
$shared = Join-Path $PSScriptRoot 'Shared\OverlayCharts.cs'
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Windows.Forms.DataVisualization.dll')
$referenceArgs = @($references | ForEach-Object { '/reference:' + (Join-Path $framework $_) })
foreach ($region in @('Gulf', 'NewFoundLand', 'SouthLabCoast')) {
    $folder = Join-Path $PSScriptRoot $region
    foreach ($source in Get-ChildItem -LiteralPath $folder -Filter '*-Overlay-*.cs') {
        $executable = [IO.Path]::ChangeExtension($source.FullName, '.exe')
        & $compiler /nologo /target:exe /optimize+ "/out:$executable" @referenceArgs $shared $source.FullName
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $($source.Name)" }
        Write-Output "Built: $executable"
    }
}
