using System.Text;
using Skybreak.Disruptor.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skybreak.AI.Disruptor
{
    /// <summary>F8 toggles a live readout of the first Disruptor's reasoning: goals, plan, facts, beliefs.</summary>
    public sealed class DisruptorDebugOverlay : MonoBehaviour
    {
        public bool visible = true;
        public DisruptorBrain target;
        GUIStyle style;
        readonly StringBuilder text = new StringBuilder(1024);

        void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f8Key.wasPressedThisFrame) visible = !visible;
            if (target == null || !target.isActiveAndEnabled) target = DisruptorBrain.All.Count > 0 ? DisruptorBrain.All[0] : null;
        }

        void OnGUI()
        {
            if (!visible || target == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            var b = target; float now = Time.time;
            text.Clear();
            text.Append("<b>RIFT DISRUPTOR</b>  (F8)   health ").Append(b.Health.ToString("0")).Append('/').Append(b.MaximumHealth.ToString("0"))
                .Append("   think ").Append(b.LastThinkMilliseconds.ToString("0.00")).Append(" ms\n");
            text.Append("<b>Goal scores</b>  ");
            for (int i = 0; i < 4; i++)
            {
                var g = (DisruptorGoal)i;
                bool chosen = b.Goal == g;
                text.Append(chosen ? "<color=#ffcc33>" : "").Append(g).Append(' ').Append(b.GoalScores[i].ToString("0.00")).Append(chosen ? "</color>" : "").Append("   ");
            }
            text.Append("\n<b>Plan</b> (cost ").Append(b.Plan.Cost.ToString("0.0")).Append(", ").Append(b.Plan.Expanded).Append(" states expanded, replans ").Append(b.Replans).Append(")\n");
            for (int i = 0; i < b.Plan.Steps.Count; i++)
                text.Append(i == b.StepIndex ? "  <color=#66ffff>> " : "    ").Append(b.Plan.Steps[i].Name).Append("  [").Append(b.Plan.Steps[i].Cost.ToString("0.0")).Append(']').Append(i == b.StepIndex ? "</color>" : "").Append('\n');
            text.Append("<b>Decision</b> ").Append(b.Decision).Append('\n');
            text.Append("<b>Facts</b> ").Append(FactSet.Describe(b.Facts)).Append('\n');
            var s = b.Senses;
            text.Append("<b>Senses</b> visible ").Append(s.Visible).Append("  memory ").Append(s.Confidence(now, b.memoryTau).ToString("0.00"))
                .Append("  aim angle ").Append(s.AimAngle.ToString("0")).Append("°  aim pressure ").Append(s.AimPressure.ToString("0.00")).Append('\n');
            var l = b.Learner;
            text.Append("<b>Dodge belief</b> Beta(").Append(l.Alpha.ToString("0.0")).Append(", ").Append(l.Beta.ToString("0.0")).Append(")  P(dodge) ").Append(l.Mean.ToString("0.00"))
                .Append("  -> charge ").Append(b.ChargeSeconds.ToString("0.00")).Append(" s, strike ").Append(b.StrikeDistance.ToString("0.0")).Append(" m\n");
            text.Append("<b>Pulse</b> ").Append(b.Pulse.Phase).Append("  released ").Append(b.PulsesReleased).Append("  hit ").Append(b.PulsesHit).Append("  staggered ").Append(b.PulsesStaggered).Append('\n');
            var t = b.Tactics;
            text.Append("<b>Tactics</b> strike ").Append(t.Strike.Found ? t.Strike.Score.ToString("0.00") + " (" + t.Strike.Source + ")" : "none")
                .Append("  escape ").Append(t.Escape.Found ? t.Escape.Score.ToString("0.00") + " (" + t.Escape.Source + ")" : "none")
                .Append("  scored ").Append(t.CandidatesScored).Append(", A* routes ").Append(t.RoutesSearched).Append('\n');
            text.Append("<b>Motor</b> ").Append(b.Follower != null ? b.Follower.Report.ToString() : "none")
                .Append("  nav version ").Append(b.grid != null ? b.grid.Version : 0);
            GUI.Box(new Rect(Screen.width - 520, 12, 508, 330), "");
            GUI.Label(new Rect(Screen.width - 510, 18, 490, 320), text.ToString(), style);
        }
    }
}
