# Rendered combat qualification, not numeric-only HP testing

The actor requested `attack`, but the imported corpus catalog defines `attack_1` through `attack_4`. Numeric hit tests could pass while the player did not animate a strike. The actor now starts `attack_1` once for each accepted CombatCore.AttackSerial. Held movement still uses idempotent PlaySemantic; ReplaySemantic is explicit for new one-shot actions.

The local actor lets the imported attack cycle finish before recovery ends. The canonical humf one-hand attack files have 26/30 seconds of authored animation. Impact time 0.18 seconds and diagnostic damage/reach remain local qualification defaults, not a claim of server/game.exe equivalence.

MON damage/attack reactions now replay on new events and return to idle when the clip ends. Death is not returned to idle. Reused pooled entities reset reaction state. The combat clock consumes remaining frame time across phase boundaries; synthetic 30/60/144 Hz checks cover hit/recovery/guard timing and rejected repeated input.

The real Map0 scenario requires original skinned meshes, the actual attack clip, forward displacement, real spawn HP changes, at least one authored strike playback and the MON `dead` semantic. It saves start/walk/attack/death frames and camera metadata. Combat relocation is explicitly reported. These are local integration captures, not automatically matched native-reference captures or proof of graphics superiority.

Source changes are staged without an additional redundant Windows sandbox job; the following workflow-only qualification revision tests this exact source using the real local corpus. Five new EditMode cases and 15 standalone combat checks are added; passing status must be taken from subsequent CI evidence.
