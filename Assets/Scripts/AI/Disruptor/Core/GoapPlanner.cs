using System;
using System.Collections.Generic;

namespace Skybreak.Disruptor.Core
{
    /// <summary>
    /// One GOAP action: what must hold before it (preconditions), what it changes (effects) and
    /// what it costs in the current situation. The runtime rewrites <see cref="Cost"/> before every
    /// plan from path lengths, exposure and the dodge belief, so the same action set plans
    /// differently as the fight changes. A non-finite cost disables the action for that plan.
    /// </summary>
    public sealed class GoapAction
    {
        public readonly int Id;
        public readonly string Name;
        public readonly FactSet Preconditions;
        public readonly FactSet Effects;
        public double Cost;

        public GoapAction(int id, string name, FactSet preconditions, FactSet effects, double cost = 1)
        {
            Id = id; Name = name; Preconditions = preconditions; Effects = effects; Cost = cost;
        }

        public bool Enabled => !double.IsNaN(Cost) && !double.IsInfinity(Cost) && Cost > 0;
    }

    public enum PlanStatus { Found, AlreadySatisfied, NoPlan, BudgetExceeded, Invalid }

    public sealed class GoapPlan
    {
        public PlanStatus Status = PlanStatus.Invalid;
        public readonly List<GoapAction> Steps = new List<GoapAction>();
        public double Cost;
        public int Expanded;
        public bool Usable => Status == PlanStatus.Found || Status == PlanStatus.AlreadySatisfied;

        public override string ToString()
        {
            if (!Usable) return Status.ToString();
            var names = new string[Steps.Count];
            for (int i = 0; i < Steps.Count; i++) names[i] = Steps[i].Name;
            return Steps.Count == 0 ? "(goal already holds)" : string.Join(" > ", names);
        }
    }

    /// <summary>
    /// Goal-Oriented Action Planning as an A* search over world states (Orkin, F.E.A.R., 2006).
    /// Nodes are fact bit sets, edges are applicable actions, edge weight is the action's cost.
    ///
    /// Heuristic: h(s) = ceil(u(s) / k) x c_min, where u(s) is the number of unsatisfied goal facts,
    /// k is the largest number of goal facts any one enabled action can set, and c_min is the cheapest
    /// enabled action. One action can fix at most k facts and costs at least c_min, so h never
    /// overestimates (admissible). It also drops by at most c_min per step, so it is consistent and a
    /// state never needs to be re-opened after it is closed.
    /// </summary>
    public static class GoapPlanner
    {
        struct Node { public uint State; public double G; public int Parent; public int Action; }

        public static GoapPlan Plan(uint start, FactSet goal, IReadOnlyList<GoapAction> actions, int budget = 256)
        {
            var plan = new GoapPlan();
            if (actions == null || budget < 1) return plan;
            if (goal.SatisfiedBy(start)) { plan.Status = PlanStatus.AlreadySatisfied; return plan; }

            int maxFix = 0; double minCost = double.PositiveInfinity;
            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                if (a == null || !a.Enabled) continue;
                maxFix = Math.Max(maxFix, FactSet.PopCount(a.Effects.Mask & goal.Mask));
                minCost = Math.Min(minCost, a.Cost);
            }
            if (maxFix == 0) { plan.Status = PlanStatus.NoPlan; return plan; }

            double H(uint s) => Math.Ceiling(goal.Unsatisfied(s) / (double)maxFix) * minCost;

            var nodes = new List<Node>(64) { new Node { State = start, G = 0, Parent = -1, Action = -1 } };
            var best = new Dictionary<uint, double> { [start] = 0 };
            var closed = new HashSet<uint>();
            var open = new PriorityQueue();
            open.Push(0, H(start), H(start));

            while (open.Count > 0)
            {
                int index = open.Pop();
                var node = nodes[index];
                if (closed.Contains(node.State) || node.G > best[node.State]) continue;   // stale queue entry
                if (goal.SatisfiedBy(node.State)) { Reconstruct(plan, nodes, index, actions); plan.Status = PlanStatus.Found; return plan; }
                if (plan.Expanded >= budget) { plan.Status = PlanStatus.BudgetExceeded; return plan; }
                plan.Expanded++;
                closed.Add(node.State);

                for (int i = 0; i < actions.Count; i++)
                {
                    var a = actions[i];
                    if (a == null || !a.Enabled || !a.Preconditions.SatisfiedBy(node.State)) continue;
                    uint next = a.Effects.ApplyTo(node.State);
                    if (next == node.State || closed.Contains(next)) continue;          // no-op or finished state
                    double g = node.G + a.Cost;
                    if (best.TryGetValue(next, out double known) && known <= g) continue;
                    best[next] = g;
                    nodes.Add(new Node { State = next, G = g, Parent = index, Action = i });
                    double h = H(next);
                    open.Push(nodes.Count - 1, g + h, h);
                }
            }
            plan.Status = PlanStatus.NoPlan;
            return plan;
        }

        static void Reconstruct(GoapPlan plan, List<Node> nodes, int index, IReadOnlyList<GoapAction> actions)
        {
            plan.Cost = nodes[index].G;
            for (int i = index; nodes[i].Parent >= 0; i = nodes[i].Parent) plan.Steps.Add(actions[nodes[i].Action]);
            plan.Steps.Reverse();
        }

        /// <summary>Binary min-heap on f, ties broken towards lower h (deeper, closer-to-goal nodes first).</summary>
        sealed class PriorityQueue
        {
            readonly List<(int item, double f, double h)> heap = new List<(int, double, double)>();
            public int Count => heap.Count;

            static bool Less((int item, double f, double h) a, (int item, double f, double h) b) =>
                a.f < b.f || (a.f == b.f && a.h < b.h);

            public void Push(int item, double f, double h)
            {
                heap.Add((item, f, h));
                int i = heap.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (!Less(heap[i], heap[p])) break;
                    (heap[i], heap[p]) = (heap[p], heap[i]); i = p;
                }
            }

            public int Pop()
            {
                int result = heap[0].item;
                heap[0] = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < heap.Count && Less(heap[l], heap[m])) m = l;
                    if (r < heap.Count && Less(heap[r], heap[m])) m = r;
                    if (m == i) break;
                    (heap[i], heap[m]) = (heap[m], heap[i]); i = m;
                }
                return result;
            }
        }
    }
}
