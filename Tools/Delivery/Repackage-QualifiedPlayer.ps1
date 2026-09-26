[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-f0-9]{40}$')][string]$PlayerCommit,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9]+$')][string]$QualificationRunId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$player=(Resolve-Path -LiteralPath $PlayerDirectory).Path
$evidence=(Resolve-Path -LiteralPath $EvidenceDirectory).Path
function Read-Single([string]$name){
    $files=@(Get-ChildItem -LiteralPath $evidence -Recurse -File -Filter $name)
    if($files.Count -ne 1){throw "Expected one immutable evidence file: $name"}
    return (Get-Content -LiteralPath $files[0].FullName -Raw | ConvertFrom-Json)
}
$ci=Read-Single 'ci-result.json'
if($ci.commit -ne $PlayerCommit -or [string]$ci.run -ne $QualificationRunId){throw 'Player source/evidence revision mismatch.'}
foreach($stage in @('tests','prepare','build','play')){
    if($ci.$stage -ne 'success'){throw "Player was not qualified at stage $stage"}
}
$qualified=Read-Single 'starting-world-qualification.json'
if(-not $qualified.passed -or -not $qualified.terrainCollisionVerified -or
    -not $qualified.defaultAppearanceVerified -or -not $qualified.nativeRadarVerified){throw 'Current content/physics/appearance qualification is absent.'}
$prepared=Read-Single 'prepared.json'
$packaged=Get-Content -LiteralPath (Join-Path $player 'source-and-content.json') -Raw | ConvertFrom-Json
foreach($field in @('schema','mapId','sourceHash','dependencyHash','scene')){
    if($prepared.$field -ne $packaged.$field){throw "Player and prepared scene evidence differ: $field"}
}
if($packaged.mapId -ne 1 -or [string]::IsNullOrWhiteSpace($packaged.sourceHash)){throw 'Original Map1 source identity is absent.'}
$reports=@(Get-ChildItem -LiteralPath $evidence -Recurse -File -Filter 'starting-world-qualification.json')
$report=$reports[0].FullName
& (Join-Path $PSScriptRoot 'Build-PortablePlayer.ps1') -PlayerDirectory $player -QualificationReport $report -OutputDirectory $OutputDirectory -PlayerCommit $PlayerCommit -QualificationRunId $QualificationRunId
# Repackaging does not edit the Unity Player or give it features from newer source.
$manifestPath=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'portable-manifest.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest | Add-Member -NotePropertyName packagingMode -NotePropertyValue 'Repackage immutable qualified Player without rebuilding or editing gameplay'
$manifest | Add-Member -NotePropertyName playerSourceHash -NotePropertyValue $packaged.sourceHash
$manifest | Add-Member -NotePropertyName gameplayEvidenceSha256 -NotePropertyValue (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "DREYNOX_REPACKAGE_QUALIFIED_OK playerCommit=$PlayerCommit evidenceRun=$QualificationRunId"
