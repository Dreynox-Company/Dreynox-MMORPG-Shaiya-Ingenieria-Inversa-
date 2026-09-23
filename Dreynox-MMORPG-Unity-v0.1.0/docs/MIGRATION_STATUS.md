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
- Streaming espacial de sectores.
- Cámara orbital con collision SphereCast.
- Networking worker TCP/UDP existente.
- Inspector de `game.exe` y catálogo de variantes conocidas.
- Sandbox jugable para iteraciones lado a lado.

## Activo ahora

1. Reproducir en Unity todos los checks funcionales que ya estaban verdes en el cliente Flutter.
2. Conectar assets reales extraídos: rigs, 3DC/3DO, ANI, mapas, cielo y materiales.
3. Portar inventario, stats, skills, buffs, quests, shops, warehouse, NPC y gatekeepers.
4. Portar Friends/Party/Trade/Duel/Guild/Raid a protocolo real y UI.
5. Completar combate PvE/PvP, muerte/rebirth, loot y blacksmith.
6. Cerrar audio, físicas, agua, EFT y efectos.
7. Comparación visual automatizada y métricas de frame time PC/móvil.

## Fuera de alcance de este repo

- Descifrado/montaje SPK.
- Herramientas de edición de Shaiya Studio.

Esas responsabilidades permanecen en el proyecto de Studio y solo entregan DATA/assets ya extraídos a Unity.
