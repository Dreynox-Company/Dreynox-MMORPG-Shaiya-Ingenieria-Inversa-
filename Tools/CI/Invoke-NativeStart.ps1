param([ValidateSet('build','play')][string]$Task='build')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root
$logs=Join-Path $root 'BuildLogs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
if($Task -eq 'build') {
    $version=((Get-Content 'ProjectSettings\ProjectVersion.txt' | Where-Object {$_ -match '^m_EditorVersion:'}) -split ':',2)[1].Trim()
    $exe=Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
    $log=Join-Path $logs 'native-start-build.log'
    $arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$root+'"'),'-executeMethod','Dreynox.Mmorpg.Editor.Build.NativeStartBuild.RunBatch','-logFile',('"'+$log+'"'),'-quit')
    $timeout=4200000
} else {
    $exe=Join-Path $root 'Builds\WindowsNativeStart\DreynoxMmorpg-NativeStart.exe'
    $output=Join-Path $root 'Artifacts\NativeStart'
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $log=Join-Path $logs 'native-start-runtime.log'
    $arguments=@('-force-d3d11','-screen-width','1024','-screen-height','768','-screen-fullscreen','0','--world-qualification','--world-output',('"'+$output+'"'),'-logFile',('"'+$log+'"'))
    $timeout=240000
}
if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){throw "Executable missing: $exe"}
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
$process=Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
$clock=[Diagnostics.Stopwatch]::StartNew()
try {
    while(-not $process.WaitForExit(30000)) {
        Write-Host "Native $Task still running: $([int]$clock.Elapsed.TotalSeconds) seconds"
        if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Tail 4 | Write-Host}
        if($clock.ElapsedMilliseconds -gt $timeout){throw "Native $Task timed out; inspect stage log."}
    }
    $process.Refresh()
    if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Tail 100 | Write-Host}
    if($process.ExitCode -ne 0){throw "Native $Task failed with code $($process.ExitCode)"}
    if($Task -eq 'play') {
        $result=Join-Path $output 'world-qualification.json'
        if(-not(Test-Path -LiteralPath $result)){throw 'Missing real Player evidence.'}
        $data=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
        if($data.mapId -ne 1 -or -not $data.passed -or $data.npcDialogues -lt 1){throw "Native-start integration failed: $($data.failure)"}
        Write-Host 'DREYNOX_NATIVE_START_PLAY_OK'
    }
} finally {
    if(-not $process.HasExited){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue}
}
