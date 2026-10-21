using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Adversarial
{
    // A two-party game seen from the deciding agent (MAX): it picks an action, then nature or the opponent answers with
    // one of several outcomes, each with a probability, a reward and a next state.
    public interface IExpectimaxModel<TState, TAction>
    {
        IEnumerable<TAction> Actions(TState state);
        IEnumerable<(double probability, double reward, TState next)> Outcomes(TState state, TAction action);
        double Evaluate(TState state);   // evaluation function at the depth limit (Lecture 10 "Evaluation Functions")
    }

    public sealed class ExpectimaxResult<TAction>
    {
        public bool HasAction { get; internal set; }
        public TAction Best { get; internal set; }
        public double Value { get; internal set; }
        public int NodesVisited { get; internal set; }
        public List<(TAction action, double value)> RootValues { get; } = new List<(TAction, double)>();
    }

    // Depth-limited expectimax (Lecture 10). MAX nodes take the best action; CHANCE nodes take the probability-weighted
    // average of their outcomes:
    //   V(s, d) = Evaluate(s)                                               if d = 0 or no actions
    //   V(s, d) = max_a  sum_o  P(o | s, a) * (R(s, a, o) + V(s'_o, d - 1))
    // With pessimistic = true the CHANCE nodes take the worst outcome instead, which is minimax against an opponent
    // that always picks the reply worst for us. Lecture 10's contrast: minimax is pessimistic, expectimax is realistic
    // about a fallible opponent.
    public static class Expectimax
    {
        public static ExpectimaxResult<TAction> Decide<TState, TAction>(IExpectimaxModel<TState, TAction> model, TState state, int depth,
            bool pessimistic = false, Func<TAction, double> rootBonus = null)
        {
            if (depth < 1) throw new ArgumentException("Depth must be at least 1");
            var result = new ExpectimaxResult<TAction>();
            int nodes = 0;
            double best = double.NegativeInfinity;
            foreach (var action in model.Actions(state))
            {
                double v = ChanceValue(model, state, action, depth, pessimistic, ref nodes) + (rootBonus != null ? rootBonus(action) : 0);
                result.RootValues.Add((action, v));
                if (v > best) { best = v; result.Best = action; result.HasAction = true; }
            }
            result.Value = result.HasAction ? best : model.Evaluate(state);
            result.NodesVisited = nodes + 1;
            return result;
        }

        static double MaxValue<TState, TAction>(IExpectimaxModel<TState, TAction> model, TState state, int depth, bool pessimistic, ref int nodes)
        {
            nodes++;
            if (depth == 0) return model.Evaluate(state);
            double best = double.NegativeInfinity; bool any = false;
            foreach (var action in model.Actions(state))
            {
                any = true;
                best = Math.Max(best, ChanceValue(model, state, action, depth, pessimistic, ref nodes));
            }
            return any ? best : model.Evaluate(state);
        }

        static double ChanceValue<TState, TAction>(IExpectimaxModel<TState, TAction> model, TState state, TAction action, int depth, bool pessimistic, ref int nodes)
        {
            nodes++;
            double total = 0, mass = 0, worst = double.PositiveInfinity;
            foreach (var (p, reward, next) in model.Outcomes(state, action))
            {
                if (p <= 0) continue;
                double v = reward + MaxValue(model, next, depth - 1, pessimistic, ref nodes);
                total += p * v; mass += p; worst = Math.Min(worst, v);
            }
            if (mass <= 0) return model.Evaluate(state);
            return pessimistic ? worst : total / mass;   // normalise in case the probabilities do not sum exactly to 1
        }
    }
}
