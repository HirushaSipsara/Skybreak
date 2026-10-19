using System.Collections.Generic;
using NUnit.Framework;
using Skybreak.AI.Algorithms.Fuzzy;

namespace Skybreak.AI.Algorithms.Tests
{
    // Each test reproduces a worked example from SE3062 Lecture 09 (Reasoning under Uncertainty, Parts 3-4).
    public class FuzzyInferenceTests
    {
        const double Eps = 1e-6;

        // Lecture 09 activity "Draw the Membership Functions": greenhouse temperature, 10-30 C.
        static LinguisticVariable Temperature() => new LinguisticVariable("Temperature", 10, 30)
            .Term("Cold", new Trapezoid(10, 10, 15, 20))
            .Term("Optimal", new Triangle(15, 20, 25))
            .Term("Hot", new Trapezoid(20, 25, 30, 30));

        [Test]
        public void Fuzzification_18C_IsFortyPercentColdAndSixtyPercentOptimal()
        {
            var mu = Temperature().Fuzzify(18);
            Assert.AreEqual(0.4, mu["Cold"], Eps);      // (20 - 18) / (20 - 15)
            Assert.AreEqual(0.6, mu["Optimal"], Eps);   // (18 - 15) / (20 - 15)
            Assert.AreEqual(0.0, mu["Hot"], Eps);
        }

        [Test]
        public void MembershipFunctions_CoreSlopesAndOutside()
        {
            var t = new Trapezoid(0, 2, 4, 8);
            Assert.AreEqual(0, t.Degree(-1), Eps);
            Assert.AreEqual(0.5, t.Degree(1), Eps);
            Assert.AreEqual(1, t.Degree(3), Eps);
            Assert.AreEqual(0.25, t.Degree(7), Eps);
            Assert.AreEqual(0, t.Degree(9), Eps);
            var shoulder = new Trapezoid(0, 0, 0, 50);   // vertical left edge
            Assert.AreEqual(1, shoulder.Degree(0), Eps);
            Assert.AreEqual(0.6, shoulder.Degree(20), Eps);
            var tri = new Triangle(15, 20, 25);
            Assert.AreEqual(1, tri.Degree(20), Eps);
            Assert.AreEqual(0.6, tri.Degree(22), Eps);
        }

        [Test]
        public void Hedges_VeryAndSlightly_MatchLectureValues()
        {
            var v = Temperature();
            var inputs = new FuzzyInputs(new Dictionary<string, Dictionary<string, double>> { { v.Name, v.Fuzzify(18) } });
            var optimal = FuzzyCondition.Is("Temperature", "Optimal");
            Assert.AreEqual(0.36, optimal.Very().Truth(inputs), Eps);              // 0.6^2
            Assert.AreEqual(0.7746, optimal.Slightly().Truth(inputs), 1e-4);       // sqrt(0.6) ~ 0.77
        }

        [Test]
        public void ZadehOperators_AndIsMin_OrIsMax_NotIsComplement()
        {
            var v = Temperature();
            var inputs = new FuzzyInputs(new Dictionary<string, Dictionary<string, double>> { { v.Name, v.Fuzzify(18) } });
            var cold = FuzzyCondition.Is("Temperature", "Cold");
            var optimal = FuzzyCondition.Is("Temperature", "Optimal");
            Assert.AreEqual(0.4, cold.And(optimal).Truth(inputs), Eps);
            Assert.AreEqual(0.6, cold.Or(optimal).Truth(inputs), Eps);
            Assert.AreEqual(0.6, cold.Not().Truth(inputs), Eps);
        }

        // Lecture 09 "Hospital Robot" pipeline: Dirt = 60 is 0.8 Medium / 0.2 High, Surface = 20 is 0.6 Delicate / 0.4 Carpet.
        // Output Intensity 0-100: Gentle falls 1 -> 0 over [0, 50]; Normal peaks at 50 and falls to 0 at 100.
        static FuzzyInferenceSystem HospitalRobot()
        {
            var dirt = new LinguisticVariable("Dirt", 0, 100)
                .Term("Low", new Trapezoid(0, 0, 20, 40))
                .Term("Medium", new Trapezoid(20, 40, 55, 80))      // 60 -> (80 - 60) / 25 = 0.8
                .Term("High", new Trapezoid(55, 80, 100, 100));     // 60 -> (60 - 55) / 25 = 0.2
            var surface = new LinguisticVariable("Surface", 0, 100)
                .Term("Delicate", new Trapezoid(0, 0, 14, 29))      // 20 -> (29 - 20) / 15 = 0.6
                .Term("Carpet", new Trapezoid(14, 29, 50, 70))      // 20 -> (20 - 14) / 15 = 0.4
                .Term("HardFloor", new Trapezoid(50, 70, 100, 100));
            var intensity = new LinguisticVariable("Intensity", 0, 100)
                .Term("Gentle", new Trapezoid(0, 0, 0, 50))
                .Term("Normal", new Triangle(0, 50, 100))
                .Term("Aggressive", new Trapezoid(50, 100, 100, 100));
            FuzzyCondition Is(string v, string t) => FuzzyCondition.Is(v, t);
            return new FuzzyInferenceSystem().Input(dirt).Input(surface).Output(intensity)
                .Rule("Rule 4", Is("Dirt", "Medium").And(Is("Surface", "Delicate")), "Intensity", "Gentle")
                .Rule("Rule 5", Is("Dirt", "Medium").And(Is("Surface", "Carpet")), "Intensity", "Normal")
                .Rule("Rule 7", Is("Dirt", "High").And(Is("Surface", "Delicate")), "Intensity", "Gentle")
                .Rule("Rule 8", Is("Dirt", "High").And(Is("Surface", "Carpet")), "Intensity", "Normal");
        }

        [Test]
        public void HospitalRobot_RuleFiring_AggregationAndCentreOfGravity()
        {
            var fis = HospitalRobot();
            var result = fis.Evaluate(new Dictionary<string, double> { { "Dirt", 60 }, { "Surface", 20 } });

            Assert.AreEqual(0.8, result.Fuzzified.Of("Dirt", "Medium"), Eps);
            Assert.AreEqual(0.2, result.Fuzzified.Of("Dirt", "High"), Eps);
            Assert.AreEqual(0.6, result.Fuzzified.Of("Surface", "Delicate"), Eps);
            Assert.AreEqual(0.4, result.Fuzzified.Of("Surface", "Carpet"), Eps);

            var strengths = new Dictionary<string, double>();
            foreach (var f in result.Firing) strengths[f.Key.Label] = f.Value;
            Assert.AreEqual(0.6, strengths["Rule 4"], Eps);    // min(0.8, 0.6)
            Assert.AreEqual(0.4, strengths["Rule 5"], Eps);    // min(0.8, 0.4)
            Assert.AreEqual(0.2, strengths["Rule 7"], Eps);    // min(0.2, 0.6)
            Assert.AreEqual(0.2, strengths["Rule 8"], Eps);    // min(0.2, 0.4)

            Assert.AreEqual(0.6, result.Activation["Intensity"]["Gentle"], Eps);   // max(0.6, 0.2)
            Assert.AreEqual(0.4, result.Activation["Intensity"]["Normal"], Eps);   // max(0.4, 0.2)

            // Lecture: area = 12 + 5 + 20 + 4 = 41, moment = 1690, COG = 1690 / 41 = 41.22
            Assert.AreEqual(1690.0 / 41.0, result["Intensity"], 0.01);
        }

        [Test]
        public void Centroid_OfSymmetricSet_IsItsCentre_AndNoRulesFiredGivesDefault()
        {
            var o = new LinguisticVariable("Out", 0, 1).Term("Mid", new Triangle(.3, .5, .7));
            Assert.AreEqual(0.5, FuzzyInferenceSystem.Centroid(o, new Dictionary<string, double> { { "Mid", 0.8 } }, 1001), 1e-6);
            o.DefaultValue = 0.25;
            Assert.AreEqual(0.25, FuzzyInferenceSystem.Centroid(o, new Dictionary<string, double> { { "Mid", 0 } }, 1001), 1e-9);
        }
    }
}
