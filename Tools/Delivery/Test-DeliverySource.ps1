[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $root
try {
    git diff --exit-code HEAD -- Tools/Delivery
    if($LASTEXITCODE -ne 0){throw 'Delivery source differs from the checked-out revision. No packaging or source repair is attempted.'}
    $scripts=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File)
    if($scripts.Count -eq 0){throw 'Delivery scripts missing.'}
    foreach($script in $scripts){
        $tokens=$null;$errors=$null
        $null=[System.Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$errors)
        $digest=(Get-FileHash -LiteralPath $script.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Host ("Delivery source: "+$script.Name+" sha256="+$digest+" PowerShell="+$PSVersionTable.PSVersion)
        if($errors.Count -gt 0){
            foreach($problem in $errors){Write-Host ($problem.Extent.ToString()+': '+$problem.Message)}
            throw ('Delivery script failed parsing: '+$script.Name)
        }
    }
    Write-Host "DREYNOX_DELIVERY_SOURCE_PREFLIGHT_OK scripts=$($scripts.Count)"
} finally {Pop-Location}
