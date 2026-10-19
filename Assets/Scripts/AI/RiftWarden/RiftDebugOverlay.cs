using System;
using System.Collections.Generic;
using System.Linq;
using Skybreak.AI.Algorithms.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Skybreak.AI.RiftWarden
{
    // Presentation only: shows the Rift Warden's reasoning live for the demo and the technical defence.
    // Press F10 in Play mode to toggle. Never writes to the brain.
    //   Screen (left): phase, mood and aim lead; the player model (velocity, range, strafe habit, learned answers per
    //                  attack); the attack learner's hit-chance beliefs; the planner's value per move and planned line.
    //   World lines:   player's smoothed velocity (yellow), predicted lead point (magenta), cut-off point (orange).
    public sealed class RiftDebugOverlay : MonoBehaviour
    {
        public RiftWardenBrain brain;
        public bool visible;
        public Key toggleKey = Key.F10;

        Mesh lines; Material material; GameObject drawer;
        GUIStyle panel, heading, text;
        float nextRebuild;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colours = new List<Color>();
        static readonly MotionResponse[] Responses = (MotionResponse[])Enum.GetValues(typeof(MotionResponse));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            foreach (var b in FindObjectsByType<RiftWardenBrain>(FindObjectsSortMode.None))
                if (b.GetComponent<RiftDebugOverlay>() == null) b.gameObject.AddComponent<RiftDebugOverlay>().brain = b;
        }

        void Awake() { if (brain == null) brain = GetComponent<RiftWardenBrain>(); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) visible = !visible;
            if (drawer != null) drawer.SetActive(visible);
            if (!visible || brain == null || brain.player == null) return;
            if (Time.unscaledTime >= nextRebuild) { nextRebuild = Time.unscaledTime + .1f; RebuildLines(); }
        }

        void OnDestroy() { if (drawer != null) Destroy(drawer); if (lines != null) Destroy(lines); if (material != null) Destroy(material); }

        void Line(Vector3 a, Vector3 b, Color c) { verts.Add(a); verts.Add(b); colours.Add(c); colours.Add(c); }
        void Ring(Vector3 p, float r, Color c)
        {
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI / 10, b = (i + 1) * Mathf.PI / 10;
                Line(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, p + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * r, c);
            }
        }

        void RebuildLines()
        {
            if (drawer == null)
            {
                drawer = new GameObject("Rift AI overlay lines") { hideFlags = HideFlags.DontSave };
                lines = new Mesh { name = "Rift AI overlay" };
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
                material.SetInt("_ZTest", (int)CompareFunction.Always); material.renderQueue = 4000;
                drawer.AddComponent<MeshFilter>().sharedMesh = lines;
                var r = drawer.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            verts.Clear(); colours.Clear();
            var m = brain.PlayerModel; Vector3 p = brain.player.position + Vector3.up * .1f;
            Line(p, p + m.Velocity, new Color(1, .9f, .2f, 1));
            var (x, z) = TargetPrediction.Lead(p.x, p.z, m.Velocity.x, m.Velocity.z, .77f, brain.leadFactor, RiftWardenBrain.MaxLead);
            var lead = new Vector3((float)x, p.y, (float)z);
            Line(p, lead, new Color(1, .2f, 1, .9f)); Ring(lead, .35f, new Color(1, .2f, 1, .9f));
            Vector3 to = brain.player.position - brain.transform.position; to.y = 0;
            if (to.sqrMagnitude > .01f)
            {
                var cut = brain.player.position + Vector3.Cross(Vector3.up, to.normalized) * m.StrafeBias * RiftWardenBrain.CutOffDistance + Vector3.up * .1f;
                Ring(cut, .5f, new Color(1, .55f, .1f, .9f));
            }
            lines.Clear();
            lines.SetVertices(verts); lines.SetColors(colours);
            lines.SetIndices(Enumerable.Range(0, verts.Count).ToArray(), MeshTopology.Lines, 0);
            lines.RecalculateBounds();
        }

        void OnGUI()
        {
            if (!visible || brain == null) return;
            if (panel == null)
            {
                panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
                text = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            }
            GUILayout.BeginArea(new Rect(12, 12, 460, Screen.height - 24), panel);
            GUILayout.Label($"RIFT WARDEN AI  ({toggleKey} to hide)", heading);
            GUILayout.Label($"<b>{brain.Action}</b>: {brain.Decision}", text);
            var d = brain.Director;
            GUILayout.Label($"Phase <b>{d.Phase}</b> · mood <b>{d.Mood}</b> · aim lead {brain.leadFactor:F2} · mercy windows {d.MercyWindows} · healer punished {brain.HealerPunishments}", text);

            var m = brain.PlayerModel;
            GUILayout.Label("Player model", heading);
            GUILayout.Label($"velocity {m.Velocity.magnitude:F1} m/s · preferred range {m.PreferredRange:F1} m · strafe bias {m.StrafeBias:+0.00;-0.00} ({(m.StrafeBias < -.2f ? "circles left" : m.StrafeBias > .2f ? "circles right" : "no habit")}) · answers seen {m.ResponsesObserved}", text);
            foreach (RiftAttackKind k in Enum.GetValues(typeof(RiftAttackKind)))
            {
                if (m.ResponseCount(k) < .5) continue;
                var parts = Responses.Select(r => (r, p: m.ResponseProbability(k, r))).OrderByDescending(t => t.p).Take(3).Select(t => $"{t.r} {t.p:P0}");
                GUILayout.Label($"{k} ({m.ResponseCount(k):F1} seen): {string.Join(", ", parts)}", text);
            }

            GUILayout.Label("Attack learner: P(hit) by range", heading);
            var l = brain.Learner;
            GUILayout.Label("              Close     Mid      Far", text);
            foreach (RiftAttackKind k in Enum.GetValues(typeof(RiftAttackKind)))
            {
                string Cell(RiftRangeBand b) { var be = l.Belief(k, b); return be.Observations > 0 ? $"<b>{be.Mean:F2}</b>({be.Observations})" : $"{be.Mean:F2}    "; }
                GUILayout.Label($"{k,-14} {Cell(RiftRangeBand.Close)}  {Cell(RiftRangeBand.Mid)}  {Cell(RiftRangeBand.Far)}", text);
            }

            GUILayout.Label("Expectimax plan", heading);
            var plan = brain.Planner.Last;
            if (plan != null)
            {
                foreach (var (move, value) in plan.RootValues.OrderByDescending(v => v.value))
                    GUILayout.Label((plan.HasAction && move.Equals(plan.Best) ? "<b>> " : "  ") + $"{move}: {(double.IsNegativeInfinity(value) ? "not available" : value.ToString("F1"))}" + (plan.HasAction && move.Equals(plan.Best) ? "</b>" : ""), text);
                GUILayout.Label($"line: {brain.Planner.PlannedLine} · depth {brain.Planner.depth} · {plan.NodesVisited} nodes · risk weight {brain.Planner.riskWeight:F1}", text);
            }
            GUILayout.EndArea();
        }
    }
}
