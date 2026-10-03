#pragma warning disable 0649 // JsonUtility populates the map DTO fields.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Skybreak.Descent.Editor
{
    // Creates native editable Unity assets; no dependency on the browser prototype at runtime.
    public static class DescentMapBuilder
    {
        public const string Root = "Assets/Skybreak/Descent";
        public const string ScenePath = Skybreak.Editor.SkybreakScenePaths.DescentDemo;
        [Serializable] public class Zone { public string id,kind; public float x1,x2,z1,z2,x,z,radius; public int phase; }
        [Serializable] public class Cover { public float x,z,w,d,h; }
        [Serializable] public class Pillar { public float x,z,radius; }
        [Serializable] public class Layout { public Zone[] zones; public Cover[] cover; public Pillar[] bossPillars; }
        static Layout map;
        static Mesh bevel, cylinder, crystal, rock;
        static readonly Dictionary<string,Material> paints = new Dictionary<string,Material>();
        static readonly Dictionary<string,List<CombineInstance>> batches = new Dictionary<string,List<CombineInstance>>();
        static readonly Dictionary<string,Transform> chunks = new Dictionary<string,Transform>();
        static readonly List<Transform> crystals = new List<Transform>();
        static Transform environment, colliders;
        static Font font;
        static int pieceCount;

        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out Color c);return c; }
        static float Rand(int i) { return Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1); }
        static bool Walkable(float x,float z) { return map.zones.Any(a=>a.kind=="circle"?Vector2.Distance(new Vector2(x,z),new Vector2(a.x,a.z))<a.radius:x>=a.x1&&x<=a.x2&&z>=a.z1&&z<=a.z2); }
        static bool Passage(float x,float z) { return x>6&&x<28&&z>24&&z<50; }
        static Transform Group(string name,Transform parent=null) { var g=new GameObject(name);if(parent!=null)g.transform.SetParent(parent,false);return g.transform; }
        static void Asset(UnityEngine.Object asset,string path) { if(AssetDatabase.LoadMainAssetAtPath(path)==null)AssetDatabase.CreateAsset(asset,path); }

        static Material Paint(string name,string color,float metal=0,float smooth=.35f,float emission=0)
        {
            string path=Root+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
            m.color=Hex(color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;
            if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Hex(color)*emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
            paints[name]=m;EditorUtility.SetDirty(m);return m;
        }
        static void CreatePalette()
        {
            Paint("Stone","355662");Paint("StoneLight","657E88");Paint("StoneDark","203743");Paint("Iron","142B38",.45f,.35f);
            Paint("Copper","B6673C",.42f,.45f);Paint("Brass","D7A66A",.5f,.47f);Paint("Cream","E3D9BC");Paint("Wood","826147");
            Paint("Cyan","3ACCDD",.1f,.45f,2.6f);Paint("Amber","FFB764",0,.4f,2.3f);Paint("Violet","AC6BED",.1f,.5f,2.5f);
            Paint("Crystal","76E7E3",.2f,.55f,.8f);Paint("Moss","4B7769");
            for(int i=0;i<5;i++){Color c=Color.Lerp(Hex("6E7C7E"),Hex("9AA3A0"),i/4f);Paint("Floor"+i,ColorUtility.ToHtmlStringRGB(c),0,.26f);}
            Paint("PassageFloor","4F6972",0,.25f);Paint("CoreFloor","696577",.1f,.36f);
            Texture2D texture=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Handcrafted stone grain"};
            var pixels=new Color[128*128];for(int i=0;i<pixels.Length;i++){float v=.86f+Rand(i)*.14f;pixels[i]=new Color(v,v,v,1);}texture.SetPixels(pixels);texture.Apply();
            string p=Root+"/Textures/StoneGrain.png";File.WriteAllBytes(p,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(p);
            var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=128;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            var grain=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            foreach(var pair in paints)if(pair.Key.StartsWith("Stone")||pair.Key.Contains("Floor"))pair.Value.SetTexture("_BaseMap",grain);
        }

        static Mesh MakeBevel()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();const float a=.45f;
            Action<Vector3[]> face=points=>{Vector3 center=Vector3.zero;foreach(var p in points)center+=p;center/=points.Length;
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0)Array.Reverse(points);
                int start=vertices.Count;for(int i=0;i<points.Length;i++){vertices.Add(points[i]);uv.Add(new Vector2(i==1||i==2?1:0,i>=2?1:0));}
                for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}};
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2){int b=(axis+1)%3,c=(axis+2)%3;var points=new Vector3[4];int[,] dirs={{-1,-1},{1,-1},{1,1},{-1,1}};
                for(int n=0;n<4;n++){points[n][axis]=sign*.5f;points[n][b]=dirs[n,0]*a;points[n][c]=dirs[n,1]*a;}face(points);}
            for(int axis=0;axis<3;axis++){int b=(axis+1)%3,c=(axis+2)%3;for(int sb=-1;sb<=1;sb+=2)for(int sc=-1;sc<=1;sc+=2){var p=new Vector3[4];
                p[0][axis]=-a;p[1][axis]=a;p[2][axis]=a;p[3][axis]=-a;p[0][b]=p[1][b]=sb*.5f;p[0][c]=p[1][c]=sc*a;p[2][b]=p[3][b]=sb*a;p[2][c]=p[3][c]=sc*.5f;face(p);}}
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)face(new[]{new Vector3(x*.5f,y*a,z*a),new Vector3(x*a,y*.5f,z*a),new Vector3(x*a,y*a,z*.5f)});
            var mesh=new Mesh{name="Beveled modular block"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh MakeCrystal()
        {
            Vector3[] v={new Vector3(0,1,0),new Vector3(0,-1,0),new Vector3(1,0,0),new Vector3(0,0,1),new Vector3(-1,0,0),new Vector3(0,0,-1)};
            int[] idx={0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2};var vertices=new List<Vector3>();foreach(int i in idx)vertices.Add(v[i]);
            var m=new Mesh{name="Faceted crystal"};m.SetVertices(vertices);m.SetTriangles(Enumerable.Range(0,idx.Length).ToArray(),0);m.RecalculateNormals();return m;
        }
        static void CreateMeshes()
        {
            bevel=MakeBevel();Asset(bevel,Root+"/Meshes/BeveledBlock.asset");bevel=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BeveledBlock.asset");
            crystal=MakeCrystal();Asset(crystal,Root+"/Meshes/Crystal.asset");crystal=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Crystal.asset");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cylinder=UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(primitive);cylinder.name="Pipe cylinder";Asset(cylinder,Root+"/Meshes/Cylinder.asset");cylinder=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Cylinder.asset");
            rock=crystal;
        }
        static string Chunk(Vector3 p) { return Mathf.FloorToInt(p.x/12)+"_"+Mathf.FloorToInt(p.z/12); }
        static void Piece(string paint,Vector3 p,Vector3 scale,Quaternion rotation,Mesh mesh=null)
        {
            mesh=mesh==null?bevel:mesh;string key=Chunk(p)+"|"+paint+"|"+mesh.name;
            if(!batches.ContainsKey(key))batches[key]=new List<CombineInstance>();
            batches[key].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,rotation,scale)});pieceCount++;
        }
        static void Box(string paint,float x,float y,float z,float w,float h,float d,float yaw=0) { Piece(paint,new Vector3(x,y,z),new Vector3(w,h,d),Quaternion.Euler(0,yaw,0)); }
        static void ColliderBox(string name,Vector3 p,Vector3 size,float yaw=0,Transform parent=null)
        {
            var go=Group(name,parent==null?colliders:parent);go.position=p;go.rotation=Quaternion.Euler(0,yaw,0);var col=go.gameObject.AddComponent<BoxCollider>();col.size=size;
        }
        static GameObject Direct(string name,string paint,Vector3 p,Vector3 size,Transform parent,Mesh mesh=null,bool solid=false)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=mesh==null?bevel:mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=paints[paint];
            if(solid)go.AddComponent<BoxCollider>();return go;
        }
        static void Text(string text,Vector3 p,float size,Transform parent,Color color,float yaw=0)
        {
            var go=Group(text.Replace('\n',' '),parent);go.position=p;go.rotation=Quaternion.Euler(0,yaw,0);
            var label=go.gameObject.AddComponent<TextMesh>();label.font=font;label.fontSize=80;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.text=text;label.color=color;
            go.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
        static void Sign(string text,float x,float y,float z,float width,float yaw=0)
        {
            Transform root=Group("Wayfinding / "+text,environment);root.position=new Vector3(x,y,z);root.rotation=Quaternion.Euler(0,yaw,0);
            Direct("Copper border","Copper",Vector3.zero,new Vector3(width,1.25f,.14f),root);
            Direct("Dark enamel","Iron",new Vector3(0,0,-.09f),new Vector3(width-.15f,1.08f,.07f),root);
            Text(text,root.TransformPoint(new Vector3(0,0,-.14f)),.055f,root,Hex("BDECEE"),yaw);
        }
        static void FlushBatches()
        {
            foreach(var pair in batches){var split=pair.Key.Split('|');if(!chunks.TryGetValue(split[0],out Transform parent))chunks[split[0]]=parent=Group("Static chunk "+split[0],environment);
                var mesh=new Mesh{name="Descent "+pair.Key,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=Root+"/Meshes/Chunk_"+pair.Key.Replace('|','_')+".asset";Asset(mesh,path);mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var go=Group(split[1]+" / "+pair.Value.Count+" pieces",parent);go.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=paints[split[1]];
                GameObjectUtility.SetStaticEditorFlags(go.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.OccluderStatic);
                if(split[1].Contains("Floor")){var col=go.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mesh;}
            }
        }

        static void FloorsAndWalls()
        {
            var cells=new HashSet<Vector2Int>();for(int x=-20;x<64;x++)for(int z=-10;z<101;z++)if(Walkable(x+.5f,z+.5f))cells.Add(new Vector2Int(x,z));
            int id=0;foreach(var cell in cells){float x=cell.x+.5f,z=cell.y+.5f;string floor=z>71?"CoreFloor":Passage(x,z)?"PassageFloor":"Floor"+(id++%5);
                Box(floor,x,-.14f,z,.985f,.28f,.985f);
                foreach(var delta in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down}){if(cells.Contains(cell+delta))continue;
                    float ex=x+delta.x*.5f,ez=z+delta.y*.5f,yaw=delta.x==0?0:90,height=Passage(ex,ez)?4.9f:4.5f;
                    for(int row=0;row<5;row++)Box(row%3==0?"StoneLight":"Stone",ex,.46f+row*.87f,ez,.97f,.82f,.72f,yaw);
                    Box("StoneDark",ex,.12f,ez,1.04f,.26f,.86f,yaw);Box("StoneLight",ex,height,ez,1.07f,.28f,.93f,yaw);
                    ColliderBox("Masonry boundary",new Vector3(ex,height/2,ez),new Vector3(1,height,.72f),yaw);
                    if((cell.x+cell.y)%4==0){Box("StoneDark",ex,2.25f,ez,.33f,4.5f,1.02f,yaw);Box("Copper",ex,3.8f,ez,.44f,.18f,1.10f,yaw);}
                    if((cell.x+cell.y)%9==0){float lx=ex-delta.x*.6f,lz=ez-delta.y*.6f;Box("Copper",lx,2.6f,lz,.46f,.85f,.36f,yaw);Box("Amber",lx-delta.x*.13f,2.6f,lz-delta.y*.13f,.25f,.54f,.08f,yaw);}
                    if((cell.x+cell.y)%5==0){float vx=ex-delta.x*.43f,vz=ez-delta.y*.43f;Box("Iron",vx,1.7f,vz,.65f,1.1f,.08f,yaw);for(int j=0;j<5;j++)Box("Brass",vx-delta.x*.055f,1.3f+j*.15f,vz-delta.y*.055f,.48f,.035f,.025f,yaw);}
                    if((cell.x+cell.y)%7==0){float rx=ex+delta.x*2.5f,rz=ez+delta.y*2.5f;if(!Walkable(rx,rz))Piece("StoneDark",new Vector3(rx,-2.6f,rz),new Vector3(1.6f,4.7f,1.6f),Quaternion.Euler(0,Rand(id++)*360,10),rock);}
                    // Deep foundation beneath the perimeter makes the overview a floating diorama.
                    Box("StoneDark",ex,-1.55f,ez,1.02f,2.7f,1.1f,yaw);
                }
            }
        }
        static GameObject SaveKit(string name,Action<Transform> build)
        {
            Transform temp=Group(name);build(temp);var prefab=PrefabUtility.SaveAsPrefabAsset(temp.gameObject,Root+"/Prefabs/"+name+".prefab");UnityEngine.Object.DestroyImmediate(temp.gameObject);return prefab;
        }
        static void Props()
        {
            var crate=SaveKit("FreightCrate",root=>{
                Direct("Container","Copper",new Vector3(0,.75f,0),new Vector3(2,1.5f,1.5f),root);
                var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.75f,0);collider.size=new Vector3(2,1.5f,1.5f);
                foreach(float x in new[]{-.68f,.68f})Direct("Iron straps","Iron",new Vector3(x,.75f,0),new Vector3(.12f,1.57f,1.56f),root);
                Direct("Copper lid","Brass",new Vector3(0,1.51f,0),new Vector3(2.06f,.12f,1.56f),root);
                Direct("Status light","Cyan",new Vector3(0,1.05f,-.79f),new Vector3(.37f,.065f,.05f),root);
                for(int x=-1;x<=1;x+=2)Direct("Latch","Brass",new Vector3(x*.69f,.68f,-.80f),new Vector3(.19f,.19f,.04f),root);
            });
            Transform freight=Group("01 / Cargo cover",environment);
            foreach(var c in map.cover){var instance=(GameObject)PrefabUtility.InstantiatePrefab(crate,freight);instance.transform.position=new Vector3(c.x,0,c.z);instance.transform.localScale=new Vector3(c.w/2,c.h/1.5f,c.d/1.5f);
                for(int j=-2;j<=2;j++)Box("Brass",c.x+j*.48f,.021f,c.z-c.d/2-.55f,.28f,.028f,.35f,35);}
            var pedestal=SaveKit("CrystalPedestal",root=>{
                Direct("Plinth","StoneDark",new Vector3(0,.57f,0),new Vector3(.95f,1.14f,.95f),root);
                Direct("Upper cornice","StoneLight",new Vector3(0,1.16f,0),new Vector3(1.18f,.22f,1.18f),root);
                Direct("Copper setting","Brass",new Vector3(0,1.34f,0),new Vector3(.64f,.13f,.64f),root);
                Direct("Faceted crystal","Crystal",new Vector3(0,1.93f,0),new Vector3(.27f,.7f,.27f),root,crystal);
                var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1,0);collider.size=new Vector3(1.18f,2,1.18f);
            });
            Transform crystalRoot=Group("02 / Crystal waylights",environment);
            foreach(var p in new[]{new Vector2(-4,2),new Vector2(4,2),new Vector2(-11,21),new Vector2(11,24),new Vector2(23.2f,31),new Vector2(7.8f,41),new Vector2(29,43),new Vector2(54,56),new Vector2(38,66),new Vector2(46,66),new Vector2(31,83),new Vector2(53,83)})
            {var instance=(GameObject)PrefabUtility.InstantiatePrefab(pedestal,crystalRoot);instance.transform.position=new Vector3(p.x,0,p.y);crystals.Add(instance.transform.Find("Faceted crystal"));}
            foreach(var p in map.bossPillars){Box("StoneLight",p.x,.18f,p.z,2.9f,.36f,2.9f);for(int j=0;j<5;j++)Box("Stone",p.x,.75f+j*.65f,p.z,2.25f,.62f,2.25f);Box("Brass",p.x,3.5f,p.z,2.4f,.18f,2.4f);Box("StoneLight",p.x,3.9f,p.z,2.7f,.5f,2.7f);
                ColliderBox("Core chamber pillar",new Vector3(p.x,2,p.z),new Vector3(2.7f,4.2f,2.7f));foreach(int side in new[]{-1,1})Box("Violet",p.x+side*.79f,2.1f,p.z-1.18f,.065f,1.7f,.06f);}
        }

        static void Cylinder(string material,Vector3 p,Vector3 scale,Quaternion rotation) { Piece(material,p,scale,rotation,cylinder); }
        static void Architecture()
        {
            foreach(var g in new[]{new Vector3(0,5,10),new Vector3(0,25.5f,26),new Vector3(42,39,28),new Vector3(42,60.7f,28),new Vector3(42,71,8)}){
                Box("Iron",g.x,6.3f,g.y,g.z,.5f,1.1f);Box("Brass",g.x,6.6f,g.y,g.z+.2f,.13f,1.25f);
                ColliderBox("Overhead freight gantry",new Vector3(g.x,6.3f,g.y),new Vector3(g.z,.5f,1.1f));
                for(float i=-g.z/2+.4f;i<g.z/2;i+=1.7f){Box("Copper",g.x+i,6.3f,g.y-.6f,.40f,.36f,.06f,15);Box("Cyan",g.x+i,5.96f,g.y-.12f,.42f,.06f,.22f);}
            }
            foreach(var p in new[]{new Vector3(-14.5f,20,17),new Vector3(14.5f,8,11),new Vector3(22,35,14),new Vector3(57.5f,51,20)}){
                Quaternion rot=Quaternion.Euler(90,0,0);Cylinder("Copper",new Vector3(p.x,4.95f,p.y),new Vector3(.35f,p.z/2,.35f),rot);
                for(float j=-p.z/2;j<p.z/2;j+=2.5f)Cylinder("Brass",new Vector3(p.x,4.95f,p.y+j),new Vector3(.52f,.08f,.52f),rot);}
            foreach(float z in new[]{26,30,34}){Box("Iron",21.5f,5.6f,z,5,.25f,.4f);for(float x=20.2f;x<24;x+=2.6f)Cylinder("Copper",new Vector3(x,5.95f,z),new Vector3(.22f,2,.22f),Quaternion.Euler(90,0,0));}
            for(int i=0;i<24;i++){float t=i/23f;Box("Iron",21.6f,4.7f-Mathf.Sin(t*Mathf.PI)*.65f,24+t*12,.055f,.06f,.57f);if(i%6==0)Box("Amber",21.6f,4.55f-Mathf.Sin(t*Mathf.PI)*.65f,24+t*12,.13f,.2f,.13f);}
            foreach(var p in new[]{new Vector2(-15.5f,24),new Vector2(16,27),new Vector2(25,59),new Vector2(60,60),new Vector2(27,93),new Vector2(58,93)}){
                Box("StoneDark",p.x,4.1f,p.y,3.4f,8.2f,3.4f);for(int i=0;i<5;i++)Box("StoneLight",p.x,.5f+i*1.65f,p.y,3.6f,.23f,3.6f);
                Box("Copper",p.x,8.3f,p.y,3.9f,.55f,3.9f);Box("Cyan",p.x,6,p.y-1.73f,.5f,2,.055f);}
            foreach(var p in new[]{new Vector2(25,40),new Vector2(59,54)}){
                Cylinder("Iron",new Vector3(p.x,2.7f,p.y),new Vector3(3.2f,2.7f,3.2f),Quaternion.identity);
                foreach(float y in new[]{.3f,1.8f,3.7f,5.3f})Cylinder("Brass",new Vector3(p.x,y,p.y),new Vector3(3.45f,.08f,3.45f),Quaternion.identity);
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Box("Copper",p.x+Mathf.Sin(a)*1.6f,2.7f,p.y+Mathf.Cos(a)*1.6f,.18f,5,.18f);}
                Cylinder("StoneLight",new Vector3(p.x,5.8f,p.y),new Vector3(2.3f,.5f,2.3f),Quaternion.identity);
            }
            // Generator panels are placed at the back of the spawn bays, leaving their centers free.
            foreach(var p in new[]{new Vector2(-17.1f,13),new Vector2(17.1f,13),new Vector2(25.1f,54),new Vector2(59.8f,46)}){
                Box("Iron",p.x,1.75f,p.y,1.05f,3.5f,1.1f);Box("Copper",p.x,2.8f,p.y,1.2f,.35f,1.2f);Box("Amber",p.x,2.05f,p.y-.59f,.48f,.10f,.04f);
                ColliderBox("Generator cabinet",new Vector3(p.x,1.75f,p.y),new Vector3(1.2f,3.5f,1.2f));for(int i=0;i<4;i++)Box("Brass",p.x,1.2f+i*.23f,p.y-.60f,.8f,.05f,.04f);}
            Sign("01 / LOADING COURT",0,5.2f,26.3f,5.7f);Sign("MAINTENANCE >",3,3.1f,26.35f,4.8f);
            Sign("NO SIGNAL",21.5f,3.3f,37.7f,3);Sign("02 / GENERATOR HALL",42,5.45f,60.5f,7);
            Sign("CORE ACCESS",42,5.7f,71.2f,5.1f);Sign("WARDEN / 0",42,9.4f,97.5f,5.8f);
        }

        static void Gates()
        {
            foreach(var info in new[]{new Vector4(13,20.5f,5.4f,90),new Vector4(27.9f,46.5f,5.4f,90),new Vector4(42,62,8.4f,0)}){
                Transform root=Group("Gate / "+(info.x==13?"outer encounter":info.x<30?"service ambush":"inner encounter"),environment);root.position=new Vector3(info.x,0,info.y);root.rotation=Quaternion.Euler(0,info.w,0);
                float width=info.z;for(int s=-1;s<=1;s+=2){Direct("Door pillar","StoneLight",new Vector3(s*(width/2+.3f),2.75f,0),new Vector3(.65f,5.5f,1.15f),root,solid:true);Direct("Copper cuff","Copper",new Vector3(s*(width/2+.3f),4.5f,0),new Vector3(.82f,.6f,1.33f),root);}
                Direct("Lintel","StoneLight",new Vector3(0,5.5f,0),new Vector3(width+1.35f,.55f,1.25f),root);
                Direct("Copper crown","Copper",new Vector3(0,5.9f,0),new Vector3(width+1.6f,.25f,1.4f),root);
                var gate=root.gameObject.AddComponent<DescentGate>();gate.encounter=info.x==13?"fight1":info.x<30?"ambush":"fight2";gate.leaves=new Transform[2];
                for(int i=0;i<2;i++){Transform leaf=Group("Raised gate leaf "+i,root);leaf.localPosition=new Vector3((i==0?-1:1)*width/4,2.15f,0);Direct("Door slab","Iron",Vector3.zero,new Vector3(width/2-.06f,4.2f,.4f),leaf,solid:true);
                    for(int j=0;j<5;j++)Direct("Door stripe","Copper",new Vector3(0,-1.5f+j*.72f,-.24f),new Vector3(width/2-.18f,.42f,.14f),leaf);
                    Direct("Access light","Cyan",new Vector3(0,0,-.33f),new Vector3(.13f,2.7f,.045f),leaf);gate.leaves[i]=leaf;}
                gate.SetClosed(false);
            }
        }
        static Mesh Torus(float radius,float tube,int rings=64,int sides=6)
        {
            var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<=rings;i++)for(int j=0;j<=sides;j++){float a=i*2*Mathf.PI/rings,b=j*2*Mathf.PI/sides;v.Add(new Vector3((radius+tube*Mathf.Cos(b))*Mathf.Cos(a),(radius+tube*Mathf.Cos(b))*Mathf.Sin(a),tube*Mathf.Sin(b)));uv.Add(new Vector2((float)i/rings,(float)j/sides));}
            for(int i=0;i<rings;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;t.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var m=new Mesh{name="Ring "+radius};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();return m;
        }
        static void Reactor()
        {
            Transform portal=Group("03 / AURORA PORTAL",environment);portal.position=new Vector3(42,4.8f,97.4f);var motion=portal.gameObject.AddComponent<DescentPortalMotion>();motion.crystals=crystals.ToArray();
            Mesh outer=Torus(3.6f,.15f);Asset(outer,Root+"/Meshes/PortalOuterRing.asset");outer=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/PortalOuterRing.asset");
            Mesh inner=Torus(3.1f,.035f);Asset(inner,Root+"/Meshes/PortalInnerRing.asset");inner=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/PortalInnerRing.asset");
            Direct("Violet containment ring","Violet",Vector3.zero,new Vector3(1,1.13f,1),portal,outer);motion.innerRing=Direct("Rotating cyan rune ring","Cyan",new Vector3(0,0,-.12f),new Vector3(1,1.13f,1),portal,inner).transform;
            var discMesh=new Mesh{name="Portal energy disc"};var vertices=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{new Vector2(.5f,.5f)};var triangles=new List<int>();
            for(int i=0;i<=64;i++){float a=i*Mathf.PI*2/64;vertices.Add(new Vector3(Mathf.Cos(a)*3.53f,Mathf.Sin(a)*3.53f,0));uv.Add(new Vector2(Mathf.Cos(a)*.5f+.5f,Mathf.Sin(a)*.5f+.5f));if(i>0)triangles.AddRange(new[]{0,i,i+1});}
            discMesh.SetVertices(vertices);discMesh.SetUVs(0,uv);discMesh.SetTriangles(triangles,0);discMesh.RecalculateNormals();Asset(discMesh,Root+"/Meshes/PortalDisc.asset");discMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/PortalDisc.asset");
            var portalMat=new Material(Shader.Find("Skybreak/Descent Portal")){name="Aurora portal energy"};Asset(portalMat,Root+"/Materials/AuroraPortal.mat");portalMat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/AuroraPortal.mat");
            var surface=Group("Swirling energy surface",portal);surface.localPosition=new Vector3(0,0,.05f);surface.localScale=new Vector3(1,1.13f,1);surface.gameObject.AddComponent<MeshFilter>().sharedMesh=discMesh;surface.gameObject.AddComponent<MeshRenderer>().sharedMaterial=portalMat;
            for(int i=0;i<13;i++){float a=i*Mathf.PI/12;Box("StoneLight",42+Mathf.Cos(a)*4.7f,1.2f+Mathf.Sin(a)*7.2f,97.7f,1.2f,1.3f,1.7f);Box("Copper",42+Mathf.Cos(a)*4.7f,1.2f+Mathf.Sin(a)*7.2f,96.77f,.25f,.6f,.09f);}
            for(int i=0;i<24;i++){float a=i*Mathf.PI/12;foreach(float radius in new[]{6.5f,11.8f}){Box("Brass",42+Mathf.Sin(a)*radius,.015f,84+Mathf.Cos(a)*radius,.08f,.028f,2*Mathf.PI*radius/24,a*Mathf.Rad2Deg+90);}
                Box("Violet",42+Mathf.Sin(a)*13.5f,.02f,84+Mathf.Cos(a)*13.5f,.14f,.03f,.8f,a*Mathf.Rad2Deg);if(i%2==0)Box("Cyan",42+Mathf.Sin(a)*9,.02f,84+Mathf.Cos(a)*9,.07f,.028f,2.5f,a*Mathf.Rad2Deg);}
        }

        static void RouteAndAnchors()
        {
            // Jagged arrays are not supported by JsonUtility; use the reviewed native coordinates.
            Vector2[] route={new Vector2(0,-4),new Vector2(0,14),new Vector2(0,21),new Vector2(8,21),new Vector2(21,21),new Vector2(21,35),new Vector2(9,35),new Vector2(9,46),new Vector2(37,46),new Vector2(42,46),new Vector2(42,57),new Vector2(42,66),new Vector2(42,84)};
            for(int i=1;i<route.Length;i++){Vector2 a=route[i-1],b=route[i];float length=Vector2.Distance(a,b),yaw=Mathf.Atan2(b.x-a.x,b.y-a.y)*Mathf.Rad2Deg;
                for(float d=0;d<length;d+=1.5f){Vector2 p=Vector2.Lerp(a,b,d/length);Box("Cyan",p.x,.016f,p.y,.045f,.025f,.7f,yaw);}}
            foreach(var p in new[]{new Vector2(0,3),new Vector2(0,8),new Vector2(21,25),new Vector2(9,40),new Vector2(42,64)}){float w=p.x==0?7:p.x==42?5:3;
                Box("Iron",p.x,.002f,p.y,w,.025f,.60f);for(float j=-w/2;j<w/2;j+=.22f)Box("StoneLight",p.x+j,.018f,p.y,.045f,.018f,.53f);}
            Transform anchors=Group("04 / Gameplay anchors (future agents)");
            foreach(var pair in new[]{("PlayerStart",new Vector3(0,.1f,-4)),("FirstEncounter",new Vector3(0,.1f,9)),("AmbushTrigger",new Vector3(21,.1f,27)),("SecondEncounter",new Vector3(32,.1f,46)),("BossCheckpoint",new Vector3(42,.1f,65)),("BossEntry",new Vector3(42,.1f,76)),("BossSpawn",new Vector3(42,.1f,91))}){Transform anchor=Group(pair.Item1,anchors);anchor.position=pair.Item2;}
            foreach(var pair in new[]{("FirstFightSpawn_W",new Vector3(-15,0,13)),("FirstFightSpawn_E",new Vector3(15,0,13)),("AmbushSpawn_A",new Vector3(17,0,27)),("AmbushSpawn_B",new Vector3(14,0,40)),("SecondFightSpawn_W",new Vector3(26.5f,0,54)),("SecondFightSpawn_E",new Vector3(58,0,46))}){Transform anchor=Group(pair.Item1,anchors);anchor.position=pair.Item2;}
        }
        static Light PointLight(string name,Vector3 p,string color,float intensity,float range,Transform parent)
        {
            var go=Group(name,parent);go.position=p;var light=go.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=Hex(color);light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;return light;
        }
        static void LightingAndCamera()
        {
            Transform lighting=Group("05 / Lighting and atmosphere");
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Hex("758D9D");RenderSettings.ambientEquatorColor=Hex("3D5869");RenderSettings.ambientGroundColor=Hex("172D3E");RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=Hex("102537");RenderSettings.fogDensity=.008f;
            var sun=Group("Cool skylight / soft shadows",lighting).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.color=Hex("CBDCE4");sun.intensity=1.65f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.78f;RenderSettings.sun=sun;
            PointLight("Warm loading lamps",new Vector3(0,4.1f,14),"FFBD7E",7,16,lighting);
            PointLight("Service passage amber",new Vector3(21,3.8f,29),"FFB977",4,9,lighting);
            PointLight("Generator warm fill",new Vector3(42,4.8f,48),"F3BB81",7,18,lighting);
            PointLight("Portal violet spill",new Vector3(42,4.8f,95),"AA78FF",9,15,lighting);
            PointLight("Core cool fill",new Vector3(42,4.5f,80),"74DDE2",5,17,lighting);
            var pipeline=UnityEngine.Object.Instantiate(UniversalRenderPipeline.asset);pipeline.name="Descent Balanced URP";pipeline.supportsHDR=true;pipeline.msaaSampleCount=2;pipeline.renderScale=1;pipeline.shadowDistance=40;pipeline.shadowCascadeCount=2;pipeline.maxAdditionalLightsCount=4;pipeline.mainLightShadowmapResolution=1024;
            var pipelineSettings=new SerializedObject(pipeline);pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            Asset(pipeline,Root+"/Settings/DescentBalancedURP.asset");pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Settings/DescentBalancedURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Descent color and glow";
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
            var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.15f;bloom.intensity.value=.28f;bloom.scatter.value=.62f;bloom.maxIterations.value=4;bloom.highQualityFiltering.value=false;
            var color=profile.Add<ColorAdjustments>(true);color.contrast.value=8;color.saturation.value=6;color.postExposure.value=.15f;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.16f;vignette.smoothness.value=.45f;
            Asset(profile,Root+"/Settings/DescentVolume.asset");foreach(var c in profile.components)if(!AssetDatabase.Contains(c))AssetDatabase.AddObjectToAsset(c,profile);
            var volume=Group("Global grade / subtle bloom",lighting).gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"/Settings/DescentVolume.asset");
            Transform cameraRoot=Group("06 / Map preview camera");var cam=cameraRoot.gameObject.AddComponent<Camera>();cam.tag="MainCamera";cam.fieldOfView=65;cam.nearClipPlane=.1f;cam.farClipPlane=220;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Hex("102537");cam.allowHDR=true;
            cam.gameObject.AddComponent<AudioListener>();var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1;data.antialiasing=AntialiasingMode.None;
            var preview=cameraRoot.gameObject.AddComponent<DescentMapPreview>();preview.view=cam;
            Transform views=Group("Camera viewpoints");preview.viewpoints=new Transform[5];preview.captions=new[]{"WHOLE FACILITY / ONE CONTINUOUS MAP","01 / LOADING COURT","SERVICE PASSAGE / AMBUSH BAYS","02 / GENERATOR HALL","03 / AURORA CORE CHAMBER"};
            Vector3[] positions={new Vector3(-77,105,-55),new Vector3(0,3.4f,-7.5f),new Vector3(21.4f,2.6f,25),new Vector3(32,3.2f,40),new Vector3(42,3.5f,73.5f)};
            Vector3[] targets={new Vector3(23,0,45),new Vector3(0,2,22),new Vector3(21.4f,2,36),new Vector3(43,2,56),new Vector3(42,4.3f,97)};
            for(int i=0;i<5;i++){Transform t=Group(preview.captions[i],views);t.position=positions[i];t.LookAt(targets[i]);preview.viewpoints[i]=t;}preview.ShowShot(1);
        }

        [MenuItem("Skybreak/Descent/Build map scene")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before building.");
            if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before building.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Descent scene already exists; preserving it.");
            foreach(string dir in new[]{"Materials","Meshes","Textures","Prefabs","Settings","Reports"})Directory.CreateDirectory(Root+"/"+dir);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));AssetDatabase.Refresh();
            map=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"/Data/DescentMapLayout.json"));paints.Clear();batches.Clear();chunks.Clear();crystals.Clear();pieceCount=0;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);scene.name="SkybreakDescentDemo";
            environment=Group("SKYBREAK / THE DESCENT / Environment");colliders=Group("Physical collision",environment);
            CreatePalette();CreateMeshes();FloorsAndWalls();Props();Architecture();Gates();Reactor();RouteAndAnchors();FlushBatches();LightingAndCamera();
            var navigation=environment.gameObject.AddComponent<NavMeshSurface>();navigation.collectObjects=CollectObjects.Children;navigation.useGeometry=NavMeshCollectGeometry.PhysicsColliders;navigation.agentTypeID=NavMesh.GetSettingsByIndex(0).agentTypeID;navigation.overrideVoxelSize=true;navigation.voxelSize=.12f;navigation.BuildNavMesh();
            if(navigation.navMeshData==null)throw new InvalidOperationException("Navigation bake produced no data.");AssetDatabase.CreateAsset(navigation.navMeshData,Root+"/Settings/DescentNavMesh.asset");
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Skybreak.Editor.SkybreakScenePaths.ConfigureBuildScenes();
            Selection.activeGameObject=environment.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(22,0,45),new Vector3(85,15,114)),false);
            return "Created "+ScenePath+"; "+pieceCount+" modular pieces, "+batches.Count+" spatial material batches, baked navigation and five preview views.";
        }

        public static string Validate()
        {
            if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the Descent scene.");
            var report=new System.Text.StringBuilder();int paths=0;
            Vector3[] route={new Vector3(0,0,-4),new Vector3(0,0,21),new Vector3(21,0,21),new Vector3(21,0,35),new Vector3(9,0,35),new Vector3(9,0,46),new Vector3(42,0,46),new Vector3(42,0,66),new Vector3(42,0,84)};
            for(int i=1;i<route.Length;i++){if(!NavMesh.SamplePosition(route[i-1],out var a,1,NavMesh.AllAreas)||!NavMesh.SamplePosition(route[i],out var b,1,NavMesh.AllAreas))throw new Exception("Missing walk surface near route leg "+i);
                var p=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,p)||p.status!=NavMeshPathStatus.PathComplete)throw new Exception("Disconnected route leg "+i);paths++;}
            int missing=0,materials=0;foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader==null||!renderer.sharedMaterial.shader.isSupported)materials++;missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(renderer.gameObject);}
            if(materials>0||missing>0)throw new Exception("Unsupported materials or missing components detected.");
            var cam=Camera.main;var data=cam.GetUniversalAdditionalCameraData();var profile=UnityEngine.Object.FindFirstObjectByType<Volume>().sharedProfile;
            if(!UniversalRenderPipeline.asset.supportsHDR||!data.renderPostProcessing||profile.components.Count<3)throw new Exception("Incomplete URP visual setup.");
            report.AppendLine("Route paths complete: "+paths+" / 8");report.AppendLine("Missing components: "+missing);report.AppendLine("Unsupported materials: "+materials);report.AppendLine("HDR, camera post-processing and volume profile: verified");
            report.AppendLine("Renderers: "+UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length);report.AppendLine("Colliders: "+UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length);
            File.WriteAllText(Root+"/Reports/MapValidation.txt",report.ToString());AssetDatabase.Refresh();return report.ToString();
        }
    }
}
