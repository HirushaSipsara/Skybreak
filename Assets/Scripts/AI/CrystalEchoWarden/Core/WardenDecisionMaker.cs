using System;

namespace Skybreak.Warden.Core
{
    /// <summary>
    /// Utility-based decision architecture for the Crystal Echo Warden.
    /// Implements Lecture 03 (Utility-based agents) with continuous multi-objective scoring:
    /// U(a) = 0.35 * (confidence) + 0.25 * (danger) + 0.20 * (combat) + 0.10 * (cover) - 0.10 * (travelCost).
    /// </summary>
    public sealed class WardenDecisionMaker
    {
        public Decision DecisionCore { get; }
        public DecisionWeights Weights => decisionWeights;
        readonly DecisionWeights decisionWeights;

        public Intent CurrentIntent => DecisionCore.Current;
        public string Reason => DecisionCore.Reason;
        public double ChangedAt => DecisionCore.ChangedAt;
        public double[] Scores => DecisionCore.Scores;

        public WardenDecisionMaker(DecisionWeights weights = null)
        {
            decisionWeights = weights ?? new DecisionWeights();
            DecisionCore = new Decision(decisionWeights);
        }

        /// <summary>
        /// Exact lecture utility formulation:
        /// U(a) = 0.35 * (player-location confidence) + 0.25 * (pedestal danger) + 0.20 * (combat advantage) + 0.10 * (cover) - 0.10 * (travel cost)
        /// </summary>
        public static double CalculateUtility(double confidence, double pedestalDanger, double combatAdvantage, double coverQuality, double travelCost)
        {
            confidence = Clamp01(confidence);
            pedestalDanger = Clamp01(pedestalDanger);
            combatAdvantage = Clamp01(combatAdvantage);
            coverQuality = Clamp01(coverQuality);
            travelCost = Clamp01(travelCost);

            return 0.35 * confidence + 0.25 * pedestalDanger + 0.20 * combatAdvantage + 0.10 * coverQuality - 0.10 * travelCost;
        }

        public Intent Decide(Situation situation, double now)
        {
            return DecisionCore.Choose(situation, now);
        }

        static double Clamp01(double v) => WorldGraph.Finite(v) ? Math.Max(0, Math.Min(1, v)) : 0;
    }
}
