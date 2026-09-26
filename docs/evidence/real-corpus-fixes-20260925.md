# Actual corpus gate exposed three previously skipped assumptions

The Windows run 36096362373 executed 123 tests with actual DATA: 120 passed, 3 failed, zero skipped. No failure was reclassified as a skip.

## DG material slots
Direct parsing of supplied L_R1_Fortress00.dg consumes exactly 4,282,898 bytes and recovers all previous mesh/collision counts. Texture slot 21 is intentionally empty in the table and four faces reference it. The table index must be preserved. The importer emits an explicitly named untextured/lightmapped material and warns that native fallback shading is still uncalibrated; it does not substitute an arbitrary texture or drop geometry. The inner fortress DG consumes 1,506,718 bytes and has no empty names.

## Login.wld
The old test contradicted itself by expecting Dragon/Starlighting/login_A in Buildings as well as Shapes/Grass. Actual DUN payload has zero Buildings, two Shapes (Dragon, Starlighting), one Grass (login_A). Keep all correct group-specific and placement/effect assertions.

## MON effect bindings — actual x86 evidence
Reference game.exe SHA-256: 509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d.

- File offset 0x458880 / VA 0x859680 is `monster.EFT`.
- VA 0x4C4847 loads global object 0x8E9498 with that name from `data/Effect` (VA 0x857CEC), calling 0x4A5660.
- VA 0x521D73 reads the MON binding's EffectId; 0x521D78 selects the same global 0x8E9498 and calls 0x40A370.
- 0x40A370 bounds-checks raw effects at object +0x0C and indexes table +0x20. Sequence lookup is a different routine (0x40A430, count+0x10/table+0x24).
- Therefore the numeric attached-effect array indexes global data/Effect/monster.EFT raw effects, NOT each record's named AttachEffect file. Retain the named field for its other semantics.

Corpus cross-check: effect/monster.eft SHA-256 e05fcb379898dbbf8c576d0fea2b6d1ce38069b2fa7e6b4c7e93edb014e948e0 has 294 raw effects, zero sequences, 111 textures. 253 monster records and 6 NPC records with bindings say LOAD; this is not missing content. The named lapis-extractor library has only 14 raw effects and one sequence, while its numeric bindings are 274..287, all valid in the global table. Goblin indices 66/67 name 17Goblin01/17goblin02 in the global library.

## Rendering qualification
URP shader materials alone do not select URP. Map0 build now creates a native pipeline+renderer and assigns GraphicsSettings and quality overrides. All values are qualification defaults, not a claim of native lighting calibration. The generated Unity Player must still be rendered and inspected.
