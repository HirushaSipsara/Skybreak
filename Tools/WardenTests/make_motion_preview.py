"""Assemble Blender animation renders into a labelled review GIF."""
from pathlib import Path
import json
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[2]
source=root/'Tools/WardenTests/bin/AnimationFrames'
manifest=json.loads((source/'manifest.json').read_text())
frames=[]
for item in manifest:
    rendered=Image.open(item['path']).convert('RGB')
    canvas=Image.new('RGB',(460,500),(15,23,36))
    canvas.paste(rendered,(0,40))
    draw=ImageDraw.Draw(canvas)
    draw.text((14,12),'CRYSTAL ECHO WARDEN  /  '+item['clip'].upper(),fill=(115,230,245))
    frames.append(canvas)
output=root/'ArtSource/Sachini/CrystalEchoWarden/Evidence/CrystalEchoWarden_Movement.gif'
frames[0].save(output,save_all=True,append_images=frames[1:],duration=140,loop=0,optimize=False)
print(output)
