$ErrorActionPreference = "Stop"

dotnet publish TowEstimator.csproj -c Release -r win-x64 | Out-Null

$publishDir = "bin/Release/net8.0-windows/win-x64/publish"
$outputDir = "dist/portable"

if (-not (Test-Path $publishDir)) {
  Write-Error "Publish output not found at $publishDir"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $outputDir "app-data") | Out-Null

$exe = Get-ChildItem -Path $publishDir -Filter *.exe | Select-Object -First 1
if (-not $exe) {
  Write-Error "No .exe found in $publishDir"
}

Copy-Item $exe.FullName -Destination (Join-Path $outputDir $exe.Name) -Force
Write-Host "Portable package created at $outputDir"
