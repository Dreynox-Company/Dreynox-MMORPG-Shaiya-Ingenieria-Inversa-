# Offline backend source provenance

The mounted user-supplied Shaiya_Offline_Nativo.zip was inspected read-only. Its service debug symbols contain SourceLink references that identify the upstream backend revision:

`aosyatnik/Imgeneus@0ce355594d521c3a06a08f24d0a9b60ebc8a459f`

Local extracted symbol checksums:

- Imgeneus.Game.pdb: 240892 bytes; SHA256 b358f1a1d6bf50b0df9f374a34f865767ae930c1bfee3755565fedbd253b41f2.
- Imgeneus.World.pdb: 210716 bytes; SHA256 ae37a6b10e3a866bd952d357b42b170469a410c51d04150633214449b04bd0ec.
- Imgeneus.Network.pdb: 78692 bytes; SHA256 c983368cccf1901f161d8f2197dfdc92c0a69fbd74aa6e7dff627a5f561ad606.
- Imgeneus.Authentication.pdb: 25128 bytes; SHA256 bbb274a038c8e526fb774a5ca555066cc02de57a7e19fee83ba2130ad2bf9667.

The source map also pins LiteNetwork at 118bc18cbe47a1a51a7ccb92a7cab49b55a41613, Parsec at ebac92c473175a5c0aae29c1e370a2a299d1dedc and the authentication submodule at 77b3a65e9a0ba60f83e1aa452e9f0b997ea27a70.

The public source was read at the pinned commit, not at current main. `src/Imgeneus.Game/AI/AIManager.cs` has blob b568eecd12a48187119172f229f07b5ff9478b90. It exposes backend-owned idle/chase/attack/return state, aggression selection, health recovery and movement notifications. These are backend behaviors to compare when replacing local fixed combat defaults; they are not evidence that the Unity client already implements them.

Important boundaries:

1. SourceLink is provenance metadata, not a proof that all compiled bytes exactly match upstream or that every path is exercised by the offline fixture.
2. The offline emulator is not the proprietary game.exe implementation. Do not label its defaults as decoded native client constants.
3. Existing Unity diagnostic damage and impact timing remain local defaults. Do not silently present 95 damage or the current single-player quest loop as a completed authoritative network game.
4. No upstream source code, server binaries, credentials, original DATA or account databases were copied into this repository by this audit. Any future source reuse requires checking and preserving its license terms.
5. Proper social/trade/guild and map transfer validation ultimately require a shared authoritative session, not independent client-only counters. The current read-only protocol inspection is preparation for that integration, not its completion.

Primary source reference: https://github.com/aosyatnik/Imgeneus/blob/0ce355594d521c3a06a08f24d0a9b60ebc8a459f/src/Imgeneus.Game/AI/AIManager.cs
