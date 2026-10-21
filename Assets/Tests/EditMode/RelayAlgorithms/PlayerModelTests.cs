using System;
using NUnit.Framework;
using Skybreak.AI.Algorithms.Prediction;
using Skybreak.AI.Algorithms.Probability;

namespace Skybreak.AI.Algorithms.Tests
{
    // Building blocks of the Rift Warden's player model (layer 1) and predictive aim (layer 2).
    public class PlayerModelTests
    {
        [Test]
        public void Dirichlet_NoData_IsUniform_ThenFollowsCounts()
        {
            var d = new DirichletCategorical(6, prior: 1);
            for (int k = 0; k < 6; k++) Assert.AreEqual(1.0 / 6, d.Probability(k), 1e-12);
            d.Observe(1); d.Observe(1); d.Observe(1); d.Observe(4);
            Assert.AreEqual((1 + 3) / (6.0 + 4), d.Probability(1), 1e-12);   // (a0 + n_k) / (K a0 + N)
            Assert.AreEqual((1 + 1) / (6.0 + 4), d.Probability(4), 1e-12);
            Assert.AreEqual(1 / 10.0, d.Probability(0), 1e-12);
            double sum = 0; for (int k = 0; k < 6; k++) sum += d.Probability(k);
            Assert.AreEqual(1, sum, 1e-12);
            Assert.AreEqual(1, d.MostLikely());
        }

        [Test]
        public void Dirichlet_Forgetting_LetsRecentHabitsTakeOver()
        {
            var plain = new DirichletCategorical(2, 1, 1);
            var fading = new DirichletCategorical(2, 1, .8);
            for (int i = 0; i < 20; i++) { plain.Observe(0); fading.Observe(0); }    // player used to dodge left...
            for (int i = 0; i < 6; i++) { plain.Observe(1); fading.Observe(1); }     // ...now dodges right
            Assert.Less(plain.Probability(1), .5, "without forgetting old habits dominate");
            Assert.Greater(fading.Probability(1), .5, "with forgetting the new habit wins");
        }

        [Test]
        public void Lead_PredictsAlongVelocity_ScaledAndCapped()
        {
            var (x, z) = TargetPrediction.Lead(10, 5, 4, 0, .5, 1, 99);
            Assert.AreEqual(12, x, 1e-12); Assert.AreEqual(5, z, 1e-12);          // 4 m/s for 0.5 s
            (x, z) = TargetPrediction.Lead(10, 5, 4, 0, .5, .5, 99);
            Assert.AreEqual(11, x, 1e-12);                                        // half lead
            (x, z) = TargetPrediction.Lead(0, 0, 6, 8, 1, 1, 2.5);
            Assert.AreEqual(2.5, Math.Sqrt(x * x + z * z), 1e-9);                 // capped length, same direction
            Assert.AreEqual(.75, x / 2, 1e-9);
            (x, z) = TargetPrediction.Lead(3, 4, 0, 0, 1, 1, 2.5);
            Assert.AreEqual(3, x, 1e-12); Assert.AreEqual(4, z, 1e-12);           // a still target is aimed at directly
        }

        [Test]
        public void SmoothingAlpha_IsFrameRateIndependent()
        {
            double a1 = TargetPrediction.SmoothingAlpha(.1, .25);
            double a2 = TargetPrediction.SmoothingAlpha(.05, .25);
            Assert.AreEqual(1 - a1, (1 - a2) * (1 - a2), 1e-12);                  // two half steps = one full step
        }

        [Test]
        public void Classify_ResponseFromDisplacement()
        {
            Assert.AreEqual(MotionResponse.Jump, TargetPrediction.Classify(0, 0, 1.0));
            Assert.AreEqual(MotionResponse.Stay, TargetPrediction.Classify(.3, .4, 0));
            Assert.AreEqual(MotionResponse.BackOff, TargetPrediction.Classify(3, 1, 0));
            Assert.AreEqual(MotionResponse.CloseIn, TargetPrediction.Classify(-2, .5, 0));
            Assert.AreEqual(MotionResponse.StrafeRight, TargetPrediction.Classify(.5, 2, 0));
            Assert.AreEqual(MotionResponse.StrafeLeft, TargetPrediction.Classify(.5, -2, 0));
        }
    }
}
