# External DATA / multi-rig qualification

## User flow delivered in this change

The Windows parity build also produces:

`Builds/WindowsParity/LocalData/DreynoxMmorpg-LocalData.exe`

Keep that executable together with its `_Data` directory, UnityPlayer.dll and other output files. Open it, select the original game folder or `DATA_Español` / `DATA_Espanol` / `DATA`, choose a rig, face and hair, and press **Cargar selección desde DATA**. Right mouse rotates; mouse wheel zooms; ANI can be paused. F8 uses the existing parity screenshot capture. `--data-root "C:\path\to\Shaiya"` selects a folder at startup. The last successful folder is stored under `Application.persistentDataPath`; no asset or executable from the original folder is modified or executed.

This is **local-data character qualification**, not a claim that Login, the network protocol, all world assets or all gameplay work in the external-folder client. The established baked Character/Canonical parity builds remain available unchanged.

## Architecture

One implementation of the 3DC/ANI binary readers now compiles into Runtime. Their historical `Dreynox.Mmorpg.Editor.LegacyFormats` namespace is temporarily retained for source compatibility; it contains no `UnityEditor` dependency. The Editor still owns AssetDatabase/Prefab/AnimationClip generation. A shared runtime skinned builder handles mesh/bones/bind poses in both routes.

A local request reads six mesh pieces, six textures and the rig's select ANI. IO/parsing and portable DDS DXT1/3/5 decoding run via Task. Texture2D/Mesh/Transform creation remains on Unity's thread. Requests are cancellable, newer requests supersede older ones, and replacements are committed only after successful construction. Failed/cancelled replacements preserve the last good character. There are bounded file and decoded texture budgets. The resolver rejects parent traversal, absolute internal paths, alternate streams, ambiguous casing and reparse entries.

ANI sampling uses 30 fps source time, binary channel lookup, basis conversion and shortest-path quaternion interpolation. It does not allocate closures on each frame and does not depend on Editor-only AnimationClip creation. Interpolation, texture orientation, lighting and joint placement **still require rendered A/B calibration** against ps0032. Rendering is not certified by binary parser success.

## Corpus defect discovered during this audit

All 400 combinations (16 rigs x 5 faces x 5 hair styles, set003) had their expected paths. Deeper inspection of the 224 unique mesh paths found **nine humf resources** with 40/72 inverse-bind entries but an ANI with 36 bones. All effective weighted indices are below 36. The former exact count equality rejected these valid resources.

The new rule checks effective weighted indices rather than demanding identical array lengths. It builds the actual ANI hierarchy and preserves each mesh piece's authored inverse-bind matrices. It does **not** clamp or silently remap invalid weighted indices, and does not invent animated bones for unused entries. Evidence hashes/counts are in `docs/evidence/local-data-multirig-audit.json`.

## Tests and limitations

`dotnet run --project Tools/LocalDataHarness/LocalDataHarness.csproj` tests the pure IO/DDS layer. Unity tests cover weighted-vs-unused tail bones, source bind poses, sampler channel edges and transactional preview replacement. The optional 224-mesh test requires `DREYNOX_CORPUS_ROOT` on the runner. A skipped corpus test must not be reported as a pass.

This change does not decrypt SPK, patch game.exe, copy proprietary DATA to Git, provide a server, or certify complete game parity. The current offline ZIP mounted in this session contains logs and SQLite saves, not a runnable native server. Native-reference screenshots from earlier work are not replaced or reclassified as new captures.
