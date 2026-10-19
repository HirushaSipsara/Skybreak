using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skybreak.AI.Algorithms.Adversarial;

namespace Skybreak.AI.Algorithms.Tests
{
    // The Rift Warden's combo planner (layer 4): depth-limited expectimax, compared with its pessimistic (minimax) form.
    public class ExpectimaxTests
    {
        // A hand-made tree. States are strings; "end" states are leaves with Evaluate = 0.
        sealed class Tree : IExpectimaxModel<string, string>
        {
            public readonly Dictionary<string, Dictionary<string, (double p, double r, string next)[]>> Nodes = new Dictionary<string, Dictionary<string, (double, double, string)[]>>();
            public readonly Dictionary<string, double> Leaf = new Dictionary<string, double>();
            public IEnumerable<string> Actions(string s) => Nodes.TryGetValue(s, out var a) ? a.Keys : Enumerable.Empty<string>();
            public IEnumerable<(double probability, double reward, string next)> Outcomes(string s, string a) => Nodes[s][a].Select(o => (o.p, o.r, o.next));
            public double Evaluate(string s) => Leaf.TryGetValue(s, out var v) ? v : 0;
        }

        static Tree Gamble()
        {
            // "risky": 50% deal 10, 50% deal 0  -> expected 5, worst 0
            // "safe" : always deal 4             -> expected 4, worst 4
            var t = new Tree();
            t.Nodes["root"] = new Dictionary<string, (double, double, string)[]>
            {
                ["risky"] = new[] { (.5, 10.0, "end"), (.5, 0.0, "end") },
                ["safe"] = new[] { (1.0, 4.0, "end") },
            };
            return t;
        }

        [Test]
        public void Expectimax_TakesTheBetterAverage_MinimaxTheBetterWorstCase()
        {
            var t = Gamble();
            var e = Expectimax.Decide(t, "root", 1);
            Assert.AreEqual("risky", e.Best); Assert.AreEqual(5, e.Value, 1e-12);
            var m = Expectimax.Decide(t, "root", 1, pessimistic: true);
            Assert.AreEqual("safe", m.Best); Assert.AreEqual(4, m.Value, 1e-12);   // Lecture 10: pessimism vs realism
        }

        [Test]
        public void DepthTwo_PlansACombo()
        {
            // Opening "poke" deals 1 but usually (80%) leaves the opponent in "open", where "punish" deals 10.
            // Opening "big" deals 6 immediately and leaves nothing. Greedy (depth 1) takes "big"; depth 2 sees the combo.
            var t = new Tree();
            t.Nodes["start"] = new Dictionary<string, (double, double, string)[]>
            {
                ["poke"] = new[] { (.8, 1.0, "open"), (.2, 1.0, "guarded") },
                ["big"] = new[] { (1.0, 6.0, "guarded") },
            };
            t.Nodes["open"] = new Dictionary<string, (double, double, string)[]> { ["punish"] = new[] { (1.0, 10.0, "end") } };
            t.Nodes["guarded"] = new Dictionary<string, (double, double, string)[]> { ["chip"] = new[] { (1.0, 1.0, "end") } };

            Assert.AreEqual("big", Expectimax.Decide(t, "start", 1).Best);
            var plan = Expectimax.Decide(t, "start", 2);
            Assert.AreEqual("poke", plan.Best);
            Assert.AreEqual(.8 * (1 + 10) + .2 * (1 + 1), plan.Value, 1e-12);   // 9.2 vs big: 6 + 1 = 7
        }

        [Test]
        public void EvaluationFunction_IsUsedAtTheDepthLimit()
        {
            var t = new Tree();
            t.Nodes["s"] = new Dictionary<string, (double, double, string)[]>
            {
                ["a"] = new[] { (1.0, 0.0, "good") },
                ["b"] = new[] { (1.0, 2.0, "bad") },
            };
            t.Leaf["good"] = 5; t.Leaf["bad"] = 0;
            var r = Expectimax.Decide(t, "s", 1);
            Assert.AreEqual("a", r.Best);
            Assert.AreEqual(5, r.Value, 1e-12);
            CollectionAssert.AreEquivalent(new[] { ("a", 5.0), ("b", 2.0) }, r.RootValues);
        }

        [Test]
        public void RootBonus_ShiftsOnlyTheRootChoice()
        {
            var t = Gamble();
            var r = Expectimax.Decide(t, "root", 1, rootBonus: a => a == "safe" ? 2 : 0);   // e.g. a Thompson-sampled bonus
            Assert.AreEqual("safe", r.Best); Assert.AreEqual(6, r.Value, 1e-12);
        }

        [Test]
        public void NoActions_FallsBackToTheEvaluation()
        {
            var t = new Tree(); t.Leaf["alone"] = 3;
            var r = Expectimax.Decide(t, "alone", 2);
            Assert.IsFalse(r.HasAction); Assert.AreEqual(3, r.Value, 1e-12);
        }
    }
}
