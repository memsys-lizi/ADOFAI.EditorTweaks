param(
    [Parameter(Mandatory = $true)]
    [string]$ToolsDirectory
)

$ErrorActionPreference = "Stop"

$ffmpegPackageName = "ffmpeg-8.1.1-essentials_build"
$url = "https://www.gyan.dev/ffmpeg/builds/packages/$ffmpegPackageName.zip"
$expectedSha256 = "6f58ce889f59c311410f7d2b18895b33c03456463486f3b1ebc93d97a0f54541"
$sourceCommit = "239f2c733d"
$sourceUrl = "https://github.com/FFmpeg/FFmpeg/tree/$sourceCommit"
$sourceArchiveUrl = "https://github.com/FFmpeg/FFmpeg/archive/$sourceCommit.zip"
$buildPageUrl = "https://www.gyan.dev/ffmpeg/builds/"
$licenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html"

$ffmpegPath = Join-Path $ToolsDirectory "ffmpeg.exe"

function Write-FfmpegDistributionFiles {
    New-Item -ItemType Directory -Force -Path $ToolsDirectory | Out-Null

    $versionOutput = "ffmpeg.exe was not available when this file was generated."
    if (Test-Path -LiteralPath $ffmpegPath) {
        try {
            $versionOutput = (& $ffmpegPath -hide_banner -version 2>&1) -join [Environment]::NewLine
        }
        catch {
            $versionOutput = "Failed to execute ffmpeg.exe -version: $($_.Exception.Message)"
        }
    }

    $buildInfoPath = Join-Path $ToolsDirectory "FFmpeg-BUILD.txt"
    $sourceInfoPath = Join-Path $ToolsDirectory "FFmpeg-SOURCE.txt"
    $noticePath = Join-Path $ToolsDirectory "FFmpeg-NOTICE.txt"

    Set-Content -LiteralPath $buildInfoPath -Encoding UTF8 -Value @"
FFmpeg binary distributed with ADOFAI.EditorTweaks
=================================================

Bundled file:
  Tools/ffmpeg.exe

Upstream binary package:
  $ffmpegPackageName.zip

Download URL:
  $url

Expected SHA-256:
  $expectedSha256

Build page:
  $buildPageUrl

License:
  GNU General Public License version 3 (GPLv3)
  $licenseUrl

The exact executable reports:

$versionOutput
"@

    Set-Content -LiteralPath $sourceInfoPath -Encoding UTF8 -Value @"
FFmpeg Corresponding Source Information
======================================

This release includes an unmodified FFmpeg executable distributed under the
GNU General Public License version 3 (GPLv3). When you publish a release that
contains Tools/ffmpeg.exe, you must also provide the machine-readable
Corresponding Source for that FFmpeg executable.

The FFmpeg source revision identified by the upstream build page is:
  $sourceCommit

FFmpeg source URL:
  $sourceUrl

FFmpeg source archive URL:
  $sourceArchiveUrl

Important:
  The bundled gyan.dev essentials build is a static GPLv3 build and includes
  external libraries such as libx264. A compliant source offer should cover
  FFmpeg and the linked GPL/LGPL libraries that form the distributed binary,
  plus the build scripts/configuration needed to rebuild it.

Recommended release practice:
  1. Keep this file, FFmpeg-BUILD.txt, FFmpeg-NOTICE.txt, and the GPLv3 text
     in the same release package as Tools/ffmpeg.exe.
  2. Attach a source archive for the bundled FFmpeg binary to the same public
     release page, or provide a clear no-charge source download next to the
     object-code download.
  3. Do not add license terms that forbid reverse engineering, modification,
     or redistribution of FFmpeg.
"@

    Set-Content -LiteralPath $noticePath -Encoding UTF8 -Value @"
Third-party notice: FFmpeg
==========================

ADOFAI.EditorTweaks distributes FFmpeg as a separate executable:
  Tools/ffmpeg.exe

FFmpeg is copyright (c) the FFmpeg developers and other contributors.
The bundled Windows build is distributed under the GNU General Public License
version 3 (GPLv3). A copy of GPLv3 is included in:
  ThirdParty/FFmpeg/GPL-3.0.txt

ADOFAI.EditorTweaks invokes FFmpeg as an external command-line program. FFmpeg
is not owned by the ADOFAI.EditorTweaks author.

Project:
  https://ffmpeg.org/

Windows build source:
  $buildPageUrl
"@
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ADOFAI.EditorTweaks.ffmpeg." + [System.Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "ffmpeg.zip"

if (Test-Path -LiteralPath $ffmpegPath) {
    Write-Host "FFmpeg already exists: $ffmpegPath"
    Write-FfmpegDistributionFiles
    exit 0
}

try {
    New-Item -ItemType Directory -Force -Path $ToolsDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    Write-Host "Downloading FFmpeg from $url"
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
    
    $actualSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "FFmpeg archive SHA-256 mismatch. Expected $expectedSha256 but got $actualSha256."
    }

    Write-Host "Extracting FFmpeg"
    Expand-Archive -Path $zipPath -DestinationPath $tempRoot -Force

    $downloadedFfmpeg = Get-ChildItem -Path $tempRoot -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
    if ($null -eq $downloadedFfmpeg) {
        throw "ffmpeg.exe was not found in the downloaded archive."
    }

    Copy-Item -LiteralPath $downloadedFfmpeg.FullName -Destination $ffmpegPath -Force
    Write-Host "FFmpeg installed to $ffmpegPath"
    Write-FfmpegDistributionFiles
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
