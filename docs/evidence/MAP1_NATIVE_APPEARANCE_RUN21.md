# Actual native-appearance Map1 Player qualification

Immutable Unity source:0ead9ff71b8c535c4934a9aeca3cafb2349fdf50.
Run36210070334 / Starting Map1 playable client21.
Evidence artifact10896847214,15996469bytes, SHA2560ac0752eacd39079c4318757c33416fa978a5f40dc908c7ebcb13cae1e90620c; downloaded archive hash matches GitHub metadata.

Actual NUnit XML:281tests,281passed,0failed,0skipped. Preparation and Player build succeeded. The standalone scripted Player also passed the original Map1 loop:

- defaultAppearanceVerified/nativeRadarVerified true;
- original humf_torso001/lower001/hand001/boots001/face001/hair001 meshes and their ML2-selected texture sources recorded;
-32physical terrain probes and starter169-vertex sword passed;
-5.4713134765625units walked, camera-relative direction dot1;
-camera yaw180to210through orbit input;
-Ethan opened, mission3400accepted through the actual quest-panel action;
-five distinct original150HPfoxes killed through the current95damage local attack adapter;10attack animation starts;
-delivery through the visible mission action,3000gold and raw5experience reward, duplicate reward denied.

The report explicitly records scripted input and combat relocation. This is not authoritative native damage, levelling, AI or network equivalence. Screenshots were inspected: original default body and distinct original minimap sprites are visible instead of the older costume and solid squares. The HUD and quest layout still have presentation/function gaps.

Packaging failed separately in the old Framework packager. New verified packaging code may repackage this exact Player without editing or recompiling it. Its manifest must keep the Player source/run distinct from the packager source/run. Repacking does not add subsequent quest-paper, observer-integrity, abandonment or vertex-pose-test changes. Those changes belong to later Unity qualification revisions.

Original DATA/game.exe/main are unchanged. No old screenshot is a new render and no source test certifies full native MMO parity.
