param(
    [Parameter(Mandatory = $true)]
    [string]$Owner,

    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [string]$Tag = "v0.1.0",

    [string]$ReleaseName = "My SSH Server Scripts v0.1.0",

    [switch]$Draft,

    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"

$token = $env:GITHUB_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    $token = $env:GH_TOKEN
}

if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Set GITHUB_TOKEN or GH_TOKEN before publishing. The token needs permission to create releases and upload assets for $Owner/$Repo."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptsDir = Join-Path $repoRoot "scripts"
$stagingDir = Join-Path $repoRoot "artifacts\server-scripts\$Tag"

$scriptFiles = @(
    "myssh-bootstrap.sh",
    "myssh-cleanup.sh",
    "myssh-disable-password.sh"
)

New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

foreach ($file in $scriptFiles) {
    $source = Join-Path $scriptsDir $file
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing server script: $source"
    }

    Copy-Item -LiteralPath $source -Destination (Join-Path $stagingDir $file) -Force
}

$hashLines = foreach ($file in $scriptFiles) {
    $path = Join-Path $stagingDir $file
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $path
    "$($hash.Hash.ToLowerInvariant())  $file"
}

$hashFile = Join-Path $stagingDir "server-scripts.sha256"
$hashLines | Set-Content -LiteralPath $hashFile -Encoding ASCII

$headers = @{
    Authorization = "Bearer $token"
    Accept = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$releaseApi = "https://api.github.com/repos/$Owner/$Repo/releases"
$releaseByTagApi = "$releaseApi/tags/$Tag"

try {
    $release = Invoke-RestMethod -Method Get -Uri $releaseByTagApi -Headers $headers
}
catch {
    $body = @{
        tag_name = $Tag
        name = $ReleaseName
        body = @"
Static server scripts for My SSH key takeover and recovery workflows.

Assets:
$($hashLines -join "`n")
"@
        draft = [bool]$Draft
        prerelease = [bool]$Prerelease
    } | ConvertTo-Json

    $release = Invoke-RestMethod -Method Post -Uri $releaseApi -Headers $headers -ContentType "application/json" -Body $body
}

$assets = @($scriptFiles + "server-scripts.sha256")
foreach ($asset in $assets) {
    $assetPath = Join-Path $stagingDir $asset

    $existingAsset = @($release.assets) | Where-Object { $_.name -eq $asset } | Select-Object -First 1
    if ($existingAsset) {
        Invoke-RestMethod -Method Delete -Uri $existingAsset.url -Headers $headers | Out-Null
    }

    $uploadUrl = "https://uploads.github.com/repos/$Owner/$Repo/releases/$($release.id)/assets?name=$([uri]::EscapeDataString($asset))"
    Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers $headers -ContentType "application/octet-stream" -InFile $assetPath | Out-Null
}

Write-Host "Published server scripts to https://github.com/$Owner/$Repo/releases/tag/$Tag"
foreach ($asset in $assets) {
    Write-Host "https://github.com/$Owner/$Repo/releases/download/$Tag/$asset"
}
