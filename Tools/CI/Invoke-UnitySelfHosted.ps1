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
        Invoke-Checked -Executable $UnityCli.Source -Arguments @("doctor", "--ci") -Description "Unity CI doctor"
        break
    }

    "test" {
        $ResultsDir = Join-Path $RepoRoot "TestResults"
        New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

        $TestResult = Join-Path $ResultsDir "editmode-results.xml"
        $TestLog = Join-Path $ResultsDir "editmode-editor.log"

        Invoke-Checked -Executable $UnityEditor -Arguments @(
            "-batchmode",
            "-nographics",
            "-projectPath", $RepoRoot,
            "-runTests",
            "-testPlatform", "EditMode",
            "-testResults", $TestResult,
            "-logFile", $TestLog,
            "-quit"
        ) -Description "Unity EditMode tests"

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
