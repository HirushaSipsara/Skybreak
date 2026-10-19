using System.Collections.Generic;
using Skybreak.AI.Algorithms.Fuzzy;

namespace Skybreak.AI.Relay
{
    // Layer 2 (Lecture 09, Fuzzy Inference System): turns crisp readings ("player at 0.48 health, threat 7.3 m
    // away") into how urgent each need is. "Low health" and "near" are vague, so a crisp threshold would make
    // Relay flip behaviour at one exact value; overlapping fuzzy sets change the urgency smoothly instead.
    public sealed class RelayFuzzyModel
    {
        public const string PlayerHealth = "PlayerHealth", ThreatDistance = "ThreatDistance", HealthTrend = "HealthTrend", RelayHealth = "RelayHealth";
        public const string HealUrgency = "HealUrgency", DefendUrgency = "DefendUrgency", SelfPreservation = "SelfPreservation";
        // Distance used for "no threat in view": the top of the ThreatDistance universe, fully Far.
        public const float NoThreatDistance = 20;

        public FuzzyInferenceSystem System { get; }
        public FuzzyResult Last { get; private set; }
        readonly Dictionary<string, double> crisp = new Dictionary<string, double>();

        public RelayFuzzyModel()
        {
            // Inputs (universe of discourse, then terms as trapezoid (a,b,c,d) / triangle (a,b,c))
            var playerHealth = new LinguisticVariable(PlayerHealth, 0, 1)
                .Term("Critical", new Trapezoid(0, 0, .15, .3))
                .Term("Low", new Triangle(.2, .45, .7))
                .Term("Healthy", new Trapezoid(.55, .8, 1, 1));
            var threatDistance = new LinguisticVariable(ThreatDistance, 0, 20)
                .Term("Near", new Trapezoid(0, 0, 4, 8))
                .Term("Medium", new Triangle(5, 10, 15))
                .Term("Far", new Trapezoid(12, 16, 20, 20));
            var healthTrend = new LinguisticVariable(HealthTrend, 0, .2)   // player health lost per second (fraction)
                .Term("Stable", new Trapezoid(0, 0, .01, .03))
                .Term("Dropping", new Triangle(.02, .05, .08))
                .Term("Plummeting", new Trapezoid(.06, .1, .2, .2));
            var relayHealth = new LinguisticVariable(RelayHealth, 0, 1)
                .Term("Weak", new Trapezoid(0, 0, .2, .4))
                .Term("Okay", new Trapezoid(.3, .6, 1, 1));

            // Outputs: three urgencies on [0, 1] sharing the same Low / Medium / High sets
            LinguisticVariable Urgency(string name) => new LinguisticVariable(name, 0, 1)
                .Term("Low", new Trapezoid(0, 0, .2, .4))
                .Term("Medium", new Triangle(.3, .5, .7))
                .Term("High", new Trapezoid(.6, .8, 1, 1));

            FuzzyCondition Is(string v, string t) => FuzzyCondition.Is(v, t);
            System = new FuzzyInferenceSystem()
                .Input(playerHealth).Input(threatDistance).Input(healthTrend).Input(relayHealth)
                .Output(Urgency(HealUrgency)).Output(Urgency(DefendUrgency)).Output(Urgency(SelfPreservation))
                // Healing: how badly does the player need health right now?
                .Rule("H1", Is(PlayerHealth, "Critical"), HealUrgency, "High")
                .Rule("H2", Is(PlayerHealth, "Low").And(Is(HealthTrend, "Dropping").Or(Is(HealthTrend, "Plummeting"))), HealUrgency, "High")
                .Rule("H3", Is(PlayerHealth, "Low"), HealUrgency, "Medium")
                .Rule("H4", Is(PlayerHealth, "Healthy"), HealUrgency, "Low")
                // Defending: how much does a threat endanger the player? Shooting matters most while the player can fight on.
                .Rule("D1", Is(ThreatDistance, "Near").And(Is(PlayerHealth, "Healthy")), DefendUrgency, "High")
                .Rule("D2", Is(ThreatDistance, "Near").And(Is(PlayerHealth, "Healthy").Not()), DefendUrgency, "Medium")
                .Rule("D3", Is(ThreatDistance, "Medium").And(Is(PlayerHealth, "Healthy")), DefendUrgency, "Medium")
                .Rule("D4", Is(ThreatDistance, "Medium").And(Is(PlayerHealth, "Healthy").Not()), DefendUrgency, "Low")
                .Rule("D5", Is(ThreatDistance, "Far"), DefendUrgency, "Low")
                .Rule("D6", Is(HealthTrend, "Plummeting").And(Is(ThreatDistance, "Far").Not()), DefendUrgency, "High")
                // Self-preservation: should Relay get itself out of danger?
                .Rule("S1", Is(RelayHealth, "Weak").And(Is(ThreatDistance, "Near")), SelfPreservation, "High")
                .Rule("S2", Is(RelayHealth, "Weak").And(Is(ThreatDistance, "Medium")), SelfPreservation, "Medium")
                .Rule("S3", Is(RelayHealth, "Weak").And(Is(ThreatDistance, "Far")), SelfPreservation, "Low")
                .Rule("S4", Is(RelayHealth, "Okay"), SelfPreservation, "Low");
        }

        public FuzzyResult Assess(float playerHealth01, float threatDistance, float healthTrend, float relayHealth01)
        {
            crisp[PlayerHealth] = playerHealth01; crisp[ThreatDistance] = threatDistance;
            crisp[HealthTrend] = healthTrend; crisp[RelayHealth] = relayHealth01;
            Last = System.Evaluate(crisp);
            return Last;
        }
    }
}
