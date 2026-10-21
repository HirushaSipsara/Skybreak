"""Validate authored Unity asset references without pretending to import them."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
scopes = [root / path for path in (
    'Assets/Scripts/AI/CrystalEchoWarden', 'Assets/Editor/CrystalEchoWarden',
    'Assets/Resources/CrystalEchoWarden', 'Assets/Scenes/Tests/CrystalEchoWarden')]
owned = []
for scope in scopes:
    for asset in [scope, *scope.rglob('*')]:
        if asset.suffix == '.meta':
            continue
        meta = Path(str(asset) + '.meta')
        assert meta.is_file(), f'Missing metadata: {asset}'
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
        assert match, f'Invalid metadata GUID: {meta}'
        owned.append((asset, match.group(1)))
all_guids = {}
for meta in (root / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
    if match:
        all_guids.setdefault(match.group(1), []).append(meta)
for asset, guid in owned:
    assert len(all_guids[guid]) == 1, f'GUID collision: {asset}'
script = root / 'Assets/Scripts/AI/CrystalEchoWarden/Runtime/WardenLab.cs'
script_guid = dict(owned)[script]
scene = root / 'Assets/Scenes/Tests/CrystalEchoWarden/CrystalEchoWardenLab.unity'
assert f'guid: {script_guid}, type: 3' in scene.read_text(), 'Lab bootstrap reference does not resolve'
source = root / 'ArtSource/Sachini/CrystalEchoWarden/Exports/CrystalEchoWarden.fbx'
resource = root / 'Assets/Resources/CrystalEchoWarden/CrystalEchoWarden.fbx'
assert source.read_bytes() == resource.read_bytes(), 'Unity FBX differs from exported source'
assert resource.read_bytes().startswith(b'Kaydara FBX Binary'), 'Unexpected FBX file header'
assert (root / 'Assets/Editor.meta').is_file(), 'Missing Editor folder metadata'
print(f'ASSET PASS: {len(owned)} assets/folders have unique metadata; lab script reference resolves; FBX resource matches source.')
print('Unity import, materials, geometry appearance and API compilation remain unverified.')
