using System;
using System.Collections.Generic;

namespace Skybreak.AI.Algorithms.Fuzzy
{
    // Membership degrees of every input term after fuzzification: degrees[variable][term].
    public sealed class FuzzyInputs
    {
        readonly Dictionary<string, Dictionary<string, double>> degrees;
        public FuzzyInputs(Dictionary<string, Dictionary<string, double>> degrees) { this.degrees = degrees; }
        public double Of(string variable, string term)
        {
            if (!degrees.TryGetValue(variable, out var terms)) throw new KeyNotFoundException($"No input variable {variable}");
            if (!terms.TryGetValue(term, out var mu)) throw new KeyNotFoundException($"{variable} has no term {term}");
            return mu;
        }
        public IReadOnlyDictionary<string, Dictionary<string, double>> All => degrees;
    }

    // The IF part of a rule. Zadeh operators (Lecture 09): AND = min, OR = max, NOT = 1 - mu.
    // Hedges: Very = mu^2 (concentration, stricter), Slightly = sqrt(mu) (dilation, more inclusive).
    public abstract class FuzzyCondition
    {
        public abstract double Truth(FuzzyInputs inputs);
        public abstract override string ToString();

        public static FuzzyCondition Is(string variable, string term) => new IsTerm(variable, term);
        public FuzzyCondition And(FuzzyCondition other) => new Operator("AND", this, other, Math.Min);
        public FuzzyCondition Or(FuzzyCondition other) => new Operator("OR", this, other, Math.Max);
        public FuzzyCondition Not() => new Hedge("NOT", this, mu => 1 - mu);
        public FuzzyCondition Very() => new Hedge("VERY", this, mu => mu * mu);
        public FuzzyCondition Slightly() => new Hedge("SLIGHTLY", this, Math.Sqrt);

        sealed class IsTerm : FuzzyCondition
        {
            readonly string variable, term;
            public IsTerm(string variable, string term) { this.variable = variable; this.term = term; }
            public override double Truth(FuzzyInputs inputs) => inputs.Of(variable, term);
            public override string ToString() => $"{variable} is {term}";
        }

        sealed class Operator : FuzzyCondition
        {
            readonly string name; readonly FuzzyCondition left, right; readonly Func<double, double, double> op;
            public Operator(string name, FuzzyCondition left, FuzzyCondition right, Func<double, double, double> op) { this.name = name; this.left = left; this.right = right; this.op = op; }
            public override double Truth(FuzzyInputs inputs) => op(left.Truth(inputs), right.Truth(inputs));
            public override string ToString() => $"({left} {name} {right})";
        }

        sealed class Hedge : FuzzyCondition
        {
            readonly string name; readonly FuzzyCondition inner; readonly Func<double, double> f;
            public Hedge(string name, FuzzyCondition inner, Func<double, double> f) { this.name = name; this.inner = inner; this.f = f; }
            public override double Truth(FuzzyInputs inputs) => f(inner.Truth(inputs));
            public override string ToString() => $"{name} ({inner})";
        }
    }

    // IF condition THEN output is term. The rule's firing strength is the truth of its condition (times its weight).
    public sealed class FuzzyRule
    {
        public string Label { get; }
        public FuzzyCondition Condition { get; }
        public string OutputVariable { get; }
        public string OutputTerm { get; }
        public double Weight { get; }

        public FuzzyRule(string label, FuzzyCondition condition, string outputVariable, string outputTerm, double weight = 1)
        {
            Label = label; Condition = condition; OutputVariable = outputVariable; OutputTerm = outputTerm; Weight = weight;
        }

        public double FiringStrength(FuzzyInputs inputs) => Weight * Condition.Truth(inputs);

        public override string ToString() => $"{Label}: IF {Condition} THEN {OutputVariable} is {OutputTerm}";
    }
}
