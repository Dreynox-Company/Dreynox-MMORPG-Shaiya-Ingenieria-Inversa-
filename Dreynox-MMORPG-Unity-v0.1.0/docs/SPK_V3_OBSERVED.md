# SPK v3 — layout observado

Esta versión implementa el header que fue observado en el corpus de trabajo y lo trata como evidencia versionada, no como definición universal de todos los SPK posibles.

| Offset | Tipo | Campo |
|---:|---|---|
| 0x00 | UInt32 LE | signature/unknown |
| 0x04 | UInt32 LE | version (`0x00030000`) |
| 0x08 | UInt64 LE | indexOffset |
| 0x10 | UInt64 LE | indexStoredBytes |
| 0x18 | UInt64 LE | indexDecodedBytes |
| 0x20 | UInt32 LE | recordCount |
| 0x24 | UInt32 LE | blockBytes |
| 0x28 | 12 bytes | index nonce |
| 0x34 | 16 bytes | GCM tag |
| 0x44 | 32 bytes | SHA-256 índice cifrado |
| 0x64 | UInt64 LE | auxiliaryOffset |
| 0x6C | UInt32 LE | auxiliaryCount |

El parser verifica límites antes de leer. `SpkV3IndexDecryptor` valida el SHA-256 del ciphertext y el tag GCM antes de aceptar el plaintext.

Los perfiles criptográficos deben vivir localmente en `Assets/DreynoxMMORPG/LocalSecrets/` y están excluidos por `.gitignore`.
