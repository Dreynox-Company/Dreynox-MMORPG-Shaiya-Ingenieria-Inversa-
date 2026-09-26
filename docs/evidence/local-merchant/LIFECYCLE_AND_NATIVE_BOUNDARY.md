# Merchant lifecycle and exact native request boundary

## Starting point and compile repair

The continuation audited branch head95ab8360ae8c665ae40c8b68a3454ea3264ecfcc and actual failed Map1 run36228636730 (#26). That run stopped at C# compilation: the new merchant code referenced NpcServiceKind.Merchant, but the established serialized capability is Shop. Commit4540cfb83a5336201dab464d6b9d70b1e9e6ef50 repaired four runtime references and changed the fixture to use the actual NpcServiceResolverCore.Resolve(1,false), without adding aliases or changing enum values. Source and real-Unity-assembly compatibility gates passed for4540cfb; its actual Windows Editor/Player checks are separate.

The source artifact10901845694 was downloaded:714622bytes, SHA25634de908327938f45128b555a8c049887d6753564eef4383cc6309184e7c2d215. Inner client-source.zip hashfb078ad0c6aae62170a285019879bd036009d0551415b44f45f7235fd77a82bc; commit marker matched95ab836. New changes extend that audited source on the same work branch.

## Actual integration corrections

LocalMerchantPanel now snapshots its immutable submitted session/quote before calling the durable journal. Persist/Changed callbacks can close the UI, disable it or begin another conversation synchronously. A saved purchase must still return success, with no null-reference error, reopened stale window or duplicate retry. A failed save remains a failed purchase with unchanged gold/items even when the same callback closes the view. The durable journal remains the only wallet/inventory.

While a transaction is committing, callbacks cannot replace its merchant, buy/sell mode or selected item. Closing is always allowed. A new HUD conversation closes only the old store and its quote; it does not clear the newly selected NPC. The selector returns the real provider result rather than claiming a shop opened after a rejected request. Disabled and ambiguous providers fail without opening a shop.

Invalid requested quantities clear previous payable quotes, including a1000input which must not be truncated into a different100quantity. Selecting stock/owned items updates the corresponding original buy/sell artwork and page before displaying its quote. The confirmation panel is centered on the active viewport through anchors rather than an old absolute screen offset.

Seventeen new actual-component NUnit cases reuse the existing real panel/journal/NPC fixture. They cover successful/failed persistence with view closure, observer reentrancy, invalid quantities, buy/sell/page consistency, actual routing rejection, new conversations, ownership isolation and viewport resize. These cases are not claimed passed before their own Unity execution. They do not establish all native gameplay or full UI equivalence.

## Further direct game.exe inspection, not emulator assumptions

Read-only extraction of cliente/game.exe from the supplied Shaiya_Offline_Nativo.zip reproduced the canonical5352488byte executable with SHA256509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d. The program was NOT executed or patched. x86 disassembly confirmed the previously identified send functions and, additionally, their actual callers:

- Buy sender0x694D70 writes opcode0702, length12. Caller0x5E4168 pushes NPC field from merchant+0x550, stock index, quantity and a DWORD read at0x5E4159 from global0x91FE3C.
- Sell sender0x694DE0 writes opcode0703, length13. Caller0x5E42A3 pushes two source-location bytes, quantity, the same merchant+0x550 NPC field and the same global0x91FE3C word. Its source lookup at0x5E418C..0x5E419B indexes records as(firstByte*24+secondByte)*132, supporting bag/slot interpretation.
- The shared global is loaded from an object WORD at+0x92 at0x6444E7..0x6444EE. Its exact domain meaning is not fully established here. It is retained as an explicit opaque ConversationWord, not replaced with a calculated price, zero, a static service key or the local journal revision.
- The previous offline emulator discrepancy remains explicit: an eight-byte buy prefix is insufficient to represent this executable's complete twelve-byte send body.

Tools/ReverseEngineering/verify_merchant_boundary.py was executed locally against that exact original file and verified21instruction anchors plus4direct call targets. It checks the entire executable hash first and refuses other variants. This is reproducible static evidence, not a native network trace or proof of every reachable call.

NativeMerchantRequestCodec preserves all twelve/thirteen body bytes with exact little-endian offsets and rejects truncated, wrong-opcode or extended records. The added standalone suite covers golden byte vectors, all truncation lengths, widths, source immutability and200generated roundtrips. No socket, transport header, encryption, fake server ACK or authoritative price formula is introduced. The local journal is deliberately not wired to raw native request bytes until a real session, identity mapping and response validation exist.

Original DATA/game.exe/configuration, main, Flutter and Studio remain unchanged. The previously delivered EXE does not gain these changes without a new compiled and qualified Player.
