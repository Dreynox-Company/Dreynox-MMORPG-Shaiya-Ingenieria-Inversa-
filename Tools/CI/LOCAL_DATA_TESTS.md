# Local DATA qualification

The standalone local-folder character loader is covered at three levels:

1. `dotnet run --project Tools/LocalDataHarness/LocalDataHarness.csproj --configuration Release`: 15 synthetic DDS and filesystem checks.
2. `pwsh -NoProfile -File Tools/CI/Test-CharacterParityDiscovery.ps1`: deterministic reproduction of inaccessible-path discovery.
3. Unity EditMode `LocalDataPipelineTests`: source inverse-bind matrices, weighted-bone bounds, sampling and optional all-rig corpus verification.

The local-folder Player is built by the existing `DreynoxWindowsBuild.BuildParityBatch` entrypoint, beside the parity-lab Player, at `Builds/WindowsParity/LocalData/DreynoxMmorpg-LocalData.exe`.

This build is a character-loading qualification executable. It does not claim complete world loading, networking or full game.exe parity. Preserve the whole Player directory beside the executable.

The source update was first validated with 129 existing deterministic gameplay checks, all 15 new local-data checks and the discovery regression. Real Unity/Windows compilation remains a separate gate.

See `docs/LOCAL_DATA_PLAYER.md` and `docs/evidence/local-data-multirig-audit.json` for the corpus evidence and supported scope.
