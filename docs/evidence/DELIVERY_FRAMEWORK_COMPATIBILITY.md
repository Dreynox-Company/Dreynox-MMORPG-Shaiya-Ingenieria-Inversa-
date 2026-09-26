# Delivery compatibility after the successful local Map1 loop

Run19 (36204716763, d69ccf65) completed preparation, Player build and scripted quest/combat/reward successfully, but failed the one-file delivery compiler with CS1061: its Framework ZipArchiveEntry reference does not expose ExternalAttributes. The previous invalid-payload-only compatibility exercise did not establish successful extraction on that runner.

The fix removes that API dependency, not the security check. PlayerZipMetadata reads the published ZIP central-directory external-attribute fields and single-disk ZIP64 ending with bounded IO. It rejects symlinks, reparse entries, special files, encryption, unsupported compression, invalid offsets/counts and truncation, including links disguised by a directory suffix. System.IO.Compression still owns actual decompression; original SHA256 coverage, paths and file-size budgets remain in force. The installed-cache verifier also rejects unlisted files before launching (not only changed expected files).

The standalone suite adds central-directory/ZIP64 malformed cases and a cache-injection regression. Windows CI now compiles with the same Framework executable, C#5 options and references as the real packager. A clearly labelled synthetic payload is extracted, verified a second time from cache, then tested with an unlisted file; it is never launched as a game or uploaded. These are delivery tests, not a new gameplay or graphics qualification.

No edits to game.exe or DATA. The new appearance/radar and gameplay changes still require their own Unity Player run. Packaging success must not relabel run19's older costume/radar as current visual output.

Primary format source: PKWARE APPNOTE 6.3.10 sections4.3.12-4.3.16, https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT.
