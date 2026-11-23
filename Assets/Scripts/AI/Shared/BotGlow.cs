using System.Collections.Generic;
using UnityEngine;

namespace Skybreak.AI
{
    /// <summary>
    /// Presentation only: makes a bot's own glowing parts (energy strips, crystals, eyes,
    /// chest gems) shine like the Rift Warden's, without adding any lights. Bloom does the rest.
    ///
    /// Only materials named as glow parts (crystal, energy, glow, gem, accent, eye, visor)
    /// are touched: already-emissive ones are boosted, the rest emit their own colour.
    /// Body armour and plates are left alone. Boosted copies are shared per source material
    /// and breathe slowly. Runtime material instances owned by other scripts (names ending
    /// in ")") are never replaced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotGlow : MonoBehaviour
    {
        public float emissionBoost = 2f, accentStrength = 1.3f, pulse = .12f, pulseSpeed = 1.6f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

        sealed class Glow { public Material material; public Color emission; public int frame = -1; }
        static readonly Dictionary<(Material, float, float), Glow> shared = new Dictionary<(Material, float, float), Glow>();
        static int users;

        readonly List<Glow> mine = new List<Glow>();
        float phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { shared.Clear(); users = 0; }

        void Start()
        {
            users++;
            phase = Random.value * 6;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var materials = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var glow = GlowFor(materials[i]);
                    if (glow == null) continue;
                    materials[i] = glow.material; changed = true;
                    if (!mine.Contains(glow)) mine.Add(glow);
                }
                if (changed) r.sharedMaterials = materials;
            }
        }

        Glow GlowFor(Material source)
        {
            if (source == null || !source.HasProperty(EmissionColor) || source.name.EndsWith(")")) return null;
            var key = (source, emissionBoost, accentStrength);
            if (shared.TryGetValue(key, out var existing)) return existing;

            // Only crystal / energy / gem style parts glow; armour, plates and joints never do.
            // A material with an emission map (e.g. the Vault Hound's single atlas material)
            // is also allowed: the map is a mask, so only its painted glow areas light up.
            bool masked = source.HasProperty(EmissionMap) && source.GetTexture(EmissionMap) != null;
            if (!masked && !IsGlowPart(source.name)) return null;
            Color emission = source.GetColor(EmissionColor);
            if (masked && emission.maxColorComponent <= .02f) emission = Color.white;
            bool emissive = source.IsKeywordEnabled("_EMISSION") && emission.maxColorComponent > .02f;
            Color target;
            if (emissive) target = emission * emissionBoost;
            else target = (emission.maxColorComponent > .02f ? emission : BaseOf(source)) * accentStrength;

            var copy = new Material(source) { name = source.name + " (bot glow)", hideFlags = HideFlags.DontSave };
            copy.EnableKeyword("_EMISSION");
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            copy.SetColor(EmissionColor, target);
            var glow = new Glow { material = copy, emission = target };
            shared[key] = glow;
            return glow;
        }

        static readonly string[] GlowWords = { "crystal", "energy", "glow", "gem", "accent", "eye", "visor", "emissi" };
        static bool IsGlowPart(string name)
        {
            name = name.ToLowerInvariant();
            foreach (var word in GlowWords) if (name.Contains(word)) return true;
            return false;
        }

        static Color BaseOf(Material m) => m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : m.color;

        void Update()
        {
            int frame = Time.frameCount;
            float breathe = 1 + pulse * Mathf.Sin(Time.time * pulseSpeed + phase);
            foreach (var glow in mine)
            {
                if (glow.frame == frame || glow.material == null) continue;
                glow.frame = frame;
                glow.material.SetColor(EmissionColor, glow.emission * breathe);
            }
        }

        void OnDestroy()
        {
            if (--users > 0) return;
            foreach (var glow in shared.Values) if (glow.material != null) Destroy(glow.material);
            shared.Clear(); users = 0;
        }
    }
}
