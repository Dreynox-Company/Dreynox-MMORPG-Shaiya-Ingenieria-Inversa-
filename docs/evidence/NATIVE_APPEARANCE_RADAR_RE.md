# ps0032 appearance and radar — executable-guided correction

## Exact evidence and scope

Baseline source: d69ccf65fe2721f0b4f3cf4088d5a81f37f88d12.
Reference: original game.exe, 5,352,488 bytes, SHA-256
509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d.
Read-only access to the supplied Sh.part1..7.rar / inner Sh.zip. No SPK work,
no modification/execution of the native binary and no upload of its bytes or
original artwork to Git. Addresses below are PE virtual addresses at image
base 0x00400000, verified by static disassembly, not runtime traces.

## Wrong costume was not a texture-quality issue

The world importer hardcoded co_humf_upper/lower/hand/foot003. That is a
costume selection, not the default unarmored player shown by the native
reference. The offline initial Fighter config supplies sword type1/id1 and
potions type25/id1, not that costume. An emulator config is supporting fixture
evidence, not a claim about every native character's server inventory.

Native code loads humf_upper.MLT at 0x51DD95 and calls 0x51F210. The latter
validates ML2, loads independent mesh and texture name tables, and at 0x51F740
allocates 12-byte selection records. The reads at 0x51F770 / 0x51F791 /
0x51F7EB populate their three 32-bit words. The third word remains an opaque
SourceFlag here; its rendering semantics have not been invented.

The six humf tables' default row resolves:

- upper: humf_torso001.3DC / humf_torso001.dds
- lower: humf_lower001.3DC / humf_lower001.dds
- hand: humf_hand001.3DC / humf_hand001.dds
- foot: humf_boots001.3DC / humf_boots001.dds
- face: humf_face001.3DC / hum_face001.dds
- hair: humf_hair001.3DC / hum_hair001.dds

New LegacyModelListCore validates record sizes, references, bounds and resource
basenames. LegacyDefaultAppearanceCore resolves body row zero and independent
face/hair rows. It never assumes meshIndex == textureIndex or invents names by
numeric suffix. The world uses this default, as do canonical creation previews
and the developer folder loader. Explicit costume lookup remains available as
an explicit request, not the initial character. The established world prefab
path is retained to avoid breaking scene references; the stored appearance
provenance now describes its actual native default content.

Independent corpus audit: the 96 correctly located tables for 16 rigs resolve
400 first-five face/hair combinations with 224 distinct meshes, all effective
bone influences inside the corresponding selection-ANI skeleton. An unrelated
viwm_upper.MLT also exists under /elf and is NOT used as a fallback for Vile.
This is structural coverage, not 400 rendered/certified character screenshots.

## Actual texture-coordinate mismatch

Legacy3dcParser preserves source UV. The shared mesh builder formerly copied
it unchanged while the managed DDS loader already returned bottom-first Unity
pixels. Direct3D's top-left texture origin and Unity's bottom-left origin need
one V conversion. The shared 3DC builder and original starter 3DO sword now
use (u, 1-v), without clamping tiling or flipping the image a second time.
Source UV, skinning weights, bone hierarchy, positions and DATA remain unchanged.

Additional source check: the original humf_face001 high-Y vertices have low
source V (roughly 0.12–0.15) while the neck's low-Y vertices are near V 0.98.
The torso similarly has low V at the collar and high V at its lower edge.
This agrees with the source image convention, not the previous Unity copy.
Map terrain UVs, DG lightmap UVs and other formats are not blindly flipped by
this targeted correction. Their coordinate contracts remain independent.

Tests verify asymmetric color sampling, one-time UV conversion, tiling,
non-finite rejection and preservation of raw source mesh data. The subsequent
Player still needs inspection of the real face/body from front/back/side.

Primary coordinate references:
https://learn.microsoft.com/windows/win32/direct3d9/directly-mapping-texels-to-pixels
https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-uv.html

## Radar: use original sprites, not empty square Images

NativeWorldHud created plain Image components without a sprite, tinted orange
or cyan. That explains the squares. The native executable does something else:

- 0x599490 initializes the radar resources.
- 0x59959B references Rader/minimap_icon_pc.tga (16x16).
- 0x5995B4 references Rader/minimap_icon_party.tga (16x16).
- 0x5995CD references Rader/minimap_icon_monster.tga (16x16).
- 0x59B96E selects the monster sprite at object offset +0x3440; 0x59B974
  calls its draw function 0x5F2120. This is a draw-path xref, not just a string.
- The available/ready quest draws select offsets +0x33C0 / +0x33D0, e.g.
  0x59A873 / 0x59A975. Quest-low is separately loaded at +0x34D0.
- NPC arrow is 8x12; service icons, party and quest markers have distinct art.

The original monster file has a transparent 16x16 canvas and a small orange
rounded center with dark outline. The new radar preserves all source colors,
alpha, border and native sprite dimensions. No color tint or white/square
fallback is allowed. NPC service markers and quest availability/completion
select separate source sprites. Social marker resources are available but do
not pretend that party/guild/network sessions have been implemented.

The original 202x226 radar frame is cropped from its atlas rather than stretched
from its full 256x256 storage. The map viewport is square and clipped; player
heading and marker positions share its projection. Zoom controls update that
projection and never leave stale dots. The local radar still uses locally
available streamed entities; native server visibility and all filter rules are
not certified by this UI correction. Zoom range/layout calibration remain
explicit local presentation choices, not recovered native constants.

## Qualification and remaining work

Source tests cover 35 pure ML2/appearance contracts and 12 new Unity cases for
real default resources, 16 rigs, texture origin, radar alpha/sprite/clipping,
projection and quest icons. Test additions are not passes until CI reports them.
The actual Player qualification additionally records six mesh/texture sources,
rejects the unrelated costume, and requires a bound original radar before the
existing walking/NPC/five-fox/quest-reward gates.

The previous terrainphysics, original sword, collision guard and visible quest
button fixes are preserved. No server-authoritative guild/trade, complete skill
system or all-map gameplay is claimed. This is a substantive appearance/radar
correction, not certification of complete native client parity.
