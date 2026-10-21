using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI.SawanIS.VaultHound
{
    /// <summary>
    /// Drives the VaultHound Animator Controller from the brain's current state.
    ///
    /// Animator parameter contract (create these in the Animator Controller):
    ///
    ///   int   "State"     — matches HoundState enum int value
    ///   float "Speed"     — locomotion speed (0 = still, 1 = walk, 2 = run)
    ///   bool  "IsAlive"   — false triggers Death clip
    ///   bool  "Searching" — plays Sniff clip during Search state
    ///
    /// Clip mapping:
    ///   Idle          ← Chambered, stopped
    ///   Alert         ← Chambered (first frame) / deploying start
    ///   Walk          ← moving slowly (deploy speed)
    ///   Run           ← moving fast (pursue speed)
    ///   Sniff         ← Search state (BFS sweep)
    ///   Lunge         ← Attack state (lunge phase)
    ///   Hit           ← LungeRecovery (miss reaction)
    ///   Death         ← Dead state
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class VaultHoundAnimator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Brain on the same or parent GameObject.")]
        [SerializeField] VaultHoundBrain brain;

        [Tooltip("NavMeshAgent or CharacterController for speed sampling.")]
        [SerializeField] UnityEngine.AI.NavMeshAgent motor;

        [Tooltip("Locomotion speed that maps to Walk blend (meters per second).")]
        [SerializeField, Min(0.1f)] float walkThreshold = 2.0f;

        // ── Private ───────────────────────────────────────────────────────────

        Animator anim;
        SkinnedMeshRenderer[] skins;
        readonly List<SkinnedMeshRenderer> hiddenSkins = new List<SkinnedMeshRenderer>();
        readonly List<Mesh> fragmentMeshes = new List<Mesh>();
        readonly List<Transform> fragments = new List<Transform>();
        GameObject debris;
        Light burstLight;
        float defeatedAt = -1, shatteredAt;
        bool shattered;
        const float BreakupDelay = .4f, DebrisLifetime = 4f;

        // Cached parameter hashes for zero-string-lookup performance.
        static readonly int ParamState     = Animator.StringToHash("State");
        static readonly int ParamSpeed     = Animator.StringToHash("Speed");
        static readonly int ParamIsAlive   = Animator.StringToHash("IsAlive");
        static readonly int ParamSearching = Animator.StringToHash("Searching");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (brain == null) brain = GetComponentInParent<VaultHoundBrain>();
            if (motor == null) motor = GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        void LateUpdate()
        {
            if (brain == null) return;
            if (brain.CurrentState != HoundState.Dead)
            {
                if (defeatedAt >= 0) ResetBreakup();
                return;
            }
            if (defeatedAt < 0) defeatedAt = Time.time;
            if (!shattered && Time.time - defeatedAt >= BreakupDelay) BreakApart();
            if (debris == null) return;
            float elapsed = Time.time - shatteredAt;
            if (burstLight != null) burstLight.intensity = 6 * Mathf.Clamp01(1 - elapsed / .3f);
            float scale = Mathf.Clamp01((DebrisLifetime - elapsed) / .7f);
            foreach (var piece in fragments) if (piece != null) piece.localScale = Vector3.one * scale;
            if (elapsed >= DebrisLifetime) ClearDebris();
        }

        void BreakApart()
        {
            shattered = true; shatteredAt = Time.time;
            debris = new GameObject("Vault Hound broken pieces (runtime)") { hideFlags = HideFlags.DontSave };
            var actorColliders = new List<Collider>(brain.GetComponentsInChildren<Collider>(true));
            foreach (var actor in Skybreak.Combat.DamageSink.Active)
                if (actor != null) actorColliders.AddRange(actor.GetComponentsInChildren<Collider>(true));
            var pieceColliders = new List<Collider>();
            Vector3 origin = brain.transform.position + Vector3.up * .6f;
            foreach (var skin in skins)
            {
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                // The imported Hound has one material and rigid, single-bone vertex weights.
                var source = skin.sharedMesh;
                if (source == null || !source.isReadable)
                {
                    Debug.LogError("Vault Hound breakup requires Read/Write on its model importer.", this);
                    continue;
                }
                var baked = new Mesh(); skin.BakeMesh(baked, false);
                // BakeMesh(false) already includes this FBX rig's imported scale, as in WardenPresentation.
                var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                var vertices = baked.vertices; var normals = baked.normals; var uv = baked.uv;
                var weights = source.boneWeights; var triangles = baked.triangles;
                var groups = new Dictionary<int, List<int>>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int bone = weights[triangles[i]].boneIndex0;
                    if (!groups.TryGetValue(bone, out var indices)) groups[bone] = indices = new List<int>();
                    indices.Add(triangles[i]); indices.Add(triangles[i + 1]); indices.Add(triangles[i + 2]);
                }
                foreach (var group in groups)
                {
                    var remap = new Dictionary<int, int>();
                    var points = new List<Vector3>(); var directions = new List<Vector3>(); var uvs = new List<Vector2>();
                    var indices = new List<int>();
                    foreach (int index in group.Value)
                    {
                        if (!remap.TryGetValue(index, out int mapped))
                        {
                            mapped = points.Count; remap[index] = mapped;
                            points.Add(toWorld.MultiplyPoint3x4(vertices[index]));
                            directions.Add(skin.transform.TransformDirection(normals[index]));
                            uvs.Add(uv[index]);
                        }
                        indices.Add(mapped);
                    }
                    var bounds = new Bounds(points[0], Vector3.zero);
                    foreach (var point in points) bounds.Encapsulate(point);
                    for (int i = 0; i < points.Count; i++) points[i] -= bounds.center;
                    var mesh = new Mesh { name = "Vault Hound fragment mesh" };
                    mesh.SetVertices(points); mesh.SetNormals(directions); mesh.SetUVs(0, uvs);
                    mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); fragmentMeshes.Add(mesh);
                    var piece = new GameObject(skin.bones[group.Key].name + " fragment") { layer = 8 };
                    piece.transform.SetParent(debris.transform, false); piece.transform.position = bounds.center;
                    piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                    piece.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    var collider = piece.AddComponent<BoxCollider>();
                    collider.center = mesh.bounds.center;
                    collider.size = Vector3.Max(mesh.bounds.size * .85f, Vector3.one * .025f);
                    foreach (var actor in actorColliders) if (actor != null) Physics.IgnoreCollision(collider, actor);
                    foreach (var other in pieceColliders) Physics.IgnoreCollision(collider, other);
                    pieceColliders.Add(collider);
                    var body = piece.AddComponent<Rigidbody>();
                    body.mass = .15f; body.linearDamping = .45f; body.angularDamping = .6f;
                    Vector3 outward = bounds.center - origin; outward.y = 0;
                    if (outward.sqrMagnitude < .01f) outward = Random.insideUnitSphere;
                    body.AddForce(outward.normalized * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1.5f, 2.8f), ForceMode.VelocityChange);
                    body.AddTorque(Random.insideUnitSphere * 7, ForceMode.VelocityChange);
                    fragments.Add(piece.transform);
                }
                Destroy(baked); skin.enabled = false; hiddenSkins.Add(skin);
            }
            var flash = new GameObject("Amber core flash"); flash.transform.SetParent(debris.transform, false); flash.transform.position = origin;
            burstLight = flash.AddComponent<Light>(); burstLight.color = new Color(1, .55f, .12f);
            burstLight.range = 4; burstLight.intensity = 6; burstLight.shadows = LightShadows.None;
        }

        void ClearDebris()
        {
            if (debris != null) { debris.SetActive(false); Destroy(debris); debris = null; }
            foreach (var mesh in fragmentMeshes) if (mesh != null) Destroy(mesh);
            fragmentMeshes.Clear(); fragments.Clear(); burstLight = null;
        }

        void ResetBreakup()
        {
            ClearDebris();
            foreach (var skin in hiddenSkins) if (skin != null) skin.enabled = true;
            hiddenSkins.Clear(); defeatedAt = -1; shattered = false;
        }

        void OnDisable() { ResetBreakup(); }
        void OnDestroy() { ClearDebris(); }

        void Update()
        {
            if (brain == null || anim == null) return;

            HoundState state = brain.CurrentState;

            // IsAlive
            bool alive = state != HoundState.Dead;
            anim.SetBool(ParamIsAlive, alive);
            if (!alive) return;

            // State int
            anim.SetInteger(ParamState, (int)state);

            // Searching bool
            anim.SetBool(ParamSearching, state == HoundState.Search);

            // Speed float: 0 = idle, 1 = walk, 2 = run
            float velocity = motor != null ? motor.velocity.magnitude : 0f;
            float speedParam = 0f;
            if (velocity > 0.1f)
                speedParam = velocity < walkThreshold ? 1f : 2f;
            anim.SetFloat(ParamSpeed, speedParam);
        }
    }
}
