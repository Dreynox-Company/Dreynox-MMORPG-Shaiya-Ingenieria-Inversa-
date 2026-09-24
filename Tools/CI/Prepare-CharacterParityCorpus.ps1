param(
    [string]$ShZipPath = "",
    [string]$CacheRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedZipSha = "78136f45ee45d3b0c6e03b829412189cab4d32ae5670a8d8b65892154673cfd5"
$ExpectedGameSha = "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d"

if ([string]::IsNullOrWhiteSpace($CacheRoot)) {
    $CacheRoot = Join-Path $env:LOCALAPPDATA "DreynoxMmorpg\ps0032-character-parity"
}

function Find-ShZip {
    if (-not [string]::IsNullOrWhiteSpace($env:DREYNOX_SH_ZIP) -and
        (Test-Path -LiteralPath $env:DREYNOX_SH_ZIP -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:DREYNOX_SH_ZIP)
    }

    $roots = @(
        (Join-Path $env:USERPROFILE "Desktop"),
        (Join-Path $env:USERPROFILE "Downloads"),
        (Join-Path $env:USERPROFILE "Documents")
    )

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        $direct = Join-Path $root "Sh.zip"
        if (Test-Path -LiteralPath $direct -PathType Leaf) {
            return $direct
        }

        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $candidate = Join-Path $_.FullName "Sh.zip"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }

            Get-ChildItem -LiteralPath $_.FullName -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                $nested = Join-Path $_.FullName "Sh.zip"
                if (Test-Path -LiteralPath $nested -PathType Leaf) {
                    return $nested
                }
            }
        }
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($ShZipPath)) {
    $ShZipPath = Find-ShZip
}

if ([string]::IsNullOrWhiteSpace($ShZipPath) -or
    -not (Test-Path -LiteralPath $ShZipPath -PathType Leaf)) {
    Write-Host "Sh.zip no fue encontrado en rutas locales acotadas."
    exit 2
}

$ShZipPath = [System.IO.Path]::GetFullPath($ShZipPath)
$zipSha = (Get-FileHash -LiteralPath $ShZipPath -Algorithm SHA256).Hash.ToLowerInvariant()

if ($zipSha -ne $ExpectedZipSha) {
    throw "Sh.zip no coincide con el corpus ps0032 canónico. Actual=$zipSha Esperado=$ExpectedZipSha"
}

$markerPath = Join-Path $CacheRoot ".source-sha256"
$cachedGame = Join-Path $CacheRoot "game.exe"
$cachedData = Join-Path $CacheRoot "DATA_Español"

if ((Test-Path -LiteralPath $markerPath -PathType Leaf) -and
    (Test-Path -LiteralPath $cachedGame -PathType Leaf) -and
    (Test-Path -LiteralPath $cachedData -PathType Container)) {
    $marker = (Get-Content -LiteralPath $markerPath -Raw).Trim()

    if ($marker -eq $zipSha) {
        $gameSha = (Get-FileHash -LiteralPath $cachedGame -Algorithm SHA256).Hash.ToLowerInvariant()

        if ($gameSha -eq $ExpectedGameSha) {
            Write-Host "Character parity corpus cache reutilizado: $CacheRoot"
            Write-Output ([System.IO.Path]::GetFullPath($CacheRoot))
            exit 0
        }
    }
}

if (Test-Path -LiteralPath $CacheRoot) {
    Remove-Item -LiteralPath $CacheRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null

$exactPaths = @(
    "game.exe",
    "DATA_Español/excelxml/wingposition.xml",
    "DATA_Español/character/wing/wing.mon",
    "DATA_Español/interface/Login/BG.tga",
    "DATA_Español/world/Login.wld",
    "DATA_Español/world/dungeon/dun_login.dg",

    "DATA_Español/character/human/3dc/co_humm_upper003.3dc",
    "DATA_Español/character/human/3dc/co_humm_lower003.3dc",
    "DATA_Español/character/human/3dc/co_humm_hand003.3dc",
    "DATA_Español/character/human/3dc/co_humm_foot003.3dc",
    "DATA_Español/character/human/3dc/humm_face001.3dc",
    "DATA_Español/character/human/3dc/humm_hair001.3dc",

    "DATA_Español/character/human/dds/co_humm_upper003.dds",
    "DATA_Español/character/human/dds/co_humm_lower003.dds",
    "DATA_Español/character/human/dds/co_humm_hand003.dds",
    "DATA_Español/character/human/dds/co_humm_foot003.dds",
    "DATA_Español/character/human/dds/hum_face001.dds",
    "DATA_Español/character/human/dds/hum_hair001.dds",

    "DATA_Español/entity/shape/dragon.smod",
    "DATA_Español/entity/shape/starlighting.smod",
    "DATA_Español/entity/grass/login_a.smod",

    "DATA_Español/effect/login.eft",
    "DATA_Español/effect/3de/vetical025.3de",
    "DATA_Español/effect/3de/login_v001.3de",
    "DATA_Español/effect/3de/login_v002.3de",
    "DATA_Español/effect/3de/shangd00.3de",
    "DATA_Español/effect/3de/shangd01.3de",
    "DATA_Español/effect/3de/shangd02.3de",

    "DATA_Español/effect/dds/vl01.dds",
    "DATA_Español/effect/dds/star003.dds",
    "DATA_Español/effect/dds/dun_login01.dds",
    "DATA_Español/effect/dds/dun_login02.dds",
    "DATA_Español/effect/dds/fire_010000.dds",
    "DATA_Español/effect/dds/fire003.dds",
    "DATA_Español/effect/dds/lamp003.dds",
    "DATA_Español/effect/dds/sball00.dds",
    "DATA_Español/effect/dds/yellcore001.dds",
    "DATA_Español/effect/dds/shangd00.dds",
    "DATA_Español/effect/dds/blueball00.dds",
    "DATA_Español/effect/dds/vetical031.dds",
    "DATA_Español/effect/dds/vetical033.dds"
)

$dgTextures = @(
    "L_Dun1_Top_005","L_Dun1_Wall_034","L_Dun1_Under_011","L_R1_DUN2_019",
    "L_A1_DUN2_005","DUN_LOGIN01","L_Dun1_Under_026","DUN_LOGIN03",
    "L_R1_DUN2_023","L_R1_DUN2_018","V_B1_Magicvill14","L_R1_DUN2_028",
    "DUN_LOGIN02","DUN_LOGIN04","L_R1_DUN2_025","L_Dun1_Etc_010",
    "L_R1_DUN2_010","L_R1_DUN2_036","D_Water01","L_A1_DUN2_021",
    "DUN_LOGIN05","L_R1_DUN2_012","L_Dun1_Etc_024","L_Dun1_Object_013",
    "L_Dun1_Object_004","L_Dun1_Object_017","L_R1_DUN2_026","DUN_LOGIN06",
    "L_Dun1_Object_002","L_Dun1_Object_001","L_Dun1_Object_003"
)

$staticTextures = @(
    "D_DragonStone02_1","D_DragonStone02_2","D_DragonStone02_3","D_DragonStone02_4",
    "F_B1_mentalhospital03a","F_R1_Starlighting04","F_R1_Starlighting03",
    "F_B1_mentalhospital09","F_R1_Starlighting02","DUN_LOGIN08"
)

foreach ($name in ($dgTextures + $staticTextures)) {
    $exactPaths += "DATA_Español/entity/texture/$name.dds"
}

$prefixes = @(
    "DATA_Español/interface/CharacterMake/",
    "DATA_Español/interface/CharacterSelect/",
    "DATA_Español/character/human/ani6/",
    "DATA_Español/world/dungeon/dun_login/"
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::OpenRead($ShZipPath)
$extracted = 0
$totalBytes = [int64]0

try {
    foreach ($entry in $archive.Entries) {
        if ([string]::IsNullOrWhiteSpace($entry.Name)) {
            continue
        }

        $name = $entry.FullName.Replace("\", "/")
        $extract = $exactPaths -contains $name

        if (-not $extract) {
            foreach ($prefix in $prefixes) {
                if ($name.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $extract = $true
                    break
                }
            }
        }

        if (-not $extract) {
            continue
        }

        $relative = $name.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
        $destination = Join-Path $CacheRoot $relative
        $directory = Split-Path -Parent $destination

        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
        }

        [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
            $entry,
            $destination,
            $true
        )

        $extracted++
        $totalBytes += $entry.Length
    }
}
finally {
    $archive.Dispose()
}

$gameSha = (Get-FileHash -LiteralPath $cachedGame -Algorithm SHA256).Hash.ToLowerInvariant()

if ($gameSha -ne $ExpectedGameSha) {
    throw "El corpus mínimo extraído no contiene el game.exe ps0032 canónico."
}

$required = @(
    "DATA_Español\world\Login.wld",
    "DATA_Español\world\dungeon\dun_login.dg",
    "DATA_Español\character\human\3dc\co_humm_upper003.3dc",
    "DATA_Español\character\human\ani6\humm_019_select.ani",
    "DATA_Español\interface\CharacterMake\basicinfo_bg.tga",
    "DATA_Español\interface\CharacterSelect\selectbg.tga"
)

foreach ($relative in $required) {
    $path = Join-Path $CacheRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "El corpus mínimo no contiene un recurso requerido: $relative"
    }
}

Set-Content -LiteralPath $markerPath -Value $zipSha -Encoding ascii

Write-Host ("Character parity corpus preparado: {0} archivos · {1:N1} MB" -f $extracted, ($totalBytes / 1MB))
Write-Output ([System.IO.Path]::GetFullPath($CacheRoot))
