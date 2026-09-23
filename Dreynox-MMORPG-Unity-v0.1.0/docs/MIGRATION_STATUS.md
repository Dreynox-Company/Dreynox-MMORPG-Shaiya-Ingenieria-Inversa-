# Estado de migración a Unity

## Ya reimplementado

- Locomoción básica y movimiento relativo a cámara.
- Jump y grounding base.
- Montura: idle/walk/run y calibración de asiento.
- Wings: equipamiento, hover, flight y retorno a suelo.
- Reglas main-hand/off-hand.
- Selección semántica de animación por estado y familia de arma.
- Combate determinista: selección, target lock, hit, recovery y guard timeout.
- Múltiples targets con HP independiente.
- Party, Trade, Duel y Raid: core de estado listo para protocolo/UI.
- Friends: solicitudes, aceptación y presencia online.
- Guild: miembros, officers y transferencia de liderazgo con capacidad configurable.
- Inventario y Warehouse: stacking, movimiento, retirada y gold.
- Stats/Buffs/Life: modificadores, expiración, death/rebirth.
- Skills: aprendizaje/rank, target lock, resource cost, windup/recovery/cooldown data-driven.
- Quests/Shops/Gatekeepers/Blacksmith: máquinas de estado y servicios data-driven.
- Loot/NPC/Weather: ownership, servicios NPC y transiciones de clima.
- Flujo de cliente: boot/login/server/character/world/disconnect.
- Streaming espacial de sectores.
- Cámara orbital con collision SphereCast.
- Networking worker TCP/UDP existente.
- Inspector de `game.exe` y catálogo de variantes conocidas.
- Sandbox jugable para iteraciones lado a lado.

## Activo ahora

1. Conectar los `ParityCore` ya portados a adapters Unity reales sin duplicar lógica.
2. Conectar assets reales extraídos: rigs, personajes, armas, alas, ANI, mapas, cielo y materiales.
3. Migrar el pipeline de animación/equipamiento validado en Flutter a Animator/PlayableGraph y sockets Unity.
4. Conectar inventario, stats, skills, buffs, quests, shops, warehouse, NPC, gatekeepers y social al protocolo real.
5. Completar combate PvE/PvP con timings, hit events, muerte/rebirth y loot observables frente a `game.exe`.
6. Cerrar audio, físicas, agua, EFT/VFX y UI real.
7. Automatizar capturas y comparación lado a lado `game.exe` ↔ Unity, además de frame-time PC/móvil.
8. Generar Client Release únicamente cuando Boot/Login/World reales estén presentes; el Parity Lab permanece separado.

## Fuera de alcance de este repo

- Descifrado/montaje SPK.
- Herramientas de edición de Shaiya Studio.

Esas responsabilidades permanecen en el proyecto de Studio y solo entregan DATA/assets ya extraídos a Unity.
