using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.Warden
{
    // Self-contained lab; no modifications to the team's main map or controllers.
    public sealed class WardenLab : MonoBehaviour
    {
        public bool fourWardens;
        public WardenMap Map {get;private set;}
        public WardenLabPlayer Player {get;private set;}
        public readonly List<WardenBrain> Wardens=new List<WardenBrain>();
        WardenNoiseBus noise;
        WardenObjective objective;
        GameObject door;
        readonly List<Material> ownedMaterials=new List<Material>();
        Material metal,energy,ground,wall,warning;
        GUIStyle textStyle;
        public void Start()
        {
            metal=Mat("Lab blue",new Color(.06f,.16f,.25f));
            energy=Mat("Lab cyan",Color.cyan,true);
            ground=Mat("Lab floor",new Color(.035f,.06f,.08f));
            wall=Mat("Lab cover",new Color(.18f,.25f,.29f));
            warning=Mat("Lab amber",new Color(1,.45f,.06f),true);
            noise=gameObject.AddComponent<WardenNoiseBus>();
            Piece("Floor",new Vector3(0,-.3f,0),new Vector3(29,.6f,29),ground);
            Piece("North",new Vector3(0,2,14),new Vector3(29,4,1),wall);
            Piece("South",new Vector3(0,2,-14),new Vector3(29,4,1),wall);
            Piece("East",new Vector3(14,2,0),new Vector3(1,4,29),wall);
            Piece("West",new Vector3(-14,2,0),new Vector3(1,4,29),wall);
            Piece("Divider north",new Vector3(0,1.8f,4),new Vector3(.8f,3.6f,5),wall);
            Piece("Divider south",new Vector3(0,1.8f,-4),new Vector3(.8f,3.6f,5),wall);
            door=Piece("Toggle security door",new Vector3(0,1.8f,0),new Vector3(.6f,3.6f,3),warning);
            door.SetActive(false);
            Piece("Cover A",new Vector3(-6,1,-4),new Vector3(1.5f,2,2),wall);
            Piece("Cover B",new Vector3(6,1,4),new Vector3(1.5f,2,2),wall);
            var pedestal=Piece("Protected crystal pedestal",new Vector3(8,.5f,8),new Vector3(.9f,1,.9f),metal);
            objective=pedestal.AddComponent<WardenObjective>();objective.noise=noise;
            var crystal=Piece("Objective crystal",new Vector3(8,1.5f,8),new Vector3(.5f,.75f,.5f),energy,false);
            crystal.transform.rotation=Quaternion.Euler(0,45,0);objective.crystalVisual=crystal.transform;
            Physics.SyncTransforms();
            Map=gameObject.AddComponent<WardenMap>();
            var positions=new List<Vector3>();var indices=new Dictionary<Vector2Int,int>();
            for(int x=-12;x<=12;x+=4)for(int z=-12;z<=12;z+=4)
            {
                Vector3 p=new Vector3(x,1,z);
                if(!Physics.CheckSphere(p+Vector3.up*.2f,.7f,Map.environmentMask,QueryTriggerInteraction.Ignore))
                {indices[new Vector2Int(x,z)]=positions.Count;positions.Add(p);}
            }
            var links=new List<WardenMap.Connection>();
            foreach(var pair in indices)foreach(var delta in new[]{new Vector2Int(4,0),new Vector2Int(0,4)})
                if(indices.TryGetValue(pair.Key+delta,out int to))links.Add(new WardenMap.Connection{a=pair.Value,b=to});
            Map.nodes=positions.ToArray();Map.connections=links.ToArray();Map.Build();
            var player=new GameObject("Arc FPS test player");player.layer=8;
            var capsule=player.AddComponent<CharacterController>();capsule.height=1.8f;capsule.radius=.35f;capsule.center=Vector3.up*.9f;
            Player=player.AddComponent<WardenLabPlayer>();Player.noise=noise;Player.objective=objective;
            var cameraObject=new GameObject("First-person camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(player.transform,false);cameraObject.transform.localPosition=Vector3.up*1.6f;
            Player.eye=cameraObject.AddComponent<Camera>();Player.eye.nearClipPlane=.05f;Player.eye.fieldOfView=75;
            Player.eye.backgroundColor=new Color(.015f,.025f,.045f);cameraObject.AddComponent<AudioListener>();
            var footsteps=player.AddComponent<WardenFootsteps>();footsteps.bus=noise;
            var light=new GameObject("Lab lighting").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(55,-30,0);
            RenderSettings.ambientLight=new Color(.3f,.4f,.5f);
            ResetRound();
        }
        Material Mat(string name,Color color,bool emission=false)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var m=new Material(shader){name=name,color=color};m.SetColor("_BaseColor",color);
            if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color);}
            ownedMaterials.Add(m);return m;
        }
        GameObject Piece(string name,Vector3 pos,Vector3 size,Material material,bool collide=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=pos;go.transform.localScale=size;go.transform.SetParent(transform);
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collide){var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);}
            return go;
        }
        public void ResetRound()
        {
            foreach(var w in Wardens)if(w!=null){w.gameObject.SetActive(false);Destroy(w.gameObject);}
            Wardens.Clear();noise.Clear();
            Player.ResetPlayer(new Vector3(-8,0,-8));objective.ResetState();
            foreach(var p in fourWardens?new[]{new Vector3(8,1,0),new Vector3(8,1,-8),new Vector3(4,1,8),new Vector3(12,1,4)}:new[]{new Vector3(8,1,0)})SpawnWarden(p);
        }
        public WardenBrain SpawnWarden(Vector3 position)
        {
            var go=new GameObject("Sachini Crystal Echo Warden");go.layer=8;go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,-90,0);
            var controller=go.AddComponent<CharacterController>();controller.radius=.62f;controller.height=1.5f;controller.center=Vector3.up*.2f;controller.stepOffset=.1f;
            var motor=go.AddComponent<WardenMotor>();motor.map=Map;
            var perception=go.AddComponent<WardenPerception>();perception.map=Map;perception.noise=noise;perception.target=Player;
            var brain=go.AddComponent<WardenBrain>();brain.map=Map;brain.objective=objective;
            var model=Resources.Load<GameObject>("CrystalEchoWarden/CrystalEchoWarden");
            GameObject visual;
            if(model!=null)
            {
                visual=Instantiate(model,go.transform);visual.name="Crystal Echo Warden visual";
                // FBX source is Z-up/-Y-forward. Verify import facing in the Unity checklist.
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    var mats=renderer.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)
                    {
                        string n=mats[i]!=null?mats[i].name.ToLowerInvariant():"";
                        mats[i]=n.Contains("cyan")?energy:n.Contains("amber")?warning:metal;
                    }
                    renderer.sharedMaterials=mats;
                }
            }
            else
            {
                visual=GameObject.CreatePrimitive(PrimitiveType.Capsule);visual.transform.SetParent(go.transform,false);visual.transform.localScale=new Vector3(.7f,.6f,.7f);
                var c=visual.GetComponent<Collider>();c.enabled=false;Destroy(c);
                visual.GetComponent<Renderer>().sharedMaterial=metal;
                Debug.LogError("Crystal Echo Warden FBX resource missing; showing diagnostic capsule.",this);
            }
            foreach(var child in go.GetComponentsInChildren<Transform>())child.gameObject.layer=8;
            var motion=go.AddComponent<WardenAnimation>();motion.brain=brain;motion.visual=visual.transform;motion.Initialize();
            var fx=go.AddComponent<WardenPresentation>();fx.brain=brain;fx.visual=visual.transform;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())if(renderer.name.Contains("resonance"))fx.core=renderer;
            var line=new GameObject("Echo pulse").AddComponent<LineRenderer>();line.transform.SetParent(go.transform,false);line.positionCount=2;line.useWorldSpace=true;line.widthMultiplier=.045f;line.sharedMaterial=energy;line.enabled=false;fx.beam=line;
            Wardens.Add(brain);return brain;
        }
        void Update()
        {
            var k=Keyboard.current;if(k==null||Map==null)return;
            if(k.f2Key.wasPressedThisFrame)SetDoorClosed(!door.activeSelf);
            if(k.f3Key.wasPressedThisFrame)objective.Disturb(1);
            if(k.f4Key.wasPressedThisFrame){fourWardens=!fourWardens;ResetRound();}
            if(k.rKey.wasPressedThisFrame)ResetRound();
        }
        public void SetDoorClosed(bool closed)
        {
            door.SetActive(closed);Physics.SyncTransforms();Map.Refresh();
        }
        void OnGUI()
        {
            if(Player==null)return;
            if(textStyle==null)textStyle=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};
            GUI.Box(new Rect(12,12,440,260),"");
            string status="CRYSTAL ECHO WARDEN — SACHINI\nWASD move · Mouse look · Shift sprint · Space jump\nClick fire · Q noise decoy · F disrupt nearby crystal\nF2 door · F3 alarm test · F4 1/4 Wardens · R reset\n";
            status+="Health "+Player.Health.ToString("0")+" · Crystal "+objective.integrity.ToString("0")+" · Door "+(door.activeSelf?"CLOSED":"OPEN")+"\n";
            if(Wardens.Count>0&&Wardens[0]!=null)
            {
                var w=Wardens[0];status+=w.Status+"\n"+w.Senses.Evidence+"\n";
                if(w.Senses.Belief!=null)status+="Evidence freshness "+w.Senses.Belief.Confidence(Time.time).ToString("P0")+" · Likely zone "+w.Senses.Belief.MostLikely+"\n";
                status+="AI tick "+w.LastTickMilliseconds.ToString("0.00")+" ms (not a benchmark) · Shots "+w.Shots;
            }
            if(!Player.Alive)status+="\nArc defeated — press R to restart.";
            GUI.Label(new Rect(24,22,418,240),status,textStyle);
            GUI.Label(new Rect(Screen.width/2-6,Screen.height/2-12,24,24),"+");
        }
        void OnDestroy(){foreach(var m in ownedMaterials)if(m!=null)Destroy(m);}
    }
}
