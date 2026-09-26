[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SourceDirectory,[Parameter(Mandatory=$true)][string]$Destination)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
$root=(Resolve-Path -LiteralPath $SourceDirectory).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
$output=[IO.Path]::GetFullPath($Destination)
if($output.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Payload output must be outside the Player directory.'
}
$pending=New-Object 'System.Collections.Generic.Stack[string]'
$files=New-Object 'System.Collections.Generic.List[string]'
$pending.Push($root)
$entries=0
while($pending.Count -gt 0){
    $directory=$pending.Pop()
    if(([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked source directory rejected.'}
    foreach($path in [IO.Directory]::EnumerateFileSystemEntries($directory)){
        $entries++
        if($entries -gt 100000){throw 'Player tree exceeds packaging entry budget.'}
        $attributes=[IO.File]::GetAttributes($path)
        if(($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Linked Player entry rejected: $path"}
        if(($attributes -band [IO.FileAttributes]::Directory) -ne 0){$pending.Push($path)}else{$files.Add($path)}
    }
}
if($files.Count -eq 0 -or $files.Count -gt 50000){throw 'Empty or oversized Player file list.'}
$files.Sort([StringComparer]::Ordinal)
$stream=New-Object IO.FileStream($output,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
$zip=$null
try{
    $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
    [long]$total=0
    foreach($path in $files){
        # Framework ZipFile.CreateFromDirectory inherits host-dependent backslash
        # defaults. Explicit ZIP names stay forward-slash on every host version.
        $name=$path.Substring($root.Length+1).Replace('\','/')
        $input=New-Object IO.FileStream($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        try{
            $total+=$input.Length
            if($input.Length -gt 2147483648 -or $total -gt 8589934592){throw 'Player exceeds packaging size budget.'}
            $entry=$zip.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
            $target=$entry.Open()
            try{$input.CopyTo($target,131072)}finally{$target.Dispose()}
        }finally{$input.Dispose()}
    }
}finally{
    if($null -ne $zip){$zip.Dispose()}
    $stream.Dispose()
}
Write-Host "DREYNOX_PLAYER_ZIP_OK files=$($files.Count) sourceBytes=$total"
