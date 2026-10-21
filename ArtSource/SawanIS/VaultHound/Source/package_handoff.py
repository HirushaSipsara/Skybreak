"""Encode rendered run frames and create a checked, portable handoff ZIP."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile
from PIL import Image

base = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--output", type=Path, default=base.parent / "VaultHound_Teammate_Package.zip")
args = parser.parse_args()
frames = [Image.open(p).convert("RGB") for p in sorted((base / "Previews/RunFrames").glob("Run_*.png"))]
assert len(frames) == 10, "Render the ten run frames before packaging."
frames[0].save(base / "Previews/VaultHound_Run.gif", save_all=True,
               append_images=frames[1:], duration=[70, 60, 70, 70, 60, 70, 70, 60, 70, 70], loop=0)
for frame in frames:
    frame.close()

def included(path):
    return (path.is_file() and path.suffix not in {".blend1", ".blend2", ".pyc"}
            and "__pycache__" not in path.parts and "RunFrames" not in path.parts
            and path.name != "PackageManifest.json")

files = sorted(p for p in base.rglob("*") if included(p))
manifest = {str(p.relative_to(base)).replace("\\", "/"):
            {"bytes": p.stat().st_size, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
            for p in files}
manifest_path = base / "Evidence/PackageManifest.json"
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
files.append(manifest_path)
args.output.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for p in files:
        archive.write(p, "VaultHound/" + p.relative_to(base).as_posix())
with zipfile.ZipFile(args.output) as archive:
    assert archive.testzip() is None
    assert len(archive.namelist()) == len(files)
    for name, record in manifest.items():
        data = archive.read("VaultHound/" + name)
        assert hashlib.sha256(data).hexdigest() == record["sha256"], name
print(json.dumps({"zip": str(args.output), "files": len(files),
                  "bytes": args.output.stat().st_size, "archive_integrity": "pass"}, indent=2))
