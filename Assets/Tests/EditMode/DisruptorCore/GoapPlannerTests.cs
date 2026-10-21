using NUnit.Framework;
using Skybreak.Disruptor.Core;

namespace Skybreak.Disruptor.Tests
{
    public sealed class GoapPlannerTests
    {
        static CostInputs Typical() => new CostInputs
        {
            StrikePathLength = 6, StrikeExposure = 0, CloseInDistance = 4, EscapePathLength = 8,
            InvestigateLength = double.PositiveInfinity, PatrolLength = 5, ChargeSeconds = 1, DodgeProbability = 0.5,
            ApproachSpeed = 3.4, DashSpeed = 8, PatrolSpeed = 2.2,
        };

        const uint SeesPlayer = (uint)(Fact.PlayerKnown | Fact.PlayerVisible | Fact.PulseReady | Fact.StrikeAnchorAvailable | Fact.EscapeAnchorAvailable);

        [Test]
        public void HitAndRunPlanEndsWithARelocation()
        {
            var d = new DisruptorDomain(); d.ApplyCosts(Typical());
            var plan = GoapPlanner.Plan(SeesPlayer, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions);
            Assert.AreEqual(PlanStatus.Found, plan.Status);
            Assert.AreEqual(4, plan.Steps.Count, plan.ToString());
            Assert.AreEqual((int)DisruptorActionId.ChargePulse, plan.Steps[1].Id);
            Assert.AreEqual((int)DisruptorActionId.ReleasePulse, plan.Steps[2].Id);
            Assert.AreEqual((int)DisruptorActionId.PhaseDash, plan.Steps[3].Id);
        }

        [Test]
        public void ConcealedStrikeStillRelocatesBecauseReleaseRevealsIt()
        {
            var d = new DisruptorDomain(); d.ApplyCosts(Typical());
            uint start = SeesPlayer | (uint)(Fact.AtStrikePosition | Fact.Concealed);
            var plan = GoapPlanner.Plan(start, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions);
            Assert.AreEqual("Charge pulse (telegraph) > Release pulse > Phase-dash to escape anchor", plan.ToString());
        }

        [Test]
        public void DodgingPlayerMakesTheFlankCheaperThanAFrontalApproach()
        {
            var d = new DisruptorDomain();
            // Flank route 10 m vs 4 m straight in: 0.5 + 10/3.4 = 3.44 against 0.5 + 4/3.4 + 1.5 x (1 + p).
            var c = Typical(); c.StrikePathLength = 10; c.CloseInDistance = 4; c.StrikeExposure = 0;
            c.DodgeProbability = 0; d.ApplyCosts(c);
            var careless = GoapPlanner.Plan(SeesPlayer, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions);
            Assert.AreEqual((int)DisruptorActionId.CloseIn, careless.Steps[0].Id, "a player who never dodges is approached directly");

            c.DodgeProbability = 1; d.ApplyCosts(c);
            var dodger = GoapPlanner.Plan(SeesPlayer, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions);
            Assert.AreEqual((int)DisruptorActionId.MoveToStrike, dodger.Steps[0].Id, "a dodging player is flanked");
        }

        [Test]
        public void NoReadyPulseMeansNoDisruptPlan()
        {
            var d = new DisruptorDomain(); d.ApplyCosts(Typical());
            uint start = SeesPlayer & ~(uint)Fact.PulseReady;
            Assert.AreEqual(PlanStatus.NoPlan, GoapPlanner.Plan(start, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions).Status);
        }

        [Test]
        public void MissingEscapeRouteFallsBackToPlainStrike()
        {
            var d = new DisruptorDomain(); var c = Typical(); c.EscapePathLength = double.PositiveInfinity; d.ApplyCosts(c);
            uint start = SeesPlayer & ~(uint)Fact.EscapeAnchorAvailable;
            Assert.IsFalse(GoapPlanner.Plan(start, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions).Usable);
            var plain = GoapPlanner.Plan(start, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt, false), d.Actions);
            Assert.AreEqual(PlanStatus.Found, plain.Status);
            Assert.AreEqual((int)DisruptorActionId.ReleasePulse, plain.Steps[plain.Steps.Count - 1].Id);
        }

        [Test]
        public void HuntInvestigatesWhenThePlayerIsOnlyRemembered()
        {
            var d = new DisruptorDomain(); var c = Typical(); c.InvestigateLength = 7; d.ApplyCosts(c);
            var plan = GoapPlanner.Plan((uint)Fact.PlayerKnown, DisruptorDomain.GoalFacts(DisruptorGoal.Hunt), d.Actions);
            Assert.AreEqual("Investigate last known position", plan.ToString());
        }

        [Test]
        public void SatisfiedGoalNeedsNoSteps()
        {
            var d = new DisruptorDomain(); d.ApplyCosts(Typical());
            var plan = GoapPlanner.Plan((uint)Fact.Concealed, DisruptorDomain.GoalFacts(DisruptorGoal.Evade), d.Actions);
            Assert.AreEqual(PlanStatus.AlreadySatisfied, plan.Status);
            Assert.AreEqual(0, plan.Steps.Count);
        }

        [Test]
        public void PlannerReturnsTheCheapestOfTwoRoutes()
        {
            var a = new GoapAction(0, "expensive", FactSet.Empty, FactSet.True(Fact.AreaSwept), 5);
            var b1 = new GoapAction(1, "cheap first", FactSet.Empty, FactSet.True(Fact.PlayerKnown), 1);
            var b2 = new GoapAction(2, "cheap second", FactSet.True(Fact.PlayerKnown), FactSet.True(Fact.AreaSwept), 1);
            var plan = GoapPlanner.Plan(0, FactSet.True(Fact.AreaSwept), new[] { a, b1, b2 });
            Assert.AreEqual("cheap first > cheap second", plan.ToString());
            Assert.AreEqual(2, plan.Cost, 1e-9);
        }

        [Test]
        public void DisabledActionsAreIgnoredAndBudgetIsRespected()
        {
            var only = new GoapAction(0, "disabled", FactSet.Empty, FactSet.True(Fact.AreaSwept), double.PositiveInfinity);
            Assert.AreEqual(PlanStatus.NoPlan, GoapPlanner.Plan(0, FactSet.True(Fact.AreaSwept), new[] { only }).Status);
            var d = new DisruptorDomain(); d.ApplyCosts(Typical());
            Assert.AreEqual(PlanStatus.BudgetExceeded, GoapPlanner.Plan(SeesPlayer, DisruptorDomain.GoalFacts(DisruptorGoal.Disrupt), d.Actions, 1).Status);
        }
    }
}
