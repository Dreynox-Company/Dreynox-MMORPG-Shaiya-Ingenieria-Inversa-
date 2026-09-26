# Native quest send bodies and local abandonment integration

Canonical original game.exe: 5352488 bytes, SHA256 509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d. All addresses below are image-base0x400000 virtual addresses from read-only static disassembly. No native execution or packet capture is claimed.

## Recovered native send boundary

All four functions write stack bytes then call common send0x693880 with ECX length and EDX body pointer:

| Function | Opcode | Body length including opcode | Fields after little-endian opcode |
|---|---|---|---|
|0x695510|0x0902|8|32-bit NPC instance ID, 16-bit quest ID|
|0x695570|0x0903|8|32-bit NPC instance ID, 16-bit quest ID|
|0x6955D0|0x0907|9|32-bit NPC instance ID, 16-bit quest ID, 8-bit choice|
|0x695630|0x0908|4|16-bit quest ID|

0x582388 calls the acceptance function. 0x5D31F8 calls abandonment after checking nonzero quest ID at0x5D31E7 and a UI helper result of1. The exact helper class/confirmation text remains unqualified. The binary body widths/offsets are directly observed, not guessed from enum names. A pinned offline-emulator packet definition independently agrees with these field orders, but its source is not copied here.

NativeQuestRequestCodec implements only those exact send bodies. It neither prepends the generic lab length framing nor pretends to establish authentication, encryption, NPC identity mapping or response handling. NPC instance ID is not the static (NpcType,TypeId) content key. Sixteen-bit quest IDs preserve wire bits; byte choice range is not narrowed to invented rules. The golden fixtures, roundtrips, truncated/trailing cases and misuse checks total21 standalone assertions.

## Actual local-client mechanism

The existing local journal already supports persisted abandonment. QuestWorldPanel now exposes it only for an active journal selection with a separate confirmation panel. Cancellation, closing, changing selection, completion elsewhere, and a stale second confirmation cannot remove a different/rewarded mission. Failed persistence keeps both the quest and the explicit retry available. No gold, experience or source inventory reward is issued by abandonment. Four Unity interaction tests cover these cases.

This is a local-client action using its existing durable journal, not a fabricated native-server reply. Network request codec and local gameplay are deliberately not wired together until the authoritative session and response contracts exist. Original DATA, native executable, main, Flutter and Studio remain unchanged.

Supporting primary provenance: aosyatnik/Imgeneus@0ce355594d521c3a06a08f24d0a9b60ebc8a459f src/Imgeneus.Network/Packets/Game/QuestStartPacket.cs and QuestQuitPacket.cs, plus PacketType.cs. Static native call sites are the source of the implemented body layouts; future network integration still needs real session traces.
