# Quest integration source notes

## Provenance and base

Prepared on the exact source snapshot a92c222169d07e23ff6b58db998f459f6dd47a36. The newer Map1/ground placement/native HUD/batch writer changes were retained. This source bundle is not a remote commit and has not been compiled or run in Unity here.

The prior 254310d4 enhanced build's passing tests must not be attributed to these new scripts. The independent executed report is QUEST_MAP1_CORPUS_AUDIT.json. The new QuestHarness/EditMode/Player qualification are pending execution.

## Format evidence

The inspected plaintext NPC header ends at byte 98637. Its 65,536 item link pairs end before the quest count at byte 627305. Records start at 627309: 4085 * 287 bytes, exact EOF 1799704. The Spanish NPC translations end at byte 219081; the following count and 12 length-prefixed strings per quest consume exact EOF 4443121. Canonical digests are in the audit JSON.

Public Parsec's layout was used as a reference for named fields, not copied as a runtime dependency or presumed to fit the file without inspection. Its older three-value QuestFaction enum does not match this corpus's seven-value distribution. Local eligibility mapping remains explicitly provisional.

Primary source references (inspected commit):
- https://github.com/matigramirez/Parsec/blob/e5cbc6d72367a4796bad080e4b34edf3c9cc96b8/src/Parsec/Shaiya/NpcQuest/NpcQuest.cs
- https://github.com/matigramirez/Parsec/blob/e5cbc6d72367a4796bad080e4b34edf3c9cc96b8/src/Parsec/Shaiya/NpcQuest/Quest.cs
- https://github.com/matigramirez/Parsec/blob/e5cbc6d72367a4796bad080e4b34edf3c9cc96b8/src/Parsec/Shaiya/NpcQuest/QuestResult.cs
- https://github.com/matigramirez/Parsec/blob/e5cbc6d72367a4796bad080e4b34edf3c9cc96b8/src/Parsec/Shaiya/NpcQuest/QuestItemLink.cs

## Integration boundaries

Local quest progression receives actual scene combat death notifications. Duplicate protection keys the pooled object's instance ID plus generation and keeps a bounded receipt window. Prospective state is persisted before in-memory mutation; a failed save does not grant a reward. Save files are separate from native databases. The local journal is not anti-cheat/server authority.

The native field EXP is retained numerically. No invented leveling table is introduced. The existing diagnostic attack damage is not renamed a native skill. Collection objectives have a transaction API but no fabricated loot source. Unsupported quest predicates fail closed while their original text remains inspectable.

## Import performance

Color import is now two-phase: generate unique files; batch import; configure import settings; batch reimport; only then load the assets. No AssetDatabase.LoadAssetAtPath/SaveAndReimport is intentionally used while this new color batch is paused. The existing LegacyAssetWriteBatch remains the world import transaction mechanism. Prepare and Build are independent Unity processes with source/dependency stamps. Timing improvement awaits measurement on the pinned Unity Editor.

Official Unity batching documentation:
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.StartAssetEditing.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.StopAssetEditing.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.WriteImportSettingsIfDirty.html

## Required acceptance evidence

1. New C# harness and actual-corpus EditMode cases pass in the pinned environment.
2. Real Map1 prepares and builds; no fallback to capsule or Map0 Player.
3. Actual Player screenshots + report show names, NPC dialog, walking, authored attack/death, five objective credits, saved original reward.
4. Native-vs-Unity comparison uses identical map/position/camera/task. Enhanced lighting differences are reviewed separately from content/behavior.

Without those artifacts, this delivery is source implementation, not 100% game parity.
