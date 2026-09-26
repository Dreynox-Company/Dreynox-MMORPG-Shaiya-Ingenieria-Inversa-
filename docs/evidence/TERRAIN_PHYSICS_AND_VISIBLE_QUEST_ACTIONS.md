# Real Map1 blocker: render-only terrain in the compiled Player

Audited Player run 36200927531 / source 6c26d2b8f91c11c7f0f59ddd4afa7faeded3b18c. Its 230 Unity tests passed; original Map1 preparation and Windows compilation completed. Actual Player evidence confirms forward walking, Ethan interaction and quest3400 acceptance, followed by `No walkable support near fox spawn 1000126`. No kills or reward were claimed. The Player was retained for internal diagnosis, not delivered as qualified.

## Root cause, verified from the compiled scene

The actual Player ZIP was downloaded, its digest checked, and its compressed UnityFS scene inspected read-only. The level0 object table contains one Terrain (class218), one CharacterController and 1,846 MeshColliders, but ZERO TerrainColliders (class154). Map001_Terrain has only its Transform and Terrain components. The original source manifest omitted `com.unity.modules.terrainphysics`. Unity's terrain renderer was available transitively, but the terrain's physical support module was not.

This explains why buildings could support walking while the outdoor field had no physical ground. It is not fixed by setting actor Y to SampleHeight, removing collision checks or adding an invisible plane. WorldGroundPlacement correctly rejected the unsupported field.

Full hashes/object IDs are retained in TERRAIN_PHYSICS_PLAYER_AUDIT.json. Original DATA and game.exe were never modified.

## Correction

Explicitly include builtin terrain and terrainphysics modules. The existing Terrain.CreateTerrainGameObject factory can now create its matching TerrainCollider for every imported FLD map. WorldTerrainBuildGuard rejects visible or inactive packaged terrain without matching enabled nontrigger collision. WorldTerrainCollision probes the real collider, including holes, rather than inferring physics from rendering. Seven new Unity tests exercise the same factory, physical field placement, absent/disabled/wrong collision, bounds and authored holes.

The actual Map1 Player qualification now requires collider support under every locally sampled fox position in the original spawn areas before walking, combat or reward checks. Those positions are sampled by the existing importer; they are not asserted to be native server spawn coordinates. SampleHeight is used only as a cross-check, never as replacement collision.

## Visible quest action path

The previously captured NPC dialogue was obscured by name labels pooled later into the same canvas. Its modal now has a separate sorting override and graphic raycaster. The same validated UI method serves both its button and the scripted Player test. The test selects a currently visible quest option, captures the actual mission page, accepts it, defeats the original enemies through combat and returns through the same visible delivery action. Hidden/ineligible quests cannot be selected via that API. Two new UI regression tests cover closed/uninitialized actions and modal ordering.

Combat, reach, source HP, reward values and collision safety are not weakened. The five-kill/reward test can still fail independently; no new gameplay success or improved rendering is claimed until this exact source has run. Earlier snapshots without this module do not validate the correction.

Primary Unity references:
- https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.modules.terrainphysics.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Terrain.CreateTerrainGameObject.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/ClassIDReference.html
