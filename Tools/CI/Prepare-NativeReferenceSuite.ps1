param(
    [string]$PackagePath = "",
    [string]$CacheRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedPackageSha =
    "983b8de803f69dac176231156acfae8da20c28c0d3004a909acfac896558e848"

if ([string]::IsNullOrWhiteSpace($CacheRoot)) {
    $CacheRoot =
        Join-Path $env:LOCALAPPDATA "DreynoxMmorpg\native-visual-reference"
}

function Get-DreynoxSearchRoots {
    $roots = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        foreach ($name in @("Desktop", "Downloads", "Documents")) {
            $candidate = Join-Path $env:USERPROFILE $name
            if (Test-Path -LiteralPath $candidate -PathType Container) {
                $roots.Add([System.IO.Path]::GetFullPath($candidate))
            }
        }
    }

    $systemDrive = [Environment]::GetEnvironmentVariable("SystemDrive")
    if ([string]::IsNullOrWhiteSpace($systemDrive)) {
        $systemDrive = "C:"
    }

    $usersRoot = Join-Path $systemDrive "Users"
    if (Test-Path -LiteralPath $usersRoot -PathType Container) {
        Get-ChildItem -LiteralPath $usersRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($name in @("Desktop", "Downloads", "Documents")) {
                $candidate = Join-Path $_.FullName $name
                if (Test-Path -LiteralPath $candidate -PathType Container) {
                    $roots.Add([System.IO.Path]::GetFullPath($candidate))
                }
            }
        }
    }

    return @($roots | Select-Object -Unique)
}

function Find-NativePackage {
    if (-not [string]::IsNullOrWhiteSpace($env:DREYNOX_NATIVE_REFERENCE_ZIP) -and
        (Test-Path -LiteralPath $env:DREYNOX_NATIVE_REFERENCE_ZIP -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:DREYNOX_NATIVE_REFERENCE_ZIP)
    }

    $roots = @(Get-DreynoxSearchRoots)

    $names = @(
        "Shaiya_Offline_Nativo.zip",
        "Shaiya Offline Nativo.zip"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        foreach ($name in $names) {
            $direct = Join-Path $root $name

            if (Test-Path -LiteralPath $direct -PathType Leaf) {
                return $direct
            }
        }

        $levelOne = @(
            Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue
        )

        foreach ($directory in $levelOne) {
            foreach ($name in $names) {
                $candidate =
                    Join-Path $directory.FullName $name

                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return $candidate
                }
            }

            $levelTwo = @(
                Get-ChildItem -LiteralPath $directory.FullName -Directory -ErrorAction SilentlyContinue
            )

            foreach ($nestedDirectory in $levelTwo) {
                foreach ($name in $names) {
                    $nested =
                        Join-Path $nestedDirectory.FullName $name

                    if (Test-Path -LiteralPath $nested -PathType Leaf) {
                        return $nested
                    }
                }
            }
        }
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Find-NativePackage
}

if ([string]::IsNullOrWhiteSpace($PackagePath) -or
    -not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    Write-Host "Shaiya_Offline_Nativo.zip no fue encontrado en rutas locales acotadas."
    return
}

$PackagePath =
    [System.IO.Path]::GetFullPath(
        $PackagePath)

$packageSha =
    (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256)
        .Hash
        .ToLowerInvariant()

if ($packageSha -ne $ExpectedPackageSha) {
    throw "El paquete offline nativo no coincide con el baseline verificado. Actual=$packageSha Esperado=$ExpectedPackageSha"
}

$editorOut =
    Join-Path $CacheRoot "05-character-editor.png"

$selectOut =
    Join-Path $CacheRoot "08-character-selection.png"

$marker =
    Join-Path $CacheRoot ".source-sha256"

if ((Test-Path -LiteralPath $marker -PathType Leaf) -and
    (Test-Path -LiteralPath $editorOut -PathType Leaf) -and
    (Test-Path -LiteralPath $selectOut -PathType Leaf)) {
    $cachedSha =
        (Get-Content -LiteralPath $marker -Raw).Trim()

    if ($cachedSha -eq $packageSha) {
        Write-Host "Native visual reference cache reutilizado: $CacheRoot"
        Write-Output ([System.IO.Path]::GetFullPath($CacheRoot))
        exit 0
    }
}

if (Test-Path -LiteralPath $CacheRoot) {
    Remove-Item -LiteralPath $CacheRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive =
    [System.IO.Compression.ZipFile]::OpenRead(
        $PackagePath)

$wanted = @{
    "pruebas/cliente_nativo_windows/05-character-editor.png" = $editorOut
    "pruebas/cliente_nativo_windows/08-character-selection.png" = $selectOut
}

$found = @{}

try {
    foreach ($entry in $archive.Entries) {
        $name =
            $entry.FullName
                .Replace("\", "/")

        foreach ($key in $wanted.Keys) {
            if (-not [string]::Equals(
                    $name,
                    $key,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
                $entry,
                $wanted[$key],
                $true)

            $found[$key] =
                $true

            break
        }
    }
}
finally {
    $archive.Dispose()
}

foreach ($key in $wanted.Keys) {
    if (-not $found.ContainsKey($key) -or
        -not (Test-Path -LiteralPath $wanted[$key] -PathType Leaf)) {
        throw "El paquete offline nativo no contiene la referencia requerida: $key"
    }
}

Set-Content -LiteralPath $marker -Value $packageSha -Encoding ascii

Write-Host "Native visual reference suite preparada: $CacheRoot"
Write-Output ([System.IO.Path]::GetFullPath($CacheRoot))
