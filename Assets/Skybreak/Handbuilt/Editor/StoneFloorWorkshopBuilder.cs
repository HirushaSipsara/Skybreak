using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Skybreak.Descent;

namespace Skybreak.Handbuilt.Editor
{
    // Native Unity authoring tool. Never replaces an existing scene or edits the
    // old Descent scene. Downloaded originals stay outside Assets in resources.
    public static class StoneFloorWorkshopBuilder
    {
        const string Root = "Assets/Skybreak/Handbuilt";
        public const string ScenePath = Skybreak.Editor.SkybreakScenePaths.Main;
        const string TextureRoot = Root + "/Textures/StoneFloor";
        const string ModelRoot = Root + "/Models/StoneFloor";
        [Serializable] public class Zone
        {
            public string id, kind;
            public float x1, x2, z1, z2, x, z, radius;
            public int phase;
            public bool Contains(float px, float pz) => kind == "circle"
                ? Vector2.Distance(new Vector2(px,pz),new Vector2(x,z)) < radius
                : px >= x1 && px < x2 && pz >= z1 && pz < z2;
        }
        [Serializable] public class Layout { public Zone[] zones; }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value, out var c); return c; }
        static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }
        static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");

        public static string PrepareTextures()
        {
            foreach (string p in Directory.GetFiles(TextureRoot).Where(p=>p.EndsWith(".jpg") || p.EndsWith(".tga")))
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.maxTextureSize = p.Contains("Grass") ? 1024 : 2048;
                ti.mipmapEnabled = true; ti.anisoLevel = 8;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.isReadable = p.Contains("Specular");
                ti.sRGBTexture = p.Contains("Diffuse");
                if (p.Contains("Normal"))
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.flipGreenChannel = true; // Matches *_Normal_Invert references in original .blend.
                }
                ti.alphaIsTransparency = p.EndsWith(".tga");
                ti.SaveAndReimport();
            }
            MakeMaterial("Skybreak_StoneAtlas", "StoneFloor", false);
            MakeMaterial("Skybreak_StoneDetails", "StoneFloorDetails", false);
            MakeMaterial("Skybreak_StoneGrass", "StoneFloorGrass", true);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Root+"/Reports/TexturesReady.txt", "Shared URP materials and 2K/1K texture imports prepared.");
            return "Prepared three shared materials; original image files are unchanged.";
        }

        static void MakeMaterial(string name, string family, bool grass)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = LoadMaterial(name);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.enableInstancing = true;
            mat.SetFloat("_WorkflowMode", 1); mat.SetFloat("_Metallic", 0);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot+"/"+family+"_Diffuse"+(grass?".tga":".jpg")));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot+"/"+family+"_Normal.jpg"));
            mat.SetFloat("_BumpScale", .45f); mat.EnableKeyword("_NORMALMAP");
            if (grass)
            {
                mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .4f);
                mat.SetFloat("_Cull", 0); mat.SetFloat("_Smoothness", .18f);
                mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = (int)RenderQueue.AlphaTest;
                mat.SetOverrideTag("RenderType", "TransparentCutout");
            }
            else
            {
                // The pack contains a legacy specular map, not measured roughness.
                // This artistic satin finish keeps worn cracks dull and stone glossy.
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot+"/"+family+"_Specular.jpg");
                var pixels = source.GetPixels32();
                for (int i=0;i<pixels.Length;i++)
                {
                    float spec = (pixels[i].r + pixels[i].g + pixels[i].b)/(3f*255f);
                    pixels[i] = new Color32(0,0,0,(byte)Mathf.RoundToInt((.62f+.25f*spec)*255));
                }
                var packed = new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,true);
                packed.SetPixels32(pixels); packed.Apply();
                string packedPath = TextureRoot+"/"+family+"_MetallicSmoothness.png";
                File.WriteAllBytes(packedPath,packed.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(packed);
                AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(packedPath);
                ti.sRGBTexture=false; ti.maxTextureSize=2048; ti.mipmapEnabled=true; ti.anisoLevel=8;
                ti.textureCompression=TextureImporterCompression.Compressed; ti.SaveAndReimport();
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
                mat.SetFloat("_Smoothness", 1); mat.SetFloat("_SmoothnessTextureChannel", 0);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                // Non-metal stone: RGB of the packed map is zero, alpha is smoothness.
            }
            EditorUtility.SetDirty(mat);
        }

        public static string PrepareModels()
        {
            foreach (string p in Directory.GetFiles(ModelRoot,"*.fbx"))
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.globalScale=1; mi.importCameras=false; mi.importLights=false; mi.importAnimation=false;
                mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                mi.SaveAndReimport();
                foreach (var source in AssetDatabase.LoadAllAssetsAtPath(p).OfType<Material>())
                {
                    var mat = LoadMaterial(source.name);
                    if (mat != null) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
                }
                mi.SaveAndReimport();
            }
            for (int i=1;i<=16;i++)
            {
                string name="FloorTile_"+i.ToString("00");
                var root=new GameObject(name+" / 1m grid");
                var tile=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+"/"+name+".fbx"));
                tile.transform.SetParent(root.transform,false); tile.name="Stone surface / Blender mesh";
                var soil=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+"/StoneFloor_Ground.fbx"));
                soil.transform.SetParent(root.transform,false); soil.name="Ground in the seams / Blender mesh";
                var collider=root.AddComponent<BoxCollider>(); collider.center=new Vector3(0,-.1f,0);collider.size=new Vector3(1,.2f,1);
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
                PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Root+"/Reports/ModelsReady.txt","26 Blender FBX exports and 16 editable one-metre tile prefabs ready.");
            return "Prepared 26 imported meshes and 16 tile prefabs.";
        }

        [MenuItem("Skybreak/World Editing/Create floor scene (new only)")]
        public static string BuildScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Floor scene already exists. Open it and edit manually; rebuilding would replace your work.");
            var map=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"/Data/ApprovedFootprint.json"));
            var prefabs=Enumerable.Range(1,16).Select(i=>AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/FloorTile_"+i.ToString("00")+".prefab")).ToArray();
            if(prefabs.Any(p=>p==null)) throw new InvalidOperationException("Prepare tile prefabs before creating the map.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=Group("01 / STONE FLOOR / editable one metre tiles");
            var groups=new Dictionary<string,Transform>(); int count=0;
            for(int z=-10;z<101;z++) for(int x=-20;x<64;x++)
            {
                var zone=map.zones.FirstOrDefault(a=>a.Contains(x+.5f,z+.5f)); if(zone==null)continue;
                string key=zone.id+" / row "+z.ToString("000");
                if(!groups.TryGetValue(zone.id,out var section)) { section=Group(zone.id,floor); groups[zone.id]=section; }
                if(!groups.TryGetValue(key,out var row)) { row=Group("Row Z="+z,section); groups[key]=row; }
                uint hash=unchecked((uint)(x*73856093 ^ z*19349663)); int variant=(int)(hash%16);
                var tile=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[variant]);tile.transform.SetParent(row,false);
                tile.transform.position=new Vector3(x+.5f,0,z+.5f);tile.transform.rotation=Quaternion.Euler(0,(hash%4)*90,0);
                tile.name="Tile ["+x+", "+z+"] / "+(variant+1).ToString("00");count++;
            }
            AddDetails(map);
            LightingAndPreview();
            EditorSceneManager.SaveScene(scene,ScenePath);
            Skybreak.Editor.SkybreakScenePaths.ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            var expected=new HashSet<Vector2Int>();
            for(int z=-10;z<101;z++)for(int x=-20;x<64;x++)if(map.zones.Any(a=>a.Contains(x+.5f,z+.5f)))expected.Add(new Vector2Int(x,z));
            var pending=new Queue<Vector2Int>();var reached=new HashSet<Vector2Int>();pending.Enqueue(new Vector2Int(0,-4));
            while(pending.Count>0){var c=pending.Dequeue();if(!expected.Contains(c)||!reached.Add(c))continue;foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})pending.Enqueue(c+d);}
            int hits=expected.Count(c=>Physics.Raycast(new Vector3(c.x+.5f,2,c.y+.5f),Vector3.down,out var hit,3) && Mathf.Abs(hit.point.y)<.01f);
            string report="Scene: "+ScenePath+"\nTiles: "+count+"\nConnected cells: "+reached.Count+" / "+expected.Count+"\nFloor collider checks: "+hits+" / "+expected.Count+"\nOriginal map was not edited.\nTextures imported at 2K (grass 1K); performance on Intel graphics has not been measured.\n";
            File.WriteAllText(Root+"/Reports/FloorBuild.txt",report);
            Selection.activeGameObject=floor.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,12),Quaternion.Euler(55,-25,0),22);
            return report;
        }

        static void AddDetails(Layout map)
        {
            var details=Group("02 / Floor wear / move or delete individual details");
            var rng=new System.Random(8916);
            string[] fragmentNames={"One","Two","Three","Four","Five"};
            foreach(var zone in map.zones.Where(z=>z.id=="start"||z.id=="fight1"||z.id=="fight2"||z.id=="boss"))
            {
                for(int n=0;n<8;n++)
                {
                    float x=zone.kind=="circle"?zone.x+(float)(rng.NextDouble()-.5)*24:Mathf.Lerp(zone.x1+.5f,zone.x2-.5f,(float)rng.NextDouble());
                    float z=zone.kind=="circle"?zone.z+(float)(rng.NextDouble()-.5)*24:Mathf.Lerp(zone.z1+.5f,zone.z2-.5f,(float)rng.NextDouble());
                    if(!zone.Contains(x,z))continue;
                    string asset="StoneFloor_Fragment"+fragmentNames[n%5];
                    var piece=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+"/"+asset+".fbx"));
                    piece.name=zone.id+" / stone chip "+(n+1);piece.transform.SetParent(details,false);piece.transform.position=new Vector3(x,.01f,z);piece.transform.rotation=Quaternion.Euler(0,rng.Next(360),0);
                    foreach(var r in piece.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
                }
            }
            // A few tufts at seams; no collision or obstacles are introduced.
            for(int i=0;i<18;i++)
            {
                var zone=map.zones[i%map.zones.Length]; if(zone.kind=="circle")continue;
                float x=zone.x1+.98f+(i%2), z=zone.z1+1+(i%3);
                var piece=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+"/StoneFloor_Grass"+(i%2==0?"One":"Two")+".fbx"));
                piece.name=zone.id+" / seam grass "+i;piece.transform.SetParent(details,false);piece.transform.position=new Vector3(x,-.02f,z);piece.transform.rotation=Quaternion.Euler(0,i*71,0);
                foreach(var r in piece.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        static void LightingAndPreview()
        {
            var lighting=Group("03 / Floor lighting and reflections");
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Hex("B4C6CC");RenderSettings.ambientEquatorColor=Hex("71818C");RenderSettings.ambientGroundColor=Hex("33434C");RenderSettings.ambientIntensity=1;
            RenderSettings.fog=false;
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Floor workshop reflection sky"};sky.SetColor("_SkyTint",Hex("8399A8"));sky.SetColor("_GroundColor",Hex("344A55"));sky.SetFloat("_Exposure",.8f);
            AssetDatabase.CreateAsset(sky,Root+"/Materials/FloorWorkshopSky.mat");RenderSettings.skybox=sky;
            var sun=Group("Warm skylight",lighting).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=Hex("FFEDD6");sun.intensity=2;sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;sun.transform.rotation=Quaternion.Euler(40,-28,0);RenderSettings.sun=sun;
            var cool=Group("Cool side fill",lighting).gameObject.AddComponent<Light>();cool.type=LightType.Directional;cool.color=Hex("7DB9DC");cool.intensity=.45f;cool.shadows=LightShadows.None;cool.transform.rotation=Quaternion.Euler(25,125,0);
            var probe=Group("Floor reflection environment",lighting).gameObject.AddComponent<ReflectionProbe>();probe.resolution=128;probe.hdr=true;probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.transform.position=new Vector3(20,3,44);probe.size=new Vector3(90,16,125);probe.center=Vector3.zero;probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.cullingMask=0;probe.intensity=.75f;probe.boxProjection=false;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Floor workshop grading";
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
            var grade=profile.Add<ColorAdjustments>(true);grade.contrast.value=5;grade.saturation.value=-4;grade.postExposure.value=.1f;
            AssetDatabase.CreateAsset(profile,Root+"/Materials/FloorWorkshopVolume.asset");foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);
            var volume=Group("Subtle colour grade",lighting).gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            var cam=Group("04 / Floor inspection camera").gameObject.AddComponent<Camera>();cam.tag="MainCamera";cam.fieldOfView=65;cam.nearClipPlane=.05f;cam.farClipPlane=280;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Hex("182A35");cam.allowHDR=true;cam.gameObject.AddComponent<AudioListener>();
            var cd=cam.GetUniversalAdditionalCameraData();cd.renderPostProcessing=true;cd.volumeLayerMask=1;
            var preview=cam.gameObject.AddComponent<DescentMapPreview>();preview.view=cam;
            preview.captions=new[]{"FLOOR ONLY / ORIGINAL MAP FOOTPRINT","STONE TILE CLOSE-UP / BLENDER ASSETS","WINDING SERVICE PASSAGE FLOOR","GENERATOR HALL FLOOR","CIRCULAR BOSS CHAMBER FLOOR"};
            preview.viewpoints=new Transform[5];var views=Group("05 / Inspection viewpoints");
            Vector3[] pos={new Vector3(-77,105,-55),new Vector3(-3.3f,2.6f,-4.8f),new Vector3(21.5f,4.5f,25),new Vector3(32,5.5f,40),new Vector3(42,7,72)};
            Vector3[] targets={new Vector3(23,0,45),new Vector3(0,0,2),new Vector3(21.5f,0,34),new Vector3(43,0,52),new Vector3(42,0,86)};
            for(int i=0;i<5;i++){var t=Group(preview.captions[i],views);t.position=pos[i];t.LookAt(targets[i]);preview.viewpoints[i]=t;}
            preview.ShowShot(1);DynamicGI.UpdateEnvironment();
        }
    }
}
