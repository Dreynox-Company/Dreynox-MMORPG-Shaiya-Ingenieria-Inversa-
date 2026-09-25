param([ValidateSet('build','play')][string]$Task='build')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root
$logs=Join-Path $root 'BuildLogs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
if($Task -eq 'build') {
    $version = ((Get-Content 'ProjectSettings\ProjectVersion.txt' | Where-Object { $_ -match '^m_EditorVersion:' }) -split ':',2)[1].Trim()
    $exe = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
    $log = Join-Path $logs 'playable-map0-build.log'
    $arguments = @('-batchmode','-force-d3d11','-projectPath',('"'+$root+'"'),'-executeMethod','Dreynox.Mmorpg.Editor.Build.PlayableWorldBuild.RunBatch','-logFile',('"'+$log+'"'),'-quit')
    $timeout=2700000
} else {
    $exe=Join-Path $root 'Builds\WindowsPlayableMap0\DreynoxMmorpg-Map0.exe'
    $output=Join-Path $root 'Artifacts\WorldQualification'
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $log=Join-Path $logs 'playable-map0-runtime.log'
    $arguments=@('-force-d3d11','-screen-width','1024','-screen-height','768','-screen-fullscreen','0','--world-qualification','--world-output',('"'+$output+'"'),'-logFile',('"'+$log+'"'))
    $timeout=180000
}
if(-not (Test-Path -LiteralPath $exe -PathType Leaf)) {throw "Executable missing: $exe"}
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
$process=Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
if(-not $process.WaitForExit($timeout)) {Stop-Process -Id $process.Id -Force;throw "World $Task timed out."}
if(Test-Path -LiteralPath $log) {Get-Content -LiteralPath $log -Tail 180 | Write-Host}

$process.Refresh()
if($process.ExitCode -ne 0) {throw "World $Task failed: $($process.ExitCode)"}
if($Task -eq 'play') {
    $result=Join-Path $output 'world-qualification.json'
    if(-not(Test-Path -LiteralPath $result)) {throw 'No runtime world qualification report.'}
    $data=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
    if(-not $data.passed) {throw "World gate failed: $($data.failure)"}
    Write-Host 'DREYNOX_WORLD_QUALIFICATION_OK'
}
