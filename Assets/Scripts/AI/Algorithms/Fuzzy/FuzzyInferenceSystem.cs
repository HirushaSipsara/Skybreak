using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Fuzzy
{
    // Everything one evaluation produced, kept for debugging, the overlay and the technical defence.
    public sealed class FuzzyResult
    {
        public FuzzyInputs Fuzzified { get; internal set; }
        public List<KeyValuePair<FuzzyRule, double>> Firing { get; } = new List<KeyValuePair<FuzzyRule, double>>();
        // Aggregated strength per output term: activation[outputVariable][term] (MAX over the rules that conclude it).
        public Dictionary<string, Dictionary<string, double>> Activation { get; } = new Dictionary<string, Dictionary<string, double>>();
        public Dictionary<string, double> Outputs { get; } = new Dictionary<string, double>();
        public double this[string output] => Outputs[output];
    }

    // Mamdani fuzzy inference system, following the Lecture 09 pipeline:
    //   1-2 inputs and fuzzification, 3 rule base, 4 inference (AND = min, parallel firing),
    //   5 aggregation (MAX per output term, each term's set clipped at its strength),
    //   6 defuzzification by centre of gravity, 7 crisp outputs.
    public sealed class FuzzyInferenceSystem
    {
        readonly Dictionary<string, LinguisticVariable> inputs = new Dictionary<string, LinguisticVariable>();
        readonly Dictionary<string, LinguisticVariable> outputs = new Dictionary<string, LinguisticVariable>();
        readonly List<FuzzyRule> rules = new List<FuzzyRule>();

        // Number of sample points used to integrate the aggregated set (more = closer to the exact integral).
        public int Resolution { get; set; } = 1001;
        public IReadOnlyList<FuzzyRule> Rules => rules;
        public IEnumerable<LinguisticVariable> Inputs => inputs.Values;
        public IEnumerable<LinguisticVariable> Outputs => outputs.Values;

        public FuzzyInferenceSystem Input(LinguisticVariable v) { inputs.Add(v.Name, v); return this; }
        public FuzzyInferenceSystem Output(LinguisticVariable v) { outputs.Add(v.Name, v); return this; }

        public FuzzyInferenceSystem Rule(string label, FuzzyCondition condition, string outputVariable, string outputTerm, double weight = 1)
        {
            if (!outputs.TryGetValue(outputVariable, out var output)) throw new ArgumentException($"Unknown output {outputVariable}");
            var unused = output[outputTerm];   // validates the term exists
            rules.Add(new FuzzyRule(label, condition, outputVariable, outputTerm, weight));
            return this;
        }

        public FuzzyResult Evaluate(IReadOnlyDictionary<string, double> crispInputs)
        {
            var result = new FuzzyResult();
            // Steps 1-2: fuzzification
            var degrees = new Dictionary<string, Dictionary<string, double>>();
            foreach (var v in inputs.Values)
            {
                if (!crispInputs.TryGetValue(v.Name, out double x)) throw new ArgumentException($"Missing input {v.Name}");
                degrees[v.Name] = v.Fuzzify(x);
            }
            result.Fuzzified = new FuzzyInputs(degrees);

            // Steps 3-5: every rule fires in parallel; aggregation keeps the strongest rule per output term (MAX)
            foreach (var o in outputs.Values)
            {
                var act = new Dictionary<string, double>();
                foreach (var t in o.Terms) act[t.Key] = 0;
                result.Activation[o.Name] = act;
            }
            foreach (var rule in rules)
            {
                double strength = Math.Max(0, Math.Min(1, rule.FiringStrength(result.Fuzzified)));
                result.Firing.Add(new KeyValuePair<FuzzyRule, double>(rule, strength));
                var act = result.Activation[rule.OutputVariable];
                act[rule.OutputTerm] = Math.Max(act[rule.OutputTerm], strength);
            }

            // Step 6: defuzzification
            foreach (var o in outputs.Values) result.Outputs[o.Name] = Centroid(o, result.Activation[o.Name], Resolution);
            return result;
        }

        // Aggregated membership at x: the union (MAX) of every term's set clipped (MIN) at its activation.
        public static double AggregatedDegree(LinguisticVariable output, IReadOnlyDictionary<string, double> activation, double x)
        {
            double mu = 0;
            foreach (var t in output.Terms)
                if (activation.TryGetValue(t.Key, out double cap) && cap > 0) mu = Math.Max(mu, Math.Min(cap, t.Value.Degree(x)));
            return mu;
        }

        // Centre of gravity: COG = integral(mu(x) x dx) / integral(mu(x) dx), integrated with the trapezoid rule.
        // Exact for the piecewise-linear sets used here whenever every corner falls on a sample point.
        public static double Centroid(LinguisticVariable output, IReadOnlyDictionary<string, double> activation, int samples)
        {
            if (samples < 3) throw new ArgumentException("Need at least 3 samples");
            double step = (output.Max - output.Min) / (samples - 1);
            double area = 0, moment = 0, prevX = output.Min, prevMu = AggregatedDegree(output, activation, prevX);
            for (int i = 1; i < samples; i++)
            {
                double x = output.Min + i * step, mu = AggregatedDegree(output, activation, x);
                area += (prevMu + mu) * .5 * step;
                moment += (prevMu * prevX + mu * x) * .5 * step;
                prevX = x; prevMu = mu;
            }
            return area > 1e-12 ? moment / area : output.DefaultValue;
        }
    }
}
