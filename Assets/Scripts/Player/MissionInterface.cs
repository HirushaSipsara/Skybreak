using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Skybreak.LocalCombatLab;
using Skybreak.Player.UI;

namespace Skybreak.Player
{
    /// <summary>
    /// Builds every mission screen (menu, pause, field manual, defeat, victory, HUD and
    /// cinematic bars) at runtime. Visual language: dark vault glass, aether-cyan light,
    /// HUD corner brackets, drifting motes and slow rune rings. All motion uses unscaled
    /// time because the game is paused behind most of these pages.
    /// </summary>
    public sealed class MissionInterface : MonoBehaviour
    {
        MissionFlowController flow;
        Font font;
        RectTransform canvas;
        GameObject main,pause,controls,defeat,victory,hud,cinematic;
        Text objective,health,ammunition,view,caption,time,pauseObjective,pauseTime,signalStatus;
        Text titleGhostA,titleGhostB,defeatTitle,defeatGhost;
        Image fade,healthFill,signalDot,scanline,defeatVignette,menuRuneA,menuRuneB,victoryRays;
        Image[] pips;
        RectTransform letterTop,letterBottom,healthFillRect,objectiveDiamond;
        CanvasGroup victoryGroup;
        RectTransform victoryRing;
        Button mainStart,pauseResume,controlsBack,retry,playAgain,skip;
        Sprite gradientSprite,vignetteSprite,glowSprite,ringSprite,thinRingSprite;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();

        static readonly Color ink=new Color(.012f,.03f,.042f,.97f),
            glass=new Color(.02f,.055f,.07f,.9f),
            cyan=new Color(.3f,.95f,.94f),
            aether=new Color(.55f,1f,.96f),
            white=new Color(.93f,.97f,.96f),
            muted=new Color(.56f,.7f,.73f),
            ember=new Color(1f,.45f,.26f),
            gold=new Color(1f,.78f,.4f);

        public void Build(MissionFlowController controller)
        {
            flow=controller;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateSprites();
            var root=new GameObject("Mission interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<RectTransform>();
            var rootCanvas=root.GetComponent<Canvas>();rootCanvas.renderMode=RenderMode.ScreenSpaceOverlay;rootCanvas.sortingOrder=100;
            var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            SetUpEvents();

            BuildMainMenu();
            BuildPause();
            BuildControls();
            BuildDefeat();
            BuildVictory();
            BuildHud();
            BuildCinematic();
            fade=Image("Extraction fade",canvas,Color.clear);Stretch(fade.rectTransform,Vector2.zero,Vector2.one);
        }

        void SetUpEvents()
        {
            var eventSystem=EventSystem.current;
            if(eventSystem==null)
            {
                var events=new GameObject("Mission UI events",typeof(EventSystem));events.transform.SetParent(transform,false);
                eventSystem=events.GetComponent<EventSystem>();
            }
            eventSystem.enabled=true;
            eventSystem.sendNavigationEvents=true;
            var inputModule=eventSystem.GetComponent<InputSystemUIInputModule>();
            if(inputModule==null)inputModule=eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            foreach(var module in eventSystem.GetComponents<BaseInputModule>())
                if(module!=inputModule)module.enabled=false;
            inputModule.enabled=true;
            inputModule.AssignDefaultActions();
        }

        // ───────────────────────────── Main menu ─────────────────────────────
        void BuildMainMenu()
        {
            main=Page("Main menu",false);
            var vignette=Image("Vignette",main.transform,new Color(0,0,0,.85f));vignette.sprite=vignetteSprite;Stretch(vignette.rectTransform,Vector2.zero,Vector2.one);
            Motes(main.transform,new Color(.55f,1,.95f,.55f),46,17);

            // Rune rings slowly turning behind the location plate.
            menuRuneA=Image("Rune ring",main.transform,new Color(cyan.r,cyan.g,cyan.b,.10f));menuRuneA.sprite=ringSprite;
            Anchor(menuRuneA.rectTransform,new Vector2(1,0),new Vector2(-150,150),new Vector2(520,520));
            menuRuneB=Image("Rune ring inner",main.transform,new Color(cyan.r,cyan.g,cyan.b,.16f));menuRuneB.sprite=thinRingSprite;
            Anchor(menuRuneB.rectTransform,new Vector2(1,0),new Vector2(-150,150),new Vector2(340,340));

            var panel=Image("Menu glass",main.transform,ink);panel.sprite=gradientSprite;
            Stretch(panel.rectTransform,Vector2.zero,new Vector2(0,1));panel.rectTransform.pivot=new Vector2(0,.5f);panel.rectTransform.sizeDelta=new Vector2(820,0);
            var edge=Image("Glass edge light",panel.transform,new Color(cyan.r,cyan.g,cyan.b,.05f));Stretch(edge.rectTransform,Vector2.zero,new Vector2(0,1));edge.rectTransform.pivot=new Vector2(0,.5f);edge.rectTransform.sizeDelta=new Vector2(2,0);

            var tag=Group("Tag",panel.transform,64,44,520,20,0);
            Diamond(tag,0,10,9,cyan);
            Label(tag,"SKYBREAK   //   AURORA RIFT   //   OPERATION DESCENT",12,cyan,20,0,500,20);

            var title=Group("Title",panel.transform,58,86,700,110,.08f);
            titleGhostA=Label(title,"SKYBREAK",96,new Color(cyan.r,cyan.g,cyan.b,.35f),0,0,700,110,FontStyle.Bold);
            titleGhostB=Label(title,"SKYBREAK",96,new Color(ember.r,ember.g,ember.b,.22f),0,0,700,110,FontStyle.Bold);
            var titleText=Label(title,"SKYBREAK",96,white,0,0,700,110,FontStyle.Bold);
            Glow(titleText,cyan,.16f);

            var sub=Group("Subtitle",panel.transform,64,200,600,26,.16f);
            var rule=Image("Rule",sub,cyan);Place(rule.rectTransform,0,12,42,2);
            Label(sub,"T H E    D E S C E N T",20,aether,58,0,500,26);

            var story=Group("Story",panel.transform,64,252,520,96,.24f);
            var bar=Image("Story bar",story,new Color(cyan.r,cyan.g,cyan.b,.6f));Place(bar.rectTransform,0,4,2,80);
            Label(story,"An ancient vault. A fractured reality.\nDescend with Relay, awaken the Aether Core\nand seal the rift before it spreads.",17,new Color(.82f,.9f,.9f),20,0,500,90).lineSpacing=1.15f;

            mainStart=Row(panel.transform,"01","START MISSION",64,378,430,56,flow.StartMission,cyan,true,.32f);
            Row(panel.transform,"02","FIELD MANUAL",64,438,430,48,flow.ShowControls,cyan,false,.38f);
            Row(panel.transform,"03","QUIT",64,490,430,48,flow.Quit,cyan,false,.44f);

            var hints=new GameObject("Hints",typeof(RectTransform)).GetComponent<RectTransform>();hints.SetParent(panel.transform,false);
            Anchor(hints,Vector2.zero,new Vector2(64,44),new Vector2(520,24));hints.pivot=Vector2.zero;hints.anchoredPosition=new Vector2(64,44);
            Reveal(hints.gameObject,.55f,new Vector2(0,-10));
            Keycap(hints,"ENTER",0,0,64,cyan);Label(hints,"SELECT",11,muted,72,3,80,18);
            Keycap(hints,"W / S",140,0,64,cyan);Label(hints,"NAVIGATE",11,muted,212,3,100,18);
            Label(hints,"BREAK THE SEAL. RECLAIM THE SKY.",11,new Color(muted.r,muted.g,muted.b,.6f),0,-30,400,18);

            // Right-side status and location plate.
            var status=new GameObject("Status",typeof(RectTransform)).GetComponent<RectTransform>();status.SetParent(main.transform,false);
            Anchor(status,Vector2.one,new Vector2(-44,-40),new Vector2(320,40));status.pivot=Vector2.one;
            Reveal(status.gameObject,.4f,new Vector2(20,0));
            signalDot=Image("Signal dot",status,cyan);signalDot.sprite=glowSprite;Place(signalDot.rectTransform,186,3,12,12);
            signalStatus=Label(status,"VAULT SIGNAL",12,cyan,0,0,180,18);signalStatus.alignment=TextAnchor.UpperRight;
            var depth=Label(status,"DEPTH 1,240 M   //   SECTOR 07",11,muted,0,20,320,18);depth.alignment=TextAnchor.UpperRight;
            var statusTitle=Label(status,"LIVE",12,white,204,0,116,18,FontStyle.Bold);statusTitle.alignment=TextAnchor.UpperLeft;

            var location=new GameObject("Location",typeof(RectTransform)).GetComponent<RectTransform>();location.SetParent(main.transform,false);
            Anchor(location,new Vector2(1,0),new Vector2(-44,44),new Vector2(420,90));location.pivot=new Vector2(1,0);
            Reveal(location.gameObject,.5f,new Vector2(20,0));
            var locTag=Label(location,"CURRENT LOCATION",11,cyan,0,0,420,18);locTag.alignment=TextAnchor.UpperRight;
            var locName=Label(location,"AURORA VAULT",30,white,0,18,420,38,FontStyle.Bold);locName.alignment=TextAnchor.UpperRight;Glow(locName,cyan,.12f);
            var locSub=Label(location,"THE RIFT BENEATH THE WORLD",13,aether,0,58,420,20);locSub.alignment=TextAnchor.UpperRight;
            var locRule=Image("Location rule",location,cyan);locRule.rectTransform.anchorMin=locRule.rectTransform.anchorMax=new Vector2(1,0);locRule.rectTransform.pivot=new Vector2(1,0);locRule.rectTransform.anchoredPosition=new Vector2(0,-6);locRule.rectTransform.sizeDelta=new Vector2(120,2);

            scanline=Image("Scanline",main.transform,new Color(cyan.r,cyan.g,cyan.b,.05f));
            scanline.rectTransform.anchorMin=new Vector2(0,1);scanline.rectTransform.anchorMax=Vector2.one;scanline.rectTransform.pivot=new Vector2(.5f,.5f);scanline.rectTransform.sizeDelta=new Vector2(0,90);scanline.sprite=glowSprite;
            Brackets(main.transform,18,34,2,new Color(cyan.r,cyan.g,cyan.b,.55f));
        }

        // ───────────────────────────── Pause ─────────────────────────────
        void BuildPause()
        {
            pause=Page("Pause",true);
            Motes(pause.transform,new Color(.55f,1,.95f,.3f),24,31);
            var card=Card(pause,"//  SYSTEM HOLD","MISSION PAUSED",470,500,cyan);
            Label(card,"CURRENT OBJECTIVE",11,cyan,36,104,300,18);
            pauseObjective=Label(card,"",17,white,36,122,400,24,FontStyle.Bold);
            pauseTime=Label(card,"",11,muted,36,148,400,18);
            Divider(card,36,180,398);
            pauseResume=Row(card,"01","RESUME MISSION",36,196,398,50,flow.ResumeMission,cyan,true,.05f);
            Row(card,"02","FIELD MANUAL",36,250,398,44,flow.ShowControls,cyan,false,.1f);
            Row(card,"03","RESTART MISSION",36,298,398,44,flow.RestartMission,cyan,false,.15f);
            Row(card,"04","MAIN MENU",36,346,398,44,flow.MainMenu,cyan,false,.2f);
            Row(card,"05","QUIT",36,394,398,44,flow.Quit,cyan,false,.25f);
            var hint=Label(card,"ESC  RESUME",11,muted,36,456,398,18);hint.alignment=TextAnchor.UpperRight;
        }

        // ───────────────────────────── Field manual ─────────────────────────────
        void BuildControls()
        {
            controls=Page("Controls",true);
            var card=Card(controls,"//  ARC  ·  HUMAN-MACHINE PATHFINDER","FIELD MANUAL",720,600,cyan);
            string[] keys={"W A S D","MOUSE","LEFT CLICK","RIGHT CLICK","R","SPACE  x2","LEFT SHIFT","V","F","Q","ESC"};
            string[] actions={"Move","Look and aim","Fire energy beam","Scope in (first person)","Recharge energy cell","Jump / double jump","Sprint","Switch first / third person","Interact","Place a sound decoy","Pause mission"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=108+i*36;
                var row=Group("Binding "+keys[i],card,36,y,648,30,.04f+i*.03f);
                Keycap(row,keys[i],0,2,150,cyan);
                Label(row,actions[i],16,white,172,5,460,22);
                var line=Image("Line",row,new Color(1,1,1,.05f));Place(line.rectTransform,172,30,476,1);
            }
            controlsBack=Row(card,"<","BACK",36,514,648,50,flow.CloseControls,cyan,true,.4f);
        }

        // ───────────────────────────── Defeat ─────────────────────────────
        void BuildDefeat()
        {
            defeat=Page("Defeat",true);
            defeatVignette=Image("Ember vignette",defeat.transform,new Color(ember.r*.6f,ember.g*.2f,ember.b*.1f,.7f));defeatVignette.sprite=vignetteSprite;Stretch(defeatVignette.rectTransform,Vector2.zero,Vector2.one);
            Motes(defeat.transform,new Color(1,.5f,.3f,.35f),30,53);
            var card=Card(defeat,"//  ARC DOWN","",520,380,ember);
            var title=Group("Defeat title",card,36,54,460,60,.05f);
            defeatGhost=Label(title,"SIGNAL LOST",44,new Color(ember.r,ember.g,ember.b,.4f),0,0,460,60,FontStyle.Bold);
            defeatTitle=Label(title,"SIGNAL LOST",44,white,0,0,460,60,FontStyle.Bold);Glow(defeatTitle,ember,.18f);
            Label(card,"Pathfinder vitals flatlined. The rift still grows.\nRise again and finish the descent.",16,new Color(.9f,.85f,.82f),36,124,450,50).lineSpacing=1.15f;
            Divider(card,36,190,448);
            retry=Row(card,"01","RETRY MISSION",36,208,448,52,flow.RestartMission,ember,true,.15f);
            Row(card,"02","MAIN MENU",36,264,448,46,flow.MainMenu,ember,false,.22f);
            Row(card,"03","QUIT",36,314,448,46,flow.Quit,ember,false,.29f);
        }

        // ───────────────────────────── Victory ─────────────────────────────
        void BuildVictory()
        {
            victory=Page("Victory",true);victoryGroup=victory.AddComponent<CanvasGroup>();
            var glow=Image("Core glow",victory.transform,new Color(cyan.r,cyan.g,cyan.b,.22f));glow.sprite=glowSprite;Center(glow.rectTransform,0,70,900,900);
            Motes(victory.transform,new Color(.6f,1,.96f,.7f),60,71);
            victoryRays=Image("Rays",victory.transform,new Color(cyan.r,cyan.g,cyan.b,.07f));Center(victoryRays.rectTransform,0,70,760,760);
            for(int i=0;i<12;i++)
            {
                var ray=Image("Ray",victoryRays.transform,Color.white);ray.sprite=glowSprite;Center(ray.rectTransform,0,0,8,760);ray.rectTransform.localRotation=Quaternion.Euler(0,0,i*15);
            }
            var rune=Image("Return signal",victory.transform,new Color(cyan.r,cyan.g,cyan.b,.16f));rune.sprite=ringSprite;victoryRing=rune.rectTransform;Center(victoryRing,0,70,480,480);
            var inner=Image("Signal core",victoryRing,new Color(cyan.r,cyan.g,cyan.b,.28f));inner.sprite=thinRingSprite;Center(inner.rectTransform,0,0,330,330);

            var tag=CenterLabel(victory.transform,"//  AETHER CORE AWAKENED  //",14,cyan,0,252,750,24,.1f);
            var titleRoot=new GameObject("Victory title",typeof(RectTransform)).GetComponent<RectTransform>();titleRoot.SetParent(victory.transform,false);Center(titleRoot,0,150,900,150);Reveal(titleRoot.gameObject,.2f,new Vector2(0,-16));
            var title=Label(titleRoot,"MISSION\nCOMPLETE",70,white,0,0,900,150,FontStyle.Bold);Stretch(title.rectTransform,Vector2.zero,Vector2.one);title.alignment=TextAnchor.MiddleCenter;title.lineSpacing=.9f;Glow(title,cyan,.2f);
            CenterLabel(victory.transform,"RIFT SEALED  ·  SKY RECLAIMED",20,aether,0,46,750,30,.35f).fontStyle=FontStyle.Bold;

            var stats=new GameObject("Stats",typeof(RectTransform)).GetComponent<RectTransform>();stats.SetParent(victory.transform,false);Center(stats,0,-10,560,56);Reveal(stats.gameObject,.45f,new Vector2(0,-10));
            var statsBg=Image("Stats glass",stats,glass);Stretch(statsBg.rectTransform,Vector2.zero,Vector2.one);
            Brackets(stats,-4,12,2,new Color(cyan.r,cyan.g,cyan.b,.7f));
            StatCell(stats,0,"MISSION TIME",out time);
            StatCell(stats,1,"WARDEN",out var warden);warden.text="DEFEATED";
            StatCell(stats,2,"RELAY",out var relay);relay.text="EXTRACTED";

            playAgain=Row(victory.transform,"01","PLAY AGAIN",0,0,360,50,flow.RestartMission,cyan,true,.55f);CenterRow(playAgain,-92);
            CenterRow(Row(victory.transform,"02","MAIN MENU",0,0,360,44,flow.MainMenu,cyan,false,.6f),-144);
            CenterRow(Row(victory.transform,"03","QUIT",0,0,360,44,flow.Quit,cyan,false,.65f),-192);
            Brackets(victory.transform,18,34,2,new Color(cyan.r,cyan.g,cyan.b,.55f));
        }

        void StatCell(Transform parent,int column,string heading,out Text value)
        {
            float w=560f/3;
            var h=Label(parent,heading,10,cyan,column*w,8,w,16);h.alignment=TextAnchor.UpperCenter;
            value=Label(parent,"",17,white,column*w,24,w,24,FontStyle.Bold);value.alignment=TextAnchor.UpperCenter;
            if(column>0){var sep=Image("Sep",parent,new Color(1,1,1,.08f));Place(sep.rectTransform,column*w,10,1,36);}
        }

        // ───────────────────────────── HUD ─────────────────────────────
        void BuildHud()
        {
            hud=Page("Mission HUD",false);
            var objectivePanel=Image("Objective panel",hud.transform,glass);objectivePanel.sprite=gradientSprite;Place(objectivePanel.rectTransform,24,24,380,78);
            var accent=Image("Accent",objectivePanel.transform,cyan);Place(accent.rectTransform,0,0,3,78);
            objectiveDiamond=Diamond(objectivePanel.transform,22,22,9,cyan);
            Label(objectivePanel.transform,"CURRENT OBJECTIVE",11,cyan,38,13,300,18);
            objective=Label(objectivePanel.transform,"",19,white,20,38,340,30,FontStyle.Bold);

            var vitals=Image("Vitals",hud.transform,glass);Bottom(vitals.rectTransform,24,24,300,84);
            Brackets(vitals.transform,-3,10,2,new Color(cyan.r,cyan.g,cyan.b,.6f));
            Label(vitals.transform,"ARC / PATHFINDER",11,cyan,16,10,200,18);
            health=Label(vitals.transform,"",24,white,16,28,268,30,FontStyle.Bold);
            var track=Image("Health track",vitals.transform,new Color(1,1,1,.08f));Place(track.rectTransform,16,64,268,6);
            healthFill=Image("Health fill",track.transform,cyan);healthFillRect=healthFill.rectTransform;
            Stretch(healthFillRect,Vector2.zero,Vector2.one);healthFillRect.pivot=new Vector2(0,.5f);

            var magazine=Image("Magazine",hud.transform,glass);BottomRight(magazine.rectTransform,24,24,320,84);
            Brackets(magazine.transform,-3,10,2,new Color(cyan.r,cyan.g,cyan.b,.6f));
            view=Label(magazine.transform,"",11,cyan,16,10,290,18);
            ammunition=Label(magazine.transform,"",22,white,16,28,290,30,FontStyle.Bold);
            pips=new Image[20];
            for(int i=0;i<pips.Length;i++){pips[i]=Image("Cell "+i,magazine.transform,cyan);Place(pips[i].rectTransform,16+i*14.4f,62,11,10);}

            var tip=Label(hud.transform,"ESC  PAUSE",11,muted,0,0,190,20);tip.rectTransform.anchorMin=tip.rectTransform.anchorMax=new Vector2(.5f,0);tip.rectTransform.pivot=new Vector2(.5f,0);tip.rectTransform.anchoredPosition=new Vector2(0,24);tip.alignment=TextAnchor.MiddleCenter;
        }

        // ───────────────────────────── Cinematic ─────────────────────────────
        void BuildCinematic()
        {
            cinematic=Page("Mission cinematic",false);
            var top=Image("Top letterbox",cinematic.transform,Color.black);letterTop=top.rectTransform;Stretch(letterTop,new Vector2(0,1),Vector2.one);letterTop.pivot=new Vector2(.5f,1);letterTop.sizeDelta=new Vector2(0,60);
            var bottom=Image("Bottom letterbox",cinematic.transform,Color.black);letterBottom=bottom.rectTransform;Stretch(letterBottom,Vector2.zero,new Vector2(1,0));letterBottom.pivot=new Vector2(.5f,0);letterBottom.sizeDelta=new Vector2(0,130);
            var topLine=Image("Top light",top.transform,new Color(cyan.r,cyan.g,cyan.b,.35f));Stretch(topLine.rectTransform,Vector2.zero,new Vector2(1,0));topLine.rectTransform.pivot=new Vector2(.5f,0);topLine.rectTransform.sizeDelta=new Vector2(0,1);
            var bottomLine=Image("Bottom light",bottom.transform,new Color(cyan.r,cyan.g,cyan.b,.35f));Stretch(bottomLine.rectTransform,new Vector2(0,1),Vector2.one);bottomLine.rectTransform.pivot=new Vector2(.5f,1);bottomLine.rectTransform.sizeDelta=new Vector2(0,1);
            caption=Label(bottom.transform,"",21,white,0,0,0,0);Stretch(caption.rectTransform,Vector2.zero,Vector2.one);caption.rectTransform.offsetMax=new Vector2(-60,-12);caption.rectTransform.offsetMin=new Vector2(60,12);caption.alignment=TextAnchor.MiddleCenter;Glow(caption,cyan,.1f);
            skip=Row(cinematic.transform,">>","SKIP  /  SPACE",0,0,230,40,flow.SkipIntroduction,cyan,false,.6f);
            BottomRight(skip.GetComponent<RectTransform>(),26,142,230,40);
        }

        // ───────────────────────────── State ─────────────────────────────
        public void Show(MissionState state)
        {
            main.SetActive(state==MissionState.MainMenu);pause.SetActive(state==MissionState.Paused);controls.SetActive(state==MissionState.Controls);
            defeat.SetActive(state==MissionState.Defeat);victory.SetActive(state==MissionState.Victory);hud.SetActive(state==MissionState.Playing);
            cinematic.SetActive(state==MissionState.Introduction||state==MissionState.Ending);skip.gameObject.SetActive(state==MissionState.Introduction);
            fade.color=Color.clear;
            Button selected=state==MissionState.MainMenu?mainStart:state==MissionState.Paused?pauseResume:state==MissionState.Controls?controlsBack:state==MissionState.Defeat?retry:state==MissionState.Victory?playAgain:null;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(selected!=null?selected.gameObject:null);
        }

        public void Tick(MissionFlowController mission,LocalCombatDriver driver)
        {
            float t=mission.StateSeconds,now=Time.unscaledTime;
            if(main.activeSelf)
            {
                menuRuneA.rectTransform.localRotation=Quaternion.Euler(0,0,now*3);
                menuRuneB.rectTransform.localRotation=Quaternion.Euler(0,0,-now*5);
                signalDot.color=new Color(cyan.r,cyan.g,cyan.b,.4f+.6f*Mathf.Abs(Mathf.Sin(now*2.2f)));
                float sweep=Mathf.Repeat(now*.12f,1.3f);
                scanline.rectTransform.anchoredPosition=new Vector2(0,-sweep*canvas.rect.height);
                Glitch(titleGhostA,titleGhostB,now,3.5f);
            }
            if(defeat.activeSelf)
            {
                defeatVignette.color=new Color(.6f,.1f,.04f,.55f+.2f*Mathf.Sin(now*2));
                Glitch(defeatGhost,null,now,6f);
            }
            if(mission.State==MissionState.Paused)
            {
                pauseObjective.text=mission.Objective.ToUpperInvariant();
                pauseTime.text="MISSION TIME   "+TimeSpan.FromSeconds(mission.MissionSeconds).ToString(@"mm\:ss");
            }
            if(mission.State==MissionState.Playing)
            {
                objective.text=mission.Objective;
                float hp=driver.vitals.Health,max=Mathf.Max(1,driver.vitals.MaximumHealth),fraction=Mathf.Clamp01(hp/max);
                health.text="HEALTH  "+hp.ToString("0");
                healthFillRect.localScale=new Vector3(Mathf.Lerp(healthFillRect.localScale.x,fraction,1-Mathf.Exp(-10*Time.unscaledDeltaTime)),1,1);
                Color hpColor=fraction>.35f?cyan:Color.Lerp(ember,gold,Mathf.Abs(Mathf.Sin(now*5)));
                healthFill.color=hpColor;
                var mag=driver.Magazine;
                ammunition.text=mag.IsReloading?"RECHARGING  "+((1-mag.ReloadProgress)*mag.reloadDuration).ToString("0.0")+"s":mag.Rounds+" / "+mag.capacity+"   UNLIMITED";
                int lit=mag.IsReloading?Mathf.FloorToInt(mag.ReloadProgress*pips.Length):mag.Rounds;
                for(int i=0;i<pips.Length;i++)pips[i].color=i<lit?(mag.IsReloading?new Color(gold.r,gold.g,gold.b,.8f):cyan):new Color(1,1,1,.08f);
                view.text=(driver.IsFirstPerson?"FIRST PERSON":"THIRD PERSON")+"   /   V SWITCH";
                objectiveDiamond.localScale=Vector3.one*(1+.15f*Mathf.Sin(now*3));
                hud.SetActive(!mission.BossIntroPlaying);
            }
            if(mission.State==MissionState.Introduction||mission.State==MissionState.Ending)
            {
                float bars=1-Mathf.Pow(1-Mathf.Clamp01(t/.6f),3);
                letterTop.sizeDelta=new Vector2(0,60*bars);letterBottom.sizeDelta=new Vector2(0,130*bars);
            }
            if(mission.State==MissionState.Introduction)
            {
                caption.text=t<2?"AURORA VAULT\nA fractured core is tearing reality apart.":"Arc and Relay: descend into the Vault.\nDefeat the Rift Warden. Awaken the Aether Core. Seal the breach.";
            }
            if(mission.State==MissionState.Ending)
            {
                caption.text=t<3?"RIFT WARDEN DEFEATED":t<5.8f?"AETHER CORE AWAKENING\nThe breach is sealed. Reality is stabilizing.":"RETURN GATE ACTIVE\nArc and Relay: passage secured.";
                fade.color=new Color(.7f,1,1,Mathf.InverseLerp(8.1f,9.4f,t));
            }
            if(mission.State==MissionState.Victory)
            {
                victoryGroup.alpha=Mathf.Clamp01(t/.6f);
                victoryRing.localRotation=Quaternion.Euler(0,0,now*5);
                victoryRing.localScale=Vector3.one*(1+Mathf.Sin(now*1.5f)*.025f);
                victoryRays.rectTransform.localRotation=Quaternion.Euler(0,0,-now*2);
                time.text=TimeSpan.FromSeconds(mission.MissionSeconds).ToString(@"mm\:ss");
            }
        }

        // Brief chromatic split on a title every few seconds.
        static void Glitch(Text a,Text b,float now,float period)
        {
            float phase=Mathf.Repeat(now,period);
            bool burst=phase<.18f||phase>1.1f&&phase<1.16f;
            float jitter=burst?Mathf.Sin(now*90)*6:0;
            if(a!=null)a.rectTransform.anchoredPosition=new Vector2(-2-jitter,burst?1:0);
            if(b!=null)b.rectTransform.anchoredPosition=new Vector2(2+jitter*.7f,burst?-1:0);
        }

        // ───────────────────────────── Builders ─────────────────────────────
        GameObject Page(string name,bool dim)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(canvas,false);Stretch(go.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
            if(dim)
            {
                var bg=go.AddComponent<Image>();bg.color=new Color(.004f,.014f,.02f,.82f);
                var vignette=Image("Vignette",go.transform,new Color(0,0,0,.9f));vignette.sprite=vignetteSprite;Stretch(vignette.rectTransform,Vector2.zero,Vector2.one);
            }
            go.SetActive(false);return go;
        }

        Transform Card(GameObject page,string tag,string title,float width,float height,Color accent)
        {
            var panel=Image("Card",page.transform,glass);Center(panel.rectTransform,0,0,width,height);
            Reveal(panel.gameObject,0,new Vector2(0,-18));
            var topLight=Image("Top light",panel.transform,accent);Place(topLight.rectTransform,0,0,width,2);
            var sheen=Image("Sheen",panel.transform,new Color(accent.r,accent.g,accent.b,.06f));sheen.sprite=gradientSprite;Place(sheen.rectTransform,0,2,width,height*.4f);
            Brackets(panel.transform,-8,22,2,accent);
            Label(panel.transform,tag,11,accent,36,30,width-72,18);
            if(!string.IsNullOrEmpty(title)){var t=Label(panel.transform,title,34,white,36,48,width-72,44,FontStyle.Bold);Glow(t,accent,.14f);}
            return panel.transform;
        }

        Button Row(Transform parent,string number,string text,float x,float y,float w,float h,Action action,Color accent,bool primary,float delay)
        {
            var root=Image(text,parent,new Color(0,0,0,0));root.raycastTarget=true;Place(root.rectTransform,x,y,w,h);
            var bg=Image("Base",root.transform,new Color(1,1,1,primary?.07f:.035f));Stretch(bg.rectTransform,Vector2.zero,Vector2.one);
            var sweep=Image("Sweep",root.transform,new Color(accent.r,accent.g,accent.b,.38f));sweep.sprite=gradientSprite;Stretch(sweep.rectTransform,Vector2.zero,Vector2.one);sweep.rectTransform.pivot=new Vector2(0,.5f);
            var line=Image("Underline",root.transform,new Color(accent.r,accent.g,accent.b,primary?.5f:.15f));Stretch(line.rectTransform,Vector2.zero,new Vector2(1,0));line.rectTransform.pivot=new Vector2(.5f,0);line.rectTransform.sizeDelta=new Vector2(0,1);
            var bar=Image("Accent",root.transform,accent);Stretch(bar.rectTransform,Vector2.zero,new Vector2(0,1));bar.rectTransform.pivot=new Vector2(0,.5f);bar.rectTransform.sizeDelta=new Vector2(3,0);
            var index=Label(root.transform,number,12,accent,14,0,30,h,FontStyle.Bold);index.alignment=TextAnchor.MiddleLeft;
            var label=Label(root.transform,text,primary?19:17,white,48,0,w-80,h,FontStyle.Bold);label.alignment=TextAnchor.MiddleLeft;
            var chevron=Label(root.transform,">",18,accent,0,0,20,h,FontStyle.Bold);chevron.alignment=TextAnchor.MiddleRight;
            chevron.rectTransform.anchorMin=chevron.rectTransform.anchorMax=new Vector2(1,.5f);chevron.rectTransform.pivot=new Vector2(1,.5f);chevron.rectTransform.anchoredPosition=new Vector2(-18,0);

            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=root;button.transition=Selectable.Transition.None;
            button.onClick.AddListener(()=>action());
            var fx=root.gameObject.AddComponent<MenuRowButton>();
            fx.sweep=sweep.rectTransform;fx.accentBar=bar.rectTransform;fx.label=label;fx.index=index;fx.chevron=chevron;
            fx.accent=accent;fx.idleLabel=primary?new Color(.9f,1,1):new Color(.78f,.86f,.87f);fx.activeLabel=Color.white;fx.labelX=48;fx.idleSweep=primary?.18f:0;
            Reveal(root.gameObject,delay,new Vector2(-24,0));
            return button;
        }

        static void CenterRow(Button button,float y){var r=button.GetComponent<RectTransform>();var size=r.sizeDelta;Center(r,0,y,size.x,size.y);}

        Transform Group(string name,Transform parent,float x,float y,float w,float h,float delay)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);Place(rect,x,y,w,h);
            Reveal(rect.gameObject,delay,new Vector2(-24,0));return rect;
        }

        Text CenterLabel(Transform parent,string text,int size,Color color,float x,float y,float w,float h,float delay)
        {
            var label=Label(parent,text,size,color,0,0,w,h);Center(label.rectTransform,x,y,w,h);label.alignment=TextAnchor.MiddleCenter;
            Reveal(label.gameObject,delay,new Vector2(0,-12));return label;
        }

        void Keycap(Transform parent,string key,float x,float y,float w,Color accent)
        {
            var cap=Image("Key "+key,parent,new Color(accent.r,accent.g,accent.b,.08f));Place(cap.rectTransform,x,y,w,24);
            var outline=cap.gameObject.AddComponent<Outline>();outline.effectColor=new Color(accent.r,accent.g,accent.b,.55f);outline.effectDistance=new Vector2(1,-1);
            var text=Label(cap.transform,key,11,accent,0,0,w,24,FontStyle.Bold);Stretch(text.rectTransform,Vector2.zero,Vector2.one);text.alignment=TextAnchor.MiddleCenter;
        }

        RectTransform Diamond(Transform parent,float x,float centerY,float size,Color color)
        {
            var d=Image("Diamond",parent,color);var r=d.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=new Vector2(x+size*.5f,-centerY);r.sizeDelta=new Vector2(size,size);r.localRotation=Quaternion.Euler(0,0,45);return r;
        }

        void Divider(Transform parent,float x,float y,float w){var d=Image("Divider",parent,new Color(1,1,1,.08f));Place(d.rectTransform,x,y,w,1);}

        void Motes(Transform parent,Color color,int count,int seed)
        {
            var go=new GameObject("Aether motes",typeof(RectTransform));go.transform.SetParent(parent,false);Stretch((RectTransform)go.transform,Vector2.zero,Vector2.one);
            go.AddComponent<MenuMotes>().Build(glowSprite,color,count,seed);
        }

        // HUD-style corner brackets around a rect; negative inset draws outside it.
        void Brackets(Transform parent,float inset,float length,float thickness,Color color)
        {
            Vector2[] corners={new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
            foreach(var c in corners)
            {
                Vector2 dir=new Vector2(c.x==0?1:-1,c.y==0?1:-1);
                var h=Image("Bracket",parent,color);var hr=h.rectTransform;hr.anchorMin=hr.anchorMax=c;hr.pivot=c;hr.anchoredPosition=dir*inset;hr.sizeDelta=new Vector2(length,thickness);
                var v=Image("Bracket",parent,color);var vr=v.rectTransform;vr.anchorMin=vr.anchorMax=c;vr.pivot=c;vr.anchoredPosition=dir*inset;vr.sizeDelta=new Vector2(thickness,length);
            }
        }

        static void Glow(Text text,Color color,float alpha)
        {
            var near=text.gameObject.AddComponent<Outline>();near.effectColor=new Color(color.r,color.g,color.b,alpha);near.effectDistance=new Vector2(2,-2);
            var far=text.gameObject.AddComponent<Shadow>();far.effectColor=new Color(color.r,color.g,color.b,alpha*.6f);far.effectDistance=new Vector2(0,-4);
        }

        static void Reveal(GameObject go,float delay,Vector2 offset)
        {
            if(go.GetComponent<CanvasGroup>()==null)go.AddComponent<CanvasGroup>();
            var reveal=go.AddComponent<MenuReveal>();reveal.delay=delay;reveal.offset=offset;
        }

        Image Image(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var result=go.GetComponent<Image>();result.color=color;result.raycastTarget=false;return result;
        }

        Text Label(Transform parent,string text,int size,Color color,float x,float y,float w,float h,FontStyle style=FontStyle.Normal)
        {
            var go=new GameObject(text.Length>25?text.Substring(0,25):text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var result=go.GetComponent<Text>();result.font=font;result.text=text;result.fontSize=size;result.color=color;result.fontStyle=style;result.raycastTarget=false;
            result.verticalOverflow=VerticalWrapMode.Overflow;result.horizontalOverflow=HorizontalWrapMode.Wrap;
            Place(result.rectTransform,x,y,w,h);return result;
        }

        static void Stretch(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
        static void Center(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        static void Anchor(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        static void Bottom(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
        static void BottomRight(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-x,y);rect.sizeDelta=new Vector2(w,h);}

        // ───────────────────────────── Procedural sprites ─────────────────────────────
        void CreateSprites()
        {
            // Solid on the left, easing out to transparent: glass panels that melt into the scene.
            gradientSprite=MakeSprite(256,4,(u,v)=>{float a=u<.42f?1:1-Mathf.SmoothStep(0,1,(u-.42f)/.58f);return a;});
            vignetteSprite=MakeSprite(128,128,(u,v)=>{float d=Vector2.Distance(new Vector2(u,v),new Vector2(.5f,.5f))/.7071f;return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1.05f,d));});
            glowSprite=MakeSprite(64,64,(u,v)=>{float d=Mathf.Clamp01(Vector2.Distance(new Vector2(u,v),new Vector2(.5f,.5f))*2);return (1-d)*(1-d);});
            ringSprite=MakeSprite(256,256,(u,v)=>Ring(u,v,.47f,.008f,true));
            thinRingSprite=MakeSprite(256,256,(u,v)=>Ring(u,v,.46f,.004f,false));
        }

        // Ring with tick marks and broken arcs so the rotation is visible.
        static float Ring(float u,float v,float radius,float width,bool ticks)
        {
            Vector2 p=new Vector2(u-.5f,v-.5f);float r=p.magnitude;float angle=Mathf.Atan2(p.y,p.x)*Mathf.Rad2Deg+180;
            float main=Mathf.Clamp01(1-Mathf.Abs(r-radius)/width);
            if(Mathf.Repeat(angle,90)>78)main*=.15f;
            float inner=Mathf.Clamp01(1-Mathf.Abs(r-(radius-.035f))/(width*.5f))*.6f;
            if(Mathf.Repeat(angle+20,45)>30)inner=0;
            float tick=0;
            if(ticks&&r>radius+.012f&&r<radius+.03f&&Mathf.Repeat(angle,10)<1.2f)tick=.8f;
            return Mathf.Max(main,Mathf.Max(inner,tick));
        }

        Sprite MakeSprite(int w,int h,Func<float,float,float> alpha)
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=new Color(1,1,1,Mathf.Clamp01(alpha((x+.5f)/w,(y+.5f)/h)));
            texture.SetPixels(pixels);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f));
            owned.Add(texture);owned.Add(sprite);return sprite;
        }

        void OnDestroy(){foreach(var o in owned)if(o!=null)Destroy(o);}
    }
}
