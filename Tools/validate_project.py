from pathlib import Path
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

required = [
    'Assets/DreynoxMMORPG/Runtime/Dreynox.Mmorpg.Runtime.asmdef',
    'Assets/DreynoxMMORPG/Editor/Dreynox.Mmorpg.Editor.asmdef',
    'Assets/DreynoxMMORPG/Runtime/ParityCore/ClientMotionCore.cs',
    'Assets/DreynoxMMORPG/Runtime/ParityCore/ClientCoordinateCore.cs',
    'Assets/DreynoxMMORPG/Runtime/ParityCore/FlightTransitionCore.cs',
    'Assets/DreynoxMMORPG/Runtime/ParityCore/CombatCore.cs',
    'Assets/DreynoxMMORPG/Runtime/Gameplay/Client/ShaiyaClientActor.cs',
    'Assets/DreynoxMMORPG/Runtime/Gameplay/Animation/SemanticAnimationPlayer.cs',
    'Assets/DreynoxMMORPG/Editor/Parity/VisualParityComparatorWindow.cs',
    'Assets/DreynoxMMORPG/Editor/LegacyFormats/Legacy3dcParser.cs',
    'Assets/DreynoxMMORPG/Editor/LegacyFormats/LegacyAniParser.cs',
    'Assets/DreynoxMMORPG/Editor/LegacyFormats/LegacyCharacterImporter.cs',
    'Assets/DreynoxMMORPG/Editor/LegacyFormats/LegacyWldTerrainParser.cs',
    'Assets/DreynoxMMORPG/Editor/LegacyFormats/LegacyWorldTerrainImporter.cs',
    'Assets/DreynoxMMORPG/Runtime/ParityCore/LegacyTerrainHeightCore.cs',
    'Assets/DreynoxMMORPG/Tests/Editor/LegacyFormatParserTests.cs',
    'Packages/manifest.json',
    'ProjectSettings/ProjectVersion.txt',
    'README.md',
]

# These files represented the bootstrap v0.1 architecture. They are
# intentionally superseded by the authoritative ParityCore + ShaiyaClientActor
# path. Reintroducing them would create two competing locomotion/flight/camera
# implementations.
superseded = [
    'Assets/DreynoxMMORPG/Runtime/Gameplay/Flight/FlightController.cs',
    'Assets/DreynoxMMORPG/Runtime/Gameplay/Locomotion/CharacterLocomotionMotor.cs',
    'Assets/DreynoxMMORPG/Runtime/Gameplay/Locomotion/ShaiyaLocomotionModel.cs',
    'Assets/DreynoxMMORPG/Runtime/Gameplay/Camera/ThirdPersonCameraCollision.cs',
]

errors = []

for relative in required:
    if not (ROOT / relative).exists():
        errors.append('missing: ' + relative)

json.loads((ROOT / 'Packages/manifest.json').read_text(encoding='utf-8'))
for asmdef in ROOT.rglob('*.asmdef'):
    json.loads(asmdef.read_text(encoding='utf-8'))

for generated in ['Library', 'Temp', 'Logs', 'UserSettings']:
    if (ROOT / generated).exists():
        errors.append('forbidden generated dir: ' + generated)

# Scope gate requested for Dreynox MMORPG: SPK/archive reverse-engineering
# belongs to Shaiya Studio, not to this Unity client.
spk_root = ROOT / 'Assets/DreynoxMMORPG/Editor/ReverseEngineering/Spk'
if spk_root.exists():
    spk_files = [p for p in spk_root.rglob('*') if p.is_file()]
    if spk_files:
        errors.append(
            'scope violation: SPK reverse-engineering belongs to Shaiya Studio: ' +
            ', '.join(str(p.relative_to(ROOT)) for p in spk_files[:10])
        )

for relative in superseded:
    if (ROOT / relative).exists():
        errors.append('superseded duplicate controller reintroduced: ' + relative)

for source in ROOT.rglob('*.cs'):
    text = source.read_text(encoding='utf-8')
    if text.count('{') != text.count('}'):
        errors.append('brace mismatch: ' + str(source.relative_to(ROOT)))
    upper = text.upper()
    if 'IMPLEMENT HERE' in upper or '// TODO:' in upper:
        errors.append('placeholder marker: ' + str(source.relative_to(ROOT)))

    # A previous folder migration accidentally committed literal "\\n"
    # tokens between C# statements. They are not line breaks and make the
    # compilation fail. Catch that specific outside-string shape early.
    if re.search(r'[;)}]\\n\s+[A-Za-z_]', text):
        errors.append(
            'literal escaped newline between C# statements: ' +
            str(source.relative_to(ROOT))
        )

    # A lone backslash inside a C# character literal is invalid. Construct
    # the token explicitly to avoid escaping ambiguity in this validator.
    invalid_backslash_char = "'" + chr(92) + "'"
    if invalid_backslash_char in text:
        errors.append(
            'invalid C# backslash character literal: ' +
            str(source.relative_to(ROOT))
        )

manifest = []
for file in sorted(x for x in ROOT.rglob('*') if x.is_file() and '.git' not in x.parts):
    digest = hashlib.sha256(file.read_bytes()).hexdigest()
    manifest.append(f'{digest}  {file.relative_to(ROOT).as_posix()}')

(ROOT / 'SOURCE_SHA256.txt').write_text(
    '\n'.join(manifest) + '\n',
    encoding='utf-8',
)

if errors:
    print('\n'.join(errors))
    sys.exit(1)

print(f'OK: {len(manifest)} files validated')
