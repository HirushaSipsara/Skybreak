using System;
using NUnit.Framework;
using Skybreak.Disruptor.Core;

namespace Skybreak.Disruptor.Tests
{
    public sealed class TacticsAndLearningTests
    {
        static AnchorFeatures Spot(double distance, double aimAngle, bool line = true, double path = 5) => new AnchorFeatures
        {
            DistanceToPlayer = distance, AimAngle = aimAngle, LineToPlayer = line, PathLength = path, SecondsSinceUsed = double.PositiveInfinity,
        };

        [Test]
        public void StrikePrefersAFlankOverTheCrosshair()
        {
            double front = AnchorScoring.Strike(Spot(3.5, 5), 3.5);
            double flank = AnchorScoring.Strike(Spot(3.5, 100), 3.5);
            Assert.Greater(flank, front);
        }

        [Test]
        public void StrikeRejectsSpotsOutOfRangeOrWithoutALine()
        {
            Assert.IsTrue(double.IsNegativeInfinity(AnchorScoring.Strike(Spot(3.5 + AnchorScoring.RangeTolerance + 0.1, 90), 3.5)));
            Assert.IsTrue(double.IsNegativeInfinity(AnchorScoring.Strike(Spot(3.5, 90, line: false), 3.5)));
            var claimed = Spot(3.5, 90); claimed.ClaimedByOther = true;
            Assert.IsTrue(double.IsNegativeInfinity(AnchorScoring.Strike(claimed, 3.5)));
        }

        [Test]
        public void RecentlyUsedSpotsAreLessAttractive()
        {
            var fresh = Spot(3.5, 90); var stale = Spot(3.5, 90); stale.SecondsSinceUsed = 1;
            Assert.Greater(AnchorScoring.Strike(fresh, 3.5), AnchorScoring.Strike(stale, 3.5));
        }

        [Test]
        public void EscapePrefersBrokenLineOfSight()
        {
            Assert.Greater(AnchorScoring.Escape(Spot(10, 30, line: false), 8), AnchorScoring.Escape(Spot(10, 30, line: true), 8));
            Assert.IsTrue(double.IsNegativeInfinity(AnchorScoring.Escape(Spot(10, 30, path: double.PositiveInfinity), 8)));
        }

        [Test]
        public void DodgeBeliefStartsAtTheUninformedPrior()
        {
            var l = new DodgeLearner();
            Assert.AreEqual(0.5, l.Mean, 1e-9);
            Assert.AreEqual((DodgeLearner.MaxCharge + DodgeLearner.MinCharge) / 2, l.ChargeSeconds, 1e-9);
        }

        [Test]
        public void RepeatedDodgesShortenTheTelegraphWithinTheFairnessBound()
        {
            var l = new DodgeLearner();
            for (int i = 0; i < 50; i++) l.Record(true);
            Assert.Greater(l.Mean, 0.85);
            Assert.GreaterOrEqual(l.ChargeSeconds, DodgeLearner.MinCharge);
            Assert.Less(l.ChargeSeconds, 0.8);
            Assert.Less(l.StrikeFraction, 0.65);
        }

        [Test]
        public void ForgettingLetsTheBeliefRecoverWhenThePlayerChangesStyle()
        {
            var l = new DodgeLearner();
            for (int i = 0; i < 30; i++) l.Record(true);
            for (int i = 0; i < 15; i++) l.Record(false);
            Assert.Less(l.Mean, 0.4, "the last 15 misses dominate; without forgetting the mean would be 0.65");
            // Steady state with gamma = 0.9: counts stay bounded near prior + 1/(1-gamma) = 12.
            Assert.Less(l.Alpha + l.Beta, 2 * DodgeLearner.PriorAlpha + 1 / (1 - DodgeLearner.Forgetting) + 1e-6);
        }

        [Test]
        public void GoalSelectorHidesUnderAimPressureAndHuntsFromMemory()
        {
            var scores = new double[4];
            GoalSelector.Score(new GoalContext { PlayerVisible = true, PulseReady = true, AimPressure = 0, Health01 = 1, PlayerAlive = true }, scores);
            Assert.AreEqual(DisruptorGoal.Disrupt, GoalSelector.Rank(scores, null)[0]);

            GoalSelector.Score(new GoalContext { PlayerVisible = true, PulseReady = true, AimPressure = 1, Health01 = 1, PlayerAlive = true }, scores);
            Assert.AreEqual(DisruptorGoal.Evade, GoalSelector.Rank(scores, null)[0]);

            GoalSelector.Score(new GoalContext { PlayerVisible = false, MemoryConfidence = 0.8, PulseReady = true, Health01 = 1, PlayerAlive = true }, scores);
            Assert.AreEqual(DisruptorGoal.Hunt, GoalSelector.Rank(scores, null)[0], "an unseen player is found before being struck");

            GoalSelector.Score(new GoalContext { PlayerVisible = false, MemoryConfidence = 0, PlayerAlive = true, Health01 = 1 }, scores);
            Assert.AreEqual(DisruptorGoal.Guard, GoalSelector.Rank(scores, null)[0]);

            GoalSelector.Score(new GoalContext { PlayerVisible = true, PulseReady = true, PlayerAlive = false, Health01 = 1 }, scores);
            Assert.AreEqual(DisruptorGoal.Guard, GoalSelector.Rank(scores, null)[0], "a downed player is not attacked");
        }

        [Test]
        public void HysteresisKeepsTheCurrentGoalOnNearTies()
        {
            var scores = new double[4];
            scores[(int)DisruptorGoal.Disrupt] = 0.50; scores[(int)DisruptorGoal.Evade] = 0.55;
            Assert.AreEqual(DisruptorGoal.Disrupt, GoalSelector.Rank(scores, DisruptorGoal.Disrupt)[0]);
            Assert.AreEqual(DisruptorGoal.Evade, GoalSelector.Rank(scores, null)[0]);
        }

        [Test]
        public void FactSetAppliesEffectsWithoutTouchingOtherFacts()
        {
            var effect = FactSet.True(Fact.PulseCharged).AndFalse(Fact.Concealed);
            uint s = effect.ApplyTo((uint)(Fact.Concealed | Fact.PlayerKnown));
            Assert.AreEqual((uint)(Fact.PulseCharged | Fact.PlayerKnown), s);
            Assert.AreEqual(3, FactSet.PopCount(0b1011));
            Assert.Throws<ArgumentException>(() => GoalSelector.Score(default, new double[2]));
        }
    }
}
