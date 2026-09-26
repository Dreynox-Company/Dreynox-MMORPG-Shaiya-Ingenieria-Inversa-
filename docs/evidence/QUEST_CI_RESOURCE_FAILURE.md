# Quest integration CI resource qualification

Run 36153340306 compiled the complete quest source and executed all 175 EditMode tests with the user's real DATA. 174 passed, 1 failed: BatchingCreatesNativeAssetsAndSubAssetsBeforePrefabReferences. The same error was present in the previous a92 baseline and persisted without StartAssetEditing in the native-object batch. Therefore suspending native creation was not the sole root cause.

URP global settings population repeatedly found two installed package paths whose objects could not be loaded: AutodeskInteractiveTransparent.shadergraph and TraceRenderingLayerMask.urtshader. Their first errors occur during initial imports, before the test body. The next revision explicitly qualifies those installed-package imports outside any test or asset-postprocess callback and reports actual disk location, bytes, GUID and loaded type. It fails if a required object remains unavailable. No LogAssert suppression, ignored tests, removed functionality or synthetic substitution is introduced.

The original-corpus quest tests, journal regression and all deterministic source harnesses already passed in this run. Actual Player build and NPC/mission/combat execution did not run because the resource gate failed. Their success is not inferred from the source tests.
