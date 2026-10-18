using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.AI.RiftWarden
{
    // Presentation only: a short boss entrance and the boss health bar. Never writes to the brain.
    // The first time the player nears the dormant boss, world time slows to a crawl (so neither Arc nor the
    // boss moves) while the camera, running on real time, sweeps around the boss, the visor ignites and the
    // title appears. Space skips. Afterwards a health bar tracks arms + core until the boss is defeated.
    [DefaultExecutionOrder(9000)]    // after the player camera rig, before CameraShake (10000)
    [DisallowMultipleComponent]
    public sealed class RiftBossIntro : MonoBehaviour
    {
        public RiftWardenBrain brain;
        public string bossName = "RIFT WARDEN";
        public string subtitle = "Keeper of the Shattered Core";
        public bool playIntro = true;
        [Tooltip("The intro starts when the player is this far beyond the arena edge, or closer.")]
        public float triggerMargin = 9f;
        public float introSeconds = 4.6f, blendBackSeconds = .9f;
        [Tooltip("Play the intro again after the encounter is reset (restart, retry).")]
        public bool replayAfterReset = true;
        [Range(0.01f, 1)] public float worldTimeScaleDuringIntro = .08f;
        public Color accent = new Color(.75f, .3f, 1f), fill = new Color(.62f, .25f, 1f), fillEnd = new Color(.3f, .9f, 1f);

        public bool IntroPlaying => playing;
        public bool IntroDone { get; private set; }

        bool playing, ownsTimeScale, wasActive;
        /// <summary>Real seconds since the current intro started (negative when none is playing).</summary>
        public float IntroTime => playing ? Time.unscaledTime - startedAt : -1;
        float startedAt, hudAlpha, shownHealth = 1, chipHealth = 1, defeatedAt = -1;
        Camera cam; Vector3 startPos; Quaternion startRot;
        Transform head; Renderer headRenderer; Color headEmission; MaterialPropertyBlock block;
        Texture2D barTex, glowTex;
        GUIStyle titleStyle, subStyle, labelStyle;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        void Awake() { if (brain == null) brain = GetComponent<RiftWardenBrain>(); }

        void Start()
        {
            if (brain == null) return;
            foreach (var t in brain.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head") head = t;
                if (t.name == "RW_Head") headRenderer = t.GetComponent<Renderer>();
            }
            if (headRenderer != null && headRenderer.sharedMaterial != null && headRenderer.sharedMaterial.HasProperty(EmissionColor))
                headEmission = headRenderer.sharedMaterial.GetColor(EmissionColor);
        }

        float PlayerDistanceToArena()
        {
            if (brain.player == null) return float.MaxValue;
            Vector3 d = brain.player.position - brain.arenaCentre; d.y = 0;
            return d.magnitude;
        }

        void Update()
        {
            if (brain == null) return;
            var action = brain.Action;
            // A reset encounter (Backspace / F7 in the lab, or a retry) gets the intro again.
            if (replayAfterReset && action == RiftAction.Dormant && wasActive && !playing) { IntroDone = false; wasActive = false; }
            bool fresh = brain.LeftHealth >= brain.armMaximum && brain.RightHealth >= brain.armMaximum && brain.CoreHealth >= brain.coreMaximum;
            if (!IntroDone && !playing && playIntro)
            {
                // Start while the boss sleeps and the player approaches, or - if the player arrived inside the arena
                // (teleport, spawn) and the boss woke first - as soon as a fresh fight begins.
                bool approaching = action == RiftAction.Dormant && PlayerDistanceToArena() < brain.arenaRadius + triggerMargin;
                bool justWoke = action != RiftAction.Dormant && !brain.Defeated && fresh;
                if (approaching || justWoke) Begin();
                else if (action != RiftAction.Dormant) IntroDone = true;   // mid-fight (test keys): no intro
            }
            if (!playIntro && !IntroDone && action != RiftAction.Dormant) IntroDone = true;
            if (action != RiftAction.Dormant) wasActive = true;

            if (playing)
            {
                var kb = Keyboard.current;
                if (Time.unscaledTime - startedAt > .4f && kb != null && kb.spaceKey.wasPressedThisFrame) startedAt = Time.unscaledTime - introSeconds;
                if (Time.unscaledTime - startedAt >= introSeconds + blendBackSeconds) End();
                else { Time.timeScale = worldTimeScaleDuringIntro; ownsTimeScale = true; }
            }

            // health bar
            float total = Mathf.Max(1, brain.armMaximum * 2 + brain.coreMaximum);
            float health = Mathf.Clamp01((brain.LeftHealth + brain.RightHealth + brain.CoreHealth) / total);
            float dt = Time.unscaledDeltaTime;
            shownHealth = Mathf.MoveTowards(shownHealth, health, dt * 2.5f);
            chipHealth = health < chipHealth ? Mathf.MoveTowards(chipHealth, health, dt * .35f) : health;
            if (brain.Defeated && defeatedAt < 0) defeatedAt = Time.unscaledTime;
            if (brain.Action == RiftAction.Dormant) defeatedAt = -1;
            bool show = IntroDone && brain.Action != RiftAction.Dormant && PlayerDistanceToArena() < brain.arenaRadius + 20
                        && (defeatedAt < 0 || Time.unscaledTime - defeatedAt < 2.5f);
            if (playing && Time.unscaledTime - startedAt > introSeconds * .7f) show = true;
            hudAlpha = Mathf.MoveTowards(hudAlpha, show ? 1 : 0, dt * 2);
        }

        void Begin()
        {
            cam = Camera.main;
            if (cam == null) { IntroDone = true; return; }
            playing = true; startedAt = Time.unscaledTime;
            startPos = cam.transform.position; startRot = cam.transform.rotation;
            shownHealth = chipHealth = 0;   // the bar fills in during the intro
        }

        void End()
        {
            playing = false; IntroDone = true;
            if (ownsTimeScale) { Time.timeScale = 1; ownsTimeScale = false; }
            SetVisor(1);
        }

        void LateUpdate()
        {
            if (!playing || cam == null) return;
            float t = Time.unscaledTime - startedAt;
            ShotPose(t, out Vector3 pos, out Quaternion rot);

            float intoShot = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .45f, t));
            if (t < .45f) { pos = Vector3.Lerp(startPos, pos, intoShot); rot = Quaternion.Slerp(startRot, rot, intoShot); }
            if (t > introSeconds)
            {
                // the player rig has already placed the camera this frame; blend from the intro shot back to it
                float back = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(introSeconds, introSeconds + blendBackSeconds, t));
                pos = Vector3.Lerp(pos, cam.transform.position, back); rot = Quaternion.Slerp(rot, cam.transform.rotation, back);
            }
            cam.transform.SetPositionAndRotation(pos, rot);

            // the visor ignites ~0.7 s in, flares, then settles
            float ignite = Mathf.InverseLerp(.6f, 1.1f, t);
            float flare = Mathf.Exp(-Mathf.Max(0, t - 1.1f) * 2.2f) * 3f;
            SetVisor(t < .6f ? .05f : ignite * (1 + flare));
        }

        /// <summary>The intro camera shot at time t (seconds), before blending with the player camera.</summary>
        public void ShotPose(float t, out Vector3 pos, out Quaternion rot)
        {
            if (head == null && brain != null) foreach (var tr in brain.GetComponentsInChildren<Transform>(true)) if (tr.name == "Head") head = tr;
            Vector3 boss = brain.transform.position;
            Vector3 fwd = brain.transform.forward, right = brain.transform.right;
            Vector3 focusLow = boss + Vector3.up * 2.2f, focusHead = head != null ? head.position : boss + Vector3.up * 3.1f;
            // shot A: low, close, looking up at the visor; shot B: sweep out to a heroic three-quarter
            Vector3 aPos = boss + fwd * 4.2f + right * -1.2f + Vector3.up * 1.0f;
            Vector3 bPos = boss + fwd * 8.5f + right * 4.5f + Vector3.up * 2.6f;
            float sweep = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.6f, introSeconds, t));
            float dolly = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 1.6f, t));
            Vector3 aPosIn = Vector3.Lerp(aPos + fwd * 1.2f, aPos, dolly);
            pos = Vector3.Slerp(aPosIn - boss, bPos - boss, sweep) + boss;
            rot = Quaternion.LookRotation(Vector3.Lerp(focusHead, focusLow, sweep) - pos);
        }

        void SetVisor(float strength)
        {
            if (headRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            headRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, headEmission * strength);
            headRenderer.SetPropertyBlock(block);
            if (Mathf.Approximately(strength, 1)) headRenderer.SetPropertyBlock(null);
        }

        void OnDisable()
        {
            if (ownsTimeScale) { Time.timeScale = 1; ownsTimeScale = false; }
            if (playing) { playing = false; IntroDone = true; }
            if (headRenderer != null) headRenderer.SetPropertyBlock(null);
        }

        // ------------------------------------------------------------------ HUD
        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            barTex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int x = 0; x < 256; x++) barTex.SetPixel(x, 0, Color.Lerp(fill, fillEnd, x / 255f));
            barTex.Apply();
            glowTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 32; y++) { float v = 1 - Mathf.Abs(y / 31f - .5f) * 2; glowTex.SetPixel(0, y, new Color(1, 1, 1, v * v)); }
            glowTex.Apply();
        }

        static string Spaced(string s) => string.Join(" ", s.ToCharArray());

        void OnGUI()
        {
            if (brain == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            if (playing)
            {
                float t = Time.unscaledTime - startedAt;
                float bars = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .5f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(introSeconds - .2f, introSeconds + blendBackSeconds, t)));
                float barH = h * .11f * bars;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, w, barH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, h - barH, w, barH), Texture2D.whiteTexture);

                float title = Mathf.InverseLerp(1.3f, 2.0f, t) * (1 - Mathf.InverseLerp(introSeconds - .5f, introSeconds, t));
                if (title > 0)
                {
                    float spread = Mathf.Lerp(1.08f, 1f, Mathf.InverseLerp(1.3f, 2.6f, t));
                    titleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(h * .085f, 36, 96));
                    subStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(h * .026f, 14, 30));
                    var r = new Rect(0, h * .66f, w, h * .12f);
                    var glowR = new Rect(w * .5f - w * .32f * spread, r.y + r.height * .2f, w * .64f * spread, r.height * .6f);
                    GUI.color = new Color(accent.r, accent.g, accent.b, .35f * title);
                    GUI.DrawTexture(glowR, glowTex);
                    string name = Spaced(bossName);
                    titleStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, .55f * title);
                    GUI.color = Color.white;
                    foreach (var o in new[] { new Vector2(-3, 0), new Vector2(3, 0), new Vector2(0, -3), new Vector2(0, 3) })
                        GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), name, titleStyle);
                    titleStyle.normal.textColor = new Color(.96f, .93f, 1f, title);
                    GUI.Label(r, name, titleStyle);
                    subStyle.normal.textColor = new Color(.7f, .92f, .95f, title * Mathf.InverseLerp(1.8f, 2.4f, t));
                    GUI.Label(new Rect(0, r.yMax - 4, w, h * .05f), subtitle, subStyle);
                }
                if (t > .4f && t < introSeconds)
                {
                    labelStyle.normal.textColor = new Color(1, 1, 1, .45f);
                    labelStyle.fontSize = 12; labelStyle.alignment = TextAnchor.LowerRight;
                    GUI.Label(new Rect(w - 220, h - 34, 200, 24), "Space to skip", labelStyle);
                    labelStyle.fontSize = 15; labelStyle.alignment = TextAnchor.MiddleLeft;
                }
            }

            if (hudAlpha <= 0.001f) { GUI.color = Color.white; return; }
            float bw = Mathf.Min(w * .5f, 640), bh = 14, bx = (w - bw) * .5f, by = Mathf.Max(24, h * .045f);
            float a = hudAlpha;
            GUI.color = new Color(0, 0, 0, .55f * a);
            GUI.DrawTexture(new Rect(bx - 6, by - 26, bw + 12, bh + 36), Texture2D.whiteTexture);
            labelStyle.normal.textColor = new Color(.95f, .9f, 1f, a);
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by - 24, bw, 20), Spaced(bossName), labelStyle);
            string phase = brain.Defeated ? "DEFEATED" : brain.CoreExposed ? "CORE EXPOSED" : brain.LeftHealth <= 0 || brain.RightHealth <= 0 ? "ARM DESTROYED" : "";
            if (phase.Length > 0)
            {
                var ps = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight };
                ps.normal.textColor = new Color(fillEnd.r, fillEnd.g, fillEnd.b, a);
                GUI.Label(new Rect(bx, by - 24, bw, 20), phase, ps);
            }
            GUI.color = new Color(.12f, .08f, .18f, .9f * a);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, .8f * a);
            GUI.DrawTexture(new Rect(bx, by, bw * chipHealth, bh), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, a);
            GUI.DrawTextureWithTexCoords(new Rect(bx, by, bw * shownHealth, bh), barTex, new Rect(0, 0, Mathf.Max(.001f, shownHealth), 1));
            // notch where only the core remains
            float coreShare = brain.coreMaximum / Mathf.Max(1, brain.armMaximum * 2 + brain.coreMaximum);
            GUI.color = new Color(1, 1, 1, .7f * a);
            GUI.DrawTexture(new Rect(bx + bw * coreShare - 1, by - 3, 2, bh + 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void OnDestroy()
        {
            if (barTex != null) Destroy(barTex);
            if (glowTex != null) Destroy(glowTex);
        }
    }
}
