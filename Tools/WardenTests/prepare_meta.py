"""Create stable Unity metadata only for this agent's new assets."""
from pathlib import Path
import uuid
root=Path(__file__).resolve().parents[2]
paths=[root/'Assets/Scripts/AI/CrystalEchoWarden',root/'Assets/Editor/CrystalEchoWarden',root/'Assets/Resources/CrystalEchoWarden',root/'Assets/Scenes/Tests/CrystalEchoWarden']
for base in paths:
    if not base.exists(): continue
    for p in [base,*sorted(base.rglob('*'))]:
        if p.suffix=='.meta': continue
        meta=Path(str(p)+'.meta')
        if meta.exists(): continue
        guid=uuid.uuid5(uuid.NAMESPACE_URL,'skybreak/sachini/'+p.relative_to(root).as_posix()).hex
        text='fileFormatVersion: 2\\nguid: '+guid+'\\n'
        if p.is_dir(): text+='folderAsset: yes\\nDefaultImporter:\\n  externalObjects: {}\\n'
        elif p.suffix=='.cs': text+='MonoImporter:\\n  externalObjects: {}\\n  serializedVersion: 2\\n  defaultReferences: []\\n  executionOrder: 0\\n  icon: {instanceID: 0}\\n'
        elif p.suffix=='.asmdef': text+='AssemblyDefinitionImporter:\\n  externalObjects: {}\\n'
        elif p.suffix=='.fbx': text+='ModelImporter:\\n  serializedVersion: 22200\\n  internalIDToNameTable: []\\n  externalObjects: {}\\n  materials:\\n    materialImportMode: 1\\n    materialName: 0\\n    materialSearch: 1\\n    materialLocation: 1\\n  animations:\\n    importAnimation: 0\\n  meshes:\\n    globalScale: 1\\n    useFileScale: 1\\n    addColliders: 0\\n  importAnimation: 0\\n  animationType: 0\\n'
        else: text+='DefaultImporter:\\n  externalObjects: {}\\n'
        meta.write_text(text.replace('\\n','\n'),encoding='utf-8')

