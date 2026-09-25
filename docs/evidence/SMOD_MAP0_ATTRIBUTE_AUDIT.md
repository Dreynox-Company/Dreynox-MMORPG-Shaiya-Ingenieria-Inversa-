# Map0 SMOD attribute audit

After the ANI-body correction, run 36109286841 imported the real human body successfully (`bones=36 parts=6 clips=28`) and then failed in SMOD ReadVector3 with a non-finite float. This was not an asset-format stride error.

The original Sh.zip was read directly inside the supplied multipart RAR without rewriting it. The 197 referenced SMOD resources (36 buildings, 117 shapes, 27 trees, 17 grasses) all consume exactly their file length using the existing 36-byte vertex layout. Eleven resources contain 33 NaN normal vectors. Ten of those vertices only belong to zero-area triangles. Four NaN UV slots across B3_Big_Dolmen_03, B3_Big_Dolmen_05 and R1_crater07 are never referenced by any face. No position or collision change is needed.

The parser keeps position/bounds/collision/index/EOF validation strict. Only invalid normals are reconstructed from that vertex's own incident triangles, area-weighted; valid authored normals are untouched. Cancellation of opposite face normals uses the strongest incident face. A vertex with no nonzero-area incident triangle receives an explicitly counted inert default. Invalid UV is accepted only for an unreferenced vertex. Referenced invalid UV still fails. The source DATA is read-only.

Each parsed mesh reports ReconstructedNormals, InactiveNormalDefaults and UnreferencedUvDefaults. The new real-corpus test audits all 197 resources and checks exactly 33 normal / 4 unreferenced UV repairs. Synthetic tests check that this compatibility path does not hide invalid geometry or visible UV corruption.

This produces finite Unity-compatible attributes, not proof of the precise fallback behavior of game.exe or lighting equivalence. The actual render still needs paired capture inspection.
