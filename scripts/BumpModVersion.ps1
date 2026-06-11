param(
    [Parameter(Mandatory = $true)]
    [string]$InfoPath,

    [ValidateSet("Major", "Minor", "Patch")]
    [string]$Kind = "Minor"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $InfoPath)) {
    throw "Info.json not found: $InfoPath"
}

$json = Get-Content -LiteralPath $InfoPath -Raw | ConvertFrom-Json
$version = [string]$json.Version
if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    throw "Unsupported mod version format '$version'. Expected semantic version like 1.2.4."
}

$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$patch = [int]$Matches[3]

switch ($Kind) {
    "Major" {
        $major += 1
        $minor = 0
        $patch = 0
    }
    "Minor" {
        $minor += 1
        $patch = 0
    }
    "Patch" {
        $patch += 1
    }
}

$nextVersion = "$major.$minor.$patch"
$json.Version = $nextVersion
$json | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $InfoPath -Encoding UTF8
Write-Host "Mod version bumped: $version -> $nextVersion"
