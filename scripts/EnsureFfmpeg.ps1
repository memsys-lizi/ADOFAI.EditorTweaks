param(
    [Parameter(Mandatory = $true)]
    [string]$ToolsDirectory
)

$ErrorActionPreference = "Stop"

$ffmpegPath = Join-Path $ToolsDirectory "ffmpeg.exe"
if (Test-Path -LiteralPath $ffmpegPath) {
    Write-Host "FFmpeg already exists: $ffmpegPath"
    exit 0
}

$url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ADOFAI.EditorTweaks.ffmpeg." + [System.Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "ffmpeg.zip"

try {
    New-Item -ItemType Directory -Force -Path $ToolsDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    Write-Host "Downloading FFmpeg from $url"
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing

    Write-Host "Extracting FFmpeg"
    Expand-Archive -Path $zipPath -DestinationPath $tempRoot -Force

    $downloadedFfmpeg = Get-ChildItem -Path $tempRoot -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
    if ($null -eq $downloadedFfmpeg) {
        throw "ffmpeg.exe was not found in the downloaded archive."
    }

    Copy-Item -LiteralPath $downloadedFfmpeg.FullName -Destination $ffmpegPath -Force
    Write-Host "FFmpeg installed to $ffmpegPath"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
