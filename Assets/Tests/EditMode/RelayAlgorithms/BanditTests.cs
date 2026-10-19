using System;
using System.Linq;
using NUnit.Framework;
using Skybreak.AI.Algorithms.Bandit;

namespace Skybreak.AI.Algorithms.Tests
{
    // The Rift Warden's attack learner (layer 3): Bayesian Beta beliefs and Thompson sampling.
    public class BanditTests
    {
        [Test]
        public void Beta_BayesUpdate_MeanAndVariance()
        {
            var b = new BetaBelief(2, 2);
            Assert.AreEqual(.5, b.Mean, 1e-12);
            b.Observe(true); b.Observe(true); b.Observe(false);            // 2 hits, 1 miss -> Beta(4, 3)
            Assert.AreEqual(4, b.Alpha, 1e-12); Assert.AreEqual(3, b.Beta, 1e-12);
            Assert.AreEqual(4.0 / 7, b.Mean, 1e-12);
            Assert.AreEqual(4.0 * 3 / (49 * 8), b.Variance, 1e-12);
        }

        [Test]
        public void Beta_MoreEvidence_MeansLessUncertainty()
        {
            var few = new BetaBelief(1, 1); var many = new BetaBelief(1, 1);
            few.Observe(true); few.Observe(false);
            for (int i = 0; i < 50; i++) { many.Observe(true); many.Observe(false); }
            Assert.AreEqual(few.Mean, many.Mean, 1e-12);                   // same hit rate...
            Assert.Less(many.Variance, few.Variance / 10);                 // ...far more certain
        }

        [Test]
        public void Beta_Forgetting_NeverDropsBelowThePrior()
        {
            var b = new BetaBelief(2, 2, .5);
            for (int i = 0; i < 30; i++) b.Observe(false);
            Assert.GreaterOrEqual(b.Alpha, 2);
            Assert.Less(b.Beta, 2 + 2.01);                                 // evidence decays to at most 1/(1-0.5) = 2
            b.Reset(); Assert.AreEqual(.5, b.Mean, 1e-12);
        }

        [Test]
        public void Beta_Samples_HaveTheRightMoments()
        {
            var b = new BetaBelief(3, 7); var rng = new Random(1);
            var draws = Enumerable.Range(0, 40000).Select(_ => b.Sample(rng)).ToArray();
            double mean = draws.Average(), variance = draws.Select(d => (d - mean) * (d - mean)).Average();
            Assert.AreEqual(.3, mean, .005);
            Assert.AreEqual(b.Variance, variance, .002);
            Assert.IsTrue(draws.All(d => d > 0 && d < 1));
        }

        [Test]
        public void Gamma_SmallShape_HasTheRightMean()
        {
            var rng = new Random(2);
            double mean = Enumerable.Range(0, 40000).Select(_ => BetaBelief.Gamma(.5, rng)).Average();
            Assert.AreEqual(.5, mean, .02);
        }

        [Test]
        public void Thompson_LearnsTheBestArm()
        {
            double[] p = { .2, .5, .8 }; var rng = new Random(3);
            var arms = p.Select(_ => new BetaBelief(1, 1)).ToArray();
            int bestPicks = 0;
            for (int t = 0; t < 1500; t++)
            {
                int a = ThompsonSampling.Choose(arms, null, rng, out _);
                arms[a].Observe(rng.NextDouble() < p[a]);
                if (t >= 1000 && a == 2) bestPicks++;
            }
            TestContext.WriteLine($"Best arm in the last 500 pulls: {bestPicks}; pulls per arm {string.Join(", ", arms.Select(x => x.Observations))}");
            Assert.Greater(bestPicks, 450);
            Assert.Greater(arms[0].Observations, 0, "every arm gets explored at least a little");
        }

        [Test]
        public void Thompson_WithForgetting_FollowsAChangeOfBestArm()
        {
            var rng = new Random(4);
            var arms = new[] { new BetaBelief(1, 1, .95), new BetaBelief(1, 1, .95) };
            double[] p = { .8, .2 };
            for (int t = 0; t < 400; t++) { int a = ThompsonSampling.Choose(arms, null, rng, out _); arms[a].Observe(rng.NextDouble() < p[a]); }
            p = new[] { .2, .8 };                                          // the player adapts: arm 1 now works
            int armOne = 0;
            for (int t = 0; t < 400; t++) { int a = ThompsonSampling.Choose(arms, null, rng, out _); arms[a].Observe(rng.NextDouble() < p[a]); if (t >= 200 && a == 1) armOne++; }
            Assert.Greater(armOne, 150);
        }

        [Test]
        public void Thompson_SkipsUnavailableArms_AndWeighsPayoffs()
        {
            var rng = new Random(5);
            var sure = new BetaBelief(200, 1);                             // almost certain hit
            int picks = 0;
            for (int i = 0; i < 200; i++)
                if (ThompsonSampling.Choose(new[] { null, sure, sure }, new[] { 99.0, 1, 3 }, rng, out _) == 2) picks++;
            Assert.AreEqual(200, picks, "unavailable arm 0 is never chosen; equal beliefs -> higher payoff wins");
        }
    }
}
