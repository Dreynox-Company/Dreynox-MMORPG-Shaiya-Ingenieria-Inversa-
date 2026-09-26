# Original quest presentation and action integrity

## Native evidence

Read-only inspection uses game.exe SHA256509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d and the uploaded Sh.part1..7/Sh.zip. Static xref0x58F1DC references quest/take.tga at VA0x860728, with0x100/0x200 dimensions pushed at0x58F1D7/0x58F1D2 before the load at0x58F1E9. These match the original256x512 texture. Nearby initialization writes three0xff bytes at offsets0x44..0x46; this supports investigating bright text but is not a complete reconstruction of the widget class. No new native process execution is claimed.

Exact TGA:507584bytes, SHA2566d407abd03f6d7d753945c8301381da55637107bececb7dde97fe8b42cbfb1d7. The paper interior has alpha218. Run19's Unity dialog stretched it to475x560 and placed dark text over a translucent backdrop.

## Integrated correction

The existing QuestWorldPanel keeps the original256x512 paper, composites it over an opaque modal, uses bright shadowed text and separate scrolling story/reward regions. Margins, font size and surrounding selection-panel layout are Unity presentation choices, not claimed pixel-perfect native positions. Original art and Spanish text are unchanged. World labels remain below the modal.

Delivery shows the original completion text and saved reward, hides claim/choice controls and removes the mission from active options. Fixed rewards have no false choice button. Closing/disabling releases dialogue and journal input ownership. The readonly journal does not expose active NPC actions.

Accept/deliver actions revalidate the selected NPC identity, active scene, proximity and clear line of sight at click time. A dialog opened earlier cannot grant actions through a newly closed wall or after the NPC disappears/player moves away. The existing local five-unit radius is not claimed as a recovered server rule.

QuestJournalCore separates durable mutation from presentation notification. UI callback failures are reported but cannot reject an already-saved reward or prevent other observers from being notified. Reentrant duplicate death notifications are reserved until commit finishes. A failed save leaves the receipt retryable; a persistence callback cannot start a nested write. This does not claim crash-durable pending death receipts or authoritative server inventory.

Seven additional standalone journal checks and six Unity interaction/presentation cases cover these changes. Three additional Unity pose tests distinguish root movement from actual skinned vertex changes. The Player scenario captures four body angles and local-space skin differences during walking, accepted attacks and mob death, plus camera metadata for each PNG. Source additions are not passing runtime or native-equivalence evidence.

## Delivery compatibility

After the unsupported ExternalAttributes API was removed, the exact Windows fixture exposed Framework ZipFile.CreateFromDirectory backslash names. The writer now explicitly creates forward-slash ZIP entries. The strict reader still rejects ambiguous paths. Verification retains a Process object and captures both pipes rather than relying on Start-Process/Refresh with a missing ExitCode. These scripts are internal CI, not user deliverables.

References: Unity6 SkinnedMeshRenderer.BakeMesh and Canvas.overrideSorting; Microsoft .NET Framework migration guide ZipArchiveEntry.FullName Path Separator; PKWARE APPNOTE6.3.10. No changes to main, original DATA/game.exe, Flutter or Studio.
