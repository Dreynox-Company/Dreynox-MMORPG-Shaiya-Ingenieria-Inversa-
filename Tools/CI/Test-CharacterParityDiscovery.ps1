$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot 'Prepare-CharacterParityCorpus.ps1'
$tokens = $null; $errors = $null
[System.Management.Automation.Language.Parser]::ParseFile($target, [ref]$tokens, [ref]$errors) | Out-Null
if ($errors.Count -gt 0) { throw ($errors | Out-String) }
$temporary = Join-Path ([System.IO.Path]::GetTempPath()) ('dreynox-discovery-' + [Guid]::NewGuid().ToString('N'))
. $target -CacheRoot $temporary -FunctionsOnly
if (Test-DreynoxPath -LiteralPath '' -PathType Container) { throw 'Empty path was accepted' }
if (Test-DreynoxPath -LiteralPath $temporary -PathType Container) { throw 'Missing path was accepted' }
# Reproduce the access-denied exception, independently of machine permissions.
function Test-Path { param($LiteralPath,$PathType,$ErrorAction) throw [System.UnauthorizedAccessException]::new('denied fixture') }
try {
    if (Test-DreynoxPath -LiteralPath 'denied-fixture' -PathType Container) { throw 'Denied path was accepted' }
} finally { Remove-Item Function:\Test-Path }
$source = Get-Content -LiteralPath $target -Raw
if ($source.Contains('$usersRoot')) { throw 'Cross-account discovery must not return' }
if ($source.Contains('Remove-Item -LiteralPath $CacheRoot -Recurse')) { throw 'Unsafe recursive cache deletion returned' }
Write-Host 'CORPUS DISCOVERY TESTS OK'
