# Dreynox Mmorpg — Unity Migration v0.1.0

Proyecto base oficial para la migración del trabajo de ingeniería inversa de Shaiya a **Unity 6 + C#**.

## Objetivo

Dreynox Mmorpg es un MMORPG 3D multiplataforma (PC y móviles) con identidad propia. El cliente Flutter y Shaiya Studio existentes **no forman parte de este repositorio y no deben modificarse**. Este repositorio reutiliza conocimiento técnico previamente confirmado, pero reimplementa todo en C# y APIs nativas de Unity.

## Rama sugerida

`work/unity-migration-phase1-v0.1.0`

## Qué incluye esta versión

- Proyecto Unity reconocible por Unity Hub (`Assets`, `Packages`, `ProjectSettings`).
- Base URP 17 para Unity 6.
- Inspector de carpeta DATA y archivos binarios.
- Parser del header SPK v3 observado en el corpus real.
- Descifrado AES-GCM 128/192/256 en C# puro para el índice SPK cuando se proporciona un perfil local autorizado.
- Perfil privado separado de Git para no publicar secretos del corpus.
- Importador de mallas guiado por perfiles de layout binario, útil para `.svmap`, `.3DC`, `.3DO` y formatos que sigan siendo investigados.
- Clasificación de recursos Shaiya por extensión.
- Pipeline de salida nativo a `Assets/DreynoxMMORPG/Imported`.
- Sistema de attachments/equipment para armas, alas y monturas.
- Estado de vuelo desacoplado de equipar alas y transición rápida de combate.
- Locomoción con CharacterController y grounding.
- Colisión de cámara por SphereCast.
- Catálogo semántico de clips de animación.
- Streaming aditivo de chunks de mundo.
- Transporte TCP/UDP asíncrono usando Tasks y ConcurrentQueue; Unity solo consume paquetes en Main Thread.
- Control de calidad adaptativo PC/móvil.
- Herramienta para GPU Instancing y flags de Occlusion Culling.
- Generador de escena Bootstrap.
- Pruebas Editor para SPK y AES-GCM.
- Validación de integridad del repositorio y workflow de GitHub sin necesidad de licencia Unity.

## Primer arranque

1. Descomprime o sube todo el contenido a la raíz del repositorio GitHub.
2. En Unity Hub: **Add > Add project from disk** y selecciona la carpeta raíz.
3. Abre con Unity 6. Unity puede solicitar actualizar el proyecto a tu patch instalado; acepta si es una versión Unity 6 compatible.
4. Espera a que Package Manager resuelva URP.
5. Ejecuta `Dreynox MMORPG > Project > Create/Refresh Bootstrap Scene`.
6. Abre `Assets/DreynoxMMORPG/Scenes/Bootstrap.unity`.
7. Para ingeniería inversa: `Dreynox MMORPG > Reverse Engineering > DATA / SPK Inspector`.

## URP

La dependencia URP está declarada en `Packages/manifest.json`. Unity 6 fija sus paquetes gráficos a versiones compatibles con el Editor. Si Unity actualiza el package lock al abrir con un patch más reciente, ese cambio es normal.

Para crear el Pipeline Asset nativo desde tu versión de Unity:

`Assets > Create > Rendering > URP Asset (with Universal Renderer)`

Así evitamos versionar un YAML de pipeline generado por un patch concreto y conservamos compatibilidad entre Unity 6.x.

## Datos originales

El repositorio no incluye `DATA`, `data.spk`, `game.exe`, claves, assets comerciales ni binarios propietarios. Usa una copia local de archivos sobre los que tengas permiso de investigación. La herramienta trabaja en solo lectura sobre el origen y escribe únicamente en el árbol `Assets/DreynoxMMORPG/Imported`.

## Estado SPK v3 migrado

La estructura observada y ya soportada por el parser incluye:

- Header de 128 bytes.
- Versión en offset `0x04`.
- Offset de índice en `0x08` (`UInt64 LE`).
- Tamaño almacenado del índice en `0x10`.
- Tamaño decodificado en `0x18`.
- Conteo de registros en `0x20`.
- Tamaño de bloque en `0x24`.
- Nonce de 12 bytes en `0x28`.
- Tag GCM de 16 bytes en `0x34`.
- Hash SHA-256 de 32 bytes en `0x44`.
- Offset auxiliar en `0x64`.
- Conteo auxiliar en `0x6C`.

El descifrado del índice requiere un perfil local. No se versiona ninguna clave en Git.

## Qué NO se afirma todavía

- No se afirma que `.svmap`, `.3DC`, `.3DO` o `.ANI` estén completamente descifrados en Unity.
- El importador de mallas es **profile-driven**: convierte correctamente cuando el layout confirmado se expresa en un perfil; no inventa offsets.
- La extracción completa de todos los recursos SPK, especialmente fragmentados/compresión específica, continúa como trabajo de ingeniería inversa.
- No se ha compilado este ZIP dentro de un Editor Unity en este entorno; se incluyen pruebas y validaciones de fuente para detectar errores estructurales antes de abrirlo.

## Estructura

```text
Assets/DreynoxMMORPG/
├── Runtime/
│   ├── App/
│   ├── Core/
│   ├── Gameplay/
│   ├── Networking/
│   ├── Rendering/
│   ├── UI/
│   └── World/
├── Editor/
│   ├── ReverseEngineering/
│   └── ProjectTools/
├── Tests/Editor/
├── Imported/
└── LocalSecrets/          # ignorado por Git
```

## Principio de arquitectura

```text
Shaiya legacy DATA/SPK
        ↓ (solo Editor)
BinaryReader + parsers + perfiles verificados
        ↓
Mesh / Texture / Material / AnimationClip / Prefab / ScriptableObject
        ↓
Dreynox Mmorpg Runtime
        ↓
URP + Unity Physics + Animator + networking asíncrono
```

El runtime final no debe depender del parsing de formatos antiguos.
