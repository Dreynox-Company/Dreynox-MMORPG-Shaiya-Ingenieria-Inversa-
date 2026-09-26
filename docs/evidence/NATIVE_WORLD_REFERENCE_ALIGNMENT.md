# Native world reference is Map 1, not the Map0 integration scene

Recovered source: Shaiya_Offline_Nativo_0_1_2_Windows.zip, archive SHA256 b4a625f9aeb67d702a3c6d661eddee4f8d54942c8e5a2772e9a9735d44dd4ff7.

The original GUI scenario metadata in `pruebas/cliente_nativo_windows/native-created-characters.json` (SHA256 225b0292eb00b03c5e15058eec985a6b3a38082c87adaaf60b437dd4fcc5346e) identifies DreynoxLocal, Map=1, position=(580,78,1760), level=1, mode=2.

The subsequent GUI/keyboard metadata in `pruebas/cliente_nativo_windows_0_1_2/native-created-characters.json` (SHA256 4ac4a16367c4a19d81e6d5332592db211e7903b8fa6ba489f3aa4496b1902157) also identifies Map=1, position=(580,78.68285369873047,1769.87744140625), level=1, mode=2.

These are recovered historical character records associated with the native scenarios, not new client execution. They do not encode the exact camera transform/FOV or the position at every screenshot timestamp.

Therefore the current Map0 integration frames must NOT be paired with those native Map1 world frames for an 'equivalence' or 'graphics improvement' score. Their difference would include different content and locations. Map0 qualification establishes original-content loading, collision/camera behavior, forward walking and local animated combat. Native equivalence is a separate gate requiring the same map, camera, pose and scenario.

Keep the current Map0 build as an integration target. For pixel-level native comparison, either capture native Map0 under a documented matched camera or generalize the preconverted importer to Map1 and reproduce the historical native route, with exact frame-level positioning. A screenshot metric alone is not a percentage of complete gameplay.
