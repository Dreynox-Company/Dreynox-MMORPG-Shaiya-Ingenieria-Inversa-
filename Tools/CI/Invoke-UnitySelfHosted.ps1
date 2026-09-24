# Dreynox MMORPG self-hosted Unity CI entrypoint.
# Unity Personal credentials remain local to the Windows user; this script never reads GitHub license secrets.

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("preflight", "test", "build")]
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
        [string]$Description
    )

    Write-Host "::group::$Description"

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false

    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo

    try {
        if (-not $process.Start()) {
            throw "No se pudo iniciar $Executable."
        }

        $process.WaitForExit()
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
        Write-Host "::endgroup::"
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
        Invoke-Checked -Executable $UnityCli.Source -Arguments @("license", "status") -Description "Unity license status"

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
                ) -Description "Unity EditMode tests"
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
