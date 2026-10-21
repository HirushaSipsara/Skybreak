using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Skybreak.LocalCombatLab;
using Skybreak.AI.RiftWarden;
using Skybreak.Warden;

namespace Skybreak.Player
{
    public enum MissionState { Initializing, MainMenu, Introduction, Playing, Paused, Controls, Ending, Victory, Defeat, Loading }

    [DefaultExecutionOrder(11000)]
    public sealed class MissionFlowController : MonoBehaviour
    {
        public static MissionFlowController Instance { get; private set; }
        public static bool InputAllowed => Instance==null || Instance.State==MissionState.Playing && !Instance.BossIntroPlaying && Time.frameCount>Instance.inputAfterFrame;
        public static bool DamageAllowed => Instance==null || Instance.State==MissionState.Playing && !Instance.BossIntroPlaying;
        public MissionState State {get;private set;}=MissionState.Initializing;
        public int ObjectiveStep {get;private set;}
        public float MissionSeconds {get;private set;}
        public int CompletionCount {get;private set;}
        public string Objective => ObjectiveStep==0?"Enter the Aurora Vault":ObjectiveStep==1?"Reach the Aether Core":"Defeat Rift Warden";
        public bool BossIntroPlaying => intro!=null && intro.IntroPlaying;
        public float StateSeconds => Time.unscaledTime-stateAt;
        static bool startAfterReload;
        LocalCombatDriver player;
        RiftWardenBrain boss;
        RiftBossIntro intro;
        CrystalWardenEncounter guards;
        MissionInterface ui;
        MissionState controlsReturn;
        float stateAt;
        Camera cameraView;
        Vector3 previewFocus, endingCameraPosition;
        Quaternion endingCameraRotation;
        readonly List<Light> worldLights=new List<Light>();
        readonly List<float> lightStrengths=new List<float>();
        readonly List<Color> lightColors=new List<Color>();
        readonly List<ParticleSystem> extraction=new List<ParticleSystem>();
        Material extractionMaterial;
        Light reactorLight;
        bool extracted;
        int inputAfterFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Instance=null;startAfterReload=false;}
        void Awake(){Instance=this;}
        IEnumerator Start()
        {
            // The combat bootstrap and its adapters finish Start before the menu takes ownership.
            yield return null;
            player=FindAnyObjectByType<LocalCombatDriver>();
            if(player==null){Debug.LogError("Mission flow requires the main-map combat controller.",this);enabled=false;yield break;}
            boss=player.boss;cameraView=player.cameraView;intro=boss.GetComponent<RiftBossIntro>();guards=FindAnyObjectByType<CrystalWardenEncounter>();
            boss.BossDefeated+=OnBossDefeated;
            previewFocus=guards!=null && guards.objectives.Length>1?guards.objectives[1].transform.position+Vector3.up*2.3f:player.transform.position+player.transform.forward*8+Vector3.up*2;
            ui=gameObject.AddComponent<MissionInterface>();ui.Build(this);
            bool launch=startAfterReload;startAfterReload=false;
            SetState(MissionState.MainMenu);
            if(launch)StartMission();
        }
        void SetState(MissionState next)
        {
            State=next;stateAt=Time.unscaledTime;
            if(next==MissionState.Playing)inputAfterFrame=Time.frameCount+1;
            bool paused=next==MissionState.Paused || next==MissionState.Controls && controlsReturn==MissionState.Paused;
            if(intro!=null)intro.SuspendPresentation(paused);
            Time.timeScale=next==MissionState.Playing?(BossIntroPlaying?.08f:1):next==MissionState.Ending?1:0;
            AudioListener.pause=paused || next==MissionState.Defeat;
            Cursor.lockState=next==MissionState.Playing?CursorLockMode.Locked:CursorLockMode.None;
            Cursor.visible=next!=MissionState.Playing;
            ui?.Show(next);
        }
        public void StartMission()
        {
            if(State!=MissionState.MainMenu)return;
            MissionSeconds=0;ObjectiveStep=0;CompletionCount=0;
            SetState(MissionState.Introduction);
        }
        public void SkipIntroduction()
        {
            if(State!=MissionState.Introduction)return;
            player.SetFirstPerson(player.IsFirstPerson);
            SetState(MissionState.Playing);
        }
        public void PauseMission(){if(State==MissionState.Playing)SetState(MissionState.Paused);}
        public void ResumeMission(){if(State==MissionState.Paused)SetState(MissionState.Playing);}
        public void ShowControls()
        {
            if(State!=MissionState.MainMenu && State!=MissionState.Paused)return;
            controlsReturn=State;SetState(MissionState.Controls);
        }
        public void CloseControls(){if(State==MissionState.Controls)SetState(controlsReturn);}
        public void RestartMission(){Reload(true);}
        public void MainMenu(){Reload(false);}
        void Reload(bool launch)
        {
            if(State==MissionState.Loading)return;
            startAfterReload=launch;SetState(MissionState.Loading);
            Time.timeScale=1;AudioListener.pause=false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        public void Quit()
        {
            Time.timeScale=1;AudioListener.pause=false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void Update()
        {
            if(ui==null)return;
            var keys=Keyboard.current;
            if(keys!=null && keys.escapeKey.wasPressedThisFrame)
            {
                if(State==MissionState.Playing)PauseMission();else if(State==MissionState.Paused)ResumeMission();else if(State==MissionState.Controls)CloseControls();
            }
            if(State==MissionState.Introduction && (StateSeconds>=6.2f || keys!=null && keys.spaceKey.wasPressedThisFrame))SkipIntroduction();
            if(State==MissionState.Playing)
            {
                // Claim completion as the core dies, so another enemy cannot cancel the ending.
                if(boss.Action==RiftAction.Dying || boss.Defeated){BeginEnding();}
                else if(!player.vitals.Alive)SetState(MissionState.Defeat);
                else
                {
                    if(!BossIntroPlaying)MissionSeconds+=Time.unscaledDeltaTime;
                    if(player.transform.position.z>8)ObjectiveStep=Mathf.Max(1,ObjectiveStep);
                    Vector3 distance=player.transform.position-boss.arenaCentre;distance.y=0;
                    if(distance.magnitude<boss.arenaRadius+10 || boss.Action!=RiftAction.Dormant)ObjectiveStep=2;
                }
            }
            if(State==MissionState.Ending)
            {
                float t=StateSeconds;
                if(t>3 && boss.returnPoint!=null){boss.returnPoint.SetPowered(true);boss.returnPoint.SetExtractionStrength(Mathf.Clamp01((t-3)/2));}
                for(int i=0;i<worldLights.Count;i++)
                {
                    if(worldLights[i]==null)continue;
                    float arrival=3+Vector3.Distance(worldLights[i].transform.position,boss.arenaCentre)/25;
                    float strength=Mathf.Clamp01((t-arrival)/1.2f);
                    worldLights[i].intensity=lightStrengths[i]*(1+.4f*strength);
                    worldLights[i].color=Color.Lerp(lightColors[i],new Color(.5f,1,1),strength*.2f);
                }
                if(reactorLight!=null)reactorLight.intensity=10*Mathf.Clamp01((t-3)/2);
                foreach(var particles in extraction)
                {
                    var emission=particles.emission;emission.rateOverTime=t>5.5f && t<8.3f?55:0;
                }
                if(t>8.35f&&!extracted)
                {
                    extracted=true;HideActor(player.transform);HideActor(player.relay.transform);
                }
                if(t>=9.5f)SetState(MissionState.Victory);
            }
            ui.Tick(this,player);
        }
        void OnBossDefeated(){if(State==MissionState.Playing)BeginEnding();}
        void BeginEnding()
        {
            if(State!=MissionState.Playing)return;
            CompletionCount++;endingCameraPosition=cameraView.transform.position;endingCameraRotation=cameraView.transform.rotation;
            SetState(MissionState.Ending);
            if(intro!=null)intro.enabled=false;
            player.relay.motor.Stop();player.relay.enabled=false;
            if(guards!=null)foreach(var guard in guards.wardens)guard.enabled=false;
            foreach(var light in FindObjectsByType<Light>())
                if(light.type==LightType.Point && light.range>3 && light.enabled && !light.name.Contains("FX"))
                {worldLights.Add(light);lightStrengths.Add(light.intensity);lightColors.Add(light.color);}
            var lamp=new GameObject("Aether Core awakening light");lamp.transform.SetParent(transform);lamp.transform.position=PortalFocus();
            reactorLight=lamp.AddComponent<Light>();reactorLight.type=LightType.Point;reactorLight.range=14;reactorLight.intensity=0;reactorLight.color=new Color(.3f,.95f,1);reactorLight.shadows=LightShadows.None;
            var fx=boss.GetComponent<RiftAttackFx>();
            if(fx!=null)extractionMaterial=fx.sparkMaterial;
            CreateExtraction(player.transform.position);CreateExtraction(player.relay.transform.position);
        }
        void CreateExtraction(Vector3 at)
        {
            var obj=new GameObject("Return teleport particles");obj.transform.SetParent(transform);obj.transform.position=at+Vector3.up*.15f;obj.transform.rotation=Quaternion.Euler(-90,0,0);
            var p=obj.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=p.main;main.loop=true;main.startLifetime=1.3f;main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,3);main.startSize=new ParticleSystem.MinMaxCurve(.035f,.09f);main.startColor=new Color(.25f,1,1);main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.65f;
            var emission=p.emission;emission.rateOverTime=0;
            var fade=p.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.cyan,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=p.GetComponent<ParticleSystemRenderer>();if(extractionMaterial!=null)renderer.sharedMaterial=extractionMaterial;
            p.Play();extraction.Add(p);
        }
        static void HideActor(Transform actor){foreach(var r in actor.GetComponentsInChildren<Renderer>())r.enabled=false;actor.gameObject.SetActive(false);}
        Vector3 PortalFocus()=>boss.returnPoint!=null?boss.returnPoint.transform.position+Vector3.up*2.3f:boss.arenaCentre+Vector3.up*2;
        void LateUpdate()
        {
            if(cameraView==null||ui==null)return;
            float t=StateSeconds;
            if(State==MissionState.MainMenu || State==MissionState.Controls && controlsReturn==MissionState.MainMenu)
                Frame(previewFocus,new Vector3(-6+Mathf.Sin(Time.unscaledTime*.14f)*.65f,1.1f,-5));
            else if(State==MissionState.Introduction)
            {
                Vector3 focus=t<2?previewFocus:player.transform.position+Vector3.up*1.3f;
                Frame(focus,t<2?new Vector3(-7,2.2f,-6):new Vector3(4-t*.18f,1.1f,4));
            }
            else if(State==MissionState.Ending)
            {
                if(t<3){cameraView.transform.SetPositionAndRotation(endingCameraPosition,endingCameraRotation);}
                else if(t<5.8f)Frame(PortalFocus(),new Vector3(-6,1.3f,-7));
                else Frame((player.transform.position+player.relay.transform.position)*.5f+Vector3.up*1.2f,new Vector3(4,1.3f,5));
            }
        }
        void Frame(Vector3 focus,Vector3 offset)
        {
            float distance=offset.magnitude;
            if(Physics.SphereCast(focus,.18f,offset.normalized,out var hit,distance,~(1<<8),QueryTriggerInteraction.Ignore))distance=Mathf.Max(.6f,hit.distance-.2f);
            var position=focus+offset.normalized*distance;
            cameraView.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));
        }
        void OnDestroy()
        {
            if(boss!=null)boss.BossDefeated-=OnBossDefeated;
            if(Instance==this)Instance=null;
            Time.timeScale=1;AudioListener.pause=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
