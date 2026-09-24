# Dreynox MMORPG self-hosted Unity CI entrypoint.
# Unity Personal credentials remain local to the Windows user; this script never reads GitHub license secrets.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("preflight", "test", "build", "character-build", "canonical-build", "visual-compare")]
    [string]$Task
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$ProjectVersionPath = Join-Path $RepoRoot "ProjectSettings\ProjectVersion.txt"

if (-not (Test-Path $ProjectVersionPath)) {
    throw "No se encontró ProjectSettings\ProjectVersion.txt."
}

$versionLine = Get-Content $ProjectVersionPath |
    Where-Object { $_ -match "^m_EditorVersion:\s*(.+)$" } |
    Select-Object -First 1

if (-not $versionLine) {
    throw "No se pudo determinar la versión de Unity del proyecto."
}

$EditorVersion = ([regex]::Match($versionLine, "^m_EditorVersion:\s*(.+)$")).Groups[1].Value.Trim()

$EntitlementPath = Join-Path $env:LOCALAPPDATA "Unity\licenses\UnityEntitlementLicense.xml"
if (-not (Test-Path $EntitlementPath)) {
    throw @"
No se encontró la licencia Unity Personal por entitlement:
$EntitlementPath

El runner debe ejecutarse con el MISMO usuario de Windows que tiene Unity Hub
iniciado y Unity Personal activado.
"@
}

$UnityEditor = $null
if ($env:UNITY_EDITOR_PATH -and (Test-Path $env:UNITY_EDITOR_PATH)) {
    $UnityEditor = (Resolve-Path $env:UNITY_EDITOR_PATH).Path
}
else {
    $candidate = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$EditorVersion\Editor\Unity.exe"
    if (Test-Path $candidate) {
        $UnityEditor = $candidate
    }
}

if (-not $UnityEditor) {
    throw @"
No se encontró Unity Editor $EditorVersion.
Instala exactamente esa versión o define UNITY_EDITOR_PATH con la ruta de Unity.exe.
Versión requerida por el proyecto: $EditorVersion
"@
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Description,
        [int]$TimeoutSeconds = 0
    )

    Write-Host "::group::$Description"

    $process = $null
    $exitCode = $null

    try {
        if ($TimeoutSeconds -le 0) {
            $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -Wait -PassThru -NoNewWindow

            if ($null -eq $process) {
                throw "No se pudo iniciar $Executable."
            }

            $exitCode = $process.ExitCode
        }
        else {
            $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -PassThru -NoNewWindow

            if ($null -eq $process) {
                throw "No se pudo iniciar $Executable."
            }

            $finished = $process.WaitForExit($TimeoutSeconds * 1000)

            if (-not $finished) {
                Write-Error "$Description excedió el timeout de $TimeoutSeconds segundos. Finalizando PID $($process.Id) y procesos hijos."

                $taskkill = Get-Command taskkill.exe -ErrorAction SilentlyContinue

                if ($null -ne $taskkill) {
                    & $taskkill.Source /PID $process.Id /T /F 2>&1 | ForEach-Object { Write-Host $_ }
                }
                elseif (-not $process.HasExited) {
                    $process.Kill()
                }

                try {
                    $process.WaitForExit(10000) | Out-Null
                }
                catch {
                    Write-Warning "No se pudo esperar el cierre del proceso después del timeout."
                }

                throw "$Description excedió el timeout de $TimeoutSeconds segundos."
            }

            $process.Refresh()
            $exitCode = $process.ExitCode
        }
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }

        Write-Host "::endgroup::"
    }

    if ($null -eq $exitCode) {
        throw "$Description terminó sin código de salida disponible."
    }

    if ($exitCode -ne 0) {
        throw "$Description falló con código de salida $exitCode."
    }
}

function Remove-DirectoryWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [int]$Attempts = 5
    )

    if (-not (Test-Path $Path)) {
        return
    }

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -ge $Attempts) {
                throw
            }

            Write-Warning "No se pudo limpiar $Path (intento $attempt/$Attempts). Reintentando..."
            Start-Sleep -Seconds 2
        }
    }
}

Set-Location $RepoRoot

Write-Host "Unity project: $RepoRoot"
Write-Host "Unity Editor requerido: $EditorVersion"
Write-Host "Unity Editor detectado: $UnityEditor"
Write-Host "Unity Personal entitlement: OK"

switch ($Task) {
    "preflight" {
        $UnityCli = Get-Command unity -ErrorAction SilentlyContinue
        if (-not $UnityCli) {
            throw @"
Unity CLI no está disponible en PATH.
Instálalo en Windows con:
  winget install Unity.CLI
Después abre una nueva terminal y verifica:
  unity --version
"@
        }

        Invoke-Checked -Executable $UnityCli.Source -Arguments @("--version") -Description "Unity CLI version"

        $licenseStatusSucceeded = $false
        $licenseStatusOutput = @()
        $licenseStatusExitCode = 0

        for ($licenseAttempt = 1;
             $licenseAttempt -le 3;
             $licenseAttempt++)
        {
            Write-Host "::group::Unity license status (attempt $licenseAttempt/3)"

            try {
                $licenseStatusOutput =
                    @(
                        & $UnityCli.Source license status 2>&1
                    )

                $licenseStatusExitCode =
                    $LASTEXITCODE

                $licenseStatusOutput |
                    ForEach-Object {
                        Write-Host $_
                    }
            }
            finally {
                Write-Host "::endgroup::"
            }

            if ($licenseStatusExitCode -eq 0) {
                $licenseStatusSucceeded = $true
                break
            }

            if ($licenseAttempt -lt 3) {
                Write-Warning "Unity CLI license status devolvió $licenseStatusExitCode; reintentando. El entitlement local ya fue validado en disco."
                Start-Sleep -Seconds (2 * $licenseAttempt)
            }
        }

        if (-not $licenseStatusSucceeded) {
            Write-Warning "Unity CLI license status siguió fallando con código $licenseStatusExitCode, pero existe un UnityEntitlementLicense.xml local válido para este usuario. Se continúa hasta unity doctor y, finalmente, Editor tests/build como gates definitivos."
        }

        # unity doctor --ci also checks cloud reachability. A transient failure of
        # services.api.unity.com must not invalidate a cached named-user
        # entitlement that was already confirmed above. Retry first; then allow
        # exactly one degraded condition: NETWORK_UNREACHABLE and no other failed
        # doctor checks. Any other doctor failure remains fatal.
        $doctorSucceeded = $false
        $doctorOutput = @()
        $doctorExitCode = 0

        for ($attempt = 1; $attempt -le 3; $attempt++) {
            Write-Host "::group::Unity CI doctor (attempt $attempt/3)"
            try {
                $doctorOutput = @(& $UnityCli.Source doctor --ci 2>&1)
                $doctorExitCode = $LASTEXITCODE
                $doctorOutput | ForEach-Object { Write-Host $_ }
            }
            finally {
                Write-Host "::endgroup::"
            }

            if ($doctorExitCode -eq 0) {
                $doctorSucceeded = $true
                break
            }

            if ($attempt -lt 3) {
                Write-Warning "Unity CI doctor devolvió $doctorExitCode; reintentando por posible fallo transitorio de red."
                Start-Sleep -Seconds (3 * $attempt)
            }
        }

        if (-not $doctorSucceeded) {
            $failedChecks = @(
                $doctorOutput |
                    Where-Object { [string]$_ -match "check\.[^\s]+\s+fail\s+" }
            )

            $nonNetworkFailures = @(
                $failedChecks |
                    Where-Object {
                        [string]$_ -notmatch "check\.network\s+fail\s+NETWORK_UNREACHABLE"
                    }
            )

            $licensePassed = [bool](
                $doctorOutput |
                    Where-Object { [string]$_ -match "check\.license\s+pass\s+LICENSE_OK" } |
                    Select-Object -First 1
            )

            if ($failedChecks.Count -gt 0 -and
                $nonNetworkFailures.Count -eq 0 -and
                $licensePassed) {
                Write-Warning "Unity doctor no pudo alcanzar services.api.unity.com, pero el entitlement local está válido. Se continúa en modo offline/degradado; Unity tests/build decidirán si falta algún recurso de red."
            }
            else {
                throw "Unity CI doctor falló con código $doctorExitCode y contiene fallos distintos de NETWORK_UNREACHABLE."
            }
        }

        break
    }

    "test" {
        $ResultsDir = Join-Path $RepoRoot "TestResults"
        New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

        $TestResult = Join-Path $ResultsDir "editmode-results.xml"
        $TestLog = Join-Path $ResultsDir "editmode-editor.log"
        $PackageCache = Join-Path $RepoRoot "Library\PackageCache"

        # Self-hosted Windows runners can retain a transient Package Manager lock
        # after a cancelled Unity process. Rebuild PackageCache from a clean state.
        Remove-DirectoryWithRetry -Path $PackageCache

        for ($attempt = 1; $attempt -le 2; $attempt++) {
            Remove-Item -LiteralPath $TestResult -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $TestLog -Force -ErrorAction SilentlyContinue

            try {
                Invoke-Checked -Executable $UnityEditor -Arguments @(
                    "-batchmode",
                    "-nographics",
                    "-projectPath", $RepoRoot,
                    "-runTests",
                    "-testPlatform", "editmode",
                    "-testResults", $TestResult,
                    "-logFile", $TestLog
                ) -Description "Unity EditMode tests" -TimeoutSeconds 1200
            }
            catch {
                $packageRenameLock = $false
                if (Test-Path $TestLog) {
                    $packageRenameLock = [bool](Select-String -Path $TestLog -Pattern "EPERM: operation not permitted, rename" -SimpleMatch -Quiet)
                }

                if ($attempt -lt 2 -and $packageRenameLock) {
                    Write-Warning "Unity Package Manager encontró un lock EPERM; limpiando PackageCache y reintentando una vez."
                    Remove-DirectoryWithRetry -Path $PackageCache
                    Start-Sleep -Seconds 2
                    continue
                }

                throw
            }

            break
        }

        if (-not (Test-Path $TestResult)) {
            throw "Unity terminó sin generar $TestResult."
        }

        Write-Host "Unity EditMode tests: OK"
        break
    }

    "visual-compare" {
        $BuildLogDir = Join-Path $RepoRoot "BuildLogs"
        New-Item -ItemType Directory -Force -Path $BuildLogDir | Out-Null
        $CompareLog = Join-Path $BuildLogDir "visual-parity-editor.log"

        if ([string]::IsNullOrWhiteSpace($env:DREYNOX_NATIVE_REFERENCE_ROOT)) {
            throw "DREYNOX_NATIVE_REFERENCE_ROOT no está configurado."
        }

        if ([string]::IsNullOrWhiteSpace($env:DREYNOX_UNITY_CAPTURE_ROOT)) {
            throw "DREYNOX_UNITY_CAPTURE_ROOT no está configurado."
        }

        Invoke-Checked -Executable $UnityEditor -Arguments @(
            "-batchmode",
            "-nographics",
            "-projectPath", $RepoRoot,
            "-executeMethod", "Dreynox.Mmorpg.Editor.Parity.NativeVisualParityBatch.RunFromEnvironment",
            "-logFile", $CompareLog,
            "-quit"
        ) -Description "Unity native-vs-canonical visual parity compare"

        $ReportPath = Join-Path $RepoRoot "Artifacts\Parity\Reports\native-vs-unity.json"

        if (-not (Test-Path $ReportPath)) {
            throw "Visual parity compare terminó sin generar $ReportPath."
        }

        Write-Host "Visual parity report: OK"
        Get-Content $ReportPath | ForEach-Object { Write-Host $_ }
        break
    }

    "character-build" {
        $BuildLogDir = Join-Path $RepoRoot "BuildLogs"
        New-Item -ItemType Directory -Force -Path $BuildLogDir | Out-Null
        $BuildLog = Join-Path $BuildLogDir "windows-character-parity-editor.log"

        if ([string]::IsNullOrWhiteSpace($env:DREYNOX_CORPUS_ROOT)) {
            throw "DREYNOX_CORPUS_ROOT no está configurado en el runner."
        }

        if (-not (Test-Path $env:DREYNOX_CORPUS_ROOT)) {
            throw "DREYNOX_CORPUS_ROOT no existe: $env:DREYNOX_CORPUS_ROOT"
        }

        Invoke-Checked -Executable $UnityEditor -Arguments @(
            "-batchmode",
            "-nographics",
            "-projectPath", $RepoRoot,
            "-executeMethod", "Dreynox.Mmorpg.Editor.Build.DreynoxWindowsBuild.BuildCharacterParityBatch",
            "-logFile", $BuildLog,
            "-quit"
        ) -Description "Unity Windows x64 Character parity build"

        $ExePath = Join-Path $RepoRoot "Builds\WindowsCharacterParity\DreynoxMmorpg-CharacterParity.exe"
        $ManifestPath = Join-Path $RepoRoot "Builds\WindowsCharacterParity\dreynox-build-manifest.txt"

        if (-not (Test-Path $ExePath)) {
            throw "El Character parity build terminó sin generar $ExePath."
        }

        if (-not (Test-Path $ManifestPath)) {
            throw "El Character parity build terminó sin generar $ManifestPath."
        }

        Write-Host "Character parity Windows x64: OK"
        Get-Content $ManifestPath | ForEach-Object { Write-Host $_ }
        break
    }

    "canonical-build" {
        $BuildLogDir = Join-Path $RepoRoot "BuildLogs"
        New-Item -ItemType Directory -Force -Path $BuildLogDir | Out-Null
        $BuildLog = Join-Path $BuildLogDir "windows-canonical-parity-editor.log"

        if ([string]::IsNullOrWhiteSpace($env:DREYNOX_CORPUS_ROOT)) {
            throw "DREYNOX_CORPUS_ROOT no está configurado en el runner."
        }

        if (-not (Test-Path $env:DREYNOX_CORPUS_ROOT)) {
            throw "DREYNOX_CORPUS_ROOT no existe: $env:DREYNOX_CORPUS_ROOT"
        }

        Invoke-Checked -Executable $UnityEditor -Arguments @(
            "-batchmode",
            "-nographics",
            "-projectPath", $RepoRoot,
            "-executeMethod", "Dreynox.Mmorpg.Editor.Build.DreynoxWindowsBuild.BuildCanonicalParityBatch",
            "-logFile", $BuildLog,
            "-quit"
        ) -Description "Unity Windows x64 canonical parity build"

        $ExePath = Join-Path $RepoRoot "Builds\WindowsCanonicalParity\DreynoxMmorpg-CanonicalParity.exe"
        $ManifestPath = Join-Path $RepoRoot "Builds\WindowsCanonicalParity\dreynox-build-manifest.txt"

        if (-not (Test-Path $ExePath)) {
            throw "El canonical build terminó sin generar $ExePath."
        }

        if (-not (Test-Path $ManifestPath)) {
            throw "El canonical build terminó sin generar $ManifestPath."
        }

        Write-Host "Canonical parity Windows x64: OK"
        Get-Content $ManifestPath | ForEach-Object { Write-Host $_ }
        break
    }

    "build" {
        $BuildLogDir = Join-Path $RepoRoot "BuildLogs"
        New-Item -ItemType Directory -Force -Path $BuildLogDir | Out-Null
        $BuildLog = Join-Path $BuildLogDir "windows-parity-editor.log"

        Invoke-Checked -Executable $UnityEditor -Arguments @(
            "-batchmode",
            "-nographics",
            "-projectPath", $RepoRoot,
            "-executeMethod", "Dreynox.Mmorpg.Editor.Build.DreynoxWindowsBuild.BuildParityBatch",
            "-logFile", $BuildLog,
            "-quit"
        ) -Description "Unity Windows x64 parity build"

        $ExePath = Join-Path $RepoRoot "Builds\WindowsParity\DreynoxMmorpg-Parity.exe"
        $ManifestPath = Join-Path $RepoRoot "Builds\WindowsParity\dreynox-build-manifest.txt"

        if (-not (Test-Path $ExePath)) {
            throw "El build terminó sin generar $ExePath."
        }

        if (-not (Test-Path $ManifestPath)) {
            throw "El build terminó sin generar $ManifestPath."
        }

        Write-Host "Build Windows x64: OK"
        Get-Content $ManifestPath | ForEach-Object { Write-Host $_ }
        break
    }
}
