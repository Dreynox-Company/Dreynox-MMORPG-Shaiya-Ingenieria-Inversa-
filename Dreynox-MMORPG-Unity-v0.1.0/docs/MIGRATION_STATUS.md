# Estado de migración

## Migrado conceptualmente y reimplementado en C#

- Archive/index inspection.
- Asset classification.
- Attachments/equipment sockets.
- Flight state/transitions.
- Locomotion and camera collision.
- Semantic animation catalog.
- World chunk streaming.
- TCP/UDP worker queues.
- Adaptive graphics tiering.

## Fase 1 activa

- SPK v3 header: implementado.
- AES-GCM index decryption: implementado con perfil local.
- Index post-processing/record parser: siguiente iteración, depende de confirmar compresión y estructura decodificada.
- `.svmap/.3DC/.3DO`: importador binario profile-driven implementado; layouts específicos todavía deben certificarse contra muestras.
- `.ANI`: catálogo semántico listo; parser binario específico pendiente de layout confirmado.

No se ha copiado código Flutter/Dart al runtime Unity.
