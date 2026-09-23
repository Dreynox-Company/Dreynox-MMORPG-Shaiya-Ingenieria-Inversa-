# Validation report — Dreynox Mmorpg v0.1.0

Fecha: 2026-09-22

## Ejecutado en el paquete

- Estructura requerida `Assets/`, `Packages/`, `ProjectSettings/`: OK.
- `Packages/manifest.json`: JSON válido.
- Assembly definitions: JSON válido.
- Exclusión de `Library`, `Temp`, `Logs`, `UserSettings`: OK.
- Conteo básico de llaves en todos los `.cs`: OK.
- Búsqueda de marcadores `TODO` / `IMPLEMENT HERE`: ninguno.
- Manifiesto SHA-256 generado en `SOURCE_SHA256.txt`.
- ZIP probado posteriormente con `unzip -t`.

## Pruebas incluidas para Unity Test Runner

- `SpkV3HeaderParserTests`: header real observado de 128 bytes.
- `AesGcmManagedTests`: vector NIST AES-GCM + rechazo de tag inválido.
- `PacketFramerTests`: reensamblado de un frame TCP partido.

## Límite de esta validación

El entorno que generó esta entrega no contiene Unity Editor, por lo que no se afirma una compilación Unity de este ZIP. El proyecto apunta a Unity `6000.0.64f1`; al abrirlo Unity importará paquetes, generará `.meta` donde corresponda y permitirá ejecutar las pruebas Editor incluidas.
