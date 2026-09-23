# Arquitectura v0.1.0

## Regla principal

Los formatos legacy se leen solamente en Unity Editor. El Player final consume assets nativos ya convertidos.

## Capas

1. `Editor/ReverseEngineering`: escaneo, BinaryReader, SPK, perfiles de layouts y conversión.
2. `Runtime/Gameplay`: locomoción, attachments, vuelo, animación y cámara.
3. `Runtime/Networking`: IO TCP/UDP y transforms fuera del Main Thread; `NetworkPump` es el único puente hacia Unity.
4. `Runtime/World`: streaming aditivo de escenas/chunks.
5. `Runtime/Core`: bootstrap y calidad adaptativa.
6. `Editor/ProjectTools`: preparación de escenas, instancing y flags de occlusion.

## Decisiones preservadas de la investigación anterior

- Alas y armas deben ser attachments semánticos por socket/bone, no offsets globales improvisados.
- Equipar alas no activa vuelo por sí solo.
- La transición de combate desde vuelo se modela separadamente del tiempo de ataque.
- El cliente mantiene lógica de red y parsing de paquetes lejos del Main Thread.
- Los recursos originales se convierten una sola vez y no se reinterpretan cada frame.
