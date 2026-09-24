# Unity Personal CI — Windows self-hosted runner

Este repositorio usa Unity Personal mediante el sistema moderno de **named-user entitlement**.

## Qué NO se usa

No se necesita ninguno de estos secretos para Unity Personal:

- `UNITY_SERIAL`
- `UNITY_LICENSE` con un archivo `.ulf`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

La licencia permanece en el perfil local de Windows mediante:

`%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml`

## Requisitos del equipo Windows

1. Inicia sesión en Unity Hub con la cuenta que tiene Unity Personal.
2. Comprueba que Unity Personal aparece activa en Hub.
3. Verifica que exista:

   `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml`

4. Instala la versión de Editor fijada por el proyecto. Actualmente:

   `6000.0.64f1`

5. Instala Unity CLI:

   `winget install Unity.CLI`

6. Abre una nueva PowerShell y comprueba:

   `unity --version`

   `unity license status`

   `unity doctor --ci`

## Registrar GitHub Actions Runner

Como este repositorio es público, el runner se debe registrar únicamente en un equipo controlado por Dreynox y **no debe ejecutar pull requests externos**.

En GitHub:

`Settings → Actions → Runners → New self-hosted runner → Windows → x64`

Sigue los comandos que GitHub genera para ese repositorio. GitHub recomienda `C:\actions-runner` como directorio en Windows.

### Importante para Unity Personal

Ejecuta inicialmente el runner con:

`.\run.cmd`

desde el **mismo usuario de Windows** que tiene Unity Hub iniciado y el entitlement activo. Esto evita que el proceso termine ejecutándose bajo otra cuenta que no tenga acceso a `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml`.

## Validación local antes de activar el CI

Desde la raíz del repositorio:

`pwsh ./Tools/CI/Invoke-UnitySelfHosted.ps1 -Task preflight`

Después:

`pwsh ./Tools/CI/Invoke-UnitySelfHosted.ps1 -Task test`

Y finalmente:

`pwsh ./Tools/CI/Invoke-UnitySelfHosted.ps1 -Task build`

El build esperado queda en:

`Builds\WindowsParity\DreynoxMmorpg-Parity.exe`

## Seguridad

El job `unity-windows` no se ejecuta en eventos `pull_request`. Los PR siguen ejecutando las validaciones deterministas en runners hospedados por GitHub, pero el PC Windows con la licencia solo acepta ejecuciones por `push` en ramas configuradas o `workflow_dispatch`.

Cuando el nuevo flujo haya producido un build correcto, los secretos antiguos de Unity pueden eliminarse del repositorio si todavía existen.
