# Arquitectura — Client Parity

## Fuente de verdad técnica

La referencia de comportamiento es el `game.exe` identificado por SHA-256 y la evidencia ya validada durante el desarrollo del cliente Flutter. El código Dart no se copia al runtime: sus contratos comprobados se reimplementan en C#.

## Capas

1. `Runtime/ParityCore`: máquinas de estado C# sin dependencia de Unity; se pueden compilar/probar fuera del Editor.
2. `Runtime/Gameplay`: adapters Unity para CharacterController, Animator, cámara, attachments y combate.
3. `Runtime/Networking`: transporte TCP/UDP asíncrono; el Main Thread solo consume eventos ya parseados.
4. `Runtime/World`: planificación/streaming de sectores y escenas.
5. `Editor/ReverseEngineering/Executable`: identidad y comparación estática de variantes de `game.exe`.
6. `Editor/ReverseEngineering/Data`: recursos ya extraídos y conversión hacia assets nativos.
7. `Editor/ProjectTools`: sandbox de paridad, preparación de escenas y build.

## Invariantes migradas

- Repetir una tecla no reinicia la animación actual.
- Shift solo no desplaza al personaje.
- Perder foco cancela input mantenido.
- Arma de dos manos invalida offhand; arma de una mano permite escudo independiente.
- Equipar alas no activa vuelo.
- Quitar alas restaura locomoción terrestre.
- Montura mantiene una calibración de asiento estable.
- Un ataque conserva el objetivo capturado cuando fue solicitado.
- Cada oponente conserva HP independiente.
- La guardia de combate se renueva con el último golpe y expira tras su timeout.
- Streaming carga proximidad y descarga sectores abandonados.

## Render

URP es el pipeline principal. PC y móvil comparten gameplay y datos, pero pueden utilizar tiers distintos de materiales, sombras, LOD y densidad. La paridad visual se compara en escenas reproducibles; no se fuerza el pipeline D3D9 del cliente clásico.
