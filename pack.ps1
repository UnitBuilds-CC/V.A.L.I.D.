# Packaging Script for V.A.L.I.D Framework
# Restores, builds, and packages the solution in Release mode.

$ErrorActionPreference = "Stop"

# 1. Clean and Setup Target Folder
$DistDir = Join-Path $PSScriptRoot "dist"
if (Test-Path $DistDir) {
    Remove-Item -Recurse -Force $DistDir
}
New-Item -ItemType Directory -Path $DistDir | Out-Null

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Packaging V.A.L.I.D in Release Mode" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

# 2. Restore and Build Generator first (since the main library packs it as analyzer)
Write-Host "Building Valid.Generator..." -ForegroundColor Green
dotnet build src/Valid.Generator/Valid.Generator.csproj -c Release --nologo

# 3. Restore and Build main Valid library
Write-Host "Building Valid..." -ForegroundColor Green
dotnet build src/Valid/Valid.csproj -c Release --nologo

# 4. Pack Valid library
Write-Host "Packing Valid NuGet package..." -ForegroundColor Green
dotnet pack src/Valid/Valid.csproj -c Release -o $DistDir --no-build --nologo

# 5. Build and Pack Valid.FSharp library
Write-Host "Building and Packing Valid.FSharp..." -ForegroundColor Green
dotnet pack src/Valid.FSharp/Valid.FSharp.fsproj -c Release -o $DistDir --nologo

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Packaging Completed Successfully!" -ForegroundColor Cyan
Write-Host "Packages located in: $DistDir" -ForegroundColor Cyan
Get-ChildItem $DistDir -Filter *.nupkg | ForEach-Object {
    Write-Host " - $($_.Name) ($([Math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor Yellow
}
Write-Host "==============================================" -ForegroundColor Cyan
