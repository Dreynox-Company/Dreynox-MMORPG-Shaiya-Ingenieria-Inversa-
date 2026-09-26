# Inspected actual Map1 quest loop — run19

## Immutable source and downloaded evidence

- Run: 36204716763 / Starting Map1 playable client #19.
- Source: d69ccf65fe2721f0b4f3cf4088d5a81f37f88d12.
- Artifact: 10895870581, Dreynox-StartingWorld-Evidence-19, 16,189,432 bytes.
- Downloaded ZIP SHA256: 2b1cea15184604ab993a890978a0a492b3b39bc8824945c7dff9a952fa1be65d, matched the Actions artifact digest.
- NUnit XML: 269 tests, 269 passed, 0 failed, 0 skipped.
- ci-result.json: tests=success, prepare=success, build=success, play=success, package=failure.

The JSON and PNG files inside the artifact were extracted and inspected. The packaged one-file executable failed as a separate delivery step; do not present the overall workflow as fully green or claim a ready-to-run delivery from this checkpoint.

## Actual Player evidence

`Artifacts/StartingWorld/run-20260925-210651/starting-world-qualification.json` records:

- passed=true; failure is empty; mapId=1; questId=3400.
- entry=(580, 77.69284057617188, 1760).
- walking displacement=5.5572509765625 units; direction dot=1.0.
- camera yaw before=180 degrees; after=210 degrees.
- originalEthanOpened=true; questAccepted=true; questDelivered=true.
- npcPositions=307; monsterInstances=1186 (logical positions, not all rendered at once).
- terrainCollisionVerified=true; terrainProbes=32.
- starterWeaponEquipped=true; 169 vertices; original item 1/1, IT2 image0, HUMF hand binding.
- five original fox targets (1000126, 1000127, 1000338, 1000337, 1000128), each from150 HP to0 through the combat adapter.
- attackAnimations=10.
- rewardGold=3000; rewardExperience=5, as stored in the quest definition. This is not a native experience/leveling implementation.

The captured dialogue displays the original quest text. The fifth-fox frame shows target health0/150 and a quest ready for delivery. The reward frame and persisted report agree on delivery and recorded reward. The existing test also rejects duplicate delivery.

## Necessary scope distinctions

This is scripted local integration, with explicit relocation near NPCs and foxes. Damage95 is a local diagnostic value. It does not establish native combat formulas, enemy retaliation, a continuous walked route between objectives, server authority, guild/trade or multiplayer parity. Animation action playback is counted; a separate pose/deformation capture test remains desirable.

The captured appearance is the OLD costume and the radar still contains old solid markers. Run19 predates the ML2 default-body, corrected texture-V, source radar sprites and target-bar changes in 10cd6d48 / qualification0ead9ff7. Do not show these PNGs as screenshots of the new appearance/radar revision. Their purpose is gameplay evidence, not proof of visual equivalence or improved character rendering.

The current screenshots also expose unfinished UI (player vitals, quest-panel proportions, title spacing and reward-state presentation), which must not be hidden by calling the entire interface complete. Preserve the real progress and the visible limitations together.
