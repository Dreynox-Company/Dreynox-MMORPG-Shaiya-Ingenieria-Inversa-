# Original entity DDS coverage audit

Read-only scan of all 2,956 DDS headers under original npc/dds and monster/dds found 2,938 supported BC resources: 885 DXT1, 1,741 DXT3 and 312 DXT5. Eighteen did not use FourCC compression. Thirteen are uncompressed 32-bit BGRA; five are indexed palette pet textures. No claim of all-DATA rendering coverage follows from these counts.

Independent Python/Pillow checks of all 13 BGRA resources matched every decoded base-level pixel after applying their declared masks/stride. No source file was rewritten. Two NPC examples:
- ctl_hen_01.dds: 349652 bytes, 256x256, SHA256 45779ddef48b8c83c1120ddfc4abc822b139a03c5b0087187e3ea40c11e0f1b3.
- hw2018_gravestone.dds: 1048704 bytes, 512x512, SHA256 3341e37bca0ae9b75c39aa16310f167f39d29e7211ea476bf528f1262b68b7dc.

Managed decoding now also supports explicit eight-bit RGB channels in 24/32-bit pixels, using declared masks and alpha. DDSD_PITCH controls row stride; a whole-image LINEARSIZE value must not be interpreted as row pitch. Output remains bottom-first RGBA8. Bounds, channel overlap, unsupported formats, truncation and cancellation are validated before unsafe/native import. Existing BC paths remain unchanged.

Nine standalone cases cover BGRA/RGBA, row padding, alpha, invalid masks/stride/truncation/cancellation and explicit rejection of paletted data. Two Unity corpus cases cover every original pixel in the two NPC examples. Actual passing status must be read from CI. The five paletted pet textures remain unimplemented; the decoder rejects them rather than inventing colors or silently replacing assets.

Primary format references: Microsoft DDS_HEADER (DDSD_PITCH / dwPitchOrLinearSize) and DDS_PIXELFORMAT (DDPF_RGB / DDPF_ALPHAPIXELS and channel masks), https://learn.microsoft.com/windows/win32/direct3ddds/dds-header and https://learn.microsoft.com/windows/win32/direct3ddds/dds-pixelformat.
