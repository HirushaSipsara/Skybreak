using UnityEngine;
using UnityEngine.Rendering;

namespace Skybreak.World
{
    // Map-wide atmosphere: blends per-zone moods (a Volume profile each) and the fog colour/density by where the
    // camera is. Zones are boxes with a soft blend distance, so walking between areas crossfades smoothly.
    // Uses global Volumes driven by weight - no trigger colliders, so gameplay raycasts are unaffected.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MapAtmosphere : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Zone
        {
            public string name = "Zone";
            public Vector3 centre;
            public Vector3 size = new Vector3(20, 10, 20);
            [Tooltip("Metres outside the box over which this zone fades out.")]
            public float blend = 6;
            public Volume volume;
            public Color fogColour = new Color(.08f, .1f, .12f);
            public float fogDensity = .02f;
            [HideInInspector] public float weight;
        }

        [Tooltip("What the zones follow. Empty = the main camera (in the editor: the Scene view camera when previewing).")]
        public Transform target;
        public bool fog = true;
        public Color defaultFogColour = new Color(.09f, .11f, .13f);
        public float defaultFogDensity = .014f;
        [Tooltip("Seconds to settle when moving between zones.")]
        public float smoothing = .6f;
        [Tooltip("Apply the moods while editing (follows the Scene view camera). Changes the scene's fog while on.")]
        public bool previewInEditor;
        public Zone[] zones = new Zone[0];

        Color fogColour; float fogDensity; bool initialised;

        void OnEnable() { initialised = false; }

        void LateUpdate()
        {
            bool playing = Application.isPlaying;
            if (!playing && !previewInEditor) return;
            Vector3 p = Position(playing);
            float dt = playing ? Time.unscaledDeltaTime : 1f;
            Apply(p, initialised ? 1 - Mathf.Exp(-dt / Mathf.Max(.01f, smoothing)) : 1);
            initialised = true;
        }

        Vector3 Position(bool playing)
        {
            if (target != null) return target.position;
#if UNITY_EDITOR
            if (!playing && UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
                return UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;
#endif
            var cam = Camera.main;
            return cam != null ? cam.transform.position : transform.position;
        }

        /// <summary>Weight of each zone at a point (1 inside, fading to 0 at the blend distance).</summary>
        public static float Weight(Zone z, Vector3 p)
        {
            Vector3 half = z.size * .5f, d = p - z.centre;
            Vector3 outside = new Vector3(Mathf.Max(0, Mathf.Abs(d.x) - half.x), Mathf.Max(0, Mathf.Abs(d.y) - half.y), Mathf.Max(0, Mathf.Abs(d.z) - half.z));
            return 1 - Mathf.Clamp01(outside.magnitude / Mathf.Max(.01f, z.blend));
        }

        /// <summary>Applies the moods for a viewpoint. lerp = 1 snaps immediately (also used by editor previews).</summary>
        public void Apply(Vector3 p, float lerp = 1)
        {
            // weighted average of the zones you are in/near; outside every zone -> the defaults
            float total = 0; Color sumColour = Color.clear; float sumDensity = 0;
            foreach (var z in zones)
            {
                float w = Weight(z, p);
                total += w; sumColour += z.fogColour * w; sumDensity += z.fogDensity * w;
                z.weight = Mathf.Lerp(z.weight, w, lerp);
                if (z.volume != null) z.volume.weight = z.weight;
            }
            float presence = Mathf.Clamp01(total);
            Color targetFog = total > 0 ? Color.Lerp(defaultFogColour, sumColour / total, presence) : defaultFogColour;
            float targetDensity = total > 0 ? Mathf.Lerp(defaultFogDensity, sumDensity / total, presence) : defaultFogDensity;
            if (!initialised || lerp >= 1) { fogColour = targetFog; fogDensity = targetDensity; }
            else { fogColour = Color.Lerp(fogColour, targetFog, lerp); fogDensity = Mathf.Lerp(fogDensity, targetDensity, lerp); }
            if (fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogColour;
                RenderSettings.fogDensity = fogDensity;
            }
        }

        void OnDrawGizmosSelected()
        {
            foreach (var z in zones)
            {
                Gizmos.color = new Color(z.fogColour.r * 3, z.fogColour.g * 3, z.fogColour.b * 3, .8f);
                Gizmos.DrawWireCube(z.centre, z.size);
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, .25f);
                Gizmos.DrawWireCube(z.centre, z.size + Vector3.one * z.blend * 2);
            }
        }
    }
}
