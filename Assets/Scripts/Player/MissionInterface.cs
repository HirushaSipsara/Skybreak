using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Skybreak.LocalCombatLab;

namespace Skybreak.Player
{
    public sealed class MissionInterface : MonoBehaviour
    {
        MissionFlowController flow;
        Font font;
        RectTransform canvas;
        GameObject main,pause,controls,defeat,victory,hud,cinematic;
        UnityEngine.UI.Text objective,health,ammunition,view,caption,time;
        UnityEngine.UI.Image fade;
        CanvasGroup victoryGroup;
        RectTransform victoryRing;
        Texture2D ringTexture;
        Sprite ringSprite;
        UnityEngine.UI.Button mainStart,pauseResume,controlsBack,retry,playAgain,skip;
        readonly Color ink=new Color(.018f,.04f,.052f,.96f),cyan=new Color(.3f,.95f,.94f),white=new Color(.93f,.97f,.96f),muted=new Color(.63f,.76f,.78f);

        public void Build(MissionFlowController controller)
        {
            flow=controller;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root=new GameObject("Mission interface",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<RectTransform>();root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=100;
            var scale=root.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            if(EventSystem.current==null)
            {
                var events=new GameObject("Mission UI events",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            main=Page("Main menu",false);var panel=Image("Menu panel",main.transform,ink);Stretch(panel.rectTransform,new Vector2(0,0),new Vector2(0,1));panel.rectTransform.pivot=new Vector2(0,.5f);panel.rectTransform.anchoredPosition=Vector2.zero;panel.rectTransform.sizeDelta=new Vector2(490,0);
            Label(panel.transform,"SKYBREAK / AURORA RIFT",14,cyan,42,38,410,24);
            Label(panel.transform,"SKYBREAK",66,white,38,112,430,88,FontStyle.Bold);
            Label(panel.transform,"T H E   D E S C E N T",22,muted,44,203,400,36);
            var line=Image("Title rule",panel.transform,cyan);Place(line.rectTransform,44,260,70,3);
            Label(panel.transform,"An ancient vault. A fractured reality.\nDescend with Relay, awaken the Aether\nCore and seal the rift before it spreads.",18,white,44,294,390,100);
            mainStart=Button(panel.transform,"START MISSION",44,430,380,58,flow.StartMission,true);
            Button(panel.transform,"CONTROLS",44,501,180,48,flow.ShowControls);
            Button(panel.transform,"QUIT",244,501,180,48,flow.Quit);
            var footer=Label(panel.transform,"BREAK THE SEAL. RECLAIM THE SKY.",13,muted,44,0,400,24);Bottom(footer.rectTransform,44,30,400,24);
            var location=Label(main.transform,"AURORA VAULT\nTHE RIFT BENEATH THE WORLD",14,white,0,0,420,54);location.alignment=TextAnchor.LowerRight;BottomRight(location.rectTransform,30,30,420,54);

            pause=Page("Pause",true);var box=Card(pause,"MISSION PAUSED",430,440);
            Label(box,"Take a breath. The Vault can wait.",16,muted,36,92,360,42);
            pauseResume=Button(box,"RESUME MISSION",36,155,358,52,flow.ResumeMission,true);
            Button(box,"CONTROLS",36,219,358,44,flow.ShowControls);
            Button(box,"RESTART MISSION",36,275,358,44,flow.RestartMission);
            Button(box,"MAIN MENU",36,331,358,44,flow.MainMenu);
            Button(box,"QUIT",36,386,358,36,flow.Quit);

            controls=Page("Controls",true);box=Card(controls,"FIELD MANUAL",680,600);
            Label(box,"ARC / HUMAN-MACHINE PATHFINDER",14,cyan,36,91,600,25);
            string[] keys={"W A S D","MOUSE","LEFT CLICK","R","SPACE / SPACE","LEFT SHIFT","V","F","Q","ESC / BACKSPACE"};
            string[] actions={"Move","Look and aim","Fire energy beam","Recharge energy cell (unlimited reserve)","Jump / double jump","Sprint","First-person / third-person view","Interact with nearby objects","Place a sound decoy (3s cooldown)","Pause / restart mission"};
            for(int i=0;i<keys.Length;i++){Label(box,keys[i],16,cyan,36,143+i*30,205,27,FontStyle.Bold);Label(box,actions[i],16,white,252,143+i*30,400,27);}
            controlsBack=Button(box,"BACK",36,510,608,50,flow.CloseControls,true);

            defeat=Page("Defeat",true);box=Card(defeat,"SIGNAL LOST",490,390);
            Label(box,"ARC DOWN",15,new Color(1,.55f,.35f),36,96,420,28);
            Label(box,"The rift still grows.\nRise again and finish the descent.",19,white,36,144,420,62);
            retry=Button(box,"RETRY MISSION",36,233,418,52,flow.RestartMission,true);
            Button(box,"MAIN MENU",36,299,418,46,flow.MainMenu);

            victory=Page("Victory",true);victoryGroup=victory.AddComponent<CanvasGroup>();
            var rune=Image("Return signal",victory.transform,new Color(cyan.r,cyan.g,cyan.b,.12f));rune.sprite=Ring();victoryRing=rune.rectTransform;Center(victoryRing,0,58,470,470);
            var inner=Image("Signal core",victoryRing,new Color(cyan.r,cyan.g,cyan.b,.2f));inner.sprite=ringSprite;Center(inner.rectTransform,0,0,310,310);
            var tag=Label(victory.transform,"AETHER CORE AWAKENED",15,cyan,0,0,750,28);Center(tag.rectTransform,0,245,750,28);tag.alignment=TextAnchor.MiddleCenter;
            var title=Label(victory.transform,"MISSION\nCOMPLETE",66,white,0,0,850,150,FontStyle.Bold);Center(title.rectTransform,0,140,850,150);title.alignment=TextAnchor.MiddleCenter;
            var won=Label(victory.transform,"YOU WON",22,cyan,0,0,650,36,FontStyle.Bold);Center(won.rectTransform,0,32,650,36);won.alignment=TextAnchor.MiddleCenter;
            var story=Label(victory.transform,"Rift sealed. Sky reclaimed.",22,white,0,0,750,36);Center(story.rectTransform,0,-21,750,36);story.alignment=TextAnchor.MiddleCenter;
            time=Label(victory.transform,"",16,muted,0,0,700,30);Center(time.rectTransform,0,-70,700,30);time.alignment=TextAnchor.MiddleCenter;
            playAgain=Button(victory.transform,"PLAY AGAIN",0,0,330,52,flow.RestartMission,true);Center(playAgain.GetComponent<RectTransform>(),0,-142,330,52);
            var menuButton=Button(victory.transform,"MAIN MENU",0,0,330,44,flow.MainMenu);Center(menuButton.GetComponent<RectTransform>(),0,-202,330,44);
            var quit=Button(victory.transform,"QUIT",0,0,330,40,flow.Quit);Center(quit.GetComponent<RectTransform>(),0,-256,330,40);

            hud=Page("Mission HUD",false);
            var objectivePanel=Image("Objective panel",hud.transform,new Color(ink.r,ink.g,ink.b,.85f));Place(objectivePanel.rectTransform,24,24,320,83);
            Label(objectivePanel.transform,"CURRENT OBJECTIVE",12,cyan,16,11,290,20);
            objective=Label(objectivePanel.transform,"",19,white,16,36,292,40,FontStyle.Bold);
            var vitals=Image("Vitals",hud.transform,new Color(ink.r,ink.g,ink.b,.88f));Bottom(vitals.rectTransform,24,24,260,82);
            Label(vitals.transform,"ARC / PATHFINDER",12,cyan,16,9,230,22);health=Label(vitals.transform,"",26,white,16,32,230,40,FontStyle.Bold);
            var magazine=Image("Magazine",hud.transform,new Color(ink.r,ink.g,ink.b,.88f));BottomRight(magazine.rectTransform,24,24,320,82);
            view=Label(magazine.transform,"",12,cyan,16,9,290,22);ammunition=Label(magazine.transform,"",23,white,16,35,290,36,FontStyle.Bold);
            var tip=Label(hud.transform,"ESC  PAUSE",12,muted,0,0,190,24);tip.rectTransform.anchorMin=tip.rectTransform.anchorMax=new Vector2(.5f,0);tip.rectTransform.pivot=new Vector2(.5f,0);tip.rectTransform.anchoredPosition=new Vector2(0,24);tip.alignment=TextAnchor.MiddleCenter;

            cinematic=Page("Mission cinematic",false);
            var top=Image("Top letterbox",cinematic.transform,Color.black);Stretch(top.rectTransform,new Vector2(0,1),Vector2.one);top.rectTransform.pivot=new Vector2(.5f,1);top.rectTransform.sizeDelta=new Vector2(0,48);
            var bottom=Image("Bottom letterbox",cinematic.transform,Color.black);Stretch(bottom.rectTransform,Vector2.zero,new Vector2(1,0));bottom.rectTransform.pivot=new Vector2(.5f,0);bottom.rectTransform.sizeDelta=new Vector2(0,120);
            caption=Label(bottom.transform,"",22,white,60,18,1160,88);caption.alignment=TextAnchor.MiddleCenter;
            skip=Button(cinematic.transform,"SKIP INTRO  /  SPACE",0,0,245,40,flow.SkipIntroduction);BottomRight(skip.GetComponent<RectTransform>(),26,132,245,40);
            fade=Image("Extraction fade",canvas,Color.clear);Stretch(fade.rectTransform,Vector2.zero,Vector2.one);fade.raycastTarget=false;
        }
        public void Show(MissionState state)
        {
            main.SetActive(state==MissionState.MainMenu);pause.SetActive(state==MissionState.Paused);controls.SetActive(state==MissionState.Controls);
            defeat.SetActive(state==MissionState.Defeat);victory.SetActive(state==MissionState.Victory);hud.SetActive(state==MissionState.Playing);
            cinematic.SetActive(state==MissionState.Introduction||state==MissionState.Ending);skip.gameObject.SetActive(state==MissionState.Introduction);
            fade.color=Color.clear;
            UnityEngine.UI.Button selected=state==MissionState.MainMenu?mainStart:state==MissionState.Paused?pauseResume:state==MissionState.Controls?controlsBack:state==MissionState.Defeat?retry:state==MissionState.Victory?playAgain:null;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(selected!=null?selected.gameObject:null);
        }
        public void Tick(MissionFlowController mission,LocalCombatDriver driver)
        {
            float t=mission.StateSeconds;
            if(mission.State==MissionState.Playing)
            {
                objective.text=mission.Objective;health.text="HEALTH  "+driver.vitals.Health.ToString("0");
                ammunition.text=driver.Magazine.IsReloading?"RECHARGING  "+((1-driver.Magazine.ReloadProgress)*driver.Magazine.reloadDuration).ToString("0.0")+"s":driver.Magazine.Rounds+" / 20   UNLIMITED";
                view.text=(driver.IsFirstPerson?"FIRST PERSON":"THIRD PERSON")+"   /   V SWITCH";
                hud.SetActive(!mission.BossIntroPlaying);
            }
            if(mission.State==MissionState.Introduction)caption.text=t<2?"AURORA VAULT\nA fractured core is tearing reality apart.":"Arc and Relay: descend into the Vault.\nDefeat the Rift Warden. Awaken the Aether Core. Seal the breach.";
            if(mission.State==MissionState.Ending)
            {
                caption.text=t<3?"RIFT WARDEN DEFEATED":t<5.8f?"AETHER CORE AWAKENING\nThe breach is sealed. Reality is stabilizing.":"RETURN GATE ACTIVE\nArc and Relay: passage secured.";
                fade.color=new Color(.7f,1,1,Mathf.InverseLerp(8.1f,9.4f,t));
            }
            if(mission.State==MissionState.Victory)
            {
                victoryGroup.alpha=Mathf.Clamp01(t/.6f);victoryRing.localRotation=Quaternion.Euler(0,0,Time.unscaledTime*5);
                victoryRing.localScale=Vector3.one*(1+Mathf.Sin(Time.unscaledTime*1.5f)*.025f);
                var duration=TimeSpan.FromSeconds(mission.MissionSeconds);time.text="MISSION TIME   "+duration.ToString(@"mm\:ss");
            }
        }
        GameObject Page(string name,bool dim)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(canvas,false);Stretch(go.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
            if(dim){var bg=go.AddComponent<UnityEngine.UI.Image>();bg.color=new Color(.006f,.019f,.027f,.94f);}
            go.SetActive(false);return go;
        }
        Transform Card(GameObject page,string title,float width,float height)
        {
            var panel=Image(title,page.transform,ink);Center(panel.rectTransform,0,0,width,height);
            var accent=Image("Accent",panel.transform,cyan);Place(accent.rectTransform,36,32,56,3);
            Label(panel.transform,title,31,white,36,49,width-72,42,FontStyle.Bold);return panel.transform;
        }
        UnityEngine.UI.Image Image(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var result=go.GetComponent<UnityEngine.UI.Image>();result.color=color;result.raycastTarget=false;return result;
        }
        UnityEngine.UI.Text Label(Transform parent,string text,int size,Color color,float x,float y,float w,float h,FontStyle style=FontStyle.Normal)
        {
            var go=new GameObject(text.Length>25?text.Substring(0,25):text,typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(parent,false);
            var result=go.GetComponent<UnityEngine.UI.Text>();result.font=font;result.text=text;result.fontSize=size;result.color=color;result.fontStyle=style;result.raycastTarget=false;result.verticalOverflow=VerticalWrapMode.Overflow;
            Place(result.rectTransform,x,y,w,h);return result;
        }
        UnityEngine.UI.Button Button(Transform parent,string label,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var image=Image(label,parent,primary?cyan:new Color(.07f,.14f,.17f));image.raycastTarget=true;Place(image.rectTransform,x,y,w,h);
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.7f,1,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.45f,.75f,.8f);colors.fadeDuration=.12f;button.colors=colors;
            button.onClick.AddListener(()=>action());var text=Label(image.transform,label,17,primary?ink:white,0,0,w,h,FontStyle.Bold);Stretch(text.rectTransform,Vector2.zero,Vector2.one);text.alignment=TextAnchor.MiddleCenter;return button;
        }
        static void Stretch(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
        static void Center(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        static void Bottom(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        static void BottomRight(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-x,y);rect.sizeDelta=new Vector2(w,h);}
        Sprite Ring()
        {
            ringTexture=new Texture2D(128,128,TextureFormat.RGBA32,false);ringTexture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){float radius=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f));pixels[y*128+x]=new Color(1,1,1,Mathf.Clamp01(1.5f-Mathf.Abs(radius-59)));}
            ringTexture.SetPixels(pixels);ringTexture.Apply();ringSprite=Sprite.Create(ringTexture,new Rect(0,0,128,128),new Vector2(.5f,.5f));return ringSprite;
        }
        void OnDestroy(){if(ringSprite!=null)Destroy(ringSprite);if(ringTexture!=null)Destroy(ringTexture);}
    }
}
