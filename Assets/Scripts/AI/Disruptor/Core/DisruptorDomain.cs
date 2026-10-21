using System;
using System.Collections.Generic;

namespace Skybreak.Disruptor.Core
{
    public enum DisruptorActionId { MoveToStrike, CloseIn, ChargePulse, ReleasePulse, PhaseDash, Investigate, Patrol }
    public enum DisruptorGoal { Disrupt, Evade, Hunt, Guard }

    /// <summary>
    /// Situation numbers the action costs depend on. The runtime measures them (A* path lengths,
    /// the player's aim, the dodge belief) and the domain turns them into planner costs.
    /// Costs are in seconds of expected effort plus weighted risk, so they share one unit.
    /// </summary>
    public struct CostInputs
    {
        public double StrikePathLength;   // metres along the A* route to the best strike anchor (inf = none)
        public double StrikeExposure;     // 0..1: how much that anchor sits in the player's aim cone
        public double CloseInDistance;    // metres to walk straight at the player before reaching strike distance
        public double EscapePathLength;   // metres to the best escape anchor (inf = none)
        public double InvestigateLength;  // metres to the last known player position (inf = unknown)
        public double PatrolLength;       // metres to the next patrol point
        public double ChargeSeconds;      // current telegraph time (adapted by the dodge learner)
        public double DodgeProbability;   // posterior mean of P(player dodges a pulse)
        public double ApproachSpeed, DashSpeed, PatrolSpeed;
    }

    /// <summary>
    /// The Disruptor's planning domain: its actions (preconditions/effects), its goals as fact sets,
    /// and the cost model. Kept free of Unity types so it can be unit tested and explained on its own.
    /// </summary>
    public sealed class DisruptorDomain
    {
        // Risk weights (seconds-equivalent). Chosen so a flank that is 4 m longer than a frontal
        // approach still wins when the player has been dodging (see Docs/IS/RiftDisruptor/DESIGN.md).
        public const double StepOverhead = 0.5;   // smallest cost of any action: keeps the heuristic meaningful
        public const double ExposureWeight = 2.0;  // arriving inside the player's aim cone
        public const double FrontalPenalty = 1.5;  // walking straight at the player is readable
        public const double DodgeWeight = 2.5;     // expected wasted telegraph when the player dodges well

        readonly GoapAction[] actions;
        public IReadOnlyList<GoapAction> Actions => actions;

        public DisruptorDomain()
        {
            actions = new GoapAction[Enum.GetValues(typeof(DisruptorActionId)).Length];
            // Move along a threat-aware route to a flanking strike anchor. The anchor is chosen with a
            // line to the player's last known position, so the player is expected to be visible on arrival.
            Add(DisruptorActionId.MoveToStrike, "Move to strike anchor",
                FactSet.True(Fact.PlayerKnown | Fact.StrikeAnchorAvailable),
                FactSet.True(Fact.AtStrikePosition | Fact.PlayerVisible).AndFalse(Fact.Concealed));
            // Walk straight at a visible player. Cheaper in distance, but the player sees it coming.
            Add(DisruptorActionId.CloseIn, "Close in directly",
                FactSet.True(Fact.PlayerVisible),
                FactSet.True(Fact.AtStrikePosition).AndFalse(Fact.Concealed));
            // Stop, face the player and telegraph the pulse ring. Interruptible by enough damage.
            Add(DisruptorActionId.ChargePulse, "Charge pulse (telegraph)",
                FactSet.True(Fact.AtStrikePosition | Fact.PulseReady | Fact.PlayerVisible),
                FactSet.True(Fact.PulseCharged));
            // The release flash gives its position away, so a concealed Disruptor is no longer concealed:
            // this is what makes the planner append a relocation after every pulse.
            Add(DisruptorActionId.ReleasePulse, "Release pulse",
                FactSet.True(Fact.PulseCharged),
                FactSet.True(Fact.PlayerDisrupted).AndFalse(Fact.PulseCharged | Fact.PulseReady | Fact.Concealed));
            // Fast relocation along a route that avoids the player's aim, ending out of view.
            Add(DisruptorActionId.PhaseDash, "Phase-dash to escape anchor",
                FactSet.True(Fact.EscapeAnchorAvailable),
                FactSet.True(Fact.Concealed).AndFalse(Fact.AtStrikePosition | Fact.PlayerVisible));
            Add(DisruptorActionId.Investigate, "Investigate last known position",
                FactSet.True(Fact.PlayerKnown).AndFalse(Fact.PlayerVisible),
                FactSet.True(Fact.PlayerVisible));
            Add(DisruptorActionId.Patrol, "Patrol to next anchor",
                FactSet.Empty.AndFalse(Fact.PlayerVisible),
                FactSet.True(Fact.AreaSwept));
        }

        void Add(DisruptorActionId id, string name, FactSet pre, FactSet effect) =>
            actions[(int)id] = new GoapAction((int)id, name, pre, effect);

        public GoapAction this[DisruptorActionId id] => actions[(int)id];

        /// <summary>Goal fact sets. Disrupt is "hit and run": the pulse lands AND the Disruptor ends concealed.</summary>
        public static FactSet GoalFacts(DisruptorGoal goal, bool requireEscape = true)
        {
            switch (goal)
            {
                case DisruptorGoal.Disrupt:
                    return requireEscape ? FactSet.True(Fact.PlayerDisrupted | Fact.Concealed) : FactSet.True(Fact.PlayerDisrupted);
                case DisruptorGoal.Evade: return FactSet.True(Fact.Concealed);
                case DisruptorGoal.Hunt: return FactSet.True(Fact.PlayerVisible);
                default: return FactSet.True(Fact.AreaSwept);
            }
        }

        /// <summary>Rewrites every action cost for this planning cycle.</summary>
        public void ApplyCosts(in CostInputs c)
        {
            double Travel(double metres, double speed) =>
                double.IsInfinity(metres) || double.IsNaN(metres) || speed <= 0 ? double.PositiveInfinity : metres / speed;

            this[DisruptorActionId.MoveToStrike].Cost = StepOverhead + Travel(c.StrikePathLength, c.ApproachSpeed) + ExposureWeight * Clamp01(c.StrikeExposure);
            this[DisruptorActionId.CloseIn].Cost = StepOverhead + Travel(c.CloseInDistance, c.ApproachSpeed) + FrontalPenalty * (1 + Clamp01(c.DodgeProbability));
            this[DisruptorActionId.ChargePulse].Cost = StepOverhead + Math.Max(0, c.ChargeSeconds) + DodgeWeight * Clamp01(c.DodgeProbability);
            this[DisruptorActionId.ReleasePulse].Cost = StepOverhead;
            this[DisruptorActionId.PhaseDash].Cost = StepOverhead + Travel(c.EscapePathLength, c.DashSpeed);
            this[DisruptorActionId.Investigate].Cost = StepOverhead + Travel(c.InvestigateLength, c.ApproachSpeed);
            this[DisruptorActionId.Patrol].Cost = StepOverhead + Travel(c.PatrolLength, c.PatrolSpeed);
        }

        static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }
}
