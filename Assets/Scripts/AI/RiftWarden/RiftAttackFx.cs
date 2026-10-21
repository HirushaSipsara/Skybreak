using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    // Presentation only: reads RiftWardenBrain and plays attack effects. Never writes to the brain.
    // Cannon muzzle/bolt/impact, blade trail, teleport portals, slam shockwave, arm-break debris and the death shatter.
    // Particle systems are built at runtime from the materials assigned in the Inspector.
    [DisallowMultipleComponent]
    public sealed class RiftAttackFx : MonoBehaviour
    {
        public RiftWardenBrain brain;

        [Header("Materials (assigned by setup)")]
        public Material glowMaterial;      // additive soft glow
        public Material sparkMaterial;     // additive streak
        public Material ringMaterial;      // additive ring, used flat on the ground
        public Material smokeMaterial;     // alpha-blended smoke
        public Material crystalMaterial;   // lit crystal shards
        public Material trailMaterial;     // additive, used by the blade ribbon and cannon bolts

        [Header("Blade ribbon (Forearm.R local space, set by setup)")]
        public Vector3 bladeBaseLocal = new Vector3(0, -.45f, 0);
        public Vector3 bladeTipLocal = new Vector3(0, -1.6f, 0);
        public float trailSeconds = .16f;
        [Tooltip("Cannon muzzle centre in Forearm.L local space (set by setup); used for the muzzle flash and bolt start.")]
        public Vector3 cannonMuzzleLocal;
        public bool hasMuzzle;

        [Header("Look")]
        public Color violet = new Color(.78f, .28f, 1f);
        public Color cyan = new Color(.35f, .95f, 1f);
        public Color ember = new Color(1f, .5f, .22f);
        public float boltSpeed = 85f;
        public float slamRadius = 3.2f;

        ParticleSystem sparks, flash, smoke, shards, ring, swirl, column, charge;
        readonly List<Light> lights = new List<Light>();
        readonly float[] lightEnd = new float[4], lightStart = new float[4], lightPeak = new float[4];
        GameObject fxRoot; bool built;
        Transform forearmR, forearmL, upperArmL, upperArmR, coreBone;

        // blade ribbon
        struct Sample { public float t; public Vector3 a, b; public float on; }
        readonly List<Sample> samples = new List<Sample>();
        Mesh ribbon; MeshRenderer ribbonRenderer;
        readonly List<Vector3> rv = new List<Vector3>(); readonly List<Color> rc = new List<Color>();
        readonly List<Vector2> ru = new List<Vector2>(); readonly List<int> ri = new List<int>();

        // bolts
        class Bolt { public GameObject go; public TrailRenderer trail; public Vector3 from, to; public float t, length; public bool live; }
        readonly List<Bolt> bolts = new List<Bolt>();

        // state tracking
        RiftAction lastAction; int lastShots, lastWindows, lastTeleports;
        bool lastLeftOn = true, lastRightOn = true, hidden;
        Vector3 lastBossPos, slamLock; float actionStart, deathStart = -1, leftSparksUntil, rightSparksUntil;
        bool overloadDone, impactDone;
        Renderer[] bossRenderers;

        void Awake() { if (brain == null) brain = GetComponent<RiftWardenBrain>(); }

        void Start()
        {
            Build();
            if (brain == null) return;
            lastAction = brain.Action; lastShots = brain.ShotsFired; lastWindows = brain.DamageWindows; lastTeleports = brain.SuccessfulTeleports;
            lastLeftOn = brain.leftArm == null || brain.leftArm.enabled; lastRightOn = brain.rightArm == null || brain.rightArm.enabled;
            lastBossPos = brain.transform.position;
        }

        // ------------------------------------------------------------------ build
        public void Build()
        {
            if (built || brain == null) return;
            built = true;
            fxRoot = new GameObject("Rift attack FX (runtime)") { hideFlags = HideFlags.DontSave };
            forearmR = Bone("Forearm.R"); forearmL = Bone("Forearm.L"); upperArmL = Bone("UpperArm.L"); upperArmR = Bone("UpperArm.R"); coreBone = Bone("Core");
            bossRenderers = brain.GetComponentsInChildren<Renderer>(true);

            sparks = Make("Sparks", sparkMaterial, 400, ParticleSystemRenderMode.Stretch, life: new Vector2(.25f, .65f),
                          speed: new Vector2(5, 15), size: new Vector2(.04f, .1f), gravity: .7f);
            var sr = sparks.GetComponent<ParticleSystemRenderer>(); sr.velocityScale = .045f; sr.lengthScale = 1.6f;
            Fade(sparks, 1, 0);
            var limit = sparks.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = .12f; limit.limit = 4;

            flash = Make("Flash", glowMaterial, 64, ParticleSystemRenderMode.Billboard, new Vector2(.08f, .12f), Vector2.zero, new Vector2(1, 1), 0);
            Fade(flash, 1, 0); Grow(flash, .6f, 1.3f);

            smoke = Make("Smoke", smokeMaterial, 160, ParticleSystemRenderMode.Billboard, new Vector2(.8f, 1.5f), new Vector2(.4f, 2.2f), new Vector2(.6f, 1.3f), -.04f);
            Fade(smoke, .55f, 0); Grow(smoke, .6f, 1.8f);
            var smain = smoke.main; smain.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var sl = smoke.limitVelocityOverLifetime; sl.enabled = true; sl.dampen = .08f; sl.limit = .5f;

            shards = Make("Crystal shards", crystalMaterial, 220, ParticleSystemRenderMode.Mesh, new Vector2(1.3f, 2.2f), new Vector2(3, 10), new Vector2(.12f, .3f), 1.6f);
            var shr = shards.GetComponent<ParticleSystemRenderer>(); shr.mesh = ShardMesh(); shr.alignment = ParticleSystemRenderSpace.Local;
            var shm = shards.main; shm.startRotation3D = true;
            shm.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); shm.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); shm.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var rot = shards.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8, 8); rot.y = new ParticleSystem.MinMaxCurve(-8, 8); rot.z = new ParticleSystem.MinMaxCurve(-8, 8);
            var col = shards.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Medium; col.bounce = .25f; col.dampen = .45f; col.collidesWith = ~(1 << 8);
            Grow(shards, 1, .55f);

            ring = Make("Ground ring", ringMaterial, 24, ParticleSystemRenderMode.HorizontalBillboard, new Vector2(.55f, .55f), Vector2.zero, new Vector2(1, 1), 0);
            Fade(ring, 1, 0); Grow(ring, .08f, 1f);

            swirl = Make("Portal swirl", glowMaterial, 300, ParticleSystemRenderMode.Billboard, new Vector2(.45f, .8f), new Vector2(0, 0), new Vector2(.1f, .22f), 0);
            Fade(swirl, 1, 0);
            var vel = swirl.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(5f); vel.y = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            vel.x = new ParticleSystem.MinMaxCurve(0); vel.z = new ParticleSystem.MinMaxCurve(0); vel.orbitalX = vel.orbitalZ = new ParticleSystem.MinMaxCurve(0);
            vel.radial = new ParticleSystem.MinMaxCurve(-1.5f);
            var sws = swirl.shape; sws.enabled = true; sws.shapeType = ParticleSystemShapeType.Circle; sws.radius = 1.4f; sws.radiusThickness = .2f; sws.rotation = new Vector3(90, 0, 0);

            column = Make("Portal column", sparkMaterial, 200, ParticleSystemRenderMode.Stretch, new Vector2(.3f, .55f), new Vector2(6, 12), new Vector2(.08f, .16f), 0);
            var cr = column.GetComponent<ParticleSystemRenderer>(); cr.velocityScale = .06f; cr.lengthScale = 2.5f;
            var cs = column.shape; cs.enabled = true; cs.shapeType = ParticleSystemShapeType.Circle; cs.radius = .9f; cs.rotation = new Vector3(-90, 0, 0); cs.arc = 360;
            Fade(column, 1, 0);

            charge = Make("Core charge", glowMaterial, 500, ParticleSystemRenderMode.Billboard, new Vector2(.6f, .9f), new Vector2(-4.5f, -3f), new Vector2(.06f, .16f), 0);
            var chs = charge.shape; chs.enabled = true; chs.shapeType = ParticleSystemShapeType.Sphere; chs.radius = 2.8f; chs.radiusThickness = .1f;
            Fade(charge, 0, 1); Grow(charge, 1, .3f);

            for (int i = 0; i < 4; i++)
            {
                var lgo = new GameObject("FX light " + i) { hideFlags = HideFlags.DontSave };
                lgo.transform.SetParent(fxRoot.transform, false);
                var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.intensity = 0; l.enabled = false;
                lights.Add(l);
            }

            var rgo = new GameObject("Blade ribbon") { hideFlags = HideFlags.DontSave };
            rgo.transform.SetParent(fxRoot.transform, false);
            ribbon = new Mesh { name = "Blade ribbon" }; ribbon.MarkDynamic();
            rgo.AddComponent<MeshFilter>().sharedMesh = ribbon;
            ribbonRenderer = rgo.AddComponent<MeshRenderer>(); ribbonRenderer.sharedMaterial = trailMaterial;
            ribbonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ribbonRenderer.receiveShadows = false;

            for (int i = 0; i < 4; i++)
            {
                var bgo = new GameObject("Cannon bolt " + i) { hideFlags = HideFlags.DontSave };
                bgo.transform.SetParent(fxRoot.transform, false);
                var tr = bgo.AddComponent<TrailRenderer>();
                tr.sharedMaterial = trailMaterial; tr.time = .12f; tr.minVertexDistance = .2f; tr.emitting = false;
                tr.widthCurve = new AnimationCurve(new Keyframe(0, .32f), new Keyframe(1, 0));
                tr.colorGradient = Gradient2(violet, 1, violet, 0);
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bolts.Add(new Bolt { go = bgo, trail = tr });
            }
        }

        Transform Bone(string name)
        {
            foreach (var t in brain.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return brain.transform;
        }

        ParticleSystem Make(string name, Material mat, int max, ParticleSystemRenderMode mode, Vector2 life, Vector2 speed, Vector2 size, float gravity)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(fxRoot.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = max; main.duration = 1;   // looping pool with zero rate: never "finishes", so emitted particles keep simulating
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 30; shape.radius = .05f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode; r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Mesh) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            ps.Play();
            return ps;
        }

        static Gradient Gradient2(Color a, float aa, Color b, float ba)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(aa, 0), new GradientAlphaKey(ba, 1) });
            return g;
        }

        static void Fade(ParticleSystem ps, float from, float to)
        {
            var c = ps.colorOverLifetime; c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(from, 0), new GradientAlphaKey(Mathf.Lerp(from, to, .35f) , .45f), new GradientAlphaKey(to, 1) });
            c.color = g;
        }

        static void Grow(ParticleSystem ps, float from, float to)
        {
            var s = ps.sizeOverLifetime; s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, from, 0, 0), new Keyframe(1, to, 0, 0)));
        }

        static Mesh ShardMesh()
        {
            // elongated, slightly twisted octahedron
            var v = new[] { new Vector3(0, .9f, 0), new Vector3(0, -.6f, 0), new Vector3(.32f, .05f, .05f), new Vector3(-.28f, -.02f, .1f),
                            new Vector3(.05f, .1f, .3f), new Vector3(-.04f, 0, -.27f) };
            int[] t = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5 };
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < t.Length; i++) { verts.Add(v[t[i]]); tris.Add(i); }
            var m = new Mesh { name = "Rift shard" };
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ emit helpers
        void Burst(ParticleSystem ps, Vector3 pos, Quaternion rot, int count, Color colour, float sizeMul = 1, float speedMul = 1, float coneAngle = -1)
        {
            if (ps == null) return;
            if (!ps.isPlaying) ps.Play();
            ps.transform.SetPositionAndRotation(pos, rot);
            if (coneAngle >= 0) { var sh = ps.shape; sh.angle = coneAngle; }
            var main = ps.main;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = true, startColor = colour };
            if (sizeMul != 1) p.startSize = Random.Range(main.startSize.constantMin, main.startSize.constantMax) * sizeMul;
            float oldMin = main.startSpeed.constantMin, oldMax = main.startSpeed.constantMax;
            if (speedMul != 1) main.startSpeed = new ParticleSystem.MinMaxCurve(oldMin * speedMul, oldMax * speedMul);
            ps.Emit(p, count);
            if (speedMul != 1) main.startSpeed = new ParticleSystem.MinMaxCurve(oldMin, oldMax);
        }

        void Flash(Vector3 pos, Color colour, float size)
        {
            if (flash == null) return;
            if (!flash.isPlaying) flash.Play();
            flash.transform.position = pos;
            flash.Emit(new ParticleSystem.EmitParams { position = pos, startSize = size, startColor = colour, applyShapeToPosition = false }, 1);
        }

        void Ring(Vector3 ground, Color colour, float diameter, float life = .55f)
        {
            if (ring == null) return;
            if (!ring.isPlaying) ring.Play();
            var main = ring.main; main.startLifetime = life;
            ring.Emit(new ParticleSystem.EmitParams { position = new Vector3(ground.x, Floor(ground) + .05f, ground.z), startSize = diameter, startColor = colour, applyShapeToPosition = false }, 1);
        }

        /// <summary>Height of the walkable floor under a point (the arena has a raised rift circle).</summary>
        float Floor(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, out var hit, 6f, ~(1 << 8), QueryTriggerInteraction.Ignore)) return hit.point.y;
            return brain != null ? brain.arenaCentre.y : p.y;
        }

        void LightFlash(Vector3 pos, Color colour, float intensity, float range, float seconds)
        {
            int best = 0; float bestEnd = float.MaxValue;
            for (int i = 0; i < lights.Count; i++) if (lightEnd[i] < bestEnd) { bestEnd = lightEnd[i]; best = i; }
            if (lights.Count == 0) return;
            var l = lights[best];
            l.transform.position = pos; l.color = colour; l.range = range; l.enabled = true;
            lightStart[best] = Time.time; lightEnd[best] = Time.time + seconds; lightPeak[best] = intensity;
        }

        // ------------------------------------------------------------------ public effect entry points (also used by previews)
        public void CannonFx(Vector3 from, Vector3 to)
        {
            Build();
            Vector3 dir = (to - from).sqrMagnitude > 1e-4f ? (to - from).normalized : brain.transform.forward;
            var rot = Quaternion.LookRotation(dir);
            Burst(sparks, from, rot, 26, violet, 1, 1, 22);
            Flash(from, violet, 2.3f);
            Burst(smoke, from, rot, 5, new Color(.45f, .3f, .6f, .6f), .7f, .6f, 25);
            LightFlash(from, violet, 22, 6, .12f);
            foreach (var b in bolts)
            {
                if (b.live) continue;
                b.from = from; b.to = to; b.t = 0; b.length = Vector3.Distance(from, to); b.live = true;
                b.go.transform.position = from; b.trail.Clear(); b.trail.emitting = true;
                break;
            }
        }

        public void ImpactFx(Vector3 at, Vector3 normal)
        {
            Build();
            var rot = Quaternion.LookRotation(normal.sqrMagnitude > 1e-4f ? normal : Vector3.up);
            Burst(sparks, at, rot, 30, Color.Lerp(violet, Color.white, .3f), 1, .8f, 60);
            Flash(at, violet, 3f);
            Burst(smoke, at, rot, 6, new Color(.35f, .25f, .45f, .55f), .8f, .5f, 50);
            LightFlash(at, violet, 16, 5, .18f);
        }

        public void PortalFx(Vector3 feet, bool arriving)
        {
            Build();
            Ring(feet, violet, arriving ? 4.2f : 3.2f, .6f);
            var sh = swirl.shape; sh.radius = arriving ? .6f : 1.6f;
            var vel = swirl.velocityOverLifetime; vel.radial = new ParticleSystem.MinMaxCurve(arriving ? 3.5f : -2.5f);
            Burst(swirl, feet + Vector3.up * .15f, Quaternion.identity, 70, Color.Lerp(violet, cyan, arriving ? .15f : .35f));
            Burst(column, feet, Quaternion.identity, 45, violet);
            Flash(feet + Vector3.up * 1.6f, violet, arriving ? 4.5f : 3.2f);
            LightFlash(feet + Vector3.up * 1.5f, violet, arriving ? 30 : 20, 8, .3f);
        }

        public void PortalWarningFx(Vector3 feet)
        {
            Build();
            Ring(feet, violet, 2.6f, .9f);
            var sh = swirl.shape; sh.radius = 1.8f;
            var vel = swirl.velocityOverLifetime; vel.radial = new ParticleSystem.MinMaxCurve(-2f);
            Burst(swirl, feet + Vector3.up * .1f, Quaternion.identity, 40, violet);
        }

        public void ShockwaveFx(Vector3 ground)
        {
            Build();
            Ring(ground, Color.Lerp(violet, Color.white, .25f), slamRadius * 3.4f, .55f);
            Ring(ground, cyan, slamRadius * 2.2f, .8f);
            var smokeShape = smoke.shape; var oldType = smokeShape.shapeType;
            smokeShape.shapeType = ParticleSystemShapeType.Circle; smokeShape.radius = slamRadius * .5f; smokeShape.rotation = new Vector3(90, 0, 0);
            Burst(smoke, ground + Vector3.up * .2f, Quaternion.identity, 26, new Color(.42f, .36f, .5f, .55f), 1.5f, 2.5f);
            smokeShape.shapeType = oldType; smokeShape.rotation = Vector3.zero; smokeShape.radius = .05f;
            Burst(shards, ground + Vector3.up * .2f, Quaternion.Euler(-90, 0, 0), 18, Color.white, 1, .9f, 40);
            Burst(sparks, ground + Vector3.up * .1f, Quaternion.Euler(-90, 0, 0), 40, ember, 1, 1, 70);
            Flash(ground + Vector3.up * .8f, Color.Lerp(violet, Color.white, .4f), 6);
            LightFlash(ground + Vector3.up * 1.2f, Color.Lerp(violet, Color.white, .3f), 45, 12, .3f);
        }

        public void ArmBreakFx(bool left)
        {
            Build();
            var target = left ? brain.leftTarget : brain.rightTarget;
            Vector3 at = target != null ? target.transform.position : brain.transform.position + Vector3.up * 2.4f;
            Vector3 outward = left ? -brain.transform.right : brain.transform.right;
            var rot = Quaternion.LookRotation(outward + Vector3.up * .4f);
            Burst(sparks, at, rot, 70, ember, 1.2f, 1, 55);
            Burst(sparks, at, rot, 30, violet, 1, .8f, 70);
            Burst(shards, at, rot, 26, Color.white, 1, .8f, 50);
            Burst(smoke, at, rot, 10, new Color(.3f, .27f, .35f, .6f), 1, .5f, 40);
            Flash(at, Color.Lerp(ember, Color.white, .3f), 3.2f);
            LightFlash(at, ember, 28, 7, .35f);
            if (left) leftSparksUntil = Time.time + 2.6f; else rightSparksUntil = Time.time + 2.6f;
        }

        public void CoreOverloadFx(float seconds)
        {
            Build();
            var em = charge.emission; em.rateOverTime = 160;
            charge.transform.position = CorePos();
            chargeUntil = Time.time + seconds;
        }
        float chargeUntil;

        public void DeathShatterFx(bool hideBoss)
        {
            Build();
            Vector3 core = CorePos();
            Vector3 ground = new Vector3(core.x, brain.arenaCentre.y, core.z);
            var up = Quaternion.Euler(-90, 0, 0);
            var sh = shards.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.1f;
            Burst(shards, core, up, 90, Color.white, 1.3f, 1.2f);
            sh.shapeType = ParticleSystemShapeType.Cone; sh.radius = .05f;
            var ss = sparks.shape; ss.shapeType = ParticleSystemShapeType.Sphere; ss.radius = .4f;
            Burst(sparks, core, up, 140, cyan, 1.3f, 1.5f);
            Burst(sparks, core, up, 80, violet, 1.2f, 1.2f);
            ss.shapeType = ParticleSystemShapeType.Cone;
            Burst(smoke, core, up, 20, new Color(.5f, .4f, .65f, .5f), 2, 1.5f, 80);
            Flash(core, Color.white, 9);
            Flash(core, cyan, 6);
            Ring(ground, cyan, 16, .8f);
            Ring(ground, violet, 10, 1.1f);
            LightFlash(core, new Color(.8f, .95f, 1f), 80, 18, .7f);
            var em = charge.emission; em.rateOverTime = 0; chargeUntil = 0;
            if (hideBoss) SetBossVisible(false);
        }

        void SetBossVisible(bool visible)
        {
            if (bossRenderers == null) return;
            foreach (var r in bossRenderers)
            {
                if (r == null) continue;
                if (visible)
                {
                    // the brain owns the arm renderers; leave them to it when restoring
                    if (r == brain.leftArm || r == brain.rightArm) continue;
                    r.enabled = true;
                }
                else r.enabled = false;
            }
            hidden = !visible;
        }

        /// <summary>Where the cannon visually fires from: the measured muzzle if known, else the brain's shot origin.</summary>
        public Vector3 Muzzle() => hasMuzzle && forearmL != null ? forearmL.TransformPoint(cannonMuzzleLocal)
                                   : brain.leftTarget != null ? brain.leftTarget.AimPoint : brain.transform.position + Vector3.up * 2;

        Vector3 CorePos() => coreBone != null ? coreBone.position : brain.transform.position + Vector3.up * 2.4f;

        // ------------------------------------------------------------------ update
        void Update()
        {
            if (!built || brain == null) return;
            float now = Time.time;
            var a = brain.Action;
            Vector3 bossPos = brain.transform.position;

            if (a != lastAction)
            {
                actionStart = now;
                if (a == RiftAction.DoubleTeleport || a == RiftAction.CoreTeleport) PortalWarningFx(bossPos);
                if (a == RiftAction.Dying) { deathStart = now; overloadDone = impactDone = false; }
                if (a == RiftAction.Dormant && hidden) SetBossVisible(true);
            }

            if (brain.ShotsFired > lastShots && brain.leftTarget != null)
            {
                Vector3 from = Muzzle();
                Vector3 aim = brain.player != null ? brain.player.position + Vector3.up * 1.2f : from + brain.transform.forward * 20;
                Vector3 dir = (aim - from).normalized, to = from + dir * 24;
                var hits = Physics.RaycastAll(from, dir, 24, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                foreach (var h in hits) if (!h.collider.transform.IsChildOf(brain.transform) && h.distance < best) { best = h.distance; to = h.point; }
                CannonFx(from, to);
            }

            if (a == RiftAction.Slam && now - actionStart < 38f / 30 && brain.player != null)
                slamLock = new Vector3(brain.player.position.x, Floor(brain.player.position), brain.player.position.z);
            if (brain.DamageWindows > lastWindows && a == RiftAction.Slam) ShockwaveFx(slamLock);

            if (brain.SuccessfulTeleports > lastTeleports)
            {
                PortalFx(lastBossPos, false);
                PortalFx(bossPos, true);
            }

            bool leftOn = brain.leftArm == null || brain.leftArm.enabled, rightOn = brain.rightArm == null || brain.rightArm.enabled;
            if (lastLeftOn && !leftOn && !hidden) ArmBreakFx(true);
            if (lastRightOn && !rightOn && !hidden) ArmBreakFx(false);

            if (a == RiftAction.Dying && deathStart >= 0)
            {
                float t = now - deathStart;
                if (!overloadDone && t >= 18f / 30) { overloadDone = true; CoreOverloadFx(41f / 30); }
                if (!impactDone && t >= 59f / 30) { impactDone = true; DeathShatterFx(true); }
            }

            // lingering shoulder sparks after an arm breaks
            if (now < leftSparksUntil && upperArmL != null && Random.value < .6f)
                Burst(sparks, upperArmL.position, Quaternion.LookRotation(-brain.transform.right + Vector3.down * .3f), 2, ember, .8f, .5f, 40);
            if (now < rightSparksUntil && upperArmR != null && Random.value < .6f)
                Burst(sparks, upperArmR.position, Quaternion.LookRotation(brain.transform.right + Vector3.down * .3f), 2, ember, .8f, .5f, 40);

            if (charge != null)
            {
                charge.transform.position = CorePos();
                if (now > chargeUntil) { var em = charge.emission; em.rateOverTime = 0; }
            }

            UpdateBolts(Time.deltaTime);
            UpdateLights(now);

            lastAction = a; lastShots = brain.ShotsFired; lastWindows = brain.DamageWindows; lastTeleports = brain.SuccessfulTeleports;
            lastLeftOn = leftOn; lastRightOn = rightOn; lastBossPos = bossPos;
        }

        void UpdateBolts(float dt)
        {
            foreach (var b in bolts)
            {
                if (!b.live) continue;
                b.t += dt * boltSpeed;
                float u = Mathf.Clamp01(b.length > 0 ? b.t / b.length : 1);
                b.go.transform.position = Vector3.Lerp(b.from, b.to, u);
                if (u >= 1)
                {
                    b.live = false; b.trail.emitting = false;
                    ImpactFx(b.to, (b.from - b.to).normalized);
                }
            }
        }

        void UpdateLights(float now)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (!l.enabled) continue;
                float span = Mathf.Max(.01f, lightEnd[i] - lightStart[i]);
                float k = 1 - Mathf.Clamp01((now - lightStart[i]) / span);
                l.intensity = lightPeak[i] * k * k;
                if (k <= 0) l.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (!built || brain == null || forearmR == null) return;
            // blade ribbon: sample after animation has posed the arm
            bool slashing = (brain.Action == RiftAction.Slash || brain.Action == RiftAction.DoubleTeleport)
                            && brain.rightArm != null && brain.rightArm.enabled && !hidden;
            float now = Time.time;
            samples.Add(new Sample { t = now, a = forearmR.TransformPoint(bladeBaseLocal), b = forearmR.TransformPoint(bladeTipLocal), on = slashing ? 1 : 0 });
            while (samples.Count > 0 && now - samples[0].t > trailSeconds) samples.RemoveAt(0);
            BuildRibbon(now);
        }

        void BuildRibbon(float now)
        {
            rv.Clear(); rc.Clear(); ru.Clear(); ri.Clear();
            bool any = false;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                float age = Mathf.Clamp01((now - s.t) / trailSeconds);
                float alpha = (1 - age) * (1 - age) * s.on;
                if (alpha > 0) any = true;
                var c = new Color(violet.r, violet.g, violet.b, alpha);
                rv.Add(s.a); rv.Add(s.b); rc.Add(c); rc.Add(c);
                ru.Add(new Vector2(age, 0)); ru.Add(new Vector2(age, 1));
                if (i > 0)
                {
                    // both windings, so the ribbon is visible from either side whatever the material's culling
                    int k = i * 2;
                    ri.Add(k - 2); ri.Add(k - 1); ri.Add(k); ri.Add(k); ri.Add(k - 1); ri.Add(k + 1);
                    ri.Add(k); ri.Add(k - 1); ri.Add(k - 2); ri.Add(k + 1); ri.Add(k - 1); ri.Add(k);
                }
            }
            ribbon.Clear();
            ribbonRenderer.enabled = any && samples.Count > 1;
            if (!ribbonRenderer.enabled) return;
            ribbon.SetVertices(rv); ribbon.SetColors(rc); ribbon.SetUVs(0, ru); ribbon.SetTriangles(ri, 0, true);
        }

        /// <summary>Diagnostics: live particle count per system.</summary>
        public string DebugCounts()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ps in new[] { sparks, flash, smoke, shards, ring, swirl, column, charge })
                if (ps != null) sb.Append($"{ps.name}={ps.particleCount} ");
            sb.Append($"ribbonVerts={(ribbon != null ? ribbon.vertexCount : 0)} ribbonOn={(ribbonRenderer != null && ribbonRenderer.enabled)}");
            if (ribbon != null && ribbon.vertexCount > 0) sb.Append($" ribbonCentre={ribbon.bounds.center} size={ribbon.bounds.size}");
            if (flash != null && flash.particleCount > 0)
            {
                var ps = new ParticleSystem.Particle[flash.particleCount]; flash.GetParticles(ps);
                foreach (var p in ps) sb.Append($" flash[pos={p.position} size={p.GetCurrentSize(flash):F2} col={p.GetCurrentColor(flash)} life={p.remainingLifetime:F3}/{p.startLifetime:F3}]");
                var r = flash.GetComponent<ParticleSystemRenderer>();
                sb.Append($" flashRenderer enabled={r.enabled} mat={(r.sharedMaterial ? r.sharedMaterial.name : "NULL")} mode={r.renderMode} maxSize={r.maxParticleSize}");
            }
            return sb.ToString();
        }

        /// <summary>Editor preview helper: clear all particles and flashes.</summary>
        public void ClearAll()
        {
            foreach (var ps in new[] { sparks, flash, smoke, shards, ring, swirl, column, charge })
                if (ps != null) ps.Clear(true);
            foreach (var l in lights) { l.enabled = false; l.intensity = 0; }
            samples.Clear(); if (ribbon != null) ribbon.Clear();
            if (hidden) SetBossVisible(true);
        }

        /// <summary>Editor preview helper: advance every effect system by the given time.</summary>
        public void SimulateAll(float seconds)
        {
            foreach (var ps in new[] { sparks, flash, smoke, shards, ring, swirl, column, charge })
                if (ps != null) ps.Simulate(seconds, true, false, false);
            UpdateLights(Time.time + 0f);
        }

        /// <summary>Editor preview helper: fake a blade sweep so the ribbon can be seen without Play mode.</summary>
        public void PreviewRibbon(Vector3[] bases, Vector3[] tips)
        {
            Build();
            samples.Clear();
            float now = Time.time;
            for (int i = 0; i < bases.Length; i++)
                samples.Add(new Sample { t = now - trailSeconds * (1 - (i + 1f) / bases.Length) * .9f, a = bases[i], b = tips[i], on = 1 });
            BuildRibbon(now);
        }

        void OnDisable()
        {
            if (hidden) SetBossVisible(true);
        }

        void OnDestroy()
        {
            if (fxRoot != null) Destroy(fxRoot);
            if (ribbon != null) Destroy(ribbon);
        }
    }
}
