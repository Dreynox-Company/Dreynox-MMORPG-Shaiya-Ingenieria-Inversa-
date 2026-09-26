# Native VANI vertex diffuse, not a bone index

## Actual failure
Run16 36198496513 (4442c83fe2d71a21940dd575540cac604fcc716e) passed 222/222 Unity tests, zero failed/skipped, then failed during Map1 preparation at `fish_02` frame0: unexpected BoneId -3815995. No Player was built. Evidence ZIP artifact10891403441 has SHA256 c235a3c5754b4d77b0f4b2dbb44d874858db9d264d924771212b28dbcb0f9cbe. The prior deferred folder-deletion failure no longer occurred.

## Read-only original evidence
Original game.exe: 5352488 bytes, SHA256 509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d, PE32 image base0x400000.
The data/entity/vani string at0x86a4c0 is used by loader0x684790 (xref0x6847c8), which calls reader0x6848c0. Its per-mesh reader0x6842b0 creates a vertex buffer with FVF0x152 at0x684469 and byte length vertexCount*36. The render path0x683f10 calls IDirect3DDevice9::SetFVF with0x152 at0x683f68 (vtable offset0x164) and SetStreamSource with stride0x24 at0x683f83 (vtable offset0x190). These addresses refer only to the exact hashed executable, not arbitrary Shaiya versions.

0x152 = XYZ(0x002) | NORMAL(0x010) | DIFFUSE(0x040) | TEX1(0x100). The layout is position float3, normal float3, D3DCOLOR ARGB DWORD, texture float2. Therefore the DWORD at vertex offset24 is diffuse color, NOT a skeleton index. The inherited parser field name BoneId is retained only for source compatibility; DiffuseArgb and DiffuseColor now expose the proven meaning. Original -1 represents opaque white, not a missing bone.

An independent read-only scan parsed all143 original VANI files to EOF (2913738 frame vertices). Thirty-four contain nonwhite diffuse values, including RGB colors and alpha. Exact fish_02.vani:293096 bytes, SHA256027f2f1c73820ef10eb82e5f4eb2eccaa36dfe475a26714802703111395ea1da,54 frames*150 vertices. Values:0xffffffff*6696,0xffc5c5c5*54,0xff8b8b8b*1350.

The upstream Parsec VaniVertexFrame comment saying this value is always -1 is contradicted by the original corpus and native vertex-buffer format. It must not be used as a validation rule.

## Implementation and boundaries
Every baked frame keeps its original RGBA via Mesh.colors32. A dedicated URP shader consumes COLOR, texture and material tint, including alpha clipping in forward/depth passes, GPU instancing and fog. Main-light/ambient lighting is the current modern presentation policy, not claimed identical to all D3D9 fixed-function render states. No object, frame, original source texture or placement is removed or substituted. Source DATA is unchanged.
Placement counts now derive from serialized resources rather than a nonserialized counter that reset on scene/prefab reload. Original eagle persistence tests remain; original fish all-frame position/color checks, prefab color reload, ARGB channel cases, shader checks and all placed Map1 VANI preflight are added. Actual passing/build/play status must be read from the next CI run; successful decoding is not a completed Player.

Primary API reference: https://learn.microsoft.com/en-us/windows/win32/direct3d9/d3dfvf (DIFFUSE is DWORD ARGB; interleaved vertex order). No original executable or DATA bytes are committed.
