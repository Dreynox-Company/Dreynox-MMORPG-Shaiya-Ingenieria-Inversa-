#!/usr/bin/env python3
"""Read-only fingerprint/PE validation of statically inspected ps0032 merchant code.

This does not launch, patch or decrypt the executable, infer missing field semantics,
or connect to a server. Instruction boundaries were inspected with objdump first.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

EXPECTED_SHA256 = "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d"
EXPECTED_BYTES = 5_352_488
# Virtual address, exact instruction bytes, observed role (not game-state semantics).
ANCHORS = (
    (0x694D87, "8b4d088a550c", "buy reads first DWORD and first byte argument"),
    (0x694D8D, "b802070000", "buy opcode 0x0702"),
    (0x694D92, "898dfedfffff", "buy DWORD at body offset 2"),
    (0x694DA5, "889502e0ffff", "buy byte at body offset 6"),
    (0x694DAB, "898d04e0ffff", "buy final DWORD at body offset 8"),
    (0x694DB1, "b90c000000", "buy send body length 12"),
    (0x694DBC, "888503e0ffff", "buy byte at body offset 7"),
    (0x694DFD, "b803070000", "sell opcode 0x0703"),
    (0x694E02, "888dfedfffff", "sell byte at body offset 2"),
    (0x694E0B, "8895ffdfffff", "sell byte at body offset 3"),
    (0x694E1E, "898d01e0ffff", "sell DWORD at body offset 5"),
    (0x694E24, "899505e0ffff", "sell final DWORD at body offset 9"),
    (0x694E2A, "b90d000000", "sell send body length 13"),
    (0x694E35, "888500e0ffff", "sell byte at body offset 4"),
    (0x5E4159, "a13cfe9100", "buy caller reads shared conversation DWORD"),
    (0x5E415E, "8b895005000050525651", "buy caller passes context, quantity, stock and NPC field"),
    (0x5E428F, "8b0d3cfe9100", "sell caller reads the same conversation DWORD"),
    (0x5E4295, "8b96500500008b45085152575350", "sell caller pushes all five arguments"),
    (0x5E418C, "8d04400fb6cb8d04c1", "sell lookup index is first byte times 24 plus second byte"),
    (0x5E4195, "69c084000000", "sell indexed record stride is 132 bytes"),
    (0x6444E7, "0fb78792000000a33cfe9100", "conversation DWORD loaded from a WORD at object offset 0x92"),
)
CALLS = ((0x694DC2, 0x693880), (0x694E3B, 0x693880),
         (0x5E4168, 0x694D70), (0x5E42A3, 0x694DE0))


def inspect(path: Path) -> dict:
    """Return a reproducible evidence report; never mutate any source bytes."""
    if path.stat().st_size != EXPECTED_BYTES:
        raise ValueError("Different executable size: qualify that revision separately.")
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise ValueError("Executable hash differs: no signature-only fallback is allowed.")
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[:2] != b"MZ" or data[pe:pe+4] != b"PE\0\0":
        raise ValueError("Invalid PE header.")
    machine, count = struct.unpack_from("<HH", data, pe+4)
    optional_size = struct.unpack_from("<H", data, pe+20)[0]
    if machine != 0x14C or struct.unpack_from("<H", data, pe+24)[0] != 0x10B:
        raise ValueError("Expected x86 PE32.")
    image_base = struct.unpack_from("<I", data, pe+24+28)[0]
    if image_base != 0x400000 or count > 96:
        raise ValueError("Unexpected image base or section count.")
    sections = []
    for i in range(count):
        start = pe+24+optional_size+40*i
        name = data[start:start+8].rstrip(b"\0").decode("ascii")
        _, rva, raw_size, raw_start = struct.unpack_from("<IIII", data, start+8)
        if raw_start + raw_size > len(data):
            raise ValueError("Section raw bytes escape file.")
        sections.append((name, rva, raw_size, raw_start))

    def code_at(address: int, length: int) -> bytes:
        rva = address-image_base
        for name, base, size, raw in sections:
            if name == ".text" and base <= rva and rva+length <= base+size:
                at = raw+rva-base
                return data[at:at+length]
        raise ValueError(f"Code address outside raw .text: 0x{address:X}")

    checked = []
    for address, instruction_hex, role in ANCHORS:
        expected = bytes.fromhex(instruction_hex)
        if code_at(address, len(expected)) != expected:
            raise ValueError(f"Different instruction at 0x{address:X}")
        checked.append({"address": f"0x{address:08X}", "bytes": len(expected), "role": role})
    calls = []
    for address, target in CALLS:
        instruction = code_at(address, 5)
        observed = address+5+struct.unpack_from("<i", instruction, 1)[0]
        if instruction[0] != 0xE8 or observed != target:
            raise ValueError(f"Direct call target changed at 0x{address:X}")
        calls.append({"address": f"0x{address:08X}", "target": f"0x{target:08X}"})
    return {
        "schema": 1,
        "scope": "read-only-static-instruction-verification-not-native-runtime-or-server-parity",
        "exeSha256": digest, "exeBytes": len(data), "imageBase": "0x00400000",
        "passed": True, "checkedAnchors": checked, "checkedDirectCalls": calls,
        "buyBodyBytes": 12, "sellBodyBytes": 13,
        "opaqueConversationWord": {
            "globalAddress": "0x0091FE3C", "buyOffset": 8, "sellOffset": 9,
            "semantics": "Kept opaque; shared conversation dataflow is verified, not a price formula or local journal revision."
        },
        "limitations": "No native process, transport, encryption, response handling, pricing or all-function coverage was executed."
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference_exe", type=Path)
    parser.add_argument("--report", type=Path, help="New evidence file; existing files are not overwritten.")
    args = parser.parse_args()
    text = json.dumps(inspect(args.reference_exe), ensure_ascii=False, indent=2)+"\n"
    if args.report is not None:
        # Exclusive creation also prevents a report from replacing the reference.
        with args.report.open("x", encoding="utf-8") as output:
            output.write(text)
    print(text, end="")


if __name__ == "__main__":
    main()
