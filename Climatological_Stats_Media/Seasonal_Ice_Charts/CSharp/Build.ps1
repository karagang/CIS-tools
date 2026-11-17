param(
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$lakeFolders = @('combined_GRTLKS', 'Erie', 'Huron', 'Michigan', 'Ontario', 'Superior')
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$buildRoot = Join-Path $tempRoot ('GreatLakesBuild-' + [guid]::NewGuid().ToString('N'))
try {
    foreach ($lake in $lakeFolders) {
        $folder = Join-Path $PSScriptRoot $lake
        $project = Join-Path $folder ($lake + '_IceCharts.csproj')
        $publishFolder = Join-Path $buildRoot ($lake + '-publish')
        # Keep intermediate and runtime-pack paths short on Windows.
        & $Dotnet publish $project --configuration Release --artifacts-path (Join-Path $buildRoot 'artifacts') --output $publishFolder --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $lake." }
        $executable = $lake + '_IceCharts.exe'
        [System.IO.File]::Copy((Join-Path $publishFolder $executable), (Join-Path $folder $executable), $true)
        Write-Host "Built standalone executable for $lake."
    }
}
finally {
    $resolvedBuild = [System.IO.Path]::GetFullPath($buildRoot)
    if (-not $resolvedBuild.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not ([System.IO.Path]::GetFileName($resolvedBuild).StartsWith('GreatLakesBuild-'))) {
        throw 'Refusing to clean an unexpected temporary build path.'
    }
    if (Test-Path -LiteralPath $resolvedBuild) { Remove-Item -LiteralPath $resolvedBuild -Recurse -Force }
}
