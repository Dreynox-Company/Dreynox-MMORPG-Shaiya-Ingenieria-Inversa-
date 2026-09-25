# Map1 original SMOD import blocker

Audited source: 75c634b48d5f772243f0ebcd4df6a033b65a27db. Windows Starting Map1 run 36157914818 passed 177 Unity tests and failed during world preparation on l_h2_cronwell01.smod mesh 4, referenced vertex 22 UV.

Read-only inspection of the uploaded Sh.part1..7 / Sh.zip confirms:
- file 175632 bytes, SHA256 9b461e242168a6d217faa7f96a5f5d325a423f9b200975d339e43b02a67a1218;
- 11 authored submeshes; submesh 4 has an empty texture name, 27 vertices and 13 faces;
- four NaN UVs at vertices 22/24/25/26; they belong to nondegenerate faces 8/9, but no texture is authored for that submesh;
- existing prefab importer already does not render empty-texture submeshes and builds collision from the independent collision records.

Fix: neutralize invalid UV values on an explicitly untextured submesh, preserving topology, coordinates, valid channels and all collision records. Count these separately as UntexturedUvDefaults. Referenced invalid UV on a textured submesh still fails. No file-name allowlist, planar projection or fabricated material is used.

Independent scan of 312 SMOD files whose basenames occur in Map1's name tables: all parse to exact EOF; this is a candidate-name scan, not an asserted count of placed objects. Other non-finite UVs in that scan are unreferenced. New Unity tests iterate actual WLD coordinate references to check the full placed static set before a Player build and retain existing strict Map0 regression tests.

Original DATA is unchanged. This corrects compatibility, not proof of complete game.exe appearance or functionality.
