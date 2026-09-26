# Native merchant evidence and local buy/sell integration

Scope: original NPC stock/artwork/template price fields, integrated into the existing LOCAL Unity journal. Not native server authority, complete inventory instances, repaired durability, discounts/taxes, premium currency or full game.exe parity. Original DATA, game.exe, main, Flutter and Studio remain unchanged.

## Inspected baseline and preparation failure

Run25 `36225504557`, source `d921fa0331ee1cb9c25e6b6a8922b93c99f63436`, evidence artifact `10900724467`: 1,203,187 bytes, SHA256 `2cb161c68df4378e0b5903cb5a72de85ec9d1cc5a3c17bb05fa7bdecc3375e19`. Downloaded ZIP hash matched. Its NUnit XML contains **323 passed, 0 failed, 0 skipped**. Scene preparation failed at native item/skill atlas conversion after 2,550 item icons, reporting an invalid DDS header. Build, Player gameplay and packaging did NOT run.

Read-only inspection of all 86 `.dds` files in the original icon directory found 77 real DDS and nine **24-bit RLE TGA payloads named .dds**: 11,17,18,20,21,32,33,35,36. All are128x512. Independent Pillow decoding succeeded for all86. This is not a new Unity render or a substituted texture. The 19 original HUD textures audited earlier were not a complete audit of this separate catalog-atlas set.

`LegacyImageSignature` now detects bounded true-color TGA and validates raw/RLE payload sizes before import. `NativeCatalogImporter` chooses a generated file extension from actual bytes, imports unchanged TGA copies, and continues to use the strict DDS decoder for DDS. It logs source hash/encoding for each atlas. No source file is renamed, repaired or rewritten. Tests include all nine actual Unity TGA imports and a **whole original catalog import**, so count-only/parser tests cannot miss this dependency again.

## Read-only native executable analysis

Canonical original executable:5,352,488bytes, SHA256 `509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d`. x86 static disassembly at PE image base0x400000, not native runtime tracing:

| Native function | Opcode | Bytes at common send boundary | Observed body |
|---|---|---:|---|
|0x694D70|0x0702|12|opcode2; DWORD at2; BYTE at6; BYTE at7; DWORD at8|
|0x694DE0|0x0703|13|opcode2; BYTE at2; BYTE at3; BYTE at4; DWORD at5; DWORD at9|

Both call0x693880 (buy call0x694DC2; sell call0x694E3B). `NpcBuyItemPacket.cs` in pinned emulator `aosyatnik/Imgeneus@0ce355594d521c3a06a08f24d0a9b60ebc8a459f` corroborates the buy prefix as NPC instanceID, stock index and quantity. It does **not** consume the trailing DWORD observed in this executable. That disagreement is recorded, not erased. Trailing buy DWORD and the full sell-field semantics remain unclassified here. No network bytes are sent and no server ACK is fabricated by this local implementation.

Original market loader references: `BasicShop/market` at VA0x866740, referenced at0x5E319C, load at0x5E31A3; `BasicShop/market_sell` at0x866808, referenced at0x5E4BC4. Artwork was read from the user's multipart Sh archive:

- `basicshop/market.tga`:76,700bytes, SHA256 `9fccb64db0c01943a7e5e5b74eaffd1661db73b075d093269a676bd6248a7fc3`.
- `basicshop/market_sell.tga`:81,577bytes, SHA256 `de79f0f7dfa803f1d60d24aba54f57e2af4dd1ab9c1918b8fbb4b0cf9a0e04b3`.

Buy/sell crop sizes256x355 and256x368, 6x5 icon grids, and top-left icon origins/strides (15,71)/(39,39) and(18,42)/(38,38) are **source-image measurements**, not claimed recovered native window coordinates. Quantity confirmation, tabs and local scope labels are explicit Unity integration choices. The original quantity glyph atlas is not misrepresented as a dialog background.

## Coherent local gameplay implementation

The existing NPC/quest selector now offers a functioning merchant service. It routes to `LocalMerchantPanel` without throwing away the original selected NPC context. Raw sale-list positions, including unresolved/sentinel entries, remain aligned. This local implementation enables ordinary category0..5 merchants; special categories require separate integration. Original template `buy`/`sell` prices are not claimed to reproduce server price modifications. Non-gold purchase methods, timed/individual-state item templates and missing prices remain blocked with a reason.

`LocalMerchantSession` issues non-mutating price/quantity quotes tied to the journal revision and selected NPC lifetime. Confirmation calls `QuestJournalCore.ExchangeLocalItem`, which persists gold, item counts and collection-quest readiness **in one existing journal snapshot**. There is no duplicate wallet or inventory. Save rejection/exception leaves both sides unchanged, and permits explicit retry of the same unchanged quote. Repeated, cancelled, foreign-session or stale confirmations cannot spend or pay twice. Checked arithmetic prevents overflows and negative balances.

The UI supports buying, selling owned types, quantity1..255, paging, original catalog icons/names, actual gold/counts, confirmation/cancel and input ownership. Distance, line of sight, current selected NPC and a local pooled-lifetime generation are revalidated at click time. An NPC GameObject reused at the same location is not the previous conversation. Closing/invalidating the service releases both merchant and conversation input as appropriate. Inventory and quest views observe the same changes; collection items bought/sold update readiness through normal journal rules.

Mission objective/reward names now use the already imported Spanish item catalog rather than raw type/ID labels. Counts reflect actual local ownership.

## Verification designed for this change

Standalone MerchantHarness checks atomic purchase/sale, duplicate/reentrant/cancelled/stale quotes, insufficient funds/items, invalid prices/quantities, persistence failure/retry, overflow, and collection-quest buy/sell/turn-in integration. Unity tests exercise the actual merchant-service/confirm buttons, original-sized grid layout, modal cancellation, stale NPC/position/obstruction, shared inventory and input cleanup using labelled synthetic art/data.

The real Player scenario runs merchant qualification **after** earning the original quest reward. It selects an accessible, affordable original Map1 merchant, buys two source items, sells one, checks exact journal revision/gold/count changes, inspects the live inventory and rejects duplicate confirmation. It seeds no extra gold or items. NPC relocation and scripted UI input remain explicit. Native world traversal, authoritative economy, stack splitting, buyback and server packet/session equivalence are not inferred from this local test.

All new runtime, Editor, gameplay and executable-delivery results must be recorded after execution. Source additions or Python/Pillow audits are not passing Unity/Player evidence.
