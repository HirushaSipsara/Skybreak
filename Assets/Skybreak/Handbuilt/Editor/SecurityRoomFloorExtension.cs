#pragma warning disable 0649 // Layout fields are populated by JsonUtility.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Skybreak.Descent;

namespace Skybreak.Handbuilt.Editor
{
    public static class SecurityRoomFloorExtension
    {
        const string Root = "Assets/Skybreak/Handbuilt";
        const string ScenePath = Skybreak.Editor.SkybreakScenePaths.Main;
        const string AdditionRoot = "06 / Security holding rooms";
        [Serializable] class Layout { public Room[] rooms; }
        [Serializable] class Room
        {
            public string id;
            public int x1,x2,z1,z2,connectorX1,connectorX2,connectorZ1,connectorZ2;
            public float doorX,doorZ,doorWidth,doorHeight;
        }
        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        static Material GuideMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void Stripe(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            // Remove only the collider on the newly created guide primitive.
            UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        }

        [MenuItem("Skybreak/World Editing/Add security room floors (once)")]
        public static string Add()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before adding the room floors.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new InvalidOperationException("Open the stone floor scene first.");
            if(GameObject.Find(AdditionRoot)!=null)throw new InvalidOperationException("Room floors already exist. Edit them manually; do not duplicate the extension.");
            var floor=GameObject.Find("01 / STONE FLOOR / editable one metre tiles");
            if(floor==null)throw new InvalidOperationException("Expected stone floor root was not found.");
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"/Data/SecurityRoomLayout.json"));
            var prefabs=Enumerable.Range(1,16).Select(i=>AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/FloorTile_"+i.ToString("00")+".prefab")).ToArray();
            if(prefabs.Any(p=>p==null))throw new InvalidOperationException("A required floor tile prefab is missing.");
            EditorSceneManager.SaveScene(scene);
            string backup=Skybreak.Editor.SkybreakScenePaths.BackupMainScene("BeforeSideRooms");

            var originalColliders=floor.GetComponentsInChildren<BoxCollider>();
            var occupied=new HashSet<Vector2Int>(originalColliders.Select(c=>new Vector2Int(Mathf.FloorToInt(c.transform.position.x),Mathf.FloorToInt(c.transform.position.z))));
            int originalCount=occupied.Count,added=0;
            var additions=new GameObject(AdditionRoot).transform;
            var data=new List<string>();
            for(int index=0;index<layout.rooms.Length;index++)
            {
                var room=layout.rooms[index];
                var floorGroup=Group((index==0?"West":"East")+" holding room / 8m x 8m",floor.transform);
                var roomTiles=Group("Room floor",floorGroup);
                var passageTiles=Group("Entrance floor / 4m wide",floorGroup);
                added+=Fill(room.x1,room.x2,room.z1,room.z2,roomTiles,prefabs,occupied);
                added+=Fill(room.connectorX1,room.connectorX2,room.connectorZ1,room.connectorZ2,passageTiles,prefabs,occupied);
                var guides=Group(room.id+" / layout guides",additions);
                var marker=Group("Room footprint / 8m x 8m",guides);
                marker.position=new Vector3((room.x1+room.x2)*.5f,.025f,(room.z1+room.z2)*.5f);
                Color color=index==0?new Color(1,.56f,.22f):new Color(.2f,.8f,.9f);
                var info=marker.gameObject.AddComponent<SecurityRoomLayoutMarker>();
                info.description=(index==0?"WEST":"EAST")+" / SECURITY HOLD / 8 x 8 m";info.guideColor=color;
                var guideMaterial=GuideMaterial(index==0?"WestRoomFloorGuide":"EastRoomFloorGuide",color);
                Stripe("South floor guide",marker,new Vector3(0,0,-3.9f),new Vector3(7.8f,.012f,.045f),guideMaterial);
                Stripe("North floor guide",marker,new Vector3(0,0,3.9f),new Vector3(7.8f,.012f,.045f),guideMaterial);
                Stripe("Outer floor guide",marker,new Vector3(index==0?-3.9f:3.9f,0,0),new Vector3(.045f,.012f,7.8f),guideMaterial);
                // Inner edge has a 3m break for the future sliding door.
                float innerX=index==0?3.9f:-3.9f;
                Stripe("Door side guide A",marker,new Vector3(innerX,0,-2.7f),new Vector3(.045f,.012f,2.4f),guideMaterial);
                Stripe("Door side guide B",marker,new Vector3(innerX,0,2.7f),new Vector3(.045f,.012f,2.4f),guideMaterial);
                var door=Group("DoorOpening_"+(index==0?"West":"East")+" / SLIDING DOOR POSITION",guides);
                door.position=new Vector3(room.doorX,0,room.doorZ);door.rotation=Quaternion.Euler(0,90,0);
                var doorInfo=door.gameObject.AddComponent<SecurityRoomLayoutMarker>();
                doorInfo.description="FUTURE SLIDING DOOR / 3m wide x 3m high";
                doorInfo.footprint=new Vector3(room.doorWidth,room.doorHeight,.25f);doorInfo.centreOffset=new Vector3(0,room.doorHeight*.5f,0);doorInfo.guideColor=color;
                Stripe("Door sill guide / visual only",door,new Vector3(0,.026f,0),new Vector3(room.doorWidth,.015f,.2f),guideMaterial);
                var approach=Group("Entrance centre / future navigation waypoint",guides);
                approach.position=new Vector3((room.connectorX1+room.connectorX2)*.5f,0,room.doorZ);
                data.Add(room.id+": floor ["+room.x1+", "+room.x2+"] x ["+room.z1+", "+room.z2+"]; door ("+room.doorX+", 0, "+room.doorZ+") / Y rotation 90 / 3m x 3m.");
            }
            var preview=UnityEngine.Object.FindAnyObjectByType<DescentMapPreview>();
            preview.captions[3]="SECOND ARENA / WEST AND EAST HOLDING ROOMS";
            preview.viewpoints[3].name="Second arena / two side rooms / top view";
            preview.viewpoints[3].position=new Vector3(42.5f,31,50);
            preview.viewpoints[3].rotation=Quaternion.Euler(90,0,0);
            preview.ShowShot(3);
            Physics.SyncTransforms();
            var reached=new HashSet<Vector2Int>();var pending=new Queue<Vector2Int>();pending.Enqueue(new Vector2Int(0,-4));
            while(pending.Count>0){var c=pending.Dequeue();if(!occupied.Contains(c)||!reached.Add(c))continue;foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})pending.Enqueue(c+d);}
            int collisionHits=0;
            foreach(var room in layout.rooms)
            {
                for(int z=room.z1;z<room.z2;z++)for(int x=room.x1;x<room.x2;x++)if(FloorHit(x,z))collisionHits++;
                for(int z=room.connectorZ1;z<room.connectorZ2;z++)for(int x=room.connectorX1;x<room.connectorX2;x++)if(FloorHit(x,z))collisionHits++;
            }
            if(reached.Count!=occupied.Count || collisionHits!=144)
                throw new InvalidOperationException("New floor validation failed; saved pre-change scene is "+backup);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            string report="Backup: "+backup+"\nOriginal tiles retained: "+originalCount+"\nAdded floor tiles: "+added+"\nTotal floor tiles: "+occupied.Count+"\nConnected cells: "+reached.Count+" / "+occupied.Count+"\nNew floor collider checks: "+collisionHits+" / 144\nDoor markers: 2 / no blocking colliders\n"+string.Join("\n",data)+"\nLayout stage only: working doors, walls and agents are not added.\n";
            File.WriteAllText(Root+"/Reports/SecurityRoomFloorBuild.txt",report);
            Selection.activeGameObject=additions.gameObject;
            var sceneView=SceneView.lastActiveSceneView;
            if(sceneView!=null){sceneView.orthographic=true;sceneView.LookAt(new Vector3(42.5f,0,50),Quaternion.Euler(90,0,0),33);sceneView.Repaint();}
            return report;
        }
        static bool FloorHit(int x,int z) => Physics.Raycast(new Vector3(x+.5f,2,z+.5f),Vector3.down,out var hit,3)&&Mathf.Abs(hit.point.y)<.01f;
        static int Fill(int x1,int x2,int z1,int z2,Transform parent,GameObject[] prefabs,HashSet<Vector2Int> occupied)
        {
            int count=0;
            for(int z=z1;z<z2;z++)for(int x=x1;x<x2;x++)
            {
                var key=new Vector2Int(x,z);if(!occupied.Add(key))continue;
                uint hash=unchecked((uint)(x*73856093 ^ z*19349663));int variant=(int)(hash%16);
                var tile=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[variant]);tile.transform.SetParent(parent,false);
                tile.transform.position=new Vector3(x+.5f,0,z+.5f);tile.transform.rotation=Quaternion.Euler(0,(hash%4)*90,0);
                tile.name="Tile ["+x+", "+z+"] / "+(variant+1).ToString("00");count++;
            }
            return count;
        }
    }
}
