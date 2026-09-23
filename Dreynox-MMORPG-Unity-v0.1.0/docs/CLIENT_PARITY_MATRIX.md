# Matriz de paridad del cliente

Estados usados: `PORTED_CORE`, `UNITY_ADAPTER`, `REAL_ASSET_PENDING`, `PROTOCOL_PENDING`, `VISUAL_QA_PENDING`.

| Dominio | Estado Unity actual | Gate siguiente |
|---|---|---|
| Idle / Walk / Run | UNITY_ADAPTER | comparar clips/velocidad con assets reales |
| Focus loss / key repeat | PORTED_CORE | EditMode + Player test |
| Jump / grounding | UNITY_ADAPTER | colisión real por mapa |
| Cámara relativa | UNITY_ADAPTER | comparación de sensibilidad y límites |
| Cámara vs geometría | UNITY_ADAPTER | mapas reales |
| One-hand + shield | UNITY_ADAPTER | assets/sockets reales |
| Two-hand / spear | UNITY_ADAPTER | clips reales por clase |
| Wings | UNITY_ADAPTER | bone/socket y ANI reales |
| Flight / hover | UNITY_ADAPTER | transición visual por arquetipo |
| Mounts | UNITY_ADAPTER | asiento por recurso + clips reales |
| Target lock | PORTED_CORE | selección/UI real |
| Multi-target HP | PORTED_CORE | entidades de mundo reales |
| Combat guard 8 s | PORTED_CORE | protocolo PvE/PvP |
| Party | PORTED_CORE | PROTOCOL_PENDING |
| Trade | PORTED_CORE | PROTOCOL_PENDING + UI |
| Duel | PORTED_CORE | PROTOCOL_PENDING + reglas PvP |
| Raid | PORTED_CORE | PROTOCOL_PENDING + UI |
| Guild | PROTOCOL_PENDING | migrar contrato Flutter |
| Friends | PROTOCOL_PENDING | migrar contrato Flutter |
| Inventory / equipment | REAL_ASSET_PENDING | datos/slots reales |
| Stats / skills / buffs | PROTOCOL_PENDING | port de modelos y opcodes |
| Quests | PROTOCOL_PENDING | modelos + UI |
| Shops / warehouse | PROTOCOL_PENDING | modelos + UI |
| Gatekeepers | REAL_ASSET_PENDING | world transitions reales |
| Blacksmith | PROTOCOL_PENDING | reglas/resultado/UI |
| Death / rebirth | PORTED_CORE parcial | actor HP + protocolo |
| World streaming | PORTED_CORE | escenas/chunks reales |
| Terrain / sky | REAL_ASSET_PENDING | importadores reales |
| Audio | REAL_ASSET_PENDING | catálogo y spatial audio |
| EFT / lapisia | REAL_ASSET_PENDING | renderer VFX |
| UI original | VISUAL_QA_PENDING | Canvas/UXML final |
| Windows executable | build method listo | requiere activación Unity en CI o build local |
| Android/iOS | arquitectura compartida | profiling físico |

| Inventory / Warehouse | PORTED_CORE | conectar UI + catálogo real |
| Stats / Buffs / Death-Rebirth | PORTED_CORE | conectar datos/protocolo real |
| Skills / Cooldowns | PORTED_CORE | mapear skill tables y packets verificados |
| Quests | PORTED_CORE | contenido real + UI/protocolo |
| Shops | PORTED_CORE | catálogo/NPC/protocolo real |
| Gatekeepers | PORTED_CORE | destinos reales + world transition |
| Blacksmith | PORTED_CORE | alimentar costes/probabilidades desde datos confirmados |
| Friends | PORTED_CORE | PROTOCOL_PENDING + UI |
| Guild | PORTED_CORE | PROTOCOL_PENDING + UI |
| Loot | PORTED_CORE | world entities + protocol |
| NPC services | PORTED_CORE | NPC metadata + UI |
| Weather | PORTED_CORE | renderer/audio + packets/world state |
| Login / server / character / world flow | PORTED_CORE | protocol packets + UI scenes |
