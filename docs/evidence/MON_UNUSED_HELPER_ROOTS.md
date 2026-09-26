# Original Map1 NPC helper-root compatibility

Run36165357425 passed186/187 tests; the new all-model test stopped on already documented absent Guard8/169 source records. The test now preserves the existing world-importer policy, verifies exactly four unresolved positions and checks all30 monster/54 resolved NPC model IDs. Missing guards are reported, not replaced by invented models.

An independent read-only scan found NPC catalog67,69,228 share four NPC_l_04_A/B/C/D meshes and three walk/br/idle ANIs. Each has61 bone entries. Entries57..60 contain NaN translation in the bind/base matrices, have no effective skin weights, no MON effect anchors, no ANI channels, and form separate root trees (57 -> 58, 59 -> 60). Body indices0..56 are finite and closed. No animation, visible mesh or collision is deleted.

A MON-only preflight trims that proven-unused trailing forest in memory, then runs the unchanged strict 3DC/ANI readers on the retained bytes. It refuses weighted, body-attached, animated or effect-anchored tails, invalid main roots and incompatible layouts. Valid rigs are not rewritten. Vertex, face, retained-matrix and retained-animation bytes are unchanged. Original DATA files remain read-only.

Original hashes:
- NPC_l_04_A.3DC 36628bytes ca4ba73c8b5357eaa927d9d8756b68dcc7698a15fe835834396425a68301d1f4
- NPC_l_04_B.3DC 18500bytes 314b4b5cf593653554d13b421a414324fb23b747c9221530ffbb192bcdcfed42
- NPC_l_04_C.3DC 26648bytes 1ae7a0e107cc9786f2ea9fb56aaea8f21613086d0ba64b73a89c793d02f7c7d0
- NPC_l_04_D.3DC 45928bytes 1ed5bc8938994a6e388ba2466f0cc67e035b5757d48d6c1c73c127b95d02c67d
- walk.ANI 23614bytes 195daa02dae74703d162ef7c8cdc0dd5e850bbcc1d33b61aa56f8e8209c515c6
- br.ANI 19622bytes f9458b89b6f792bf153388a022b7105017dd892ece960f32707ad0b2b695501b
- idle.ANI 31046bytes d2f0f561a19f075b76f7e4cb21b945aa60e4416b7c4e33f47a5d7ea33957042c

Six new tests include three actual NPC catalog records. Their execution status and actual Player appearance must be taken from the following real-corpus Unity run, not inferred from this source audit.
