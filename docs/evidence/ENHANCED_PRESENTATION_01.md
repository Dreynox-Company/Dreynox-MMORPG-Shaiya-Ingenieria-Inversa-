# Enhanced presentation 01 — original content, improved rendering

User decision: replicate Shaiya's content and functions, not the graphical limits of its old renderer. Main remains unchanged. Preconverted Unity content is the shipping path; raw DATA selection remains developer-only.

## Audited baseline

Source fd21d58cd3711ffc4799343dae8f4eac93c60d40 / Map0 run 36121679798 compiled and walked but failed combat. The native reference is Map1, so Map0 frames are not evidence of native scene equivalence.

The renderer cast all NPC/mob colliders as world obstacles, allowing camera distance to collapse to 0.05 units. Its sweep radius ignored near-plane width. The sky/cloud importer generated very large meshes; cloud materials enabled _Surface without completing the blend state. Lit/terrain texture setup cast DDS importers to TextureImporter, but Unity imports DDS through IHVImageFormatImporter, so that configuration was silently skipped. The URP asset disabled HDR and did not explicitly enable soft shadows despite lights requesting them. Scene trilight ambient colors were not explicitly assigned.

## Changes

- Camera query ignores actor/NPC/mob bodies without disabling their gameplay colliders. Solid-world obstruction is still checked, including initial overlap and saturated query buffers. The near-plane envelope scales with FOV/aspect. Outward recovery is smooth, retraction immediate. If geometry forces a very close view, the whole avatar is temporarily shadows-only for that camera, rather than a permanently missing head.
- A central post-import presentation pass normalizes generated opaque URP/Lit materials and terrain layers before the preconverted world build. Color DDS base mips are decoded with the existing tested decoder to PNG, then imported explicitly as sRGB with generated mipmaps, trilinear filtering and 8x anisotropy. Source files, UVs and alpha are not repainted or resized. Platform compression remains a separate Unity step. Lightmaps and normal maps are not color-converted.
- The original sky BMP and two cloud TGAs are reused by a dedicated far-depth skybox shader. Generated sphere/plane primitives are removed; no authored map objects are removed. Clouds blend alpha into the background and cannot write scene depth. Cloud drift is labeled artistic, not a decoded native timing.
- PC presentation enables HDR, 4x MSAA, four shadow cascades, soft shadows, explicit balanced ambient colors and neutral tone mapping. No bloom/blur/DOF is used to hide asset defects. Surface gloss uses an explicit modest value, not arbitrary albedo alpha.
- The existing real Map0 builder invokes this pass; this does not replace the original world with a synthetic scene.

## Source checks

Read-only inspection of Sh.part1..7: 1,068 matching DDS resources checked (223 DXT1, 270 DXT3, 575 DXT5), no unsupported format among that inspected set. This is not a claim to have exhaustively verified every texture in DATA.

Canonical atmosphere:
- sky_a2.bmp: 32x32 RGB, SHA256 21ed773ac1743239294c600a4a111d9e1fa22f14e396097246abd84ecb1920a7.
- clouds01_b1.tga: 512x512 RGBA, SHA256 3b2d65b1bd199d86f2e086661d080ede8ae43c35dc109259601e2909da1f8633.
- clouds02_b1.tga: 512x512 RGBA, SHA256 e7a97ffcc05fb326b02d1e5e68ac4ccba9465872429402322975796c1ef190f8.

Nine new EditMode cases cover actor-collision exclusion, solid obstacles, recovery, wide near planes, overlap diagnostics, finite inputs, DDS pixel/orientation roundtrip, read-only imports and material/shader policy. Their passing status must come from Unity CI, not this document.

## Boundaries

No quests, server authority, damage values, spawn positions, map identity, mesh topology or data files are changed here. Existing combat placement/target-selection failures remain separate work. A rendering profile is not a completed game. Graphical superiority needs actual new captures and review; no old Player or edited screenshot should be presented as the new build.

API references: Unity 6000.0 Camera.nearClipPlane, RenderPipelineManager.beginCameraRendering; Unity 17.0 UniversalRenderPipelineAsset/Tonemapping; Unity IHVImageFormatImporter. Shader/serialized field compatibility is checked against the pinned project Editor/package versions and fails explicitly if the schema changes.
