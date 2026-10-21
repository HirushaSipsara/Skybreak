using UnityEngine;
using UnityEngine.Rendering;
using Skybreak.AI;
using Skybreak.Descent;

namespace Skybreak.LocalCombatLab
{
    // Baseline weapon rig and feedback; the imported model follows this pose.
    [DefaultExecutionOrder(100)]
    public sealed class LocalPlayerFeedback : MonoBehaviour
    {
        public CombatAudio audioCues;
        public Transform Muzzle { get; private set; }
        public GameObject Weapon { get; private set; }
        public int FireReactions { get; private set; }
        public int HitsReacted { get; private set; }
        public int JumpBursts { get; private set; }
        public bool HitActive => Time.time - hitAt < .4f;
        DescentCharacterController arc;
        Transform headPivot;
        Transform leftElbow, rightElbow, magazine;
        LocalMagazine ammunition;
        Vector3 aimTarget;
        Mesh bevelMesh;
        AudioClip reloadClick;
        bool wasReloading;
        Quaternion visualRest;
        Vector3 visualPosition;
        Material dark, teal, orange, cyan, armor;
        LineRenderer jumpRing;
        Vector3 burstPosition;
        float fireAt = -100, hitAt = -100, jumpAt = -100, markerAt = -100, gait, aimPitch, pace;
        bool grounded = true;
        int jumpNumber;
        string hitDirection = "";
        public float FireStrength => Mathf.Clamp01(1 - (Time.time - fireAt) / .14f);
        public Vector3 CameraOffset => HitActive ? new Vector3(Mathf.Sin(Time.time * 77), Mathf.Sin(Time.time * 61), 0) * (.065f * Mathf.Clamp01(1 - (Time.time - hitAt) / .4f)) : Vector3.zero;
        public Vector3 CameraAngles => new Vector3(-1.15f * FireStrength, 0, HitActive ? Mathf.Sin(Time.time * 55) * 2.5f * Mathf.Clamp01(1 - (Time.time - hitAt) / .4f) : 0);
        Material Paint(Color colour, bool unlit = false)
        {
            var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour); if (!unlit) material.SetFloat("_Smoothness", .3f); return material;
        }
        Transform Piece(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 8; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }
        Mesh BevelMesh()
        {
            var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
            System.Action<Vector3[],Vector3> face=(points,normal)=>
            {
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)System.Array.Reverse(points);
                int start=vertices.Count;vertices.AddRange(points);for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
            };
            const float h=.5f,c=.36f;
            Vector2[] perimeter={new Vector2(-c,-h),new Vector2(c,-h),new Vector2(h,-c),new Vector2(h,c),new Vector2(c,h),new Vector2(-c,h),new Vector2(-h,c),new Vector2(-h,-c)};
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                var points=new Vector3[8];var normal=Vector3.zero;normal[axis]=sign;
                for(int i=0;i<8;i++){var v=Vector3.zero;v[axis]=sign*h;v[(axis+1)%3]=perimeter[i].x;v[(axis+2)%3]=perimeter[i].y;points[i]=v;}face(points,normal);
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                face(new[]{new Vector3(x*c,y*h,z*h),new Vector3(x*h,y*c,z*h),new Vector3(x*h,y*h,z*c)},new Vector3(x,y,z));
            var mesh=new Mesh{name="Arc chamfered armor"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        Transform Armor(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {var piece=Piece(name,parent,position,scale,material);piece.GetComponent<MeshFilter>().sharedMesh=bevelMesh;return piece;}
        void PaintExisting()
        {
            foreach(var renderer in arc.visual.GetComponentsInChildren<MeshRenderer>())
            {
                string name=renderer.sharedMaterial!=null?renderer.sharedMaterial.name:"";
                renderer.sharedMaterial=name=="Arc_Suit"?teal:name=="Arc_Armor"?armor:name=="Arc_Harness"?orange:name=="Arc_Dark"?dark:renderer.sharedMaterial;
                if(renderer.name=="Boot")renderer.sharedMaterial=dark;
            }
            foreach(string name in new[]{"Orange rescue vest","Vest center stripe","Hair cap","Hair back","Hair fringe","Visor frame","Cyan visor","Cyan rescue unit"})
            {var part=arc.visual.Find(name);if(part!=null)part.gameObject.SetActive(false);}
            var pack=arc.visual.Find("Backpack");pack.localScale=new Vector3(.37f,.43f,.17f);pack.localPosition=new Vector3(0,1.16f,-.24f);pack.GetComponent<Renderer>().sharedMaterial=dark;
            var inset=arc.visual.Find("Backpack inset");inset.localScale=new Vector3(.29f,.28f,.03f);inset.localPosition=new Vector3(0,1.17f,-.34f);inset.GetComponent<Renderer>().sharedMaterial=teal;
            Armor("Left chest plate",arc.visual,new Vector3(-.12f,1.21f,.19f),new Vector3(.23f,.33f,.10f),armor).localRotation=Quaternion.Euler(0,0,8);
            Armor("Right chest plate",arc.visual,new Vector3(.12f,1.21f,.19f),new Vector3(.23f,.33f,.10f),teal).localRotation=Quaternion.Euler(0,0,-8);
            Piece("Chest energy slit",arc.visual,new Vector3(.06f,1.27f,.25f),new Vector3(.17f,.025f,.015f),cyan);
            Armor("Abdominal plates",arc.visual,new Vector3(0,.98f,.18f),new Vector3(.42f,.14f,.07f),dark);
            Armor("Orange scarf collar",arc.visual,new Vector3(0,1.44f,0),new Vector3(.28f,.13f,.30f),orange);
            Armor("Scarf tail",arc.visual,new Vector3(-.23f,1.27f,-.23f),new Vector3(.11f,.36f,.035f),orange).localRotation=Quaternion.Euler(-8,0,-15);
            for(int side=-1;side<=1;side+=2)
            {
                Piece("Back energy vent",arc.visual,new Vector3(side*.08f,1.18f,-.365f),new Vector3(.035f,.18f,.015f),cyan);
                Armor("Utility pouch",arc.visual,new Vector3(side*.24f,.83f,.18f),new Vector3(.13f,.18f,.12f),dark);
            }
            foreach(var leg in new[]{arc.leftLeg,arc.rightLeg})
            {
                Armor("Shin plate",leg,new Vector3(0,-.52f,.14f),new Vector3(.18f,.19f,.07f),armor);
                Piece("Boot orange trim",leg,new Vector3(0,-.70f,.215f),new Vector3(.21f,.03f,.025f),orange);
            }
        }
        Transform ArticulateArm(Transform shoulder)
        {
            var sleeve=shoulder.Find("Sleeve");sleeve.localPosition=new Vector3(0,-.17f,0);sleeve.localScale=new Vector3(.18f,.24f,.22f);
            var joint=new GameObject("Articulated elbow").transform;joint.SetParent(shoulder,false);joint.localPosition=new Vector3(0,-.30f,0);
            var glove=shoulder.Find("Glove");glove.SetParent(joint,false);glove.localPosition=new Vector3(0,-.28f,.015f);
            var cuff=shoulder.Find("Forearm cuff");cuff.SetParent(joint,false);cuff.localPosition=new Vector3(0,-.19f,0);cuff.localScale=new Vector3(.20f,.12f,.24f);
            Armor("Forearm armor",joint,new Vector3(0,-.105f,0),new Vector3(.16f,.19f,.20f),dark);
            Piece("Wrist energy strip",joint,new Vector3(0,-.19f,.125f),new Vector3(.13f,.02f,.015f),cyan);
            var shoulderArmor=shoulder.Find("Shoulder armor");shoulderArmor.GetComponent<MeshFilter>().sharedMesh=bevelMesh;
            shoulderArmor.localScale=new Vector3(.27f,.20f,.30f);return joint;
        }
        public void Initialize()
        {
            if (arc != null) return;
            arc = GetComponent<DescentCharacterController>(); audioCues = GetComponent<CombatAudio>();
            visualRest = arc.visual.localRotation; visualPosition = arc.visual.localPosition;
            dark = Paint(new Color(.035f,.055f,.07f)); teal = Paint(new Color(.055f,.23f,.28f)); orange = Paint(new Color(.85f,.36f,.08f)); cyan = Paint(new Color(.2f,2,2.4f),true);armor=Paint(new Color(.58f,.68f,.66f));
            bevelMesh=BevelMesh();PaintExisting();ammunition=GetComponent<LocalMagazine>();
            var head = arc.visual.Find("Head"); var joint = new GameObject("Local animated head pivot"); joint.transform.SetParent(arc.visual,false); joint.transform.localPosition = head.localPosition; headPivot = joint.transform;
            string[] headParts = { "Head", "Hair cap", "Hair back", "Hair fringe", "Visor frame", "Cyan visor" };
            foreach (var name in headParts) { var part = arc.visual.Find(name); if (part != null) part.SetParent(headPivot, true); }
            Armor("Explorer helmet",headPivot,new Vector3(0,.025f,-.015f),new Vector3(.46f,.44f,.43f),teal);
            Armor("Helmet jaw",headPivot,new Vector3(0,-.12f,.14f),new Vector3(.33f,.17f,.20f),dark);
            Piece("Visor recess",headPivot,new Vector3(0,.06f,.211f),new Vector3(.37f,.095f,.035f),dark);
            Piece("Explorer luminous visor",headPivot,new Vector3(0,.063f,.234f),new Vector3(.31f,.042f,.02f),cyan);
            Armor("Helmet crown",headPivot,new Vector3(0,.25f,-.06f),new Vector3(.10f,.08f,.26f),orange);
            for(int side=-1;side<=1;side+=2)Armor("Helmet comms",headPivot,new Vector3(side*.235f,.01f,-.01f),new Vector3(.06f,.16f,.18f),armor);
            leftElbow=ArticulateArm(arc.leftArm);rightElbow=ArticulateArm(arc.rightArm);
            Weapon = new GameObject("Arc energy rifle"); Weapon.layer = 8; Weapon.transform.SetParent(arc.visual,false);
            Weapon.transform.localPosition = new Vector3(.13f,1.20f,.37f);
            Armor("Rifle receiver",Weapon.transform,new Vector3(0,.015f,.09f),new Vector3(.16f,.16f,.43f),teal);
            Armor("Rifle stock",Weapon.transform,new Vector3(0,.025f,-.24f),new Vector3(.12f,.17f,.20f),dark);
            Piece("Rifle barrel",Weapon.transform,new Vector3(0,0,.39f),new Vector3(.11f,.11f,.29f),dark);
            Armor("Rifle hand grip",Weapon.transform,new Vector3(0,-.10f,-.02f),new Vector3(.09f,.17f,.10f),orange);
            magazine=Armor("Removable magazine",Weapon.transform,new Vector3(0,-.16f,.16f),new Vector3(.09f,.20f,.12f),dark);
            Piece("Magazine charge window",magazine,new Vector3(.515f,0,0),new Vector3(.04f,.55f,.5f),cyan);
            Piece("Rifle muzzle",Weapon.transform,new Vector3(0,0,.548f),new Vector3(.09f,.08f,.025f),cyan);
            Piece("Rifle energy rail",Weapon.transform,new Vector3(0,.105f,.12f),new Vector3(.06f,.02f,.28f),cyan);
            Armor("Rifle sight",Weapon.transform,new Vector3(0,.15f,-.03f),new Vector3(.10f,.08f,.13f),dark);
            var muzzle = new GameObject("Muzzle origin"); muzzle.transform.SetParent(Weapon.transform,false); muzzle.transform.localPosition = new Vector3(0,.025f,.565f); Muzzle = muzzle.transform;
            aimTarget=transform.position+transform.forward*80+Vector3.up*1.6f;
            var click=new float[1920];var random=new System.Random(311);float low=0;
            for(int i=0;i<click.Length;i++){float n=(float)(random.NextDouble()*2-1);low+=.3f*(n-low);click[i]=(n-low)*Mathf.Exp(-i/230f)*.35f;}
            reloadClick=AudioClip.Create("Rifle magazine latch",click.Length,1,24000,false);reloadClick.SetData(click,0);
            var ring = new GameObject("Local jump energy burst"); ring.transform.SetParent(transform,false); jumpRing = ring.AddComponent<LineRenderer>();
            jumpRing.useWorldSpace = true; jumpRing.positionCount = 49; jumpRing.sharedMaterial = cyan; jumpRing.shadowCastingMode = ShadowCastingMode.Off; jumpRing.receiveShadows = false; jumpRing.enabled = false;
        }
        public void Movement(Vector2 input, bool onGround, float pitch, float dt)
        {
            grounded = onGround; pace = Mathf.Clamp01(input.magnitude); gait += dt * 10 * pace; aimPitch = pitch;
        }
        public void Fire(bool confirmed)
        {
            fireAt = Time.time; FireReactions++; if (confirmed) markerAt = Time.time;
            if (audioCues != null) audioCues.Play(CombatSound.PlayerShot,.65f);
        }
        public void ConfirmHit(){markerAt=Time.time;}
        public void AimAt(Vector3 point){aimTarget=point;}
        public void BeginReload(){if(audioCues!=null)audioCues.Source.PlayOneShot(reloadClick,.18f);}
        public void Hurt(Transform source)
        {
            hitAt = Time.time; HitsReacted++;
            Vector3 delta = source != null ? source.position - transform.position : -transform.forward; delta.y = 0;
            float front = Vector3.Dot(transform.forward,delta.normalized), side = Vector3.Dot(transform.right,delta.normalized);
            hitDirection = Mathf.Abs(front) >= Mathf.Abs(side) ? front >= 0 ? "FRONT" : "BEHIND" : side >= 0 ? "RIGHT" : "LEFT";
            if (audioCues != null) audioCues.Play(CombatSound.Hit,.85f);
        }
        public void Jump(int number)
        {
            jumpAt = Time.time; jumpNumber = number; JumpBursts++; burstPosition = transform.position + Vector3.up * .12f;
            if (audioCues != null) audioCues.Play(number == 2 ? CombatSound.DoubleJump : CombatSound.Jump,.65f);
        }
        public void Land() { if (audioCues != null) audioCues.Play(CombatSound.Land,.4f); }
        public void ResetPose() { fireAt = hitAt = jumpAt = markerAt = -100; jumpNumber = 0;wasReloading=false; if (jumpRing != null) jumpRing.enabled = false; }
        public void RefreshPose() { LateUpdate(); }
        void LateUpdate()
        {
            if (arc == null) return;
            float fire = FireStrength, hit = Mathf.Clamp01(1 - (Time.time - hitAt)/.4f), jump = Mathf.Clamp01(1 - (Time.time - jumpAt)/.35f);
            float swing = Mathf.Sin(gait) * 29 * pace * (grounded ? 1 : .2f);
            arc.leftLeg.localRotation = Quaternion.Euler(swing - (grounded ? 0 : 18 * jump),0,0);
            arc.rightLeg.localRotation = Quaternion.Euler(-swing + (grounded ? 0 : 22 * jump),0,0);
            arc.visual.localRotation = visualRest * Quaternion.Euler(hit*9 - jump*(jumpNumber==2?18:8),0,fire*2+hit*3);
            arc.visual.localPosition = visualPosition + Vector3.up * (grounded ? Mathf.Abs(Mathf.Sin(gait))*.025f*pace : 0);
            headPivot.localRotation = Quaternion.Euler(aimPitch*.6f-fire*7 + hit*15,0,-hit*6);
            bool reload=ammunition!=null&&ammunition.IsReloading;float p=reload?ammunition.ReloadProgress:0, motion=Mathf.Sin(p*Mathf.PI);
            if(wasReloading&&!reload&&audioCues!=null)audioCues.Source.PlayOneShot(reloadClick,.23f);wasReloading=reload;
            Weapon.transform.localPosition = new Vector3(.13f,1.20f,.37f-fire*.03f);
            var combat=GetComponent<LocalCombatDriver>();
            if(combat!=null&&combat.IsFirstPerson&&combat.cameraView!=null)
                Weapon.transform.position=combat.cameraView.transform.TransformPoint(new Vector3(.28f,-.26f,.4f-fire*.06f));
            Vector3 direction=aimTarget-Weapon.transform.position;
            if(direction.sqrMagnitude>.001f)Weapon.transform.rotation=Quaternion.LookRotation(direction,transform.up)*Quaternion.Euler(25*motion,10*motion,-32*motion);
            // Muzzle and barrel share their centre line, so their forward vector reaches the aim point.
            Muzzle.localPosition=new Vector3(0,0,.565f);
            magazine.localPosition=new Vector3(0,-.16f-.26f*motion,.16f);magazine.localRotation=Quaternion.Euler(-10,0,-20*motion);
            PoseArm(arc.rightArm,rightElbow,Weapon.transform.TransformPoint(new Vector3(0,-.10f,-.02f)),transform.right-Vector3.up);
            PoseArm(arc.leftArm,leftElbow,reload?magazine.position:Weapon.transform.TransformPoint(new Vector3(-.10f,-.02f,.04f)),-transform.right-Vector3.up);
            jumpRing.enabled = jump > 0;
            if (jump > 0)
            {
                float radius = Mathf.Lerp(jumpNumber==2?1.4f:1.0f,.28f,jump); jumpRing.widthMultiplier = .05f*jump;
                for (int i=0;i<=48;i++) { float a=i*Mathf.PI*2/48; jumpRing.SetPosition(i,burstPosition+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)); }
            }
        }
        static void PoseArm(Transform upper,Transform elbow,Vector3 target,Vector3 hint)
        {
            float a=.30f*upper.lossyScale.y,b=.28f*upper.lossyScale.y;
            Vector3 delta=target-upper.position;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);Vector3 direction=delta.normalized;
            if(direction.sqrMagnitude<.1f)return;
            float along=Mathf.Clamp((a*a+distance*distance-b*b)/(2*distance),-a,a);
            Vector3 bend=Vector3.ProjectOnPlane(hint,direction).normalized;if(bend.sqrMagnitude<.1f)bend=Vector3.ProjectOnPlane(Vector3.forward,direction).normalized;
            Vector3 joint=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.LookRotation(joint-upper.position,Vector3.up)*Quaternion.Euler(-90,0,0);
            elbow.rotation=Quaternion.LookRotation(upper.position+direction*distance-elbow.position,Vector3.up)*Quaternion.Euler(-90,0,0);
        }
        public void DrawHUD()
        {
            Color old = GUI.color;
            if (HitActive)
            {
                GUI.color = new Color(1,.08f,.04f,.32f*Mathf.Clamp01(1-(Time.time-hitAt)/.4f));
                float edge = Mathf.Min(Screen.width,Screen.height)*.035f;
                GUI.DrawTexture(new Rect(0,0,Screen.width,edge),Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0,Screen.height-edge,Screen.width,edge),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0,0,edge,Screen.height),Texture2D.whiteTexture); GUI.DrawTexture(new Rect(Screen.width-edge,0,edge,Screen.height),Texture2D.whiteTexture);
                GUI.color = new Color(1,.3f,.2f); GUI.Label(new Rect(Screen.width/2f-70,Screen.height/2f+38,160,25),"HIT FROM " + hitDirection);
            }
            GUI.color = Color.white;
            if (Time.time-markerAt < .13f)
            {
                var camera=GetComponent<LocalCombatDriver>().cameraView;Rect view=camera.pixelRect;
                Vector2 centre=new Vector2(view.x+view.width*.5f,Screen.height-view.y-view.height*.5f);Matrix4x4 matrix=GUI.matrix;
                GUIUtility.RotateAroundPivot(45,centre);
                GUI.DrawTexture(new Rect(centre.x-14,centre.y-1,6,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(centre.x+8,centre.y-1,6,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centre.x-1,centre.y-14,2,6),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(centre.x-1,centre.y+8,2,6),Texture2D.whiteTexture);
                GUI.matrix=matrix;
            }
            GUI.color = old;
        }
        void OnDestroy() { foreach (var m in new[]{dark,teal,orange,cyan,armor}) if(m != null) Destroy(m);if(bevelMesh!=null)Destroy(bevelMesh);if(reloadClick!=null)Destroy(reloadClick); }
    }
}
