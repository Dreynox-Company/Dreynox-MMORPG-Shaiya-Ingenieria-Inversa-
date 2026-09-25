# Map0 MON rig qualification

## Reproduced blocker

Map0 qualification run 36114183712 (source b7e7f76fde23745ea0c07bf29092ebcec185d009) passed real-corpus EditMode tests, imported the 36-bone, six-part human with 28 clips, then failed during prefab creation with `Mob_Arachne_08 reference ANI/3DC skeleton mismatch`. World execution and captures did not run. Log artifact 10854808734 was downloaded and inspected; it is not a Player.

## Original-corpus audit

The supplied Sh multipart archive was opened read-only. Map0 SVMAP contains 1,330 spawn instances across 64 monster IDs; DBMonsterData maps these to 52 MON models. Their 441 named animation slots were enumerated. Absent `LOAD` slots are not animations.

- MON 372 Mob_Arachne_08 has three 79-bind-pose parts, but its idle, attack_2 and damage use 118-track ANI. All first 79 parent indices match the authored 79-track body hierarchy; the remaining tracks are outside this skinned body's hierarchy. Similar extra-track cases in other Arachne records total six slots.
- Tyross records 722, 723, 729, 731, 732, 733 and 735 have multipart bind tables of 47 and 58 entries, with a 58-bone animation rig. The first piece is not necessarily the complete skeleton.
- Rend records 205 and 206 change parents of bones 27 and 31 from 2 to 1 in walk/run. These four slots cannot be assigned unchanged local curves under the idle hierarchy.
- One non-finite normal was found in the referenced monster meshes, vertex 287 in mob_nepen_01_A.3DC. Positions, UV and weights remain strict. No authored geometry is dropped to bypass this defect.

## Implementation

The MON importer preflights every part and every declared animation before creating assets. It chooses an authored ANI that covers all used skin/effect indices and a part with the complete bind table. Each piece retains its own inverse-bind matrices. Extra, unbound ANI tracks are retained in the source object but not invented as skin bones.

Matching body hierarchies use the existing non-mutating body-track view. Changed parent tracks are transformed into the target hierarchy at authored 30 fps sample times, preserving evaluated world poses. Only changed-parent channels are rebaked. Scale/shear loss and non-finite transforms fail explicitly. This sampling is an implementation bridge; subframe interpolation and native visual equivalence still require rendered comparison.

Animation-file caching no longer removes semantic aliases. Idle and breath can refer to the same file while remaining separate catalog entries. Missing named resources fail; only explicit absent slots remain absent.

An explicit `ParseWithTopologyNormals` path repairs invalid MON normals from incident triangles. Default 3DC parsing stays strict. Finite authored normals, positions, UV, weights, indices and original DATA are unchanged.

## New regression gate

Six added EditMode tests cover long idle tracks, multipart skeleton source selection, rejected missing bones, reparented world-pose preservation, opt-in normal repair and all 52 canonical Map0 monster models. The real-corpus test expects 441 slots, six extra-track slots, four reparented slots and one repaired normal. Passing status must come from the subsequent Unity run; this audit is not a claim that the world Player has been built or that gameplay matches game.exe.
