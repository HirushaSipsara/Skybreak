using System;

namespace Skybreak.Disruptor.Core
{
    /// <summary>What the goal layer reads each decision cycle.</summary>
    public struct GoalContext
    {
        public bool PlayerVisible;
        public double MemoryConfidence;   // 0..1, decays after sight is lost
        public double AimPressure;        // 0..1, how long and how directly the player has been aiming at it
        public double Health01;
        public bool PulseReady;
        public double PulseCooldown01;    // 0 = ready, 1 = just fired
        public bool PlayerAlive;
    }

    /// <summary>
    /// Utility-based goal arbitration: every goal gets an insistence in [0,1], the highest wins,
    /// and the goal being pursued gets a small <see cref="Hysteresis"/> bonus so near-ties do not make
    /// the agent flip between plans every cycle. GOAP then decides HOW to achieve the chosen goal.
    /// </summary>
    public static class GoalSelector
    {
        public const double Hysteresis = 0.1;
        public const double GuardBaseline = 0.2;

        public static void Score(in GoalContext c, double[] scores)
        {
            if (scores == null || scores.Length < 4) throw new ArgumentException("needs one slot per goal");
            bool known = c.PlayerVisible || c.MemoryConfidence > 0.05;
            double health = Clamp01(c.Health01);

            // Disrupt: wants a visible, living player and a ready pulse. Being aimed at suppresses it,
            // because charging in the crosshair invites the stagger. A player it only remembers is
            // worth a little (it may strike on arrival), but finding them first (Hunt) ranks higher.
            double disrupt = 0;
            if (c.PlayerAlive && known && c.PulseReady)
                disrupt = (c.PlayerVisible ? 0.9 : 0.25 * Clamp01(c.MemoryConfidence)) * (1 - 0.6 * Clamp01(c.AimPressure));

            // Evade: aim pressure, more urgent when hurt; and while the pulse recharges, standing in
            // the open has no purpose, so it hides between strikes (the "run" in hit-and-run).
            double evade = 0;
            if (c.PlayerAlive && known)
            {
                double pressure = Clamp01(c.AimPressure) * (0.75 + 0.25 * (1 - health));
                double recharging = !c.PulseReady && c.PlayerVisible ? 0.45 + 0.2 * Clamp01(c.PulseCooldown01) : 0;
                evade = Math.Max(pressure, recharging);
            }

            // Hunt: lost sight but still remembers where the player was.
            double hunt = c.PlayerAlive && !c.PlayerVisible && c.MemoryConfidence > 0.05 ? 0.3 + 0.4 * Clamp01(c.MemoryConfidence) : 0;

            scores[(int)DisruptorGoal.Disrupt] = disrupt;
            scores[(int)DisruptorGoal.Evade] = evade;
            scores[(int)DisruptorGoal.Hunt] = hunt;
            scores[(int)DisruptorGoal.Guard] = GuardBaseline;
        }

        /// <summary>Goals ordered best first, with hysteresis applied to <paramref name="current"/>.
        /// The runtime tries them in order and keeps the first one the planner can achieve.</summary>
        public static DisruptorGoal[] Rank(double[] scores, DisruptorGoal? current)
        {
            var order = new[] { DisruptorGoal.Disrupt, DisruptorGoal.Evade, DisruptorGoal.Hunt, DisruptorGoal.Guard };
            double Effective(DisruptorGoal g) => scores[(int)g] + (current == g ? Hysteresis : 0);
            Array.Sort(order, (a, b) =>
            {
                int byScore = Effective(b).CompareTo(Effective(a));
                return byScore != 0 ? byScore : ((int)a).CompareTo((int)b);
            });
            return order;
        }

        static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }
}
