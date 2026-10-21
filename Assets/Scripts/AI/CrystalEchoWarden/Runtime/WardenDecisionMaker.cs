using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    /// <summary>
    /// Runtime adapter for the Warden's utility-based decision making.
    /// Evaluates competing action utilities (guard, patrol, investigate, pursue, intercept, flank, retreat, attack).
    /// Implements Lecture 03 (Utility-based agent architecture).
    /// </summary>
    public sealed class WardenDecisionMaker : MonoBehaviour
    {
        public DecisionWeights weights = new DecisionWeights();

        public Core.WardenDecisionMaker CoreDecisionMaker { get; private set; }

        public Intent CurrentIntent => CoreDecisionMaker?.CurrentIntent ?? Intent.Guard;
        public string Reason => CoreDecisionMaker?.Reason ?? "Idle";
        public double[] Scores => CoreDecisionMaker?.Scores;

        void Awake()
        {
            CoreDecisionMaker = new Core.WardenDecisionMaker(weights);
        }

        public void ResetForEncounter()
        {
            CoreDecisionMaker = new Core.WardenDecisionMaker(weights);
        }

        public Intent Decide(Situation situation)
        {
            if (CoreDecisionMaker == null) CoreDecisionMaker = new Core.WardenDecisionMaker(weights);
            return CoreDecisionMaker.Decide(situation, Time.time);
        }

        /// <summary>
        /// Exact lecture utility formulation:
        /// U(a) = 0.35 * (player-location confidence) + 0.25 * (pedestal danger) + 0.20 * (combat advantage) + 0.10 * (cover) - 0.10 * (travel cost)
        /// </summary>
        public static double ScoreUtility(double confidence, double pedestalDanger, double combatAdvantage, double coverQuality, double travelCost)
        {
            return Core.WardenDecisionMaker.CalculateUtility(confidence, pedestalDanger, combatAdvantage, coverQuality, travelCost);
        }
    }
}
