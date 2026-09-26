[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [int]$ExpectedExitCode=0,
    [ValidateRange(1,600)][int]$TimeoutSeconds=300
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$info=New-Object Diagnostics.ProcessStartInfo
$info.FileName=(Resolve-Path -LiteralPath $Executable).Path
$info.Arguments='--verify-only'
$info.WorkingDirectory=[IO.Path]::GetDirectoryName($info.FileName)
$info.UseShellExecute=$false
$info.CreateNoWindow=$true
$info.RedirectStandardError=$true
$info.RedirectStandardOutput=$true
$process=New-Object Diagnostics.Process
$process.StartInfo=$info
try{
    if(-not $process.Start()){throw 'Windows did not start the verification process.'}
    # Read both pipes concurrently; do not deadlock on a full error pipe.
    $stdout=$process.StandardOutput.ReadToEndAsync()
    $stderr=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit($TimeoutSeconds*1000)){
        $process.Kill();$process.WaitForExit();throw 'Packaged Player verification timed out.'
    }
    $exitCode=$process.ExitCode
    Write-Host $stdout.Result
    Write-Host $stderr.Result
    if($exitCode -ne $ExpectedExitCode){throw "Verification exit $exitCode; expected $ExpectedExitCode."}
}finally{$process.Dispose()}
