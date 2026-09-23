from pathlib import Path
import hashlib, json, sys
ROOT = Path(__file__).resolve().parents[1]
required = [
    'Assets/DreynoxMMORPG/Runtime/Dreynox.Mmorpg.Runtime.asmdef',
    'Assets/DreynoxMMORPG/Editor/Dreynox.Mmorpg.Editor.asmdef',
    'Packages/manifest.json',
    'ProjectSettings/ProjectVersion.txt',
    'README.md'
]
errors=[]
for r in required:
    if not (ROOT/r).exists(): errors.append('missing: '+r)
json.loads((ROOT/'Packages/manifest.json').read_text(encoding='utf-8'))
for p in ROOT.rglob('*.asmdef'): json.loads(p.read_text(encoding='utf-8'))
for forbidden in ['Library','Temp','Logs','UserSettings']:
    if (ROOT/forbidden).exists(): errors.append('forbidden generated dir: '+forbidden)
for p in ROOT.rglob('*.cs'):
    text=p.read_text(encoding='utf-8')
    if text.count('{') != text.count('}'): errors.append('brace mismatch: '+str(p.relative_to(ROOT)))
    if 'TODO' in text or 'IMPLEMENT HERE' in text.upper(): errors.append('placeholder marker: '+str(p.relative_to(ROOT)))
manifest=[]
for p in sorted(x for x in ROOT.rglob('*') if x.is_file() and '.git' not in x.parts):
    h=hashlib.sha256(p.read_bytes()).hexdigest()
    manifest.append(f'{h}  {p.relative_to(ROOT).as_posix()}')
(ROOT/'SOURCE_SHA256.txt').write_text('\n'.join(manifest)+'\n',encoding='utf-8')
if errors:
    print('\n'.join(errors)); sys.exit(1)
print(f'OK: {len(manifest)} files validated')
