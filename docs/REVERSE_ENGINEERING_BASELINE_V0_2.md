# Dreynox MMORPG — Reverse Engineering Baseline v0.2.0

## Scope

This iteration moves verified Shaiya SPK knowledge into Unity/C# without modifying the frozen Flutter game or Shaiya Studio projects.

## SPK v3 index layout now migrated

The decoded index is exactly 96 bytes per record. The currently verified record layout is:

| Offset | Type | Meaning |
|---:|---|---|
| 0x00 | UInt64 LE | entryId |
| 0x08 | UInt64 LE | dataOffset |
| 0x10 | UInt64 LE | storedBytes |
| 0x18 | UInt64 LE | storedBytes mirror |
| 0x20 | UInt64 LE | decodedBytes |
| 0x28 | UInt32 LE | recordType |
| 0x2C | UInt32 LE | auxiliaryStart |
| 0x30 | 32 bytes | metadata |
| 0x50 | 16 bytes | reserved, must be zero |

For simple resources (`recordType == 1`), metadata contains a 12-byte nonce, 16-byte GCM tag, and 32-bit flags. For fragmented resources (`recordType == 3`), the final 32 bits of metadata encode `chunkCount`.

The auxiliary table is 32 bytes per row:

| Offset | Type | Meaning |
|---:|---|---|
| 0x00 | UInt64 LE | chunk dataOffset |
| 0x08 | UInt32 LE | storedBytes |
| 0x0C | UInt32 LE | storedBytes mirror |
| 0x10 | 16 bytes | authentication metadata |

Validation additionally requires exact resource coverage from byte 128 to `auxiliaryOffset`, no gaps/overlaps, contiguous fragment chains, exact chunk byte totals, and no unused auxiliary rows.

## Known original game.exe identity

A previously validated reference build is tracked only by non-secret static identity:

- bytes: `5,352,488`
- SHA-256: `509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d`

`EXE Parity Inspector` verifies whether a selected local `game.exe` is that exact reference and captures PE architecture, sections and import DLLs. The Unity executable is not expected to be byte-identical; future parity gates are behavioral/visual.

## Windows build

`Dreynox MMORPG > Build > Windows x64 Release` creates `Builds/Windows/DreynoxMmorpg.exe`. The CI workflow can use GameCI when Unity activation secrets are configured in GitHub.
