# Map1 NPC texture preparation — bypass native legacy DDS import

Actual run 36168379406 (source 096fb27dda96466f6648393652208f086ae0a7f9) passed 193 Unity tests with no failures/skips. World preparation subsequently crashed inside Unity's IHVImageFormatImporter. No Player was built. Exact final import: LocalLegacyGenerated/Entities/Npc/0137_NPC_elmr_ul/Textures/00_elmr_torso010_2.dds. The managed stack enters LegacySkinnedAssetBuilder.ImportLitMaterial -> AssetDatabase.ImportAsset. This is a native crash, not a managed parser exception or qualification timeout.

Read-only inspection of original Sh.zip recovered the precise NPC texture:
- DATA_Español/npc/dds/elmr_torso010_2.dds, 349680 bytes.
- SHA256 b089857babb7ab69bd24ca456b82a3af902e5304a1eb32be51b4bcf336ddd014.
- 512x512 DXT3, ten declared mip levels. Legacy reserved fields contain DDSX / 0xffffffff and bitcount=256. The base mip contains the complete 262144 bytes expected from BC2 blocks.
- A same-basename texture also exists under character/elf/dds, with different size/hash. The fix uses the exact NPC resource, not this other copy.
- Independent Pillow decoding succeeds as 512x512 RGBA. This does not isolate the native crash root cause: header tolerance, importer state and memory pressure remain possible contributors.

The shared skinned material builder now decodes DDS through the existing managed BC1/BC2/BC3 path into PNG BEFORE adding an asset to Unity. It no longer stages an original DDS and only later attempts to configure an incompatible TextureImporter. Original source files, colors/alpha, UVs, meshes, skeletons and gameplay remain unchanged. PNG pixels come from the decoded original base mip; Unity regenerates mips/filtering via the established sRGB color import policy. No native DDS fallback, alternate texture substitution or per-filename bypass is added. Linear lightmaps/normal maps do not use this color-only builder.

Three new Unity cases cover managed malformed-data rejection, the real original NPC texture and pixel/alpha-preserving PNG output, plus configured material/importer state. They must pass on the runner before this route is called Unity-qualified. Native failure evidence is preserved in artifact 10881814190; the original DATA is not modified. The currently running prior revision is immutable and does not include this fix.
