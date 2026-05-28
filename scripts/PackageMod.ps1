param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDirectory,

    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$BuildDirectory,

    [Parameter(Mandatory = $true)]
    [string]$InfoPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SourceDirectory)) {
    throw "Source directory not found: $SourceDirectory"
}

if (-not (Test-Path -LiteralPath $InfoPath)) {
    throw "Info.json not found: $InfoPath"
}

$info = Get-Content -LiteralPath $InfoPath -Raw | ConvertFrom-Json
$id = [string]$info.Id
$version = [string]$info.Version
if ([string]::IsNullOrWhiteSpace($id)) {
    $id = [System.IO.Path]::GetFileName($ProjectDirectory.TrimEnd('\', '/'))
}

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Info.json Version is empty."
}

$packageName = "$id-$version"
$stageDirectory = Join-Path $BuildDirectory $packageName
$zipPath = Join-Path $BuildDirectory "$packageName.zip"

New-Item -ItemType Directory -Force -Path $BuildDirectory | Out-Null

if (Test-Path -LiteralPath $stageDirectory) {
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $stageDirectory | Out-Null
Copy-Item -Path (Join-Path $SourceDirectory '*') -Destination $stageDirectory -Recurse -Force

$itemsToArchive = Join-Path $stageDirectory '*'
Compress-Archive -Path $itemsToArchive -DestinationPath $zipPath -Force

Write-Host "Mod package folder: $stageDirectory"
Write-Host "Mod package zip: $zipPath"
