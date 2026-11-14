$ErrorActionPreference = 'Stop'
$files = @('IceClimatology.exe', 'README.txt', 'THIRD_PARTY_NOTICES.txt', 'TEST_RESULTS.txt') |
    ForEach-Object { Join-Path $PSScriptRoot $_ }
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing package file: $file" }
}
$archive = Join-Path $PSScriptRoot 'IceClimatology-Windows-x64.zip'
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'IceClimatology.exe') -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  IceClimatology.exe" } |
    Set-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Shareable ZIP: $archive"
