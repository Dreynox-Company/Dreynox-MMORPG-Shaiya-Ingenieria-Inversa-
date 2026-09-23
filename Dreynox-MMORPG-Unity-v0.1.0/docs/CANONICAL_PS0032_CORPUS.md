# Canonical ps0032 client corpus

This project now uses one exact local corpus as the primary legacy-client
reference for client parity work.

## Identity

- Client id: `ps0032-x86-3.3.2.10`
- `game.exe`: 5,352,488 bytes
- SHA-256: `509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d`
- Inner archive: `Sh.zip`
- `Sh.zip`: 3,452,205,786 bytes
- SHA-256: `78136f45ee45d3b0c6e03b829412189cab4d32ae5670a8d8b65892154673cfd5`

The asset corpus remains external to Git. The repository stores hashes,
structural evidence and parity contracts, not the proprietary 7+ GB DATA tree.

## Corpus scale

The supplied archive contains 55,539 files total, of which 55,521 are below
`DATA_Español/`, with approximately 7.24 GB uncompressed data.

Key counts:

- DDS: 19,652
- TGA: 10,184
- 3DC: 7,915
- ANI: 7,305
- SMOD: 2,956
- WAV: 1,872
- 3DE: 1,460
- EFT: 1,066
- 3DO: 557
- SVMAP: 111
- SData: 87

This is sufficiently complete to replace the partial fixtures previously used
for visual/client migration.

## game.exe static anchors

The exact client contains direct string references to, among others:

- `Login.wld`
- `Login/BG.tga`
- `CharacterSelect/*`
- `CharacterMake/*`
- `data/Character/Human`
- `data/Character/Elf`
- `data/Character/DeathEater`
- `data/Character/Vile`
- `data/ExcelXml/WingPosition.xml`
- `Wing.MON`
- `data/Character/Wing`
- `WING_ROT_X/Y/Z`
- `WING_UP_DOWN`
- `WING_FRONT_BACK`
- `WING_LEFT_RIGHT`
- D3D9 / D3DX shader/effect strings

These are evidence anchors only; static string presence is not treated as proof
of every runtime code path.

## Verified wing positioning table

`WingPosition.xml`:

- 48,463 bytes
- SHA-256 `8a2c376c898bb025550b5fe34b92a40dbbbb9e39063619cfee4756006908cd03`
- 48 data profiles
- 4 families × 6 jobs × 2 sexes
- all profiles use legacy bone index 4
- parameters: ROT_X, ROT_Y, ROT_Z, UP_DOWN, FRONT_BACK, LEFT_RIGHT

The exact numeric table is represented by `LegacyWingPoseCore`. Unity applies
those named axes through `WingAttachmentPoseApplier`; sign/axis conversion
remains an explicit adapter concern and is closed by visual parity rather than
hidden inside the data table.

## Offline baseline

The supplied offline package documents a loopback-only ps0032 setup. The login
server listens on port 30800 and two captured sessions reproduce the same world
population counts.

Examples:

- Map 0: 11 portals, 141 NPCs, 509 mob areas, 1,330 mobs, 1 obelisk.
- Map 1: 11 portals, 247 NPCs, 502 mob areas, 1,186 mobs.
- Map 2: 11 portals, 194 NPCs, 800 mob areas, 868 mobs.
- Map 18: 2 portals, 72 NPCs, 221 mob areas, 452 mobs, 1 obelisk.

The complete observed table is stored in
`docs/evidence/ps0032-canonical-corpus.json`.

## Parity rule

Future `game.exe ↔ Unity` comparisons must record the exact baseline hash.
Results from a different client variant must not silently overwrite ps0032
evidence.
