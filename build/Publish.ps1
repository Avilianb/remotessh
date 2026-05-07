param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\MySSH\MySSH.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$distDir = Join-Path $repoRoot "dist"
$exeName = "MySSH.exe"

New-Item -ItemType Directory -Force -Path $publishDir, $distDir | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

$sourceExe = Join-Path $publishDir $exeName
$distExe = Join-Path $distDir $exeName
Copy-Item -LiteralPath $sourceExe -Destination $distExe -Force

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $distExe
"$($hash.Hash.ToLowerInvariant())  $exeName" | Set-Content -LiteralPath (Join-Path $distDir "$exeName.sha256") -Encoding ASCII

Write-Host "Published $distExe"
Write-Host "SHA256 $($hash.Hash.ToLowerInvariant())"
