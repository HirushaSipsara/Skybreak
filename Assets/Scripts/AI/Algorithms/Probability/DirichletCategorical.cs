using System;

namespace Skybreak.AI.Algorithms.Probability
{
    // Belief over K outcomes (e.g. how a player answers an attack) learned from counts.
    // Prior: Dirichlet(a0, ..., a0). After n_k observations of outcome k the posterior predictive is
    //   P(k) = (a0 + n_k) / (K * a0 + N)
    // so with no data every outcome is equally likely, and each observation shifts belief gradually (Bayes, Lecture 09).
    // Optional forgetting multiplies all counts by gamma before each new observation, so recent behaviour weighs more.
    public sealed class DirichletCategorical
    {
        readonly double[] counts;
        public double Prior { get; }
        public double Forgetting { get; }
        public int Categories => counts.Length;
        public double Total { get; private set; }

        public DirichletCategorical(int categories, double prior = 1, double forgetting = 1)
        {
            if (categories < 2) throw new ArgumentException("Need at least two categories");
            if (prior <= 0) throw new ArgumentException("Prior must be positive");
            if (forgetting <= 0 || forgetting > 1) throw new ArgumentException("Forgetting must be in (0, 1]");
            counts = new double[categories]; Prior = prior; Forgetting = forgetting;
        }

        public void Observe(int category, double weight = 1)
        {
            if (category < 0 || category >= counts.Length) throw new ArgumentOutOfRangeException(nameof(category));
            if (Forgetting < 1) { Total = 0; for (int i = 0; i < counts.Length; i++) { counts[i] *= Forgetting; Total += counts[i]; } }
            counts[category] += weight; Total += weight;
        }

        public double Count(int category) => counts[category];

        public double Probability(int category) => (Prior + counts[category]) / (counts.Length * Prior + Total);

        public int MostLikely()
        {
            int best = 0;
            for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            return best;
        }

        public void Clear() { Array.Clear(counts, 0, counts.Length); Total = 0; }
    }
}
