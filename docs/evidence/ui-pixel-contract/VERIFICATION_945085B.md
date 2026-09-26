# Native-art UI checkpoint — independently checked source, logic and Unity import

Implementation commit: `945085b255675d2376a47fba71b28ce3e3df9f93`.
Parent: `4c8de91620dc139e491b7ddc9e82e9c0bd056a94`.
Branch: `work/unity-client-parity-v0.4.0`; PR4. 32 tracked files changed against the parent source archive.

## Actual checks completed

Native UI contracts run `36218737362`, job `108339839319`, completed successfully. Its downloaded log records:

- Project validator:381files checked (not a count of gameplay behaviors).
- Native UI harness:93passed logic/filesystem checks.
- Combat timing harness:15passed checks.
- Quest journal harness:31passed checks.
-92actual Unity managed DLL hashes verified against immutable qualified21Player artifact10896444373.
-All current runtime C# compiled against those genuine Unity6000.0.64f1assemblies without stubs; build succeeded with zero errors. This compatibility project suppresses selected obsolete/unassigned-field warnings and is not a claim that the complete Unity project has no warnings.

Verified executable delivery tests run `36218737336` also passed both Linux archive-security and actual Windows Framework extraction/cache/tamper jobs. These use explicitly synthetic delivery fixtures, not a new game build.

Starting Map1 run `36218737343` / revision24 passed exact-source delivery-script parsing, corpus checks, Unity entitlement, actual graphics-import preflight and the Unity Editor test step. At this checkpoint the original Map1 scene preparation is in progress. The Editor step has a successful Actions outcome; its NUnit XML has not yet been downloaded, so no exact Unity test count is claimed here. Player build, rendered screenshots, gameplay qualification and one-file publication are still separate pending evidence.

## Independent archive/source/native checks

Client source evidence run `36218737340`, artifact `10898430797`:
- Outer ZIP:650690bytes, SHA256 `4ca7d91dc37b69e086498f515459c14b225c4416dc6829cefa156ba13c1267ae`.
- Inner `client-source.zip`: SHA256 `47598373268292cbcc69fb6494a3f476f0f62e9e328b950753c20a8775582314`, matched the workflow's separate SHA256SUMS.
-`commit.txt` matches945085b exactly.
-Downloaded runtime, editor and test implementation bytes match the reviewed local implementation. The intentional remaining differences are documentation, JSON formatting, workflow revision note and the added delivery-source preflight script.

The canonical game.exe hash and all19texture records in atlas.json were rechecked against the original uploaded files: file lengths, SHA256, dimensions and alpha bounds agree. No native executable was run, patched or replaced for this read-only audit.

The archive's inherited tracked `SOURCE_SHA256.txt` is an older baseline, not this revision's source-archive checksum. It is deliberately not used to certify this checkpoint. Use the immutable commit and the separately generated workflow archive digest above.

## Scope boundaries

Implemented source includes native quickbar skin and pixel geometry, five pages, three views, drag/swap and atomic local preferences, original multi-state controls, actual combat-state cooldown, compact separate NPC/quest presentations, original scroll art, modal Escape ordering, original installed Arial family with explicit fallback, and actual UI-geometry capture code.

This is not a statement of pixel-identical rendering to a matched native-client screenshot. Native Arial/D3DX rasterization, world-name styling, complete chat/bottom menus, inventory/skills/social windows, authoritative player vitals and native multiplayer mechanisms remain unqualified or incomplete. Unknown vitals are not filled with copied screenshot values; empty quickbar cells are not presented as working skills.

The previously delivered qualified21EXE remains an older Player and does not contain this UI revision. No new playable download is certified by this document. New `.ui.json` measurements and page5/vertical/additional-bar PNGs must come from revision24's actual Player before visual claims are made.

Main was re-read at `ebb3a2c3d01d6f3a2bb53a120b576e16482fa166`; this UI work does not modify main, original DATA/game.exe/config.ini, Flutter or Studio.
