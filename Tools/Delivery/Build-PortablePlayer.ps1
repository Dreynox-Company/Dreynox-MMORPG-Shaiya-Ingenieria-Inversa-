[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [Parameter(Mandatory=$true)][string]$QualificationReport,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidatePattern('^[a-f0-9]{40}$')][string]$PlayerCommit=$env:GITHUB_SHA,
    [ValidatePattern('^[0-9]+$')][string]$QualificationRunId=$env:GITHUB_RUN_ID
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT'){throw 'The portable executable packager requires Windows.'}
$player=(Resolve-Path -LiteralPath $PlayerDirectory).Path
$report=Get-Content -LiteralPath $QualificationReport -Raw | ConvertFrom-Json
if(-not $report.passed -or $report.mapId -ne 1 -or $report.kills -ne 5 -or
   -not $report.questAccepted -or -not $report.questDelivered -or -not $report.originalEthanOpened -or
   $report.attackAnimations -lt 1){throw 'Map1 actual Player qualification is incomplete. No distributable generated.'}
foreach($name in @('DreynoxMmorpg-Map1.exe','UnityPlayer.dll','SHA256SUMS.txt','source-and-content.json')){
    if(-not(Test-Path -LiteralPath (Join-Path $player $name) -PathType Leaf)){throw "Complete Player missing: $name"}
}
if(-not(Test-Path -LiteralPath (Join-Path $player 'DreynoxMmorpg-Map1_Data') -PathType Container)){throw 'Unity content folder is missing.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stage=Join-Path $output ('build-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$zip=Join-Path $stage 'player.zip'
& (Join-Path $PSScriptRoot 'New-PlayerPayload.ps1') -SourceDirectory $player -Destination $zip
$digest=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$identity=Join-Path $stage 'PayloadIdentity.cs'
"namespace Dreynox.Delivery { internal static class PayloadIdentity { public const string Sha256 = `"$digest`"; } }" |
    Set-Content -LiteralPath $identity -Encoding UTF8
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not(Test-Path -LiteralPath $csc)){throw 'Installed .NET Framework C# compiler is unavailable.'}
$executable=Join-Path $output 'Dreynox-Mmorpg-Map1-Play.exe'
if(Test-Path -LiteralPath $executable){throw 'Refusing to replace an existing distributable.'}
$core=Join-Path $PSScriptRoot 'VerifiedPlayerArchive.cs'
$metadata=Join-Path $PSScriptRoot 'PlayerZipMetadata.cs'
$launcher=Join-Path $PSScriptRoot 'PortablePlayerLauncher.cs'
& $csc /nologo /target:winexe /platform:x64 /langversion:5 /optimize+ "/out:$executable" "/resource:$zip,Dreynox.PlayerPayload" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $identity $core $metadata $launcher
if($LASTEXITCODE -ne 0){throw 'Portable executable compilation failed.'}
& (Join-Path $PSScriptRoot 'Invoke-VerifiedPlayerCheck.ps1') -Executable $executable -ExpectedExitCode 0
@{
    schema=2;sourceCommit=$PlayerCommit;run=$QualificationRunId;payloadSha256=$digest;
    packagerCommit=$env:GITHUB_SHA;packagerRun=$env:GITHUB_RUN_ID;
    executable=[IO.Path]::GetFileName($executable);sha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant();
    bytes=(Get-Item -LiteralPath $executable).Length;playerQualification=$report.passed;extractionVerified=$true;
    scope='Qualified local Map1 quest loop; not complete native MMO parity';
    deployment='Per-user cache, original preconverted Unity Player; no admin, no original DATA writes, no external downloads'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'portable-manifest.json') -Encoding UTF8
Write-Host "DREYNOX_PORTABLE_PLAYER_OK $executable"
