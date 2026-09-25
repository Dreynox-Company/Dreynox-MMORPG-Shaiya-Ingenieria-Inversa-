[CmdletBinding()]
param(
    [ValidateSet('All','Test','Prepare','Build','Play','Qualify')][string]$Task='All',
    [string]$DataRoot='C:\Juegos\Dreynox\DATA',
    [string]$UnityEditor='',
    [ValidateRange(10,180)][int]$PreparationMinutes=90,
    [ValidateRange(10,120)][int]$BuildMinutes=60
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location -LiteralPath $project
if($env:OS -ne 'Windows_NT'){throw 'This script requires Windows. It never modifies the original DATA.'}
if($Task -in @('All','Test','Prepare')){
    if(-not(Test-Path -LiteralPath $DataRoot -PathType Container)){throw "DATA folder not found: $DataRoot"}
    $env:DREYNOX_CORPUS_ROOT=(Resolve-Path -LiteralPath $DataRoot).Path
}
if([string]::IsNullOrWhiteSpace($UnityEditor)){
    $line=Get-Content -LiteralPath 'ProjectSettings\ProjectVersion.txt' | Where-Object {$_ -match '^m_EditorVersion:'} | Select-Object -First 1
    $version=($line -split ':',2)[1].Trim()
    $UnityEditor=Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if($Task -in @('All','Test','Prepare','Build') -and -not(Test-Path -LiteralPath $UnityEditor -PathType Leaf)){throw "Pinned Unity Editor not found: $UnityEditor"}
$logs=Join-Path $project 'BuildLogs\StartingWorld'
$artifacts=Join-Path $project 'Artifacts\StartingWorld'
New-Item -ItemType Directory -Force -Path $logs,$artifacts | Out-Null
function Invoke-OwnedProcess([string]$Executable,[string[]]$Arguments,[int]$Minutes,[string]$Log){
    $start=Get-Date
    Write-Host "Starting $Executable; evidence: $Log"
    $process=Start-Process -FilePath $Executable -ArgumentList $Arguments -PassThru
    while(-not $process.WaitForExit(15000)){
        Write-Host "StartingWorld still running: $([int]((Get-Date)-$start).TotalSeconds) seconds"
        if(Test-Path -LiteralPath $Log){Get-Content -LiteralPath $Log -Tail 3 | Write-Host}
        if(((Get-Date)-$start).TotalMinutes -gt $Minutes){
            # Only terminate the process this script started, never another user's Editor.
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "Timed out after $Minutes minutes. Inspect $Log; no success is asserted."
        }
    }
    $process.Refresh()
    if(Test-Path -LiteralPath $Log){Get-Content -LiteralPath $Log -Tail 50 | Write-Host}
    if($process.ExitCode -ne 0){throw "Process exit $($process.ExitCode). See $Log"}
}
function Invoke-Editor([string]$Method,[string]$Name,[int]$Minutes){
    $log=Join-Path $logs "$Name.log"
    Invoke-OwnedProcess $UnityEditor @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"'),'-quit') $Minutes $log
}
if($Task -in @('All','Test')){
    $log=Join-Path $logs 'editmode.log';$xml=Join-Path $artifacts 'editmode.xml'
    if(Test-Path $xml){Remove-Item -LiteralPath $xml}
    Invoke-OwnedProcess $UnityEditor @('-batchmode','-projectPath',('"'+$project+'"'),'-runTests','-testPlatform','EditMode','-testResults',('"'+$xml+'"'),'-logFile',('"'+$log+'"')) 30 $log
    if(-not(Test-Path $xml)){throw 'No NUnit results were produced.'}
    [xml]$results=Get-Content -LiteralPath $xml -Raw
    $run=$results.'test-run'
    Write-Host "NUnit passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped)"
    if([int]$run.failed -gt 0){throw 'EditMode tests failed.'}
    $required=$results.SelectNodes("//test-case[contains(@fullname,'StartingWorldRegressionTests.Canonical')]")
    if($required.Count -lt 2){throw 'Canonical Map1/quest tests did not run.'}
    foreach($test in $required){if($test.result -ne 'Passed'){throw "Canonical corpus gate did not pass: $($test.fullname)"}}
}
if($Task -in @('All','Prepare')){Invoke-Editor 'Dreynox.Mmorpg.Editor.Build.StartingWorldBuild.PrepareBatch' 'prepare-map1' $PreparationMinutes}
if($Task -in @('All','Build')){Invoke-Editor 'Dreynox.Mmorpg.Editor.Build.StartingWorldBuild.BuildPreparedBatch' 'build-map1' $BuildMinutes}
$player=Join-Path $project 'Builds\WindowsStartingWorld\DreynoxMmorpg-Map1.exe'
if($Task -in @('Play','Qualify')){
    if(-not(Test-Path $player)){throw 'Map1 Player is not built. No old Map0 or capsule Player is substituted.'}
    $log=Join-Path $logs 'player.log'
    $arguments=@('-force-d3d11','-screen-width','1021','-screen-height','739','-screen-fullscreen','0','-logFile',('"'+$log+'"'))
    if($Task -eq 'Qualify'){
        $output=Join-Path $artifacts ('run-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Force -Path $output | Out-Null
        $arguments+=@('--starting-world-qualification','--qualification-output',('"'+$output+'"'))
        Invoke-OwnedProcess $player $arguments 6 $log
        $report=Join-Path $output 'starting-world-qualification.json'
        if(-not(Test-Path $report)){throw 'No actual Player report exists.'}
        $evidence=Get-Content $report -Raw | ConvertFrom-Json
        if(-not $evidence.passed){throw "Player qualification failed: $($evidence.failure)"}
        Write-Host "PASS local Map1/NPC/quest loop. Native graphical/combat equivalence is NOT asserted. $report"
    }else{Start-Process -FilePath $player -ArgumentList $arguments | Out-Null}
}
Write-Host 'Original DATA was only read. Source compilation and actual gameplay qualification are separate gates.'
