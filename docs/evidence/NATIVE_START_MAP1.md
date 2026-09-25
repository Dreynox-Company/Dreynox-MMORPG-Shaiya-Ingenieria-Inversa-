# Native Map1 start integration — evidence and limits

Base: 254310d4364f11de00676cc0c4385523383278a8. The previous Map0 enhanced build exceeded its 45-minute limit; the source log contained 14,886 refresh records, 1940.621 cumulative seconds. No new Player was produced by that run.

The full multipart Sh corpus was extracted read-only. Native offline `servicios/world/config/character.json` and the recorded GUI-created character specify Map1 (580,78,1760). Map1 SVMAP SHA256: 15b0899be083c09344a27310a85ded371c3f61b60a07b167d39e467584cf06cc. Exact parse: 2048 map size, 502 monster areas, 1186 instances, 11 portals, 252 NPC definition rows, 311 positions.

NpcQuest was decrypted using the repository's existing SEED implementation, independently checked with CRC32 d5fde4af. Of the Map1 rows, 248 resolve to definitions, 307 positions, 54 models. Four rows of type8/id169 have no definition. They are recorded and explicitly excluded as unresolved, not replaced with invented guards. All other missing keys still fail. The older offline log reports247 NPCs; that is not the same as the present corpus's248 resolved definition rows and is NOT silently called equivalent. A complete client must resolve this discrepancy through version/runtime evidence.

At the native start: type7/id1167 is Instructor de la Luz (582.2256,77.7955,1772.2686), greeting Te doy la bienvenida a Shaiya, guerrero de la Luz. Type7/id1081 is Ethan el Reclutador (565.1857,77.9668,1765.835). The UI uses those source names and greetings, not placeholder NPCs.

New path: NativeStartBuild.RunBatch imports the real Map1 into preconverted Unity content, then applies the existing enhanced rendering. Native asset writes are staged and flushed before prefab/scene serialization; no suspended imports are loaded inside AssetDatabase.StartAssetEditing. Source texture/audio inputs remain synchronous and unchanged.

Ground placement resolves actual colliders near authored height, rejects slopes and capsule obstruction, and resets vertical velocity on grounded relocation. It does not teleport to the highest roof or accept a terrain sample below a building as proof of a valid spawn.

The Canvas HUD includes original player frame, class icon, action frame, minimap_1.tga, nearby NPC/mob names and map dots, live target health, original NPC greeting dialogue, and attack controls wired to the existing local combat interaction. It does not fake server player statistics, quest rewards, persistence or complete quest semantics. Dialogues do not imply all NPC services are implemented.

The automated Player sequence checks map/start identity, supported placement, forward walking, an actual original NPC dialogue, and a reachable level<=8 mob with original HP<=2500. It drives the real public attack action and requires attack/death animation states. The encounter relocation is explicit. Damage95 is a local test input, not a recovered server formula. Actual rendered bone deformation and matched native animation timing remain independent checks.

No SPK, Flutter or Shaiya Studio changes. No main changes. No full-game or100% claim. Build/tests/captures must be taken from the new Windows run before distributing an executable.
