param(
    [string] $Version = "v0.1.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repoRoot "dist"
$staging = Join-Path $dist "staging"
$exe = Join-Path $dist "FinalTacticsLauncher.exe"
$zipName = "finaltactics-startup-patch-$Version.zip"
$zip = Join-Path $dist $zipName
$checksums = Join-Path $dist "SHA256SUMS.txt"

& (Join-Path $PSScriptRoot "build.ps1")

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Force -Recurse
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Copy-Item -LiteralPath $exe -Destination (Join-Path $staging "FinalTacticsLauncher.exe") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "release\README.txt") -Destination (Join-Path $staging "README.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "release\THIRD_PARTY_NOTICES.txt") -Destination (Join-Path $staging "THIRD_PARTY_NOTICES.txt") -Force

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip)
Remove-Item -LiteralPath $staging -Force -Recurse

$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash

@(
    "$zipHash  dist/$zipName"
) | Set-Content -LiteralPath $checksums -Encoding ASCII

Remove-Item -LiteralPath $exe -Force

Write-Host "Packaged: $zip"
Write-Host "SHA256: $zipHash"
