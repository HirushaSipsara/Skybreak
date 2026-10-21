using System;

namespace Skybreak.Disruptor.Core
{
    /// <summary>
    /// Symbolic facts the GOAP planner reasons over. A world state is a bit set of these facts,
    /// so a state is a single uint and two states compare in one instruction.
    /// </summary>
    [Flags]
    public enum Fact : uint
    {
        None = 0,
        PlayerKnown = 1 << 0,            // seen recently, or a hit revealed where the shot came from
        PlayerVisible = 1 << 1,          // in sight right now (or expected to be after a move)
        AtStrikePosition = 1 << 2,       // within strike distance of the player with a clear line
        PulseReady = 1 << 3,             // pulse cooldown has elapsed
        PulseCharged = 1 << 4,           // telegraph finished; release is possible
        PlayerDisrupted = 1 << 5,        // a pulse has been released at the player (goal fact)
        Concealed = 1 << 6,              // the player is not looking at the Disruptor
        AreaSwept = 1 << 7,              // a patrol leg has been completed (goal fact)
        StrikeAnchorAvailable = 1 << 8,  // tactical search found a reachable strike position
        EscapeAnchorAvailable = 1 << 9,  // tactical search found a reachable escape position
    }

    /// <summary>
    /// A partial assignment of facts: <see cref="Mask"/> says which facts matter, <see cref="Values"/> what they must be.
    /// Used for preconditions, effects and goals alike.
    /// </summary>
    public readonly struct FactSet
    {
        public readonly uint Mask;
        public readonly uint Values;

        public FactSet(uint mask, uint values) { Mask = mask; Values = values & mask; }

        public static readonly FactSet Empty = new FactSet(0, 0);

        /// <summary>Starts a set in which every listed fact must be true.</summary>
        public static FactSet True(Fact facts) => new FactSet((uint)facts, (uint)facts);

        /// <summary>Adds facts that must be false.</summary>
        public FactSet AndFalse(Fact facts) => new FactSet(Mask | (uint)facts, Values & ~(uint)facts);

        /// <summary>Adds facts that must be true.</summary>
        public FactSet AndTrue(Fact facts) => new FactSet(Mask | (uint)facts, Values | (uint)facts);

        public bool SatisfiedBy(uint state) => (state & Mask) == Values;

        /// <summary>Applies this set as an effect: masked bits are overwritten, the rest are kept.</summary>
        public uint ApplyTo(uint state) => (state & ~Mask) | Values;

        /// <summary>Number of facts in this set the state does not yet satisfy.</summary>
        public int Unsatisfied(uint state) => PopCount((state ^ Values) & Mask);

        public static int PopCount(uint v)
        {
            v -= (v >> 1) & 0x55555555u;
            v = (v & 0x33333333u) + ((v >> 2) & 0x33333333u);
            return (int)((((v + (v >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24);
        }

        public static string Describe(uint state)
        {
            if (state == 0) return "(none)";
            var parts = new System.Collections.Generic.List<string>();
            foreach (Fact f in Enum.GetValues(typeof(Fact)))
                if (f != Fact.None && (state & (uint)f) != 0) parts.Add(f.ToString());
            return string.Join(", ", parts);
        }
    }
}
