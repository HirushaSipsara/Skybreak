using System;

namespace Skybreak.AI.Algorithms.Fuzzy
{
    // A membership function mu_A(x) maps a crisp value to a degree of membership in [0, 1] (Lecture 09).
    public interface IMembershipFunction
    {
        double Degree(double x);
    }

    // Trapezoid (a, b, c, d): 0 below a, rises to 1 at b, stays 1 (the "core") until c, falls to 0 at d.
    // a == b gives a vertical left edge (a left "shoulder"), c == d a vertical right edge.
    public sealed class Trapezoid : IMembershipFunction
    {
        public double A { get; }
        public double B { get; }
        public double C { get; }
        public double D { get; }

        public Trapezoid(double a, double b, double c, double d)
        {
            if (!(a <= b && b <= c && c <= d)) throw new ArgumentException("Trapezoid needs a <= b <= c <= d");
            A = a; B = b; C = c; D = d;
        }

        public double Degree(double x)
        {
            if (x < A || x > D) return 0;
            if (x >= B && x <= C) return 1;                // core: full membership
            if (x < B) return (x - A) / (B - A);           // ascending slope: (x - start) / (peak - start)
            return (D - x) / (D - C);                      // descending slope: (end - x) / (end - start)
        }

        public override string ToString() => $"Trapezoid({A}, {B}, {C}, {D})";
    }

    // Triangle (a, b, c): a trapezoid whose core is the single peak b.
    public sealed class Triangle : IMembershipFunction
    {
        readonly Trapezoid shape;
        public double A => shape.A;
        public double B => shape.B;
        public double C => shape.D;

        public Triangle(double a, double b, double c) { shape = new Trapezoid(a, b, b, c); }

        public double Degree(double x) => shape.Degree(x);

        public override string ToString() => $"Triangle({A}, {B}, {C})";
    }
}
