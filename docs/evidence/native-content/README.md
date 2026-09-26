# Original item and skill definitions integrated into Unity

This checkpoint extends the existing Unity client, not Studio/SPK work. It does not certify full game.exe parity, learned skills, native bag transactions or multiplayer gameplay.

## Read-only original evidence

The supplied `Sh.part1.rar` through `Sh.part7.rar` contain `Sh.zip`; the nested archive was opened and its selected original files inspected without modification. Canonical game.exe is5352488bytes with SHA256 `509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d`.

An independent C translation of the repository's existing SEED block operation was used for local read-only inspection. The resulting full plaintext tables contain:

| Original file | Rows | Columns | Original SHA256 | Plaintext SHA256 |
|---|---:|---:|---|---|
|dbitemdata.sdata|28142|70|cdb71e93b0f683b6db4adb9c45df7b1d95c481d3ab0d94092018217ad3afaa28|7a2dffa9655b365e66997e65c0ec88ee44267337e251ddabc0bf8a14c0d8a0c9|
|dbitemtext_spn.sdata|28142|4|f1b1e1129caf8ce95bff310a5bcadabb29fcbd674db0fa2e22819a30216b204b|same as original|
|dbskilldata.sdata|12060|101|b7b99f7e681de8bc26d5cbe74e9ab45fbba0ab79491f8db51cde6732cb53218b|beb148b8363b9147c8a7b2c494a0314c9ca9e78de566060852e876376a3b19d4|
|dbskilltext_spn.sdata|12060|4|f6570b0b80d56cd16d36e3c25e9fe39e69b52011c6b97918d8ac9f69e458754d|same as original|

Skill rows mean804IDs with15ranks each, not12060distinct learned abilities. All numerical fields remain signed64-bit values. Column names are length-prefixed UTF16LE; Spanish text fields use length-prefixed Windows1252. Opaque128-byte prefixes and zero footers are retained for byte-exact plaintext roundtrips. Numeric item/skill footers have2/13bytes respectively. Text joins use native key pairs, not row positions. `Espada Larga` is item1/1; the first musculature-training rank is skill1/1.6144item rows declare icon0 and are retained.

The embedded checksums of the two exact numeric files disagree with the currently used checksum calculation. Their historical cause has not been established. This is not generalized permission to disable checksums: the Editor importer accepts this exception only when BOTH the complete original-file and decrypted-plaintext SHA256 match the explicitly pinned profile. New files/variants require a separate audit. Unity tests rerun the C# decryptor and full lossless joins; local C inspection alone is not a passing Unity test.

## New native skill-icon trace

Read-only x86 disassembly of the canonical executable shows:

-0x4E055A..0x4E05C1 registers ten source sheets, starting with `icon_skill.tga`, then `icon_skill2.dds`, etc.; registrations use512x512textures.
-0x4EAEA5 reads the skill image WORD.
-0x4EAEA9..0x4EAECB computes the thousand-bank quotient and remainder.
-0x4EAECD decrements the remainder and clamps negative values to0.
-0x4EAED6 uses a16-column32-pixel cell grid.

The implementation uses bank=image/1000 and cell=max(0,image%1000-1). It rejects unregistered banks and out-of-sheet cells rather than displaying another skill's icon. The current12060rows use the first three sheets. Item atlas routing continues to use the existing recovered type normalization and one-based icon index. Missing/invalid original icon cases are counted and recorded, not discarded or covered with an unrelated icon.

## Editor conversion and actual runtime integration

`NativeCatalogImporter` consumes the four pinned files with the existing C# decryptor and lossless parsers. It creates native `Items.asset` / `Skills.asset`, preserving every row, original Spanish name/description and numeric field. DDS/TGA artwork is converted/imported once in Editor. Atlas textures are deduplicated, and the Player only reads Unity-serialized assets: no runtime SData parsing or decryption is introduced.

`NativeContentSceneInstaller` binds these assets and original inventory/skill artwork in the real prepared Map1 scene. `I` opens a local inventory view sourced from the SAME durable `QuestJournalCore.Inventory` and Gold, not a second ownership store. Its24visible cells page across all owned types, including more than120types without loss. These are aggregated local types, not authoritative native bag-instance positions/stack splitting. A displayed starter sword is tied to the actual still-equipped starter attachment definition; it never grants an extra bag item. Clicks revalidate ownership and reject stale inventory snapshots.

`F9` is a Development Player/Editor-only catalog browser. It searches and inspects the original item definitions and skill ranks, including all raw numeric fields, with bounded result pages and original atlas icons. It is explicitly labelled read-only. It does NOT learn abilities, cast skills, invent formulas, grant objects, spend gold or fabricate server replies. The standard learned-skills window remains a separate integration requirement.

Original artwork is used at284x564inventory and500x626skill-frame display regions.24bag cells follow the retained6x4grid;22catalog rows reuse the source skill frame. These are source-art/layout measurements and integration choices, not a claim that every native window position, font rasterization or behavior is matched. The developer search controls and raw-field inspector are intentionally additional diagnostics, not represented as original native controls.

Closing, disabling, NPC conversations and quest navigation release this UI's input ownership; inactive search fields cannot keep gameplay shortcuts captured. Search changes remove stale detail panels. Gameplay data is not mutated by catalog navigation.

## Verification and delivery boundaries

New Editor tests cover real original corpus counts/joins/skill-icon bounds and separate synthetic native-asset/UI binding cases. The existing real Player scenario invokes `NativeContentQualification` sequentially BEFORE movement/combat, captures inventory, sword search, full skill catalog and skill details, verifies unchanged journal snapshots and releases input. These new tests are source additions until CI actually executes them; no new pass count is asserted here.

Previous actual run36218737343 / revision24 independently passed303Unity tests and its original Map1 quest/combat/pose scenario. Its final packaging step failed because an executable already existed at the destination; that guard is retained, not bypassed. The next run uses a fresh per-invocation output folder and a per-attempt isolated Unity project, preserving previous executables.

Main, original DATA/game.exe/configuration, Flutter and Studio are not modified by this integration. Native network session, real bag transactions, learned-skill execution, authoritative stats, full social interfaces and all-map gameplay remain separate uncompleted parity work.
