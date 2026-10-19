"""Create the bootstrap scene and copy the measured Blender export into Unity."""
from pathlib import Path
import uuid
root=Path(__file__).resolve().parents[2]
target=root/'Assets/Resources/CrystalEchoWarden/CrystalEchoWarden.fbx'
target.parent.mkdir(parents=True,exist_ok=True)
source=root/'ArtSource/Sachini/CrystalEchoWarden/Exports/CrystalEchoWarden.fbx'
target.write_bytes(source.read_bytes())
guid=lambda p: uuid.uuid5(uuid.NAMESPACE_URL,'skybreak/sachini/'+p).hex
reference=(root/'Assets/Models/Custom/RiftWarden/RiftWarden.fbx.meta').read_text()
import re
reference=re.sub(r'^guid: .*$', 'guid: '+guid(target.relative_to(root).as_posix()), reference, flags=re.M)
reference=re.sub(r'importAnimation: 1','importAnimation: 0',reference)
reference=re.sub(r'animationType: [0-9]+','animationType: 0',reference)
Path(str(target)+'.meta').write_text(reference)
lab=root/'Assets/Scenes/Tests/CrystalEchoWarden/CrystalEchoWardenLab.unity'
lab.parent.mkdir(parents=True,exist_ok=True)
script=guid('Assets/Scripts/AI/CrystalEchoWarden/Runtime/WardenLab.cs')
lab.write_text("""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1001}
  - component: {fileID: 1002}
  m_Layer: 0
  m_Name: Crystal Echo Warden Lab - press Play
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &1001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
--- !u!114 &1002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: SCRIPT_GUID, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  fourWardens: 0
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 1001}
""".replace('SCRIPT_GUID',script))
