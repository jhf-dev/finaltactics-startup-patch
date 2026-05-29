param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "src\FinalTacticsLauncher.cs"
$outDir = Join-Path $repoRoot "dist"
$out = Join-Path $outDir "FinalTacticsLauncher.exe"
$ddraw = Join-Path $repoRoot "vendor\DDrawCompat\ddraw.dll"
$dsetup = Join-Path $repoRoot "vendor\dsetup_stub\DSETUP.DLL"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $csc)) {
    throw "csc.exe not found: $csc"
}
foreach ($path in @($src, $ddraw, $dsetup)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file not found: $path"
    }
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

& $csc `
    /nologo `
    /target:winexe `
    /platform:x86 `
    /out:$out `
    /resource:"$ddraw,FinalTacticsCompatibility.ddraw.dll" `
    /resource:"$dsetup,FinalTacticsCompatibility.DSETUP.DLL" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    $src

if ($LASTEXITCODE -ne 0) {
    throw "C# build failed with exit code $LASTEXITCODE"
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $out).Hash
Write-Host "Built: $out"
Write-Host "SHA256: $hash"
