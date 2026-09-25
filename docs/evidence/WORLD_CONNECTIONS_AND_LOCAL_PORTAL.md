# Original world connectivity and first local return portal

Read-only audit of the supplied Sh.part1..7 / Sh.zip parsed all 108 numerically named world/*.svmap resources to exact EOF. The other three .svmap files are backups: 103.bak, 11.bak and 67.bak. Every numbered SVMAP has a corresponding WLD: 40 FLD and 68 DUN. There are 388 authored portal records. Targets 200 and 4444 have no numbered map pair in this corpus. Reachability from Map1 is 68 maps when deliberately ignoring faction, levels, bosses and server configuration; this is a resource graph, not 68 playable scenes.

Map1's 11 original portals include a return within Map1:
- original portal position: (1016.385498046875, 8.319992065429688, 671.4583740234375)
- authored destination: (542.25, 77.75, 1760.25)
- portal rule: 1; source level bounds: 0..999
- 1.svmap: 554884 bytes; SHA256 15b0899be083c09344a27310a85ded371c3f61b60a07b167d39e467584cf06cc.

LocalPortalTravel connects same-map records to the existing collision-aware ground placement, with original faction/level/boss policy checks. It runs from the explicit StartingWorldBuild local game session, not a global auto-bootstrap. It verifies source scene identity and entry proximity. An invalid destination never moves the player. An accepted transfer flushes an already accepted hit once, cancels outstanding target actions and resets the following camera; it does not award quests or rewrite the DATA. PortalId is a rule field, not the unique placement identifier.

The local 2.5-unit activation radius and ground-only rule are qualification settings, not claimed recovered native constants. Cross-map portals explicitly report unavailable destinations rather than substituting Map1. Full scene streaming, network travel, instance selection and server authority remain separate work.

Ten added Unity test cases cover a grounded same-map transfer, missing maps, levels/factions, missing support, distant requests, dialogue/boss locks, pending-vs-accepted hits and the exact original return record. Passing status must come from the subsequent Unity run. This source addition does not alter the already running immutable source of Map1 run 36168379406.
