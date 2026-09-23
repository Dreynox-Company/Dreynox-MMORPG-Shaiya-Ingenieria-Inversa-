# Dreynox MMORPG — matriz de paridad del cliente

Esta matriz mide la migración del **cliente de juego** desde el trabajo validado de Shaiya Flutter hacia Unity 6.  
**SPK, descifrado de archivos y Shaiya Studio están fuera del alcance de este repositorio.**

Estados:
- `PORTED_CORE`: regla determinista migrada y cubierta por pruebas/harness.
- `UNITY_ADAPTER`: existe implementación ejecutable en Unity.
- `REAL_ASSET_PENDING`: la lógica existe, pero falta conectar modelos/ANI/materiales/escenas reales.
- `PROTOCOL_PENDING`: falta conectar paquetes/opcodes confirmados.
- `VISUAL_QA_PENDING`: falta comparación visual reproducible contra game.exe.

| Dominio | Estado | Evidencia / siguiente gate |
|---|---|---|
| Idle / Walk / Run | UNITY_ADAPTER | actor Unity + harness; calibrar velocidad con captura real |
| Key repeat / focus loss | PORTED_CORE | no reinicia clip; focus loss limpia movimiento |
| Salto / grounding | UNITY_ADAPTER | actor + CharacterController; validar mapas reales |
| Cámara relativa | PORTED_CORE + UNITY_ADAPTER | +90° + W => -X fijado por contrato |
| Cámara vs geometría | UNITY_ADAPTER | SphereCast; validar paredes/mapas reales |
| One-hand + shield | PORTED_CORE + UNITY_ADAPTER | coexistencia permitida |
| Two-hand / spear | PORTED_CORE + UNITY_ADAPTER | offhand eliminado; run_spear semántico |
| Wings equipadas | PORTED_CORE | equipar alas NO inicia vuelo |
| Takeoff / hover / flight | PORTED_CORE | transición determinista y estados explícitos |
| Descenso de combate | PORTED_CORE | ventana recuperada de ~0,165 s |
| Reanudar vuelo tras combate | PORTED_CORE | conserva intención y espera fin de guardia |
| Mount idle/walk/run | PORTED_CORE + UNITY_ADAPTER | asiento no acumulativo |
| Target lock de ataque | PORTED_CORE | el golpe queda ligado al target original |
| Multi-target HP | PORTED_CORE | salud independiente |
| Combat guard | PORTED_CORE | 8 s desde último hit |
| Inventory / Warehouse | PORTED_CORE | conectar UI + datos/protocolo |
| Stats / Buffs / Death-Rebirth | PORTED_CORE | conectar actor y protocolo |
| Skills / Cooldowns | PORTED_CORE | conectar skill tables/opcodes confirmados |
| Quests | PORTED_CORE | contenido real + UI/protocolo |
| Shops | PORTED_CORE | catálogo/NPC/protocolo |
| Gatekeepers | PORTED_CORE | destinos reales + transición de mundo |
| Blacksmith | PORTED_CORE | alimentar reglas desde datos confirmados |
| Friends | PORTED_CORE | conectar protocolo/UI |
| Party / Raid | PORTED_CORE | conectar protocolo/UI |
| Trade | PORTED_CORE | conectar protocolo/UI |
| Duel | PORTED_CORE | conectar reglas PvP/protocolo |
| Guild | PORTED_CORE | conectar protocolo/UI |
| Loot | PORTED_CORE | entidades de mundo + protocolo |
| NPC services | PORTED_CORE | metadata + UI |
| Weather | PORTED_CORE | renderer/audio + estado de mundo |
| Login → server → character → world | PORTED_CORE | conectar escenas UI + protocolo |
| World sector streaming | PORTED_CORE | conectar escenas/chunks reales |
| Terrain / sky | REAL_ASSET_PENDING | migrar pipeline visual del cliente Flutter |
| Personajes / sets / armas reales | REAL_ASSET_PENDING | prefabs, rigs, materiales, sockets |
| ANI reales | REAL_ASSET_PENDING | Animator/PlayableGraph + catálogo por clase/arma |
| EFT / lapisia / VFX | REAL_ASSET_PENDING | VFX Graph/Particle System/URP |
| UI completa | VISUAL_QA_PENDING | reconstrucción Canvas/UI Toolkit y capturas |
| Audio | REAL_ASSET_PENDING | AudioMixer + spatial audio |
| Networking cliente | PROTOCOL_PENDING | adaptar contratos confirmados, IO fuera Main Thread |
| game.exe reference | PORTED_CORE tooling | huellas conocidas + inspector PE; no igualdad binaria |
| Windows Parity Lab | build pipeline listo | CI compila si hay activación Unity |
| Windows Client Release | bloqueado deliberadamente | exige escenas reales; nunca usa placeholders |
| Android/iOS | arquitectura compartida | después del cierre funcional Windows + profiling físico |

## Criterio de progreso

La paridad no se medirá por similitud binaria entre ejecutables. Unity y el cliente clásico usan toolchains diferentes.  
Cada sistema debe cerrar cuatro gates cuando apliquen:

1. **Contrato determinista** reproducible.
2. **Adapter Unity** funcional.
3. **Assets/protocolo reales** conectados.
4. **Comparación game.exe ↔ Unity** por capturas, timing, movimiento, animación y resultado observable.

Un dominio no se marcará como completo solo porque exista un script o una escena sintética.
