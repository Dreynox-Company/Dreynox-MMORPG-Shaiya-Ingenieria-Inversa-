# Legacy 3DC / ANI coverage — canonical ps0032 corpus

Baseline:

- client: `ps0032-x86-3.3.2.10`
- `game.exe` SHA-256:
  `509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d`
- supplied inner archive SHA-256:
  `78136f45ee45d3b0c6e03b829412189cab4d32ae5670a8d8b65892154673cfd5`

## ANI

The complete supplied DATA contains **7,305 ANI files**.

A structural pass over every ANI in the supplied corpus found:

- 7,305 parsed successfully;
- 0 structural failures;
- both legacy ANI and `ANI_V2` signatures are present;
- keyframes use a 30 FPS frame index;
- each bone stores parent index, absolute base matrix, rotation keyframes and
  translation keyframes.

The Unity importer derives a fallback local pose from:

`inverse(parent absolute ANI matrix) × bone absolute ANI matrix`

and uses raw local rotation/translation keyframes when present. This matches the
previous verified Flutter viewer data numerically.

## 3DC

The DATA contains **7,915 files with the .3DC extension**.

Those files contain **7,918 mesh sections** because three resources concatenate
multiple standard 3DC sections in one file.

Observed section layouts:

| Layout | Sections | Notes |
| --- | ---: | --- |
| Standard | 7,844 | version 0 (40-byte vertex) or 444 (48-byte vertex) |
| Skeletonless | 72 | cloak meshes; version 0, vertices/faces without embedded bone matrices |
| Texture-prefixed | 2 | `mob_rend_01_a/b`; embedded TGA name, then EP5-style skeleton/mesh |
| Unsupported | 0 | structural corpus pass |

Three monster/NPC files contain multiple standard sections and are handled by
`Legacy3dcParser.ParseMany`.

## Unity coordinate bridge

Shaiya data is converted explicitly rather than by leaving a hidden negative
scale on the actor:

- position / normal: `(x, y, z) → (x, y, -z)`;
- triangle winding is reversed;
- rotation quaternion basis conversion:
  `(x, y, z, w) → (-x, -y, z, w)`;
- matrices use `S × M × S`, where `S = diag(1,1,-1,1)`.

UV values are preserved unchanged. This was cross-checked against the previous
Flutter/WebGL character viewer.

## Local full-corpus audit

After selecting the canonical corpus in Unity, run:

`Dreynox MMORPG > Client Parity > Audit Entire Canonical 3DC + ANI Corpus`

The tool reparses every local 3DC/ANI and writes:

`Artifacts/Parity/legacy-format-corpus-audit.json`

The original game resources remain outside Git.
