# Content delivery: preconverted client + optional developer DATA loader

## Confirmed product decision

The shipped Dreynox MMORPG client consumes preconverted Unity assets. Its normal startup must not ask for DATA_Español or parse the original tree. All existing Editor importers, canonical scene builders and shared parsers remain in place. No Flutter/Studio changes and no SPK work are part of this change.

Direct DATA-folder loading remains an additional developer/testing option only. For now it is distributed as the separate developer utility `LocalData/DreynoxMmorpg-LocalData.exe` alongside the Parity Lab. It is not the full game, and is not a prerequisite for the preconverted game.

## Enforced build boundary

- `BuildLocalDataLab` passes `BuildOptions.Development` and the per-build `DREYNOX_DEV_DATA` symbol through `BuildPlayerOptions.extraScriptingDefines`.
- The filesystem/UI entrypoints `LocalCharacterLoader` and `LocalDataScreen` compile only in the Editor or when both Development Build and the dedicated opt-in are present. The command-line DATA path handling belongs to the excluded screen.
- Accidentally specifying DREYNOX_DEV_DATA in a non-development Player produces a compile error, rather than leaking the feature into release.
- The flag is not persisted to project-wide PlayerSettings.
- `LocalDataBuildGuard` rejects a shipping scene list containing the DATA tool and checks scene objects, including inactive objects, for developer loader components.
- The build manifest records developmentBuild, localDataDeveloperTools and contentMode explicitly.
- Shared format readers and mesh-building primitives remain reusable by Editor importers. Excluding developer entrypoints does not remove or duplicate the validated content pipeline.

Five new EditMode tests cover accepted shipping content, explicit developer opt-in, release rejection, empty scene lists and inactive developer components. Until their Unity run finishes they are tests added, not tests passed.

## Most recent compiled baseline inspected before this change

Source: caffabc7013c348c72de9092922c715b5aa1939d.
GitHub Actions run: 36088024194 / Unity Windows #684.

- Deterministic parity gate: success.
- Unity Personal preflight: success.
- Unity EditMode XML: 101 tests, 85 passed, 0 failed, 16 skipped.
- Windows build, manifest verification, artifact upload and Parity Lab smoke capture: success.
- Canonical Character build/captures/native comparison and full canonical world build: skipped because corpus preparation did not enable those stages.
- Artifact id 10844168797, 68,029,087 bytes, SHA-256 fb02de1711a68520b6e07fe4998cd8c30c5c3d6aeb440774f3a19c311b6a9857.
- ZIP contains both DreynoxMmorpg-Parity.exe and LocalData/DreynoxMmorpg-LocalData.exe with their complete Unity runtime folders.
- This artifact predates the explicit developer-only guard added in this revision.

The local-data executable is a character qualification utility: six mesh pieces, local DDS textures, selection ANI, rig/face/hair controls, cancellation and read-only folder access. It does not load the complete world/gameplay from an arbitrary DATA folder.

## Progress interpretation

3DC/ANI importers, 16-rig preview switching, face/hair variants, CharacterSelect/Make builders, WLD/SVMAP/SMOD/DG/WTR environment importers, vegetation/VAni/MAni, NPC/mob streaming and audio/EFT runtime code exist. Existence and synthetic tests do not certify the full rendered game or native protocol behavior.

The release gate remains a real, preconverted Login/CharacterSelect/CharacterMake/Map0 Player exercised with the canonical corpus, with native-vs-Unity captures and behavior checks. No percent of completion or graphical superiority can be inferred from executable size, line count, commits or pure-core test count. In particular, Unity executables can share the same launcher hash while their actual game assemblies and data differ.
