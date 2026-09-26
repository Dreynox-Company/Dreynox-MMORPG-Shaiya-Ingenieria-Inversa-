# Map1 animated world asset ordering

Run 36181618811 at c91482256f09b5bb023edcd9747a01e9c0bedbd4 passed 213/213 Unity tests, with zero failures or skips, then failed preparing the world. Evidence artifact 10885052048 (ZIP SHA256 1930deba5d92c5a6dd91e932cd406647e984e89b9792079162176a3ef97f6641) was downloaded and its NUnit XML and complete prepare-map1.log inspected. No Player was built.

The final error was `Parent directory must exist before creating asset .../World/Map000/VAni/eagle/Meshes/Mesh_00_Frame_000.asset`. The batch queued DeleteAsset for a missing VANI resource folder. The importer then created that folder and textures. Flush finally deleted that newly-created folder before writing the staged mesh. Separately, the VANI builder loaded staged meshes and material through AssetDatabase before they existed on disk, producing null references. A Dispose-time flush masked the initiating failure.

Corrections:
- A missing DeleteAsset is a no-op at call time. Existing generated folder deletion flushes earlier operations and executes synchronously before recreation; individual existing asset writes retain batching.
- Reject noncanonical generated paths before destructive operations. An explicit Abort preserves the initiating exception and prevents Dispose from persisting an invalid pending batch. This is not an atomic on-disk transaction or rollback of already-flushed assets.
- VANI uses its actual staged mesh/material objects, flushes them before runtime validation, and shares the same authored resource across the two WLD groups without deleting its first occurrence.
- Output belongs to the selected map, rather than the hardcoded Map000 directory. VANI albedo also uses the established managed DDS-to-PNG path. Original source files are never rewritten.
- Existing original geometry, UVs, face order, transforms, placement counts and authored interval fields are retained. The interval's native game.exe calibration remains explicitly false.

Seven batch ordering/abort/path test cases and two original-eagle cases cover both synchronous and batched persistence, shared resources across WLD groups, all 64 original frame positions and prefab reload. Test output is owned separately from real Map0/Map1. Source validation passed locally; actual Unity/Player status must come from the subsequent CI run. No rendering screenshot, playable executable or import-speed result is asserted by this code change.

Primary API semantics: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.DeleteAsset.html and https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.StartAssetEditing.html.
