using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Skybreak.AI.Relay
{
    // Presentation only: shows Relay's reasoning live, layer by layer, for the demo and the technical defence.
    // Press F9 in Play mode to toggle. Never writes to RelayBrain.
    //   Screen panel: action and reason, threats remembered, fuzzy memberships / rule firing / urgencies,
    //                 expected utility of every action, A* statistics, simulated-annealing statistics.
    //   World lines:  A* explored nodes (blue) and route (cyan), annealing samples (green accepted / red rejected),
    //                 chosen spot (white ring), remembered threats (red, fading with confidence), player's line of fire (orange).
    public sealed class RelayDebugOverlay : MonoBehaviour
    {
        public RelayBrain brain;
        public bool visible;
        public Key toggleKey = Key.F9;

        Mesh lines; Material material; GameObject drawer;
        GUIStyle panel, heading, text;
        float nextRebuild;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colours = new List<Color>();

        // Attach to every Relay when a scene starts, so no scene or prefab has to be edited.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            foreach (var b in FindObjectsByType<RelayBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<RelayDebugOverlay>() == null) b.gameObject.AddComponent<RelayDebugOverlay>().brain = b;
        }

        void Awake() { if (brain == null) brain = GetComponent<RelayBrain>(); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) visible = !visible;
            if (drawer != null) drawer.SetActive(visible);
            if (!visible || brain == null) return;
            if (Time.unscaledTime >= nextRebuild) { nextRebuild = Time.unscaledTime + .1f; RebuildLines(); }
        }

        void OnDestroy() { if (drawer != null) Destroy(drawer); if (lines != null) Destroy(lines); if (material != null) Destroy(material); }

        // ---------------------------------------------------------------- world lines
        void EnsureDrawer()
        {
            if (drawer != null) return;
            drawer = new GameObject("Relay AI overlay lines") { hideFlags = HideFlags.DontSave };
            lines = new Mesh { name = "Relay AI overlay" };
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            if (material.HasProperty("_Surface")) { material.SetFloat("_Surface", 1); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); }
            material.SetInt("_ZTest", (int)CompareFunction.Always);   // visible through walls: it is a diagnostic
            material.renderQueue = 4000;
            drawer.AddComponent<MeshFilter>().sharedMesh = lines;
            var r = drawer.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Line(Vector3 a, Vector3 b, Color c) { verts.Add(a); verts.Add(b); colours.Add(c); colours.Add(c); }
        void Cross(Vector3 p, float s, Color c) { Line(p + new Vector3(-s, 0, 0), p + new Vector3(s, 0, 0), c); Line(p + new Vector3(0, 0, -s), p + new Vector3(0, 0, s), c); }
        void Ring(Vector3 p, float r, Color c)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                Line(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, p + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * r, c);
            }
        }

        void RebuildLines()
        {
            EnsureDrawer();
            verts.Clear(); colours.Clear();
            Vector3 up = Vector3.up * .08f;
            var plan = brain.LastPlan;
            if (plan != null)
            {
                foreach (var p in plan.explored) Cross(p + up, .12f, new Color(.3f, .5f, 1f, .45f));
                for (int i = 1; i < plan.waypoints.Count; i++) Line(plan.waypoints[i - 1] + up * 2, plan.waypoints[i] + up * 2, new Color(.2f, 1f, 1f, 1));
            }
            var search = brain.positions.Last;
            if (search != null)
                foreach (var (state, _, accepted) in search.Trace)
                    Cross(state + up * 3, .08f, accepted ? new Color(.3f, 1f, .4f, .9f) : new Color(1f, .3f, .3f, .6f));
            if (brain.TargetSpot.HasValue) Ring(brain.TargetSpot.Value + up * 3, .45f, Color.white);
            foreach (var t in brain.ThreatTracks)
            {
                float c = t.Confidence(Time.time); var at = t.Predicted(Time.time);
                Line(brain.AimPoint, at, new Color(1f, .25f, .2f, c)); Cross(at, .3f, new Color(1f, .25f, .2f, c));
            }
            if (brain.player != null)
            {
                Vector3 f = brain.player.forward; f.y = 0;
                if (f.sqrMagnitude > .01f)
                {
                    f.Normalize(); Vector3 side = new Vector3(f.z, 0, -f.x) * brain.positions.fireLaneRadius, o = brain.player.position + up + f * .5f;
                    Vector3 e = o + f * (brain.positions.fireLaneLength - .5f); var orange = new Color(1f, .6f, .1f, .8f);
                    Line(o + side, e + side, orange); Line(o - side, e - side, orange); Line(e + side, e - side, orange);
                }
            }
            lines.Clear();
            lines.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            lines.SetVertices(verts); lines.SetColors(colours);
            lines.SetIndices(Enumerable.Range(0, verts.Count).ToArray(), MeshTopology.Lines, 0);
            lines.RecalculateBounds();
        }

        // ---------------------------------------------------------------- screen panel
        void OnGUI()
        {
            if (!visible || brain == null) return;
            if (panel == null)
            {
                panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
                text = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = false };
            }
            float w = 430, x = Screen.width - w - 12;
            GUILayout.BeginArea(new Rect(x, 12, w, Screen.height - 24), panel);
            GUILayout.Label($"RELAY AI  ({toggleKey} to hide)", heading);
            GUILayout.Label($"<b>{brain.Action}</b>: {brain.Decision}", text);

            var p = brain.LastPercept;
            GUILayout.Label("Layer 1  World model", heading);
            GUILayout.Label($"player health {p.playerHealth01:P0}, losing {p.healthTrend * 100:F1}%/s, threat {(p.threat != null ? p.threatToPlayer.ToString("F1") + " m from player" : "none in view")}", text);
            var sb = new StringBuilder();
            foreach (var t in brain.ThreatTracks) sb.Append($"{t.sink.name} conf {t.Confidence(Time.time):F2}   ");
            GUILayout.Label(sb.Length > 0 ? "remembered: " + sb : "remembered: none", text);

            var fz = brain.LastAssessment;
            if (fz != null)
            {
                GUILayout.Label("Layer 2  Fuzzy inference (mu per term)", heading);
                foreach (var kv in fz.Fuzzified.All)
                    GUILayout.Label($"{kv.Key}: " + string.Join("  ", kv.Value.Select(t => t.Value > 0 ? $"<b>{t.Key} {t.Value:F2}</b>" : $"{t.Key} 0")), text);
                var firing = fz.Firing.Where(f => f.Value > 0).OrderByDescending(f => f.Value).Take(5).Select(f => $"{f.Key.Label} {f.Value:F2}");
                GUILayout.Label("rules firing: " + string.Join(", ", firing), text);
                GUILayout.Label($"COG  heal {brain.HealUrgency:F2}   defend {brain.DefendUrgency:F2}   self {brain.SelfPreservation:F2}", text);
            }

            GUILayout.Label("Layer 3  Expected utility", heading);
            Bar("Heal", brain.HealScore, brain.Action == RelayAction.Heal);
            Bar("Defend", brain.DefendScore, brain.Action == RelayAction.Defend);
            Bar("Retreat", brain.RetreatScore, brain.Action == RelayAction.Retreat);
            Bar("Reposition", brain.RepositionScore, brain.Action == RelayAction.Reposition);
            Bar("Follow", brain.FollowScore, brain.Action == RelayAction.Follow);

            GUILayout.Label("Layer 4  Tactical A*", heading);
            GUILayout.Label(brain.PlannerStatus, text);
            var plan = brain.LastPlan;
            if (plan != null) GUILayout.Label($"{plan.strategy}: found {plan.found}, expanded {plan.expanded}, cost {plan.cost:F1} = length {plan.length:F1} + {brain.planner.lambda:F0} x exposure {plan.exposure:F1}", text);

            GUILayout.Label("Layer 5  Simulated annealing", heading);
            var s = brain.positions.Last;
            if (s != null) GUILayout.Label($"goal {brain.positions.LastGoal}: {s.Iterations} steps, {s.AcceptedBetter} better, {s.AcceptedWorse} worse accepted, {s.Rejected} rejected; E {s.StartScore:F2} -> {s.BestScore:F2}", text);
            GUILayout.EndArea();
        }

        void Bar(string label, float value, bool chosen)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label((chosen ? "<b>> " : "  ") + label + (chosen ? "</b>" : ""), text, GUILayout.Width(95));
            var r = GUILayoutUtility.GetRect(220, 12);
            GUI.DrawTexture(new Rect(r.x, r.y + 2, 220, 8), Texture2D.grayTexture);
            GUI.color = chosen ? Color.cyan : Color.white;
            GUI.DrawTexture(new Rect(r.x, r.y + 2, 220 * Mathf.Clamp01(value), 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Label(value.ToString("F2"), text, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }
    }
}
