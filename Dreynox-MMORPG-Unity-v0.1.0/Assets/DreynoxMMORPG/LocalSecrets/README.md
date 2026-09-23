# LocalSecrets

Esta carpeta existe para perfiles criptográficos privados usados únicamente durante la ingeniería inversa local.

Todo archivo aquí está ignorado por Git salvo este README.

Formato aceptado para el índice SPK v3:

```json
{
  "schema": 1,
  "profileId": "perfil-local",
  "indexSha256": "hash-del-indice-cifrado-opcional",
  "algorithm": "AES",
  "chainingMode": "GCM",
  "secretHex": "CLAVE_HEX_LOCAL"
}
```

No publiques claves ni material privado en el repositorio.
