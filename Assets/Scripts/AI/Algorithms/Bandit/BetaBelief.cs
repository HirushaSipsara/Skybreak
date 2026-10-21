using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Bandit
{
    // Belief about an unknown success probability theta (e.g. "this attack hits this player"), as a Beta(alpha, beta)
    // distribution. Bayes' rule with a Bernoulli likelihood keeps it a Beta (a conjugate prior, Lecture 09):
    //   success: alpha <- alpha + 1        failure: beta <- beta + 1
    //   mean = alpha / (alpha + beta)      variance = alpha * beta / ((alpha + beta)^2 (alpha + beta + 1))
    // Forgetting (gamma < 1) shrinks the evidence towards the prior before each update, so beliefs track an opponent
    // who changes behaviour (a non-stationary bandit).
    public sealed class BetaBelief
    {
        public double PriorAlpha { get; }
        public double PriorBeta { get; }
        public double Forgetting { get; }
        public double Alpha { get; private set; }
        public double Beta { get; private set; }
        public int Observations { get; private set; }

        public BetaBelief(double priorAlpha = 1, double priorBeta = 1, double forgetting = 1)
        {
            if (priorAlpha <= 0 || priorBeta <= 0) throw new ArgumentException("Beta parameters must be positive");
            if (forgetting <= 0 || forgetting > 1) throw new ArgumentException("Forgetting must be in (0, 1]");
            PriorAlpha = priorAlpha; PriorBeta = priorBeta; Forgetting = forgetting;
            Alpha = priorAlpha; Beta = priorBeta;
        }

        public double Mean => Alpha / (Alpha + Beta);
        public double Variance { get { double s = Alpha + Beta; return Alpha * Beta / (s * s * (s + 1)); } }

        public void Observe(bool success)
        {
            // Decay only the evidence (the part above the prior), never below the prior itself.
            Alpha = PriorAlpha + (Alpha - PriorAlpha) * Forgetting;
            Beta = PriorBeta + (Beta - PriorBeta) * Forgetting;
            if (success) Alpha += 1; else Beta += 1;
            Observations++;
        }

        public void Reset() { Alpha = PriorAlpha; Beta = PriorBeta; Observations = 0; }

        // A random draw theta ~ Beta(alpha, beta), via two Gamma draws: X/(X+Y) with X ~ Gamma(alpha), Y ~ Gamma(beta).
        public double Sample(Random rng)
        {
            double x = Gamma(Alpha, rng), y = Gamma(Beta, rng);
            return x / (x + y);
        }

        // Marsaglia-Tsang (2000) Gamma(shape, 1) sampler; shapes below 1 use the boost Gamma(a) = Gamma(a+1) * U^(1/a).
        public static double Gamma(double shape, Random rng)
        {
            if (shape < 1) return Gamma(shape + 1, rng) * Math.Pow(rng.NextDouble(), 1 / shape);
            double d = shape - 1.0 / 3, c = 1 / Math.Sqrt(9 * d);
            while (true)
            {
                double x, v;
                do { x = Normal(rng); v = 1 + c * x; } while (v <= 0);
                v = v * v * v;
                double u = rng.NextDouble();
                if (u < 1 - .0331 * x * x * x * x) return d * v;
                if (Math.Log(u) < .5 * x * x + d * (1 - v + Math.Log(v))) return d * v;
            }
        }

        static double Normal(Random rng)   // Box-Muller
        {
            double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }

    // Thompson sampling for a multi-armed bandit: draw one plausible theta from each arm's belief and play the arm whose
    // draw (times its payoff) is largest. Arms we are unsure about sometimes draw high and get tried (exploration);
    // arms that have proven good usually win (exploitation). This balances the trade-off from Lecture 03's learning agents.
    public static class ThompsonSampling
    {
        public static int Choose(IReadOnlyList<BetaBelief> arms, IReadOnlyList<double> payoffs, Random rng, out double bestValue)
        {
            int best = -1; bestValue = double.NegativeInfinity;
            for (int i = 0; i < arms.Count; i++)
            {
                if (arms[i] == null) continue;   // unavailable arm
                double value = arms[i].Sample(rng) * (payoffs != null ? payoffs[i] : 1);
                if (value > bestValue) { bestValue = value; best = i; }
            }
            return best;
        }
    }
}
