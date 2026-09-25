# Ready-to-run delivery, without user build scripts

The user requested only a runnable executable, not source archives or CMD/PowerShell steps. The internal packager embeds the entire qualified preconverted Unity Player (including its DATA directory and native DLLs) into a Windows Framework 4.8 launcher. On first use it extracts to a private per-user cache, verifies every file against the embedded manifest, and starts the actual Unity game. Later launches reverify expected hashes. It requires no Unity Editor, elevation, network download or original DATA access.

This wrapper is delivery, not gameplay progress or native parity. It is not digitally signed by this change. Do not distribute the tiny synthetic CI fixture or mislabel the generic Unity launcher stub as a complete standalone game.

`Build-PortablePlayer.ps1` is an internal CI action only. It rejects a qualification report unless the actual Map1 Player passed walking/NPC/mission/five-fox combat/reward checks. It requires the complete Player manifest. It also runs the packaged EXE in verify-only mode before reporting success. Original DATA is never written. The shipping content pipeline and developer-only DATA separation remain unchanged.

Separate CI verifies archive path safety, hash failures, coverage, duplicate files and mutation of cached binaries, plus Windows C#5 compilation and rejection of an invalid embedded payload. These tests do not run the game. An actual game executable may be published only with its corresponding successful Player evidence.

Starting source: 096fb27dda96466f6648393652208f086ae0a7f9. The current Map1 run continues independently; this addition does not cancel or change its immutable source.
