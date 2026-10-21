using UnityEngine;
using UnityEngine.Rendering;

namespace Skybreak.AI.Disruptor
{
    /// <summary>
    /// Readable pulse telegraph: a ground ring that grows to the pulse radius while charging and flashes
    /// on release. Read-only view of <see cref="DisruptorBrain"/>; removing it leaves the AI unchanged.
    /// Also builds a temporary primitive body when no model is assigned.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class DisruptorTelegraph : MonoBehaviour
    {
        public DisruptorBrain brain;
        public LineRenderer ring;
        public Renderer core;
        public bool buildPlaceholderBody = true;
        public Color chargeColour = new Color(1f, .25f, .85f), idleColour = new Color(.2f, .9f, 1f), releaseColour = Color.white;

        const int Segments = 48;
        Material ringMaterial, coreMaterial, bodyMaterial;
        float flashUntil;

        void Awake()
        {
            if (brain == null) brain = GetComponent<DisruptorBrain>();
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (ring == null)
            {
                var go = new GameObject("Pulse telegraph ring"); go.transform.SetParent(transform, false); go.layer = 8;
                ring = go.AddComponent<LineRenderer>();
                ring.loop = true; ring.positionCount = Segments; ring.useWorldSpace = true; ring.widthMultiplier = .08f;
                ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
            }
            ringMaterial = new Material(unlit); ring.sharedMaterial = ringMaterial; ring.enabled = false;
            if (core == null && buildPlaceholderBody) BuildPlaceholder(unlit);
        }

        void BuildPlaceholder(Shader unlit)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            bodyMaterial = new Material(lit) { color = new Color(.12f, .14f, .2f) };
            bodyMaterial.SetColor("_BaseColor", new Color(.12f, .14f, .2f));
            coreMaterial = new Material(unlit);
            var shell = Primitive(PrimitiveType.Sphere, "Placeholder shell", new Vector3(0, 1.3f, 0), new Vector3(.9f, .7f, .9f), bodyMaterial);
            var c = Primitive(PrimitiveType.Sphere, "Placeholder core", new Vector3(0, 1.3f, .32f), Vector3.one * .38f, coreMaterial);
            Primitive(PrimitiveType.Cylinder, "Placeholder emitter", new Vector3(0, .75f, 0), new Vector3(.5f, .05f, .5f), bodyMaterial);
            core = c.GetComponent<Renderer>();
            shell.name = "Placeholder shell (replace with the Disruptor model)";
        }

        GameObject Primitive(PrimitiveType type, string name, Vector3 local, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 8;
            go.transform.SetParent(transform, false); go.transform.localPosition = local; go.transform.localScale = scale;
            var col = go.GetComponent<Collider>(); col.enabled = false; Destroy(col);   // the CharacterController is the hitbox
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        void OnEnable() { if (brain != null) brain.PulseReleased += OnReleased; }
        void OnDisable() { if (brain != null) brain.PulseReleased -= OnReleased; }
        void OnReleased(DisruptorBrain b) { flashUntil = Time.time + .18f; }

        void LateUpdate()
        {
            if (brain == null) return;
            float now = Time.time;
            bool flashing = now < flashUntil;
            bool charging = brain.Pulse.Busy && brain.Alive;
            Color colour = flashing ? releaseColour : charging ? Color.Lerp(idleColour, chargeColour, brain.Pulse.Progress(now)) : idleColour;
            if (!brain.Alive) colour = Color.gray;
            if (coreMaterial != null) { coreMaterial.SetColor("_BaseColor", colour); coreMaterial.color = colour; }

            ring.enabled = charging || flashing;
            if (!ring.enabled) return;
            float radius = flashing ? brain.pulseRadius : Mathf.Lerp(.4f, brain.pulseRadius, brain.Pulse.Progress(now));
            ringMaterial.SetColor("_BaseColor", colour); ringMaterial.color = colour;
            ring.widthMultiplier = flashing ? .25f : .06f + .08f * brain.Pulse.Progress(now);
            Vector3 c = transform.position + Vector3.up * .06f;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2 / Segments;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
        }

        void OnDestroy()
        {
            if (ringMaterial != null) Destroy(ringMaterial);
            if (coreMaterial != null) Destroy(coreMaterial);
            if (bodyMaterial != null) Destroy(bodyMaterial);
        }
    }
}
