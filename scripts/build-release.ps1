[CmdletBinding()]
param(
    [string]$Version = '2.0.2-alpha.1',
    [switch]$SkipInstall
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$') { throw 'Use a semantic version such as 2.0.2-alpha.1.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = Join-Path $repoRoot 'release'
$destination = Join-Path $releaseRoot "DesktopTranslator-$Version-win-x64"
if (Test-Path -LiteralPath $destination) { throw "Release already exists: $destination. Choose a new version or move it before rebuilding." }
$staging = Join-Path $releaseRoot ('.staging-' + [guid]::NewGuid().ToString('N'))
function Run([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Program failed with exit code $LASTEXITCODE" }
}
Push-Location $repoRoot
try {
    $frontend = Join-Path $repoRoot 'KikitanTranslator.Photino/UserInterface'
    if (!$SkipInstall) { Run npm @('ci', '--prefix', $frontend, '--no-audit', '--no-fund') }
    Run npm @('run', 'build', '--prefix', $frontend)
    Run dotnet @('build', 'kikitan-translator.sln', '--nologo', '-m:1', '-p:UseSharedCompilation=false')
    Run dotnet @('run', '--project', 'tests/DesktopTranslator.Tests', '--no-build', '--no-restore')
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Run dotnet @('publish', 'KikitanTranslator.Photino/KikitanTranslator.Photino.csproj', '-c', 'Release', '-r', 'win-x64',
        '--self-contained', 'true', '-o', $staging, '-m:1', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=None', '-p:SkipVelopack=true', '-p:SkipNpmBuild=true', '-p:UseSharedCompilation=false', "-p:Version=$Version")
    foreach ($required in @('DesktopTranslator.exe', 'KikitanTranslator.Subtitles.exe', 'wwwroot/index.html', 'wwwroot/silero_vad.onnx')) {
        if (!(Test-Path -LiteralPath (Join-Path $staging $required))) { throw "Missing release component: $required" }
    }
    Run (Join-Path $staging 'KikitanTranslator.Subtitles.exe') @('--self-test')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $staging
    $packageDocs = Join-Path $staging 'docs'
    New-Item -ItemType Directory -Path $packageDocs -Force | Out-Null
    foreach ($document in @('ENGINEERING_AUDIT.md', 'MANUAL_TESTS.md')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot "docs/$document") -Destination $packageDocs
    }
    & (Join-Path $PSScriptRoot 'verify-release.ps1') -Path $staging
    $hashes = @('DesktopTranslator.exe', 'KikitanTranslator.Subtitles.exe', 'wwwroot/silero_vad.onnx', 'wwwroot/index.html') | ForEach-Object {
        $hash = Get-FileHash -LiteralPath (Join-Path $staging $_) -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $_"
    }
    $hashes | Set-Content -LiteralPath (Join-Path $staging 'SHA256SUMS.txt') -Encoding utf8
    Move-Item -LiteralPath $staging -Destination $destination
    Write-Host "Release ready: $destination"
} finally { Pop-Location }
