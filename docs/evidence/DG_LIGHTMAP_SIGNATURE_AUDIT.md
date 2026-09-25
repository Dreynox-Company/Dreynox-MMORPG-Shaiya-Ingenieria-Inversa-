# Fortress DG lightmaps are BMP payloads named DDS

Map0 qualification run 36112406472 passed all 139 Unity tests with the actual corpus. Its world build passed the repaired SMOD resources and stopped at Resource_00 fortress lightmap 0. Unity's IHVImageFormatImporter reported `Not a valid DDS file`.

Inspecting the supplied Sh.zip found 634 files named .dds under world/dungeon. Of those, 629 have DDS signatures and five have BMP signatures. All five are l_r1_fortress00_l0..l4.dds: 196,662 bytes, 256x256, 24-bit BMP with 54-byte header. Lightmap 0 SHA256: 23a189c2999eab3ab7b757380117c4554a2bb5fdf8c32809c2a9a0712c215f67.

The DG importer now chooses the generated asset filename from the image signature, not the legacy extension. The source .dds path and bytes are untouched. Its exact BMP payload is copied into the managed Unity output with .bmp extension, allowing Unity's TextureImporter to decode it as LightmapData. The existing UV0/UV2, per-mesh texture/lightmap indices, collision and concatenation behavior is unchanged. Material textures use the same explicit detection.

Four new EditMode tests cover ambiguous legacy naming, rejected malformed signatures, actual Unity BMP import, and all five real fortress lightmaps. Their pass status belongs to the subsequent qualification run. No source texture replacement or omitted dungeon is used to hide the failure.
