using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Fuzzy
{
    // A linguistic variable (Lecture 09): a quantity whose values are words ("Low", "Near"), each word
    // being a fuzzy set over the variable's universe of discourse [Min, Max].
    public sealed class LinguisticVariable
    {
        readonly List<KeyValuePair<string, IMembershipFunction>> terms = new List<KeyValuePair<string, IMembershipFunction>>();

        public string Name { get; }
        public double Min { get; }
        public double Max { get; }
        // Crisp output used when no rule fires for this variable (defuzzification has no area).
        public double DefaultValue { get; set; }
        public IReadOnlyList<KeyValuePair<string, IMembershipFunction>> Terms => terms;

        public LinguisticVariable(string name, double min, double max)
        {
            if (!(max > min)) throw new ArgumentException("Universe needs max > min");
            Name = name; Min = min; Max = max; DefaultValue = min;
        }

        public LinguisticVariable Term(string term, IMembershipFunction membership)
        {
            foreach (var t in terms) if (t.Key == term) throw new ArgumentException($"Duplicate term {term} in {Name}");
            terms.Add(new KeyValuePair<string, IMembershipFunction>(term, membership));
            return this;
        }

        public IMembershipFunction this[string term]
        {
            get
            {
                foreach (var t in terms) if (t.Key == term) return t.Value;
                throw new KeyNotFoundException($"{Name} has no term {term}");
            }
        }

        // Fuzzification: the degree to which the crisp value belongs to every term (input clamped to the universe).
        public Dictionary<string, double> Fuzzify(double crisp)
        {
            double x = Math.Max(Min, Math.Min(Max, crisp));
            var degrees = new Dictionary<string, double>();
            foreach (var t in terms) degrees[t.Key] = t.Value.Degree(x);
            return degrees;
        }
    }
}
