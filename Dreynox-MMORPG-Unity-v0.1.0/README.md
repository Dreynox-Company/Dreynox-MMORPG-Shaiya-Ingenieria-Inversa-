# Dreynox MMORPG — Unity Client Parity

Cliente MMORPG 3D en **Unity 6 + C#** que continúa la ingeniería inversa y reconstrucción funcional iniciada en el cliente Shaiya Flutter.

## Alcance de este repositorio

Este repositorio trabaja sobre **paridad del cliente de juego** frente a `game.exe`: locomoción, cámara, mundo, personajes, equipamiento, animación, combate, UI, red, social y rendimiento.

**Shaiya Studio permanece separado.** El montaje/descifrado SPK y las herramientas de archivo no forman parte de este proyecto Unity. Unity consume una carpeta `DATA` ya extraída o assets nativos previamente convertidos.

## Rama activa

`work/unity-reverse-engineering-v0.2.0`

## Baseline actual migrado

- Núcleo C# determinista de locomoción y selección semántica de animaciones.
- Idle / walk / run / jump / mounted idle-walk-run.
- Movimiento relativo a cámara y cancelación al perder foco.
- Reglas de arma de una mano, escudo y armas de dos manos.
- Perfil específico de movimiento con lanza.
- Alas equipadas sin activar vuelo automáticamente.
- Vuelo manual, hover y movimiento de vuelo.
- Montura y calibración de altura de asiento sin acumulación por frame.
- Target lock de combate, vida independiente por oponente y guardia de 8 s.
- Party, Trade, Duel y Raid como máquinas de estado puras para integrar con protocolo.
- Streaming espacial determinista de sectores.
- Cámara tercera persona con órbita, zoom y colisión SphereCast.
- Sandbox Unity jugable para comparación iterativa con `game.exe`.
- Inspector PE para catalogar exactamente qué variante de `game.exe` se usa en cada comparación.
- Transporte TCP/UDP fuera del Main Thread ya existente.
- URP y calidad adaptativa PC/móvil.

## Abrir el proyecto

En Unity Hub selecciona esta carpeta, la que contiene directamente:

```text
Assets/
Packages/
ProjectSettings/
```

Después ejecuta:

`Dreynox MMORPG > Client Parity > Create Refresh Sandbox`

El sandbox contiene un actor controlable, cámara tercera persona, objetivos independientes y HUD de diagnóstico.

Controles actuales de validación:

- `W/A/S/D`: movimiento.
- `Shift`: correr.
- `Space`: salto.
- `Shift + Space`: alternar vuelo cuando hay alas.
- `Q/E`: bajar/subir durante vuelo.
- Botón derecho + ratón: cámara.
- Rueda: zoom.
- Click izquierdo: seleccionar objetivo.
- `1..4`: ataques de prueba.

## Build Windows

Dentro de Unity:

`Dreynox MMORPG > Build > Windows x64 Release`

CI puede compilar el Player con GameCI cuando el repositorio tenga configurada activación Unity. Sin licencia, CI ejecuta igualmente validación de fuentes y el parity harness C# puro.

## DATA legado

`Dreynox MMORPG > Legacy Client > Extracted DATA Inspector`

Ese módulo únicamente inspecciona recursos **ya extraídos** y permite convertir layouts confirmados a assets Unity. No abre ni descifra SPK.

## Regla de paridad

No buscamos igualdad binaria entre Unity y el ejecutable clásico. La paridad se mide por comportamiento reproducible:

1. estado de personaje y animación;
2. cámara y movimiento;
3. equipamiento / sockets;
4. combate y timings;
5. mundo y streaming;
6. UI y social;
7. protocolo y networking;
8. render y rendimiento;
9. capturas lado a lado y regresiones automatizadas.

`main` no debe recibir una fase hasta que su gate correspondiente esté verificado.
