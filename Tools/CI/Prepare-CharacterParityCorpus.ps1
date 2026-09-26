param(
    [string]$ShZipPath = "",
    [string]$CacheRoot = "",
    [switch]$FunctionsOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedZipSha = "78136f45ee45d3b0c6e03b829412189cab4d32ae5670a8d8b65892154673cfd5"
$ExpectedGameSha = "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d"

if ([string]::IsNullOrWhiteSpace($CacheRoot)) {
    $baseCache = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $CacheRoot = Join-Path $baseCache "DreynoxMmorpg\ps0032-character-parity-v2"
}

function Test-DreynoxPath {
    param([AllowEmptyString()][string]$LiteralPath, [ValidateSet("Leaf", "Container", "Any")][string]$PathType = "Any")
    if ([string]::IsNullOrWhiteSpace($LiteralPath)) { return $false }
    try { return [bool](Test-Path -LiteralPath $LiteralPath -PathType $PathType -ErrorAction Stop) }
    catch [System.UnauthorizedAccessException] { return $false }
    catch [System.IO.IOException] { return $false }
    catch [System.Management.Automation.ItemNotFoundException] { return $false }
}

function Test-PreparedCache {
    param([string]$Root)
    $manifest = Join-Path $Root ".resource-manifest.json"
    try {
        if (-not (Test-DreynoxPath -LiteralPath $manifest -PathType Leaf)) { return $false }
        $entries = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
        if ($entries.Count -lt 200) { return $false }
        foreach ($entry in $entries) {
            $path = Join-Path $Root $entry.path
            if (-not (Test-DreynoxPath -LiteralPath $path -PathType Leaf)) { return $false }
            $info = Get-Item -LiteralPath $path -ErrorAction Stop
            if ($info.Length -ne $entry.bytes) { return $false }
        }
        return $true
    } catch { return $false }
}

function Get-DreynoxSearchRoots {
    $roots = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        foreach ($name in @("Desktop", "Downloads", "Documents")) {
            $candidate = Join-Path $env:USERPROFILE $name
            if (Test-DreynoxPath -LiteralPath $candidate -PathType Container) {
                $roots.Add([System.IO.Path]::GetFullPath($candidate))
            }
        }
    }

    # Never enumerate other Windows accounts. Explicit paths can be supplied
    # through DREYNOX_SH_ZIP, otherwise search only this account's known folders.
    foreach ($kind in @([Environment+SpecialFolder]::DesktopDirectory, [Environment+SpecialFolder]::MyDocuments)) {
        $candidate = [Environment]::GetFolderPath($kind)
        if (Test-DreynoxPath -LiteralPath $candidate -PathType Container) { $roots.Add($candidate) }
    }

    return @($roots | Select-Object -Unique)
}

function Find-ShZip {
    if (-not [string]::IsNullOrWhiteSpace($env:DREYNOX_SH_ZIP) -and
        (Test-DreynoxPath -LiteralPath $env:DREYNOX_SH_ZIP -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:DREYNOX_SH_ZIP)
    }

    $cachedSource = Join-Path $env:LOCALAPPDATA "DreynoxMmorpg\ps0032-source\Sh.zip"
    if (Test-DreynoxPath -LiteralPath $cachedSource -PathType Leaf) { return $cachedSource }
    $roots = @(Get-DreynoxSearchRoots)

    foreach ($root in $roots) {
        if (-not (Test-DreynoxPath -LiteralPath $root -PathType Container)) {
            continue
        }

        $direct = Join-Path $root "Sh.zip"
        if (Test-DreynoxPath -LiteralPath $direct -PathType Leaf) {
            return $direct
        }

        $levelOne = @(
            Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue
        )

        foreach ($directory in $levelOne) {
            $candidate = Join-Path $directory.FullName "Sh.zip"

            if (Test-DreynoxPath -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }

            $levelTwo = @(
                Get-ChildItem -LiteralPath $directory.FullName -Directory -ErrorAction SilentlyContinue
            )

            foreach ($nestedDirectory in $levelTwo) {
                $nested = Join-Path $nestedDirectory.FullName "Sh.zip"

                if (Test-DreynoxPath -LiteralPath $nested -PathType Leaf) {
                    return $nested
                }
            }
        }
    }

    return $null
}

function Find-ShMultipart {
    $roots = @(Get-DreynoxSearchRoots)

    $directories = New-Object System.Collections.Generic.List[string]

    foreach ($root in $roots) {
        if (-not (Test-DreynoxPath -LiteralPath $root -PathType Container)) {
            continue
        }

        $directories.Add($root)

        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $directories.Add($_.FullName)

            Get-ChildItem -LiteralPath $_.FullName -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                $directories.Add($_.FullName)
            }
        }
    }

    foreach ($directory in ($directories | Select-Object -Unique)) {
        $part1 = Join-Path $directory "Sh.part1.rar"

        if (-not (Test-DreynoxPath -LiteralPath $part1 -PathType Leaf)) {
            continue
        }

        $complete = $true

        for ($part = 2; $part -le 7; $part++) {
            $candidate = Join-Path $directory ("Sh.part{0}.rar" -f $part)

            if (-not (Test-DreynoxPath -LiteralPath $candidate -PathType Leaf)) {
                $complete = $false
                break
            }
        }

        if ($complete) {
            return [System.IO.Path]::GetFullPath($part1)
        }
    }

    return $null
}

function Resolve-7Zip {
    if (-not [string]::IsNullOrWhiteSpace($env:DREYNOX_7ZIP) -and
        (Test-DreynoxPath -LiteralPath $env:DREYNOX_7ZIP -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:DREYNOX_7ZIP)
    }

    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles "7-Zip\7z.exe"))
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $candidates.Add((Join-Path $programFilesX86 "7-Zip\7z.exe"))
    }

    foreach ($candidate in $candidates) {
        if (Test-DreynoxPath -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    $command = Get-Command 7z.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $command = Get-Command 7zz.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    return $null
}

function Prepare-ShZipFromMultipart {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Part1Path
    )

    $sourceCache = Join-Path $env:LOCALAPPDATA "DreynoxMmorpg\ps0032-source"
    $cachedZip = Join-Path $sourceCache "Sh.zip"

    if (Test-DreynoxPath -LiteralPath $cachedZip -PathType Leaf) {
        $cachedSha = (Get-FileHash -LiteralPath $cachedZip -Algorithm SHA256).Hash.ToLowerInvariant()

        if ($cachedSha -eq $ExpectedZipSha) {
            Write-Host "Sh.zip canónico reutilizado desde caché: $cachedZip"
            return $cachedZip
        }

        Remove-Item -LiteralPath $cachedZip -Force
    }

    $sevenZip = Resolve-7Zip

    if ([string]::IsNullOrWhiteSpace($sevenZip)) {
        Write-Host "Multipart Sh encontrado, pero 7-Zip no está disponible en el runner."
        return $null
    }

    New-Item -ItemType Directory -Force -Path $sourceCache | Out-Null

    Write-Host "Extrayendo Sh.zip desde multipart: $Part1Path"
    Write-Host "7-Zip: $sevenZip"

    $arguments = @(
        "x",
        "-y",
        "-bd",
        "-bso1",
        "-bsp0",
        ('-o"{0}"' -f $sourceCache),
        ('"{0}"' -f $Part1Path),
        "Sh.zip"
    )

    $process = Start-Process -FilePath $sevenZip -ArgumentList $arguments -Wait -PassThru -NoNewWindow

    if ($null -eq $process -or $process.ExitCode -ne 0) {
        $code = if ($null -eq $process) { "null" } else { [string]$process.ExitCode }
        throw "7-Zip no pudo extraer Sh.zip desde multipart. ExitCode=$code"
    }

    if (-not (Test-DreynoxPath -LiteralPath $cachedZip -PathType Leaf)) {
        throw "7-Zip finalizó sin generar Sh.zip: $cachedZip"
    }

    $sha = (Get-FileHash -LiteralPath $cachedZip -Algorithm SHA256).Hash.ToLowerInvariant()

    if ($sha -ne $ExpectedZipSha) {
        Remove-Item -LiteralPath $cachedZip -Force -ErrorAction SilentlyContinue
        throw "Sh.zip extraído del multipart no coincide con el corpus ps0032 canónico. Actual=$sha Esperado=$ExpectedZipSha"
    }

    Write-Host "Sh.zip canónico reconstruido desde multipart: $cachedZip"
    return $cachedZip
}

if ($FunctionsOnly) { return }

if ([string]::IsNullOrWhiteSpace($ShZipPath)) {
    $ShZipPath = Find-ShZip
}

if ([string]::IsNullOrWhiteSpace($ShZipPath) -or
    -not (Test-DreynoxPath -LiteralPath $ShZipPath -PathType Leaf)) {
    $part1 = Find-ShMultipart

    if (-not [string]::IsNullOrWhiteSpace($part1)) {
        $ShZipPath = Prepare-ShZipFromMultipart -Part1Path $part1
    }
}

if ([string]::IsNullOrWhiteSpace($ShZipPath) -or
    -not (Test-DreynoxPath -LiteralPath $ShZipPath -PathType Leaf)) {
    Write-Host "Sh.zip ni el multipart Sh.part1.rar..Sh.part7.rar fueron encontrados/preparados en rutas locales acotadas."
    return
}

$ShZipPath = [System.IO.Path]::GetFullPath($ShZipPath)
$zipSha = (Get-FileHash -LiteralPath $ShZipPath -Algorithm SHA256).Hash.ToLowerInvariant()

if ($zipSha -ne $ExpectedZipSha) {
    throw "Sh.zip no coincide con el corpus ps0032 canónico. Actual=$zipSha Esperado=$ExpectedZipSha"
}

$markerPath = Join-Path $CacheRoot ".source-sha256"
$cachedGame = Join-Path $CacheRoot "game.exe"
$cachedData = Join-Path $CacheRoot "DATA_Español"

if ((Test-DreynoxPath -LiteralPath $markerPath -PathType Leaf) -and
    (Test-DreynoxPath -LiteralPath $cachedGame -PathType Leaf) -and
    (Test-DreynoxPath -LiteralPath $cachedData -PathType Container)) {
    $marker = (Get-Content -LiteralPath $markerPath -Raw).Trim()

    if ($marker -eq $zipSha) {
        $gameSha = (Get-FileHash -LiteralPath $cachedGame -Algorithm SHA256).Hash.ToLowerInvariant()

        if ($gameSha -eq $ExpectedGameSha -and (Test-PreparedCache -Root $CacheRoot)) {
            Write-Host "Character parity corpus cache reutilizado: $CacheRoot"
            Write-Output ([System.IO.Path]::GetFullPath($CacheRoot))
            exit 0
        }
    }
}

# Never recursively delete a caller-supplied directory. Only reuse a managed cache.
$ownerMarker = Join-Path $CacheRoot ".dreynox-managed-cache"
if ((Test-DreynoxPath -LiteralPath $CacheRoot -PathType Container) -and
    -not (Test-DreynoxPath -LiteralPath $ownerMarker -PathType Leaf) -and
    @(Get-ChildItem -LiteralPath $CacheRoot -Force -ErrorAction Stop).Count -gt 0) {
    throw "La carpeta de caché contiene datos ajenos; no se borrará. Indique una carpeta nueva."
}
New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null
Set-Content -LiteralPath $ownerMarker -Value "dreynox-ps0032-v2" -Encoding ascii
$resourceManifest = New-Object System.Collections.Generic.List[object]

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
    "DATA_Español/effect/dds/vetical033.dds",
    "DATA_Español/effect/dds/v02.dds",
    "DATA_Español/effect/dds/blucore001.dds"
)

$previewRigs = @(
    @{ Folder = "human"; Prefix = "humf"; TexturePrefix = "hum" },
    @{ Folder = "human"; Prefix = "humm"; TexturePrefix = "hum" },
    @{ Folder = "human"; Prefix = "huwf"; TexturePrefix = "huw" },
    @{ Folder = "human"; Prefix = "huwm"; TexturePrefix = "huw" },

    @{ Folder = "elf"; Prefix = "elmr"; TexturePrefix = "elm" },
    @{ Folder = "elf"; Prefix = "elmm"; TexturePrefix = "elm" },
    @{ Folder = "elf"; Prefix = "elwr"; TexturePrefix = "elw" },
    @{ Folder = "elf"; Prefix = "elwm"; TexturePrefix = "elw" },

    @{ Folder = "deatheater"; Prefix = "demf"; TexturePrefix = "dem" },
    @{ Folder = "deatheater"; Prefix = "demr"; TexturePrefix = "dem" },
    @{ Folder = "deatheater"; Prefix = "dewf"; TexturePrefix = "dew" },
    @{ Folder = "deatheater"; Prefix = "dewr"; TexturePrefix = "dew" },

    @{ Folder = "vile"; Prefix = "vimr"; TexturePrefix = "vim" },
    @{ Folder = "vile"; Prefix = "vimm"; TexturePrefix = "vim" },
    @{ Folder = "vile"; Prefix = "viwr"; TexturePrefix = "viw" },
    @{ Folder = "vile"; Prefix = "viwm"; TexturePrefix = "viw" }
)

foreach ($rig in $previewRigs) {
    foreach ($part in @("upper", "lower", "hand", "foot")) {
        $exactPaths += "DATA_Español/character/$($rig.Folder)/3dc/co_$($rig.Prefix)_$($part)003.3dc"
        $exactPaths += "DATA_Español/character/$($rig.Folder)/dds/co_$($rig.Prefix)_$($part)003.dds"
    }

    for ($variant = 1; $variant -le 5; $variant++) {
        $index = $variant.ToString("D3")
        $exactPaths += "DATA_Español/character/$($rig.Folder)/3dc/$($rig.Prefix)_face$index.3dc"
        $exactPaths += "DATA_Español/character/$($rig.Folder)/3dc/$($rig.Prefix)_hair$index.3dc"
        $exactPaths += "DATA_Español/character/$($rig.Folder)/dds/$($rig.TexturePrefix)_face$index.dds"
        $exactPaths += "DATA_Español/character/$($rig.Folder)/dds/$($rig.TexturePrefix)_hair$index.dds"
    }

    $exactPaths += "DATA_Español/character/$($rig.Folder)/ani6/$($rig.Prefix)_019_select.ani"
}

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
        if ([System.IO.Path]::IsPathRooted($relative) -or
            $name.Split('/') -contains '..' -or $name.Contains(':')) {
            throw "Ruta de archivo ZIP no permitida: $name"
        }
        $destination = [System.IO.Path]::GetFullPath((Join-Path $CacheRoot $relative))
        $boundary = [System.IO.Path]::GetFullPath($CacheRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        if (-not $destination.StartsWith($boundary, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "El recurso ZIP sale de la carpeta de caché: $name"
        }
        $directory = Split-Path -Parent $destination

        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
        }

        [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
            $entry,
            $destination,
            $true
        )

        $resourceManifest.Add(@{path = $relative; bytes = $entry.Length})
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
    "DATA_Español\interface\CharacterMake\basicinfo_bg.tga",
    "DATA_Español\interface\CharacterSelect\selectbg.tga"
)

foreach ($rig in $previewRigs) {
    foreach ($part in @("upper", "lower", "hand", "foot")) {
        $required += "DATA_Español\character\$($rig.Folder)\3dc\co_$($rig.Prefix)_$($part)003.3dc"
        $required += "DATA_Español\character\$($rig.Folder)\dds\co_$($rig.Prefix)_$($part)003.dds"
    }

    for ($variant = 1; $variant -le 5; $variant++) {
        $index = $variant.ToString("D3")
        $required += "DATA_Español\character\$($rig.Folder)\3dc\$($rig.Prefix)_face$index.3dc"
        $required += "DATA_Español\character\$($rig.Folder)\3dc\$($rig.Prefix)_hair$index.3dc"
        $required += "DATA_Español\character\$($rig.Folder)\dds\$($rig.TexturePrefix)_face$index.dds"
        $required += "DATA_Español\character\$($rig.Folder)\dds\$($rig.TexturePrefix)_hair$index.dds"
    }

    $required += "DATA_Español\character\$($rig.Folder)\ani6\$($rig.Prefix)_019_select.ani"
}

foreach ($relative in ($required | Select-Object -Unique)) {
    $path = Join-Path $CacheRoot $relative
    if (-not (Test-DreynoxPath -LiteralPath $path -PathType Leaf)) {
        throw "El corpus mínimo no contiene un recurso requerido: $relative"
    }
}

$resourceManifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $CacheRoot ".resource-manifest.json") -Encoding utf8
Set-Content -LiteralPath $markerPath -Value $zipSha -Encoding ascii

Write-Host ("Character parity corpus preparado: {0} archivos · {1:N1} MB" -f $extracted, ($totalBytes / 1MB))
Write-Output ([System.IO.Path]::GetFullPath($CacheRoot))
