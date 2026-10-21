using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Local
{
    // A local-search problem (Lecture 05): only the final state matters, not the path to it.
    // Score(s) is the objective to MAXIMISE; Neighbour proposes a nearby state (its step may shrink with temperature).
    public interface ILocalSearchProblem<TState>
    {
        double Score(TState state);
        TState Neighbour(TState state, double temperature, Random rng);
    }

    public sealed class LocalSearchResult<TState>
    {
        public TState Best { get; internal set; }
        public double BestScore { get; internal set; }
        public double StartScore { get; internal set; }
        public int Iterations { get; internal set; }
        public int AcceptedBetter { get; internal set; }
        public int AcceptedWorse { get; internal set; }   // downhill moves: how annealing escapes local maxima
        public int Rejected { get; internal set; }
        public List<(TState state, double score, bool accepted)> Trace { get; } = new List<(TState, double, bool)>();
    }

    // Simulated annealing (Lecture 05): like hill climbing, but a worse neighbour is still accepted with
    // probability P = e^(dE / T), where dE < 0 is how much worse it is and T is the temperature. Early on T is
    // high and bad moves are often accepted (exploration); T cools geometrically (T <- alpha * T) until the search
    // behaves like hill climbing (exploitation). The best state ever seen is returned.
    public static class SimulatedAnnealing
    {
        public static double AcceptanceProbability(double deltaE, double temperature) =>
            deltaE >= 0 ? 1 : temperature <= 0 ? 0 : Math.Exp(deltaE / temperature);

        public static double Temperature(double t0, double alpha, int step) => t0 * Math.Pow(alpha, step);

        public static LocalSearchResult<TState> Run<TState>(ILocalSearchProblem<TState> problem, TState start, int iterations,
            double t0, double alpha, Random rng, bool recordTrace = false)
        {
            var r = new LocalSearchResult<TState>();
            TState current = start; double currentScore = problem.Score(start);
            r.Best = current; r.BestScore = r.StartScore = currentScore;
            double t = t0;
            for (int i = 0; i < iterations; i++, t *= alpha)
            {
                TState next = problem.Neighbour(current, t, rng);
                double nextScore = problem.Score(next);
                double deltaE = nextScore - currentScore;
                bool accept = rng.NextDouble() < AcceptanceProbability(deltaE, t);
                if (accept)
                {
                    if (deltaE >= 0) r.AcceptedBetter++; else r.AcceptedWorse++;
                    current = next; currentScore = nextScore;
                    if (currentScore > r.BestScore) { r.Best = current; r.BestScore = currentScore; }
                }
                else r.Rejected++;
                if (recordTrace) r.Trace.Add((next, nextScore, accept));
                r.Iterations++;
            }
            return r;
        }
    }

    // Stochastic (first-choice) hill climbing: accept a neighbour only if it is no worse. Greedy, so it stops
    // climbing at the first local maximum it reaches (Lecture 05 "Hill Climbing: Strengths and Weaknesses").
    // Used as the baseline that simulated annealing is compared against.
    public static class HillClimbing
    {
        public static LocalSearchResult<TState> Run<TState>(ILocalSearchProblem<TState> problem, TState start, int iterations, Random rng)
        {
            var r = new LocalSearchResult<TState>();
            TState current = start; double currentScore = problem.Score(start);
            r.Best = current; r.BestScore = r.StartScore = currentScore;
            for (int i = 0; i < iterations; i++)
            {
                TState next = problem.Neighbour(current, 0, rng);
                double nextScore = problem.Score(next);
                if (nextScore >= currentScore) { current = next; currentScore = nextScore; r.AcceptedBetter++; }
                else r.Rejected++;
                r.Iterations++;
            }
            r.Best = current; r.BestScore = currentScore;
            return r;
        }
    }
}
