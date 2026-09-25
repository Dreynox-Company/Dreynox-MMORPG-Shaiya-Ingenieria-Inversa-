# Read-only discovery. The only implicit non-cache location is explicitly authorized by the user.
param([string]$SelectedRoot = $env:DREYNOX_CORPUS_ROOT)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$aliases = @('DATA', ('DATA_Espa' + [char]0x00f1 + 'ol'), 'DATA_Espanol')
function Child([string]$root, [string]$relative) {
    $current = $root
    foreach ($part in $relative.Replace('\','/').Split('/')) {
        if ([string]::IsNullOrWhiteSpace($part) -or $part -eq '..' -or $part -eq '.' -or $part.Contains(':')) { throw 'Invalid relative path' }
        $matches = @(Get-ChildItem -LiteralPath $current -Force -ErrorAction Stop | Where-Object { $_.Name -ieq $part })
        if ($matches.Count -eq 0) { return $null }
        if ($matches.Count -ne 1) { throw "Ambiguous resource: $relative" }
        if (($matches[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Corpus links are not followed: $relative" }
        $current = $matches[0].FullName
    }
    return $current
}
function Output([string]$name, [string]$value) {
    Write-Host "$name=$value"
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) { "$name=$value" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 -Append }
}
if ([string]::IsNullOrWhiteSpace($SelectedRoot)) {
    if (Test-Path -LiteralPath 'C:\Juegos\Dreynox\DATA' -PathType Container) { $SelectedRoot = 'C:\Juegos\Dreynox\DATA' }
    else {
        $prepared = @(& (Join-Path $PSScriptRoot 'Prepare-CharacterParityCorpus.ps1'))
        if ($prepared.Count -gt 0) { $SelectedRoot = [string]$prepared[-1] }
    }
}
if ([string]::IsNullOrWhiteSpace($SelectedRoot)) {
    Output 'character_available' 'false'; Output 'world_available' 'false'
    Write-Host 'No authorized corpus found. No visual qualification claimed.'
    return
}
$root = [IO.Path]::GetFullPath($SelectedRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Selected corpus does not exist: $root" }
if (((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Selected corpus is a link.' }
$data = $root
if ($aliases -inotcontains (Split-Path -Leaf $root)) {
    $children = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object { $aliases -icontains $_.Name })
    if ($children.Count -ne 1) { throw 'Select exactly one DATA directory explicitly.' }
    $data = $children[0].FullName
}
$anchors = @{
    'excelxml/wingposition.xml' = '8a2c376c898bb025550b5fe34b92a40dbbbb9e39063619cfee4756006908cd03'
    'world/Login.wld' = 'f5508581e39ab01db155432de44fd5eebba240bd49a3d491f4564fbb45f03365'
    'character/human/ani6/humf_019_select.ani' = '8786f0ecb423c2cd4446d862a8f34433da99b59c29720ddd7ef38681048c50d8'
}
foreach ($relative in $anchors.Keys) {
    $path = Child $data $relative
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing content anchor: $relative" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $anchors[$relative]) { throw "Not the expected content anchor: $relative -> $hash" }
    Write-Host "MATCH $relative $hash"
}
$world = $true
$worldAnchors = @{
    'world/0.wld' = '04786e1f660c53f906d64c198f4c297209e9761a07df8735bd5fd53c92c80cbf'
    'world/0.svmap' = '46f4a77ec0189aba8996954c35be9d50eaa6eab150339897d941d3eb706539e2'
}
foreach ($relative in $worldAnchors.Keys) {
    $path = Child $data $relative
    if ([string]::IsNullOrWhiteSpace($path)) { $world = $false; continue }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $worldAnchors[$relative]) { throw "Map0 anchor mismatch: $relative" }
}
$exe = Join-Path (Split-Path -Parent $data) 'game.exe'
$exeVerified = $false
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    $exeVerified = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() -eq '509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d'
    if (-not $exeVerified) { throw 'Present native executable does not match pinned ps0032.' }
}
$env:DREYNOX_CORPUS_ROOT = $data
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_ENV)) {
    "DREYNOX_CORPUS_ROOT=$data" | Out-File -LiteralPath $env:GITHUB_ENV -Encoding utf8 -Append
}
Output 'root' $data
Output 'character_available' 'true'
Output 'world_available' $world.ToString().ToLowerInvariant()
Output 'reference_exe_verified' $exeVerified.ToString().ToLowerInvariant()
Write-Host 'Content anchors verified; this does not certify all resources or native gameplay. Original files are unchanged.'
