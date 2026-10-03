using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Skybreak.Descent.Editor
{
    public static class DescentVisualPolish
    {
        public static string Apply()
        {

            if (File.Exists("Docs/GV/LevelDesign/Reports/Dungeon/VisualPolish.txt")) return "Visual polish already applied.";
            var map=JsonUtility.FromJson<DescentMapBuilder.Layout>(File.ReadAllText("Assets/Data/World/Dungeon/DescentMapLayout.json"));
            Func<float,float,bool> inside=(x,z)=>Array.Exists(map.zones,a=>a.kind=="circle"?Vector2.Distance(new Vector2(x,z),new Vector2(a.x,a.z))<a.radius:x>=a.x1&&x<=a.x2&&z>=a.z1&&z<=a.z2);
            var cells=new HashSet<Vector2Int>();
            for(int x=-20;x<64;x++)for(int z=-10;z<101;z++)if(inside(x+.5f,z+.5f))cells.Add(new Vector2Int(x,z));
            var lenses=new List<Vector3>();var offsets=new List<Vector3>();
            foreach(var cell in cells)foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
            {
                if(cells.Contains(cell+d)||(cell.x+cell.y)%9!=0)continue;
                lenses.Add(new Vector3(cell.x+.5f-d.x*.23f,2.6f,cell.y+.5f-d.y*.23f));
                offsets.Add(new Vector3(-d.x*.11f,0,-d.y*.11f));
            }
            int shifted=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/Models/Environment/Dungeon/Meshes"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.Contains("_Amber_"))continue;
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var vertices=mesh.vertices;bool changed=false;
                for(int i=0;i<vertices.Length;i++)for(int j=0;j<lenses.Count;j++)
                {
                    Vector3 delta=vertices[i]-lenses[j];
                    if(Mathf.Abs(delta.y)>.28f||Mathf.Abs(delta.x)>.13f||Mathf.Abs(delta.z)>.13f)continue;
                    vertices[i]+=offsets[j];shifted++;changed=true;break;
                }
                if(changed){mesh.vertices=vertices;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);}
            }
            var signMaterial=new Material(Shader.Find("Skybreak/Depth Tested Sign")){name="Depth tested lettering"};
            signMaterial.mainTexture=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf").material.mainTexture;
            AssetDatabase.CreateAsset(signMaterial,"Assets/Materials/Environment/Dungeon/SignLettering.mat");
            foreach(var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))text.GetComponent<MeshRenderer>().sharedMaterial=signMaterial;
            RenderSettings.fogDensity=.0028f;
            RenderSettings.ambientEquatorColor=new Color(.30f,.40f,.46f);
            RenderSettings.ambientGroundColor=new Color(.12f,.20f,.25f);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(light.type==LightType.Directional){light.intensity=2.1f;light.shadowStrength=.68f;}
                else light.intensity*=7;
            }
            var preview=UnityEngine.Object.FindFirstObjectByType<DescentMapPreview>();preview.ShowShot(1);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            string result="Adjusted "+shifted+" lamp-lens vertices; depth-tested lettering; improved light fill and reduced overview fog.";
            File.WriteAllText("Docs/GV/LevelDesign/Reports/Dungeon/VisualPolish.txt",result);return result;
        }
    }
}
