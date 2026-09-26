# Original Map1 terrain resources

Run 36155571240 passed all 175 Unity EditMode tests with the original corpus and then correctly refused to prepare Map1 because A1_Grass_Earth.tga was not a file. The supplied Sh.zip contains terrain/detail/a1_grass_earth.dds (262272 bytes), not that TGA. The WLD-authored basename is correct; its legacy extension is stale.

The terrain importer now retains authored names and tile scale, first resolves an exact file, then allows only the identical-basename TGA-to-DDS counterpart. It does not rename or edit DATA and does not substitute an unrelated texture. Original bytes feed the existing DDS decoder and batched sRGB/mipmap conversion. Native TerrainLayer assets are created after all color textures finish importing.

Two added tests cover exact-first/counterpart resolution, missing and unsafe paths, source immutability and the complete real Map1 texture list. They require a subsequent Unity run for passing evidence.

A second observation: the earlier package-resource preflight reported UnityEditor.DefaultAsset for shadergraph/urtshader. That proves existence, not a correct graphics import. It now forces those stale/default imports and rejects the result if it is still a placeholder. No errors are suppressed and no test is skipped.
