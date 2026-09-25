# Map0 NPC preflight continuation

While the monster regression ran, the same read-only corpus scan was extended to the NPC models selected by Map0 SVMAP + NpcQuest definitions. It resolves 41 unique npc.mon records and 135 declared animation slots.

NPC 186 N_A8B8_01_E01 has 79 mesh inverse-bind entries (highest weighted index 77), while all three animations have 81 tracks. Tracks 79 and 80 form an independent tail. The first 79 tracks are a closed parent-before-child hierarchy. The rig resolver now has a second, explicit fallback for this case: use the mesh-backed prefix of an authored hierarchy, provided every skin influence and attached-effect anchor fits. It does not invent bind matrices or remove a required effect bone.

NPC 40 changes two parent links in its walk animation and uses the same world-pose-preserving conversion already needed by Rend monsters. NPCs 214/215 contain respectively 149/337 invalid normals; the opt-in topology repair path handles these without accepting invalid positions or UVs. These attribute recoveries are not a claim of native lighting equivalence.

Added a synthetic long-ANI/short-mesh check, including rejection of an effect anchored outside the backed body, plus a real-corpus census test: 41 models, 135 slots, three extra-track slots, one reparented slot, 486 invalid normals. These tests supplement the 52-model monster gate; their passing status belongs to the next Unity run.
