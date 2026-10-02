[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$artifactRoot = (Resolve-Path -LiteralPath $Path).Path
$files = @(Get-ChildItem -LiteralPath $artifactRoot -File -Recurse)
$forbidden = @($files | Where-Object {
    $_.Name -eq 'config.json' -or $_.Extension -in @('.cs', '.csproj', '.sln', '.log', '.pdb', '.onnx.data') -or
    $_.FullName -match '[\\/](node_modules|logs|obj|src)[\\/]'
})
if ($forbidden.Count) { throw "Release contains $($forbidden.Count) forbidden source/config/log files." }
$pattern = '(gsk_[A-Za-z0-9]{32,}|AIzaSy[A-Za-z0-9_-]{33}|sk-(proj-)?[A-Za-z0-9_-]{32,}|dpapi:v1:)'
foreach ($file in $files | Where-Object { $_.Extension -in @('.json', '.js', '.html', '.css', '.md', '.txt') }) {
    if ([IO.File]::ReadAllText($file.FullName) -match $pattern) { throw "Potential credential found in $($file.Name); value withheld." }
}
$size = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host ("Release contents verified: {0} files, {1:N1} MiB; no source, config, logs or known credential patterns." -f $files.Count, ($size / 1MB))
