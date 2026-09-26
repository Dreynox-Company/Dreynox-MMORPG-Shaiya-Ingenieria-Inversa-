# Native HUD pixel contract — implementation checkpoint, not complete visual parity

## Read-only native sources inspected

Canonical `game.exe`: 5,352,488 bytes; SHA256 `509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d`. Disassembly below is x86 at image base0x400000, inspected from the user's original uploaded archive. Original TGA files were read from `Sh.part1.rar` through `Sh.part7.rar`, nested `Sh.zip`, `DATA_Español/interface`. `atlas.json` records their individual sizes, hashes, dimensions and nonzero-alpha bounds. No original asset, executable or configuration was edited.

The native screenshot inspected is `Shaiya_Offline_Nativo/pruebas/cliente_nativo_windows_0_1_2/10-world-loaded.png`. It includes Windows non-client borders/titlebar: do not treat its whole1024x768 bitmap as a same-size Unity viewport or compute a misleading full-frame pixel error.

## Direct binary evidence

| Contract | Native evidence |
|---|---|
| Actual horizontal/vertical art | `Slot/main_slot_1.tga` at VA0x860B80, load0x5955A2..0x5955AE; `Slot/main_slot_2.tga` at0x860B98, load0x5955BA. The prior `slot/main_1.tga` was a different skin. |
| Frame dimensions | 446x53; constructor immediate values0x59555B/0x595562. Vertical swaps the dimensions. |
| Cell count and geometry | Ten cells, constructor0x595504. Horizontal origin24,7 and stride40 at0x596EE4..0x596F06; vertical swaps axes. Original action atlas cells32x32. |
| Page limits | Index0..4, branch clamps0x59663E..0x596692. Three configurable bar instances occur in native configuration paths, not ten different native skills. |
| Control coordinates | Straight-line initializer0x598600..0x598761: horizontal previous6,8; next6,32; expand421,5; rotate421,25. Vertical previous7,6; next31,6; expand25,421; rotate5,421. |
| Default horizontal position | Integer viewportWidth/2 minus223, y0 in0x595535..0x595544. Per-resolution placement/orientation keys occur near0x597854. |
| Font family | ASCII `arial` at0x858354; creation call sites0x49D66B/0x49D6A5/0x49D6D3/0x49D6F7. Nearby12/13pixel and normal/bold configurations exist. This does not prove Unity rasterization matches D3DX pixel-for-pixel. |

`decoded-field-initializers.json` retains a bounded read-only decode of immediate geometry assignments, including status fields. Unnamed offsets are not all labelled as fully understood widget semantics.

## Integrated in the existing Unity HUD

The pixel canvas uses constant1:1units, not reference-resolution enlargement. Native player frame218x64, class41x44, source150x8resource strips, small Arial text, original native quickbar atlas and original multi-state buttons replace the earlier stretched/basic presentation. No OS font file is packaged or redistributed; an explicitly reported fallback is used when Arial is absent.

The quickbar has ten clickable/keyed cells, five pages, horizontal/vertical orientation, two optional extra views, revision-checked drag/swap, and atomic local preferences. Mouse/keyboard dispatch converge on one existing local basic attack, at most once per frame. Empty slots do not execute invented95/130/180/240damage 'skills'. The recovery overlay reads the actual combat state machine rather than a second timer. Native animation/damage equivalence and a full learned-skills palette remain outside this checkpoint.

Window dragging uses integer coordinates, viewport clamping and per-resolution UI-only storage. Bad/foreign preferences are not silently overwritten. Esc cancels a top quest-abandon confirmation before closing the parent journal; scroll/drag/UIhover do not move or zoom the game camera. Slot dragging is distinct from window dragging. Diagnostics are hidden behind F10 instead of occupying the player name/status panel.

The NPC selector342x229 and quest paper256x512 are separate presentations, rather than a stretched580x592combined substitute. Original Spanish quest strings, acceptance, delivery, stored rewards and confirmed abandonment remain connected. Original red button states and scroll artwork are used; missing services are still labelled as not integrated. Text margins, 3pixel nine-slice command-button borders, tooltip placement and initial secondary-window positions are declared implementation choices, not recovered native constants.

Unknown player HP/MP/SP stay explicitly unbound; the screenshot's255/95/180values belong to its reference actor and are not copied into a fabricated live actor. The view accepts real supplied values, but native stats/levelling/retaliation are not invented to fill bars. Chat, bottom menu, full inventory/skills/social windows, original world-name styling and authoritative multiplayer mechanisms remain incomplete.

## Verification scope

`NativeUiHarness` checks geometry, pages, input deduplication, reentrancy, stale moves, atomic saves, invalid preferences and actual combat recovery fractions. `UnityRuntimeContracts.csproj` compiles all runtime source against hash-verified actual Unity6000.0.64f1assemblies from the immutable qualified21Player, without stubs and without executing the Player. This is API/source compatibility, not Unity Editor import or rendering. The pinned artifact is an expiring audit checkpoint: a replacement must be requalified, never selected silently by a latest-artifact query.

Unity Editor tests exercise original-dimension geometry using clearly synthetic art, real page/rotate/additional-view button callbacks, pixel policy, sprite states, empty slots, input release and quest escape/selector transitions. The full Map1 qualification adds actual native-art page5, vertical and additional-bar PNGs and `.ui.json`measurements, while preserving the real quest/fox/reward scenario. New tests/capture code must actually execute before reporting results. No pass count or new screenshot is presumed in this document.

Primary API references: Unity6000.0Canvas.pixelPerfect and Font.CreateDynamicFontFromOSFont, official uGUI CanvasScaler documentation. Native binary and user assets above, not an unrelated skin or mockup, supply the recovered coordinates.
