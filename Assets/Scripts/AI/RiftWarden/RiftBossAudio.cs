using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    // Presentation only: reads RiftWardenBrain and adds the fight's extra audio. Never writes to the brain,
    // and leaves the brain's own CombatAudio cues untouched.
    //  - intro: a rising power-up hum while RiftBossIntro plays (faded out if the intro is skipped)
    //  - crystal crunches when the player damages an arm or the core (random variant + pitch)
    //  - a boom when the slam lands and a crystal shatter at the death impact (synced to RiftAttackFx)
    //  - adaptive music: three sample-synced loops whose volumes follow the fight stage
    [DisallowMultipleComponent]
    public sealed class RiftBossAudio : MonoBehaviour
    {
        public RiftWardenBrain brain;

        [Header("Clips")]
        public AudioClip introHum;
        [Tooltip("Mechanical awakening roar, played when the visor ignites during the intro.")]
        public AudioClip introRoar;
        public float roarAt = .55f;
        [Range(0, 1)] public float roarVolume = .9f;
        public AudioClip[] crystalHits;
        public AudioClip slamBoom, deathShatter;
        [Tooltip("Stage 1 bed, stage 2 drums/arp, core-exposed lead/choir. Same length, played in sync.")]
        public AudioClip musicStage1, musicStage2, musicStage3;

        [Header("Mix")]
        [Range(0, 1)] public float musicVolume = .5f;
        [Range(0, 1)] public float introVolume = .85f, hitVolume = .45f, slamVolume = .9f, shatterVolume = 1f;
        [Tooltip("Stage 1 layer level once the other layers have joined (it is rendered louder for solo play).")]
        [Range(0, 1)] public float stage1WithOthers = .67f;
        public float musicFadeSeconds = 2.5f, hitMinGap = .07f;
        [Tooltip("Music is full volume this far beyond the arena edge and silent at twice the distance.")]
        public float musicReach = 20f;

        AudioSource sfx, introSource, shatterSource, roarSource;
        bool roarPlayed;
        readonly AudioSource[] hitSources = new AudioSource[3];
        readonly AudioSource[] music = new AudioSource[3];
        readonly float[] layer = new float[3];
        RiftBossIntro intro;
        bool introStarted, musicRunning;
        int hitIndex, lastWindows; float lastHitAt = -10, deathStart = -1, quietSince = -1;
        float lastLeft, lastRight, lastCore; RiftAction lastAction;
        bool shatterPlayed;

        void Awake()
        {
            if (brain == null) brain = GetComponent<RiftWardenBrain>();
            intro = GetComponent<RiftBossIntro>();
            var root = new GameObject("Rift boss audio") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * 2f;
            sfx = Source(root, true, 64);
            shatterSource = Source(root, true, 32); shatterSource.spatialBlend = .35f;
            introSource = Source(root, false, 16);
            roarSource = Source(root, true, 16); roarSource.spatialBlend = .4f; roarSource.maxDistance = 60;
            for (int i = 0; i < hitSources.Length; i++) hitSources[i] = Source(root, true, 96);
            for (int i = 0; i < music.Length; i++)
            {
                var m = Source(root, false, 0);
                m.loop = true; m.volume = 0;
                music[i] = m;
            }
            music[0].clip = musicStage1; music[1].clip = musicStage2; music[2].clip = musicStage3;
        }

        static AudioSource Source(GameObject host, bool spatial, int priority)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = false; s.priority = priority; s.dopplerLevel = 0;
            s.spatialBlend = spatial ? 1 : 0; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 4; s.maxDistance = 45;
            return s;
        }

        void Start()
        {
            if (brain == null) return;
            lastLeft = brain.LeftHealth; lastRight = brain.RightHealth; lastCore = brain.CoreHealth;
            lastWindows = brain.DamageWindows; lastAction = brain.Action;
        }

        void Update()
        {
            if (brain == null) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            var a = brain.Action;

            // intro: hum from the first frame, the awakening roar when the visor ignites; replays with every intro
            bool introNow = intro != null && intro.IntroPlaying;
            if (introNow && !introStarted)
            {
                introStarted = true; roarPlayed = false;
                if (introHum != null) { introSource.clip = introHum; introSource.volume = introVolume; introSource.Play(); }
            }
            if (introNow && !roarPlayed && intro.IntroTime >= roarAt)
            {
                roarPlayed = true;
                if (introRoar != null) roarSource.PlayOneShot(introRoar, roarVolume);
            }
            if (!introNow && introStarted)
            {
                // skipped or finished: let the hum tail off, and re-arm for the next intro
                if (introSource.isPlaying) introSource.volume = Mathf.MoveTowards(introSource.volume, 0, dt * 2);
                else introStarted = false;
            }

            // crystal crunches on player hits
            if (brain.LeftHealth < lastLeft - .001f || brain.RightHealth < lastRight - .001f || (brain.CoreHealth < lastCore - .001f && a != RiftAction.Dying))
                CrystalHit();

            // slam boom
            if (brain.DamageWindows > lastWindows && a == RiftAction.Slam && slamBoom != null) sfx.PlayOneShot(slamBoom, slamVolume);

            // death shatter, synced to the death clip's impact (scaled time, like the effect)
            if (a != lastAction && a == RiftAction.Dying) { deathStart = Time.time; shatterPlayed = false; }
            if (a == RiftAction.Dying && !shatterPlayed && deathStart >= 0 && Time.time - deathStart >= 59f / 30)
            {
                shatterPlayed = true;
                if (deathShatter != null) shatterSource.PlayOneShot(deathShatter, shatterVolume);
            }

            UpdateMusic(now, dt, a);

            lastLeft = brain.LeftHealth; lastRight = brain.RightHealth; lastCore = brain.CoreHealth;
            lastWindows = brain.DamageWindows; lastAction = a;
        }

        void CrystalHit()
        {
            float now = Time.unscaledTime;
            if (crystalHits == null || crystalHits.Length == 0 || now - lastHitAt < hitMinGap) return;
            lastHitAt = now;
            var s = hitSources[hitIndex++ % hitSources.Length];
            s.pitch = Random.Range(.9f, 1.12f);
            s.PlayOneShot(crystalHits[Random.Range(0, crystalHits.Length)], hitVolume);
        }

        void UpdateMusic(float now, float dt, RiftAction a)
        {
            // stage targets
            float t1 = 0, t2 = 0, t3 = 0;
            bool fighting = a != RiftAction.Dormant && a != RiftAction.Dying && !brain.Defeated;
            if (fighting)
            {
                bool armLost = brain.LeftHealth <= 0 || brain.RightHealth <= 0;
                bool core = brain.CoreExposed || a == RiftAction.CoreOpen;
                t2 = armLost || core ? 1 : 0;
                t3 = core ? 1 : 0;
                t1 = t2 > 0 ? stage1WithOthers : 1;
            }
            float reach = 1;
            if (brain.player != null)
            {
                Vector3 d = brain.player.position - brain.arenaCentre; d.y = 0;
                reach = 1 - Mathf.Clamp01((d.magnitude - brain.arenaRadius - musicReach) / Mathf.Max(1, musicReach));
            }

            if (fighting && !musicRunning && musicStage1 != null)
            {
                double at = AudioSettings.dspTime + .1;              // sample-accurate start for all layers
                foreach (var m in music) if (m.clip != null) { m.volume = 0; m.PlayScheduled(at); }
                musicRunning = true;
            }
            if (!musicRunning) return;

            float fade = dt / Mathf.Max(.1f, a == RiftAction.Dying ? 1.5f : musicFadeSeconds);
            layer[0] = Mathf.MoveTowards(layer[0], t1, fade);
            layer[1] = Mathf.MoveTowards(layer[1], t2, fade);
            layer[2] = Mathf.MoveTowards(layer[2], t3, fade);
            for (int i = 0; i < 3; i++) music[i].volume = layer[i] * musicVolume * reach;

            bool silent = layer[0] + layer[1] + layer[2] < .001f;
            if (silent) { if (quietSince < 0) quietSince = now; }
            else quietSince = -1;
            if (silent && now - quietSince > 1f)
            {
                foreach (var m in music) m.Stop();
                musicRunning = false; quietSince = -1;
            }
        }

        void OnDisable()
        {
            foreach (var m in music) if (m != null) m.Stop();
            if (introSource != null) introSource.Stop();
            musicRunning = false; for (int i = 0; i < 3; i++) layer[i] = 0;
        }
    }
}
