# Canonical humf body ANI audit

Source baseline: fe2dc449969ef7abfe109b724280788a34eabcf9. Failure from Map0 run 36097644337: `swim ANI bone count does not match the mesh`. The source tree and failure artifact were downloaded and inspected before modifying code.

The supplied multipart archive was read without changing it. All 28 ANI named by LegacyCharacterImporter parsed to exact EOF. All 28 have the same first 36 parent indices. Fourteen have only 36 tracks; twelve have 38 (last two independent roots); two mount clips have 72 (another tree follows the body prefix). First body tracks do not depend on tail tracks.

Examples:
- humf_000_normal.ani: 14,098 bytes, 36 tracks, SHA256 185398a2f1dc39ce8c035f095157a14384df4f14ce4d994833d64117f49fd2b4.
- humf_007_swim.ani: 17,094 bytes, 38 tracks, SHA256 9beea580f85456bdca47a31dad02a5397c85857aab9cab998badb07f598aa53a.
- humf_008_jump.ani: 12,934 bytes, 38 tracks, SHA256 101b50647350ece3054cac07f7c074de194a5a02a6a5a1d0ab989352803141b7.
- humf_009_die.ani: 35,018 bytes, 38 tracks, SHA256 8a073a61bcff90f32e93f52cb0ac22ea696614c8e022b176bcae190dd243b951.

The importer now validates hierarchy identity for every body index before creating assets. It passes a read-only view of those 36 tracks to the existing shared AnimationClip builder. It does not mutate the source, invent body bones, reindex vertices, discard requested clip semantics, or claim to interpret tail tracks for equipment/mounts. Missing body tracks or a different parent chain still fail.

The previous private duplicated mesh/material/curve implementations were replaced by the already used LegacySkinnedAssetBuilder. Output paths and the public import entrypoint are unchanged. Added six Unity test cases include all 28 real-corpus clips; their runtime evidence belongs to the subsequent Windows run, not to this static audit.
