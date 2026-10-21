using System;

namespace Skybreak.Disruptor.Core
{
    /// <summary>Measured properties of one candidate position, relative to the player and to this agent.</summary>
    public struct AnchorFeatures
    {
        public double DistanceToPlayer;    // metres, planar
        public double AimAngle;            // degrees between the player's aim and the direction to the candidate (0 = dead centre)
        public bool LineToPlayer;          // unobstructed line from the candidate to the player's chest
        public double PathLength;          // metres along this agent's A* route (inf = unreachable)
        public double SecondsSinceUsed;    // since this agent last stood here (inf = never)
        public bool ClaimedByOther;        // another Disruptor is heading there
    }

    /// <summary>
    /// Weighted scoring of candidate positions (a linear evaluation function, as in game-tree evaluation):
    /// score = sum of w_i x f_i over normalised features f_i in [0,1]. Hard constraints return -infinity.
    ///
    /// Strike: be inside pulse range, at the edge of it (so the ring still covers a dodge step),
    ///         outside the player's aim, with a line to the player, reachable quickly, not repeated.
    /// Escape: be out of the player's line and aim, far enough to break pursuit, reachable quickly.
    /// </summary>
    public static class AnchorScoring
    {
        // Strike weights (sum 1). Flank is weighted highest: the Disruptor's identity is attacking from
        // where the player is not looking.
        public const double StrikeRangeWeight = 0.30, StrikeFlankWeight = 0.35, StrikeTravelWeight = 0.20, StrikeNoveltyWeight = 0.15;
        // Escape weights (sum 1). Breaking line of sight matters most.
        public const double EscapeCoverWeight = 0.45, EscapeFlankWeight = 0.20, EscapeDistanceWeight = 0.15, EscapeTravelWeight = 0.20;

        public const double FlankAngle = 70;          // degrees: beyond this the player has to turn to see it
        public const double NoveltySeconds = 8;       // a spot used this recently is predictable
        public const double TravelHorizon = 18;       // metres: longer routes score 0 on travel
        public const double RangeTolerance = 0.5;     // metres beyond strike distance still accepted (grid snapping)

        public static double Strike(in AnchorFeatures f, double strikeDistance)
        {
            if (!Valid(f) || !f.LineToPlayer || strikeDistance <= 0) return double.NegativeInfinity;
            // Must actually be in strike range on arrival (plus grid snapping tolerance), otherwise the
            // charge precondition fails there and the agent would loop back to the same spot.
            if (f.DistanceToPlayer > strikeDistance + RangeTolerance || f.DistanceToPlayer < 1.2) return double.NegativeInfinity;
            // Range: 1 at the ideal distance (ring edge), falls linearly to 0 at the player.
            double range = 1 - Math.Min(1, Math.Abs(f.DistanceToPlayer - strikeDistance) / strikeDistance);
            return StrikeRangeWeight * range + StrikeFlankWeight * Flank(f.AimAngle) +
                   StrikeTravelWeight * Travel(f.PathLength) + StrikeNoveltyWeight * Novelty(f.SecondsSinceUsed);
        }

        public static double Escape(in AnchorFeatures f, double minimumDistance)
        {
            if (!Valid(f)) return double.NegativeInfinity;
            double cover = f.LineToPlayer ? 0 : 1;
            double distance = Clamp01((f.DistanceToPlayer - minimumDistance * 0.5) / Math.Max(0.1, minimumDistance));
            return EscapeCoverWeight * cover + EscapeFlankWeight * Flank(f.AimAngle) +
                   EscapeDistanceWeight * distance + EscapeTravelWeight * Travel(f.PathLength);
        }

        /// <summary>0 when the candidate is dead centre in the player's aim, 1 beyond <see cref="FlankAngle"/>.</summary>
        public static double Flank(double aimAngle) => Clamp01(aimAngle / FlankAngle);

        /// <summary>How much of the player's aim cone a point sits in, 0..1 (1 = centre). Used as cost exposure.</summary>
        public static double Exposure(double aimAngle, bool lineToPlayer) => lineToPlayer ? 1 - Flank(aimAngle) : 0;

        static double Travel(double pathLength) => 1 - Clamp01(pathLength / TravelHorizon);
        static double Novelty(double seconds) => double.IsInfinity(seconds) ? 1 : Clamp01(seconds / NoveltySeconds);
        static bool Valid(in AnchorFeatures f) => !f.ClaimedByOther && !double.IsNaN(f.PathLength) && !double.IsInfinity(f.PathLength) && !double.IsNaN(f.DistanceToPlayer);
        static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }
}
