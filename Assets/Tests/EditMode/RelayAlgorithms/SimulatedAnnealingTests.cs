using System;
using NUnit.Framework;
using Skybreak.AI.Algorithms.Local;

namespace Skybreak.AI.Algorithms.Tests
{
    public class SimulatedAnnealingTests
    {
        // A 1-D landscape with a local maximum (height 1 at x = 2) and a higher global maximum (height 2 at x = 6.5),
        // separated by a flat valley. Starting at x = 1, a greedy climber reaches the small peak and stops there.
        sealed class TwoPeaks : ILocalSearchProblem<double>
        {
            public double Score(double x) => Math.Max(Peak(x, 2, 1, 1.8), Peak(x, 6.5, 2, 1.8));
            static double Peak(double x, double centre, double height, double width) => height * Math.Max(0, 1 - Math.Abs(x - centre) / width);
            public double Neighbour(double x, double t, Random rng)
            {
                double step = 0.4 + 1.6 * Math.Min(1, t);        // larger steps while hot (like Relay's position search)
                return Math.Max(0, Math.Min(10, x + (rng.NextDouble() * 2 - 1) * step));
            }
        }

        [Test]
        public void AcceptanceProbability_FollowsExpDeltaOverT()
        {
            Assert.AreEqual(1, SimulatedAnnealing.AcceptanceProbability(0.5, 1), 1e-12);          // better: always
            Assert.AreEqual(Math.Exp(-1), SimulatedAnnealing.AcceptanceProbability(-1, 1), 1e-12);  // ~0.368
            Assert.AreEqual(Math.Exp(-10), SimulatedAnnealing.AcceptanceProbability(-1, 0.1), 1e-12); // cold: almost never
            Assert.AreEqual(0, SimulatedAnnealing.AcceptanceProbability(-1, 0), 1e-12);
        }

        [Test]
        public void Temperature_CoolsGeometrically()
        {
            Assert.AreEqual(1.0, SimulatedAnnealing.Temperature(1, .85, 0), 1e-12);
            Assert.AreEqual(.85 * .85 * .85, SimulatedAnnealing.Temperature(1, .85, 3), 1e-12);
        }

        [Test]
        public void Annealing_FindsGlobalPeakMoreOftenThanHillClimbing()
        {
            var p = new TwoPeaks();
            int annealGlobal = 0, climbGlobal = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var a = SimulatedAnnealing.Run(p, 1.0, 200, 1.0, .98, new Random(seed));
                var h = HillClimbing.Run(p, 1.0, 200, new Random(seed));
                if (a.BestScore > 1.5) annealGlobal++;
                if (h.BestScore > 1.5) climbGlobal++;
            }
            TestContext.WriteLine($"Global peak found: annealing {annealGlobal}/100, hill climbing {climbGlobal}/100");
            Assert.Greater(annealGlobal, climbGlobal + 20);
        }

        [Test]
        public void Annealing_AcceptsSomeWorseMovesWhileHot_AndKeepsTheBestSeen()
        {
            var r = SimulatedAnnealing.Run(new TwoPeaks(), 1.0, 120, 1.0, .95, new Random(7), recordTrace: true);
            Assert.Greater(r.AcceptedWorse, 0, "a hot search must sometimes go downhill");
            Assert.GreaterOrEqual(r.BestScore, r.StartScore);
            Assert.AreEqual(120, r.Iterations);
            Assert.AreEqual(120, r.Trace.Count);
            Assert.AreEqual(r.Iterations, r.AcceptedBetter + r.AcceptedWorse + r.Rejected);
        }

        [Test]
        public void HillClimbing_NeverAcceptsWorse()
        {
            var r = HillClimbing.Run(new TwoPeaks(), 1.0, 200, new Random(3));
            Assert.AreEqual(0, r.AcceptedWorse);
            Assert.GreaterOrEqual(r.BestScore, r.StartScore);
        }
    }
}
