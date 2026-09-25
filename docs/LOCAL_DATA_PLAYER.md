# External DATA / multi-rig qualification

## Windows user flow

The Windows parity build also builds:

`Builds/WindowsParity/LocalData/DreynoxMmorpg-LocalData.exe`

Keep the complete Player folder: executable, `_Data`, UnityPlayer.dll and the other generated files. Select the extracted game folder or `DATA_Español` / `DATA_Espanol` / `DATA`, choose a rig, face and hair, and press **Cargar selección desde DATA**. Right mouse rotates; mouse wheel zooms; ANI can be paused. F8 uses the parity screenshot capture. `--data-root "C:\path\to\Shaiya"` selects a folder at startup. Only the last successfully loaded folder is persisted under Application.persistentDataPath. Original files are read-only and game.exe is never executed by this loader.

This is **local-data character qualification**, not a claim that Login, networking, world loading or all gameplay are connected in the external-folder client. The existing baked Character/Canonical parity builds remain available. Extract multipart archives before selecting the folder; this loader does not mount RAR or SPK.

## Architecture

The 3DC/ANI binary readers now compile into Runtime, unchanged. Their historical `Dreynox.Mmorpg.Editor.LegacyFormats` namespace is temporarily retained for source compatibility, but the files contain no UnityEditor dependency. AssetDatabase, prefab serialization and AnimationClip curve generation remain Editor responsibilities. Both paths use one shared runtime skinned builder.

Each local request reads six mesh pieces, six textures and the rig's select ANI. IO/parsing and DDS DXT1/3/5 decoding run through Task. Texture2D, Mesh and Transform creation stays on Unity's main thread. Requests are cancellable and generation-checked. A failed or superseded replacement leaves the last good character intact. File/texture memory budgets are enforced. Internal path traversal, absolute paths, alternate streams, ambiguous case and reparse entries are rejected.

ANI sampling uses source time at 30 fps, binary channel lookup, coordinate-basis conversion and shortest-path quaternion interpolation without per-frame closure allocations. Interpolation, DDS orientation, lighting and joint alignment still require rendered A/B calibration against ps0032. Successful parsing alone does not certify rendering parity.

## Actual corpus defect

All 400 combinations (16 rigs x 5 faces x 5 hair styles, set003) resolve to existing corpus resources. The 224 unique meshes include nine humf resources with 40/72 inverse-bind entries while their selection ANI contains 36 bones. All effective weighted indices are below 36. The former exact-size check would reject those valid resources.

The shared builder validates used bone indices, uses the ANI hierarchy and retains each mesh piece's own inverse-bind matrices. It does not clamp invalid indices, silently remap them or invent animated bones for unused table entries. See `docs/evidence/local-data-multirig-audit.json`.

## Verification

- Existing deterministic gameplay harness: 129 checks passed in the audited-source run.
- LocalDataHarness: 15 pure C# DDS/filesystem checks passed.
- PowerShell denied-path discovery regression: passed.
- Unity EditMode and Windows Player compilation are separate gates; inspect the run for the source revision before claiming a build.
- Optional all-rig corpus test needs DREYNOX_CORPUS_ROOT; skipped tests are not passes.

## Native reference recovered

The small `ShaiyaOffline(1).zip` contains logs and saves. Separately, the Library's full `Shaiya_Offline_Nativo_0_1_2_Windows.zip` has now been recovered. It includes the canonical game.exe and local login/world service files, plus the exact eight original screenshots expected by NativeVisualReferenceCore. All eight SHA-256 values and the 1024x768 dimensions match. The pinned client crop remains x=3, y=26, width=1021, height=739.

The archived screenshot scope explicitly identifies the diagnostic audio variant and does not claim complete gameplay verification. These are recovered historical references, not new executions or new game.exe-vs-Unity comparison results. See `docs/evidence/native-reference-recovery.json`.

This change does not decrypt SPK, modify game.exe, publish the proprietary corpus to Git or certify complete game parity.
