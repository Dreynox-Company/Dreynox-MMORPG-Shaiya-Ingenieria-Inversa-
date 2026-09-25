# Playable Map0 qualification — work branch only

## Verified problem evidence

Read-only runner audit `36095000122` found `C:\Juegos\Dreynox\DATA`. WingPosition, Login.wld, 0.wld, 0.svmap and humf_019_select.ani match the supplied multipart corpus hashes. No native game.exe was found directly beside that DATA. This does not prevent import of verified asset content, and must not be reported as native executable verification.

The previous Unity failure was recovered from its actual NUnit XML: 106 tests, 89 passed, 1 failed, 16 skipped. The failing fixture tried to create an additive Editor scene while an untitled scene was unsaved. It was not established to be a licensing or renderer failure. The test now uses an isolated preview scene without replacing/saving the user's scene.

## Integration changes

- DATA aliases resolve centrally for Editor imports as well as the developer loader. No rename, junction or corpus write is needed.
- Content fingerprints and native executable identity are separate. A present, mismatching game.exe is still rejected.
- Unity camera yaw is converted to the recovered legacy movement convention exactly once; forward movement is tested at four camera angles.
- Camera excludes the player's own collider and retracts immediately at walls; outward recovery is smoothed along the camera ray.
- The local combat adapter checks reach and line of sight at request and impact, and validates the generation of pooled targets.
- Wounded/dead mob state survives streaming; streaming does not silently heal or respawn a mob.
- The Map0 qualification build adds the missing live scene combat adapter and packages real original assets, not the capsule sandbox.
- An opt-in Development Player scenario moves the actual CharacterController, captures the world, relocates to a real spawn (explicitly recorded), applies normal local attack requests and verifies damage/death. It does not force health to zero.

## Honest boundaries

The local damage/reach/timing defaults are not certified ps0032 server combat rules. The native screenshots are historical reference evidence, not proof of new native execution. A successful parser, synthetic test or Windows build is not visual parity. Claim a playable world only after the actual Player produces its runtime report and captures. The final release remains preconverted; external DATA selection remains a developer-only option.
