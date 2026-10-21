using System;

namespace Skybreak.Disruptor.Core
{
    /// <summary>
    /// Learns how well this player dodges the pulse, as a Beta-Bernoulli belief over
    /// p = P(player leaves the ring before release).
    ///
    ///   prior        Beta(alpha0, beta0) = Beta(2, 2): mean 0.5, but only "worth" four pulses
    ///   update       dodged: alpha += 1, missed: beta += 1   (conjugate update)
    ///   forgetting   before each update both counts decay towards the prior by factor gamma,
    ///                so the effective memory is about 1 / (1 - gamma) = 10 pulses and the
    ///                Disruptor re-adapts when the player changes style.
    ///
    /// The posterior mean drives two bounded adaptations: a dodging player gets a shorter telegraph
    /// and a tighter strike distance (less room to step out). The bounds keep the attack fair: the
    /// ring is always visible for at least <see cref="MinCharge"/> seconds.
    /// </summary>
    public sealed class DodgeLearner
    {
        public const double PriorAlpha = 2, PriorBeta = 2, Forgetting = 0.9;
        public const double MaxCharge = 1.1, MinCharge = 0.7;           // seconds of telegraph
        public const double LooseStrike = 0.85, TightStrike = 0.6;      // strike distance as a fraction of pulse radius

        public double Alpha { get; private set; } = PriorAlpha;
        public double Beta { get; private set; } = PriorBeta;
        public int Observations { get; private set; }
        public int Dodges { get; private set; }

        public double Mean => Alpha / (Alpha + Beta);
        /// <summary>Posterior variance; shrinks as evidence accumulates (shown on the debug overlay).</summary>
        public double Variance { get { double s = Alpha + Beta; return Alpha * Beta / (s * s * (s + 1)); } }

        public void Record(bool dodged)
        {
            Alpha = PriorAlpha + Forgetting * (Alpha - PriorAlpha);
            Beta = PriorBeta + Forgetting * (Beta - PriorBeta);
            if (dodged) { Alpha += 1; Dodges++; } else Beta += 1;
            Observations++;
        }

        public void Reset() { Alpha = PriorAlpha; Beta = PriorBeta; Observations = Dodges = 0; }

        public double ChargeSeconds => Lerp(MaxCharge, MinCharge, Mean);
        public double StrikeFraction => Lerp(LooseStrike, TightStrike, Mean);

        static double Lerp(double a, double b, double t) => a + (b - a) * Math.Max(0, Math.Min(1, t));
    }
}
