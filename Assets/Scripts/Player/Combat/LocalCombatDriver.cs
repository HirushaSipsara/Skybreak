using System;
using Skybreak.AI.RiftWarden;
using Skybreak.AI.Relay;
using Skybreak.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.LocalCombatLab
{
    // Resolve the camera/shot after the animated pose and the other actors' updates.
    [DefaultExecutionOrder(150)]
    public sealed class LocalCombatDriver : MonoBehaviour
    {
        public Camera cameraView;
        public RiftWardenBrain boss;
        public RelayBrain relay;
        public LocalPlayerVitals vitals;
        public Skybreak.Warden.WardenNoiseBus noiseBus;
        CharacterController capsule;
        float yaw, pitch = 8, vertical;
        public float shotDamage = 18;
        public bool IsFirstPerson { get; private set; }
        float thirdPersonNearClip;
        public LocalMagazine Magazine { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Vector3 ShotOrigin { get; private set; }
        public Vector3 ShotEnd { get; private set; }
        public bool ShotBlocked { get; private set; }
        bool fireRequested;
        int jumps;
        Vector3 spawn;
        Vector3 bossSpawn, relaySpawn;
        readonly RaycastHit[] hits = new RaycastHit[40];
        Skybreak.AI.CombatSignals shotEffects;
        LocalPlayerFeedback feedback;
        public LocalPlayerFeedback Feedback => feedback;
        public int JumpsUsed => jumps;
        public float VerticalSpeed => vertical;
        public Skybreak.AI.CombatSignals ShotEffects => shotEffects;
        public int Shots { get; private set; }
        public string LastHit { get; private set; } = "None";
        void Start()
        {
            capsule = GetComponent<CharacterController>(); spawn = transform.position;
            bossSpawn = boss.transform.position; relaySpawn = relay.transform.position;
            yaw = transform.eulerAngles.y; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            thirdPersonNearClip=cameraView.nearClipPlane;
            Magazine=GetComponent<LocalMagazine>();if(Magazine==null)Magazine=gameObject.AddComponent<LocalMagazine>();Magazine.ResetMagazine();
            feedback=GetComponent<LocalPlayerFeedback>();if(feedback==null)feedback=gameObject.AddComponent<LocalPlayerFeedback>();feedback.Initialize();
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        }
        void Update()
        {
            fireRequested=false;
            if(!Skybreak.Player.MissionFlowController.InputAllowed)return;
            var keys = Keyboard.current; var mouse = Mouse.current; float dt = Mathf.Min(Time.deltaTime, .05f);
            if (Skybreak.Player.MissionFlowController.Instance==null&&keys != null && keys.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (keys != null && keys.backspaceKey.wasPressedThisFrame)
            {
                if(Skybreak.Player.MissionFlowController.Instance!=null){Skybreak.Player.MissionFlowController.Instance.RestartMission();return;}
                ResetLab();
            }
            if (keys != null && keys.f7Key.wasPressedThisFrame) GoToArena();
            if (keys != null && keys.f3Key.wasPressedThisFrame) { boss.Activate();boss.DamagePart(RiftPart.LeftArm,999); }
            if (keys != null && keys.f4Key.wasPressedThisFrame) { boss.Activate();boss.DamagePart(RiftPart.RightArm,999); }
            if (keys != null && keys.f5Key.wasPressedThisFrame) boss.DamagePart(RiftPart.Core,999);
            if (Cursor.lockState != CursorLockMode.Locked || !vitals.Alive) return;
            if(keys!=null&&keys.vKey.wasPressedThisFrame)ToggleView();
            if(keys!=null&&keys.rKey.wasPressedThisFrame)Reload();
            if (mouse != null) { var look = mouse.delta.ReadValue(); yaw += look.x * .10f; pitch = Mathf.Clamp(pitch - look.y * .10f, -65, 70); }
            transform.rotation = Quaternion.Euler(0, yaw, 0); Vector3 input = Vector3.zero;
            if (keys != null)
            {
                if (keys.wKey.isPressed) input.z++; if (keys.sKey.isPressed) input.z--; if (keys.dKey.isPressed) input.x++; if (keys.aKey.isPressed) input.x--;
                if (keys.f2Key.wasPressedThisFrame) boss.Activate();
            }
            MoveCharacter(new Vector2(input.x,input.z),keys!=null&&keys.spaceKey.wasPressedThisFrame,keys!=null&&keys.leftShiftKey.isPressed,dt);
            fireRequested=mouse!=null&&mouse.leftButton.isPressed;
        }
        public bool RequestJump()
        {
            if (!vitals.Alive || jumps >= 2) return false;
            vertical=Mathf.Sqrt(2*22*1.6f);jumps++;if(feedback!=null)feedback.Jump(jumps);return true;
        }
        public void MoveCharacter(Vector2 input,bool jump,bool sprint,float dt)
        {
            dt=Mathf.Clamp(dt,0,.05f);bool wasGrounded=capsule.isGrounded;
            if(wasGrounded&&vertical<0){vertical=-2;jumps=0;}
            if(jump)RequestJump();vertical-=22*dt;
            capsule.Move((transform.TransformDirection(new Vector3(input.x,0,input.y).normalized)*Mathf.Clamp01(input.magnitude)*(sprint?7.6f:4.7f)+Vector3.up*vertical)*dt);
            if(!wasGrounded&&capsule.isGrounded&&vertical<0&&feedback!=null)feedback.Land();
            if(feedback!=null)feedback.Movement(input,capsule.isGrounded,pitch,dt);
        }
        void LateUpdate()
        {
            var mission=Skybreak.Player.MissionFlowController.Instance;
            if(mission!=null&&mission.State!=Skybreak.Player.MissionState.Playing)return;
            UpdateCamera();
            if(fireRequested&&Skybreak.Player.MissionFlowController.InputAllowed)ShootFromView();
            UpdateAim(cameraView.ViewportPointToRay(new Vector3(.5f,.5f,0)));
            feedback?.RefreshPose();
        }
        void UpdateCamera()
        {
            Vector3 kick=feedback!=null?feedback.CameraAngles:Vector3.zero;
            Quaternion rotation = Quaternion.Euler(pitch+kick.x,yaw+kick.y,kick.z);
            Vector3 pivot=transform.position+Vector3.up*(IsFirstPerson?2.08f:1.8f)+(feedback!=null?feedback.CameraOffset:Vector3.zero);
            if(IsFirstPerson)
            {
                cameraView.transform.SetPositionAndRotation(pivot+transform.forward*.08f,rotation);
                return;
            }
            Vector3 offset = rotation * new Vector3(1.05f, 0, -4.5f); float distance = offset.magnitude;
            if (Physics.SphereCast(pivot, .2f, offset.normalized, out var hit, distance, ~(1 << 8), QueryTriggerInteraction.Ignore)) distance = Mathf.Max(.2f, hit.distance - .1f);
            cameraView.transform.SetPositionAndRotation(pivot + offset.normalized * distance, rotation);
        }
        public void ToggleView(){SetFirstPerson(!IsFirstPerson);}
        public void SetFirstPerson(bool firstPerson)
        {
            IsFirstPerson=firstPerson;
            cameraView.nearClipPlane=firstPerson?Mathf.Min(.05f,thirdPersonNearClip):thirdPersonNearClip;
            UpdateCamera();
            UpdateAim(cameraView.ViewportPointToRay(new Vector3(.5f,.5f,0)));
            feedback?.RefreshPose();
        }
        public bool Reload()
        {
            if(!vitals.Alive||!Magazine.BeginReload())return false;
            feedback?.BeginReload();return true;
        }
        bool Trace(Ray ray,float distance,out RaycastHit first)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, 0, count, HitOrder.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; if (hit.collider.transform.IsChildOf(transform)) continue;
                var receiver = hit.collider.GetComponentInParent<DamageSink>();
                // Actor motor capsules are for movement; anatomical damage colliders take shots.
                if (receiver == null && hit.collider.gameObject.layer == 8) continue;
                first=hit;return true;
            }
            first=default;return false;
        }
        public void UpdateAim(Ray cameraRay)
        {
            AimPoint=Trace(cameraRay,80,out var target)?target.point:cameraRay.GetPoint(80);
            feedback?.AimAt(AimPoint);
        }
        public bool ShootFromView()
        {return ShootInternal(default,true);}
        public bool Shoot(Ray cameraRay)
        {return ShootInternal(cameraRay,false);}
        bool ShootInternal(Ray cameraRay,bool useView)
        {
            if(!Skybreak.Player.MissionFlowController.InputAllowed)return false;
            if(!vitals.Alive)return false;
            if(!Magazine.TryFire()){if(Magazine.Rounds==0)Reload();return false;}
            Shots++;LastHit="Miss";bool confirmed=false;ShotBlocked=false;
            if(noiseBus!=null)noiseBus.Emit(transform.position,26,Skybreak.Warden.WardenNoiseKind.Gunshot);
            feedback?.Fire(false);
            // Fire recoil must be present in the rendered camera before choosing its centre ray.
            if(useView){UpdateCamera();cameraRay=cameraView.ViewportPointToRay(new Vector3(.5f,.5f,0));}
            Physics.SyncTransforms();UpdateAim(cameraRay);feedback?.RefreshPose();
            ShotOrigin=feedback!=null&&feedback.Muzzle!=null?feedback.Muzzle.position:transform.position+Vector3.up*1.5f;
            Vector3 delta=AimPoint-ShotOrigin;ShotEnd=AimPoint;
            // A close first-person weapon must not fire from inside or beyond a wall.
            Vector3 barrel=ShotOrigin-cameraView.transform.position;
            if(IsFirstPerson&&barrel.sqrMagnitude>.0001f&&Trace(new Ray(cameraView.transform.position,barrel.normalized),barrel.magnitude,out var obstruction))
            {
                ShotEnd=obstruction.point;LastHit=obstruction.collider.name;ShotBlocked=true;
            }
            else if(delta.sqrMagnitude>.0001f&&Trace(new Ray(ShotOrigin,delta.normalized),Mathf.Min(80,delta.magnitude+.08f),out var hit))
            {
                ShotEnd=hit.point;LastHit=hit.collider.name;var receiver=hit.collider.GetComponentInParent<DamageSink>();
                ShotBlocked=Vector3.Distance(ShotEnd,AimPoint)>.12f;
                if(receiver!=null&&receiver.Team==Allegiance.Hostile){float before=receiver.Health;receiver.ReceiveDamage(shotDamage,transform);confirmed=receiver.Health<before;}
            }
            if(confirmed)feedback?.ConfirmHit();
            if (shotEffects == null) { var go = new GameObject("Local player shot effects"); go.transform.SetParent(transform, false); shotEffects = go.AddComponent<Skybreak.AI.CombatSignals>(); }
            shotEffects.Shot(ShotOrigin,ShotEnd,new Color(.3f,2f,2.5f),.06f);
            if(feedback!=null)shotEffects.FollowMuzzle=feedback.Muzzle;
            shotEffects.AnchorShotOrigin=true;
            if(Magazine.Rounds==0)Reload();
            return true;
        }
        public void ResetLab()
        {
            fireRequested=false;ShotBlocked=false;
            capsule.enabled = false; transform.position = spawn; capsule.enabled = true; vertical = 0; jumps = 0; vitals.health = 100;
            boss.motor.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(bossSpawn); boss.ResetEncounter();
            relay.motor.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(relaySpawn); relay.ResetSupport();
            if(feedback!=null)feedback.ResetPose();if(shotEffects!=null)shotEffects.Clear();
            Magazine.ResetMagazine();
            var wardens=FindAnyObjectByType<Skybreak.Warden.CrystalWardenEncounter>();
            if(wardens!=null)wardens.ResetEncounter();
        }
        public void GoToArena()
        {
            ResetLab();
            capsule.enabled=false;transform.SetPositionAndRotation(new Vector3(42,.04f,77),Quaternion.identity);capsule.enabled=true;
            yaw=0;pitch=8;relay.motor.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(44,.04f,77));
            Physics.SyncTransforms();
        }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void OnGUI()
        {
            if(Skybreak.Player.MissionFlowController.Instance!=null)
            {
                if(!Skybreak.Player.MissionFlowController.InputAllowed)return;
                DrawReticle();feedback?.DrawHUD();return;
            }
            GUI.Box(new Rect(16, 16, 760, 184), "ARC / SKYBREAK · "+(IsFirstPerson?"FIRST PERSON":"THIRD PERSON")+" · V switch view");
            GUI.Label(new Rect(28, 44, 720, 25), "WASD / Mouse · LMB fire · R reload · Space double jump · Shift sprint · Backspace restart");
            GUI.Label(new Rect(28, 70, 670, 25), "Arc " + vitals.Health.ToString("F0") + " | L arm " + boss.LeftHealth.ToString("F0") + " | R arm " + boss.RightHealth.ToString("F0") + " | Core " + boss.CoreHealth.ToString("F0") + (boss.CoreExposed ? " exposed" : " shielded"));
            GUI.Label(new Rect(28, 96, 670, 25), "Warden: " + boss.Action + " / " + boss.Decision);
            GUI.Label(new Rect(28, 122, 670, 25), "Relay: " + relay.Action + " / " + relay.Decision + " | Last hit: " + LastHit);
            GUI.Label(new Rect(28,148,720,25),"F7 boss room · F3/F4 break arms · F5 exposed core · Energy "+Magazine.Rounds+"/20 · Unlimited reserve"+(Magazine.IsReloading?" — RECHARGING "+(Magazine.ReloadProgress*100).ToString("F0")+"%":""));
            DrawReticle();
            GUI.Box(new Rect(Screen.width-240,Screen.height-90,220,70),Magazine.IsReloading?"RECHARGING — "+((1-Magazine.ReloadProgress)*Magazine.reloadDuration).ToString("F1")+"s":Magazine.Rounds+" / 20     UNLIMITED ENERGY\nR recharge");
            if (!vitals.Alive) GUI.Box(new Rect(Screen.width / 2f - 150, Screen.height / 2f - 40, 300, 80), "Arc down — Backspace to restart");
            if(feedback!=null)feedback.DrawHUD();
        }
        void DrawReticle()
        {
            // Draw geometry at the camera centre; GUI.Label padding displaced the old '+' glyph.
            Rect view=cameraView.pixelRect;float x=view.x+view.width*.5f,y=Screen.height-(view.y+view.height*.5f);Color old=GUI.color;
            GUI.color=ShotBlocked?new Color(1,.55f,.18f):Color.white;
            GUI.DrawTexture(new Rect(x-11,y-1,7,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x+4,y-1,7,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x-1,y-11,2,7),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x-1,y+4,2,7),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x-1,y-1,2,2),Texture2D.whiteTexture);GUI.color=old;
        }
        sealed class HitOrder : System.Collections.Generic.IComparer<RaycastHit>
        { public static readonly HitOrder Instance = new HitOrder(); public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance); }
    }
}
