using Skybreak.AI.Algorithms.Bandit;

namespace Skybreak.AI.RiftWarden
{
    public enum RiftRangeBand { Close, Mid, Far }

    // Layer 3 (Bayesian learning, Lecture 09; learning agents, Lecture 03; multi-armed bandits beyond the lectures):
    // for every attack and range band, a Beta belief about "this attack hits THIS player from here".
    // Each cannon shot is one trial; a slash, rear strike or slam is one trial. Old evidence fades (gamma 0.97) so the
    // boss follows a player who changes tactics. The decision layer samples these beliefs (Thompson sampling).
    public sealed class RiftAttackLearner
    {
        public const double PriorAlpha = 2, PriorBeta = 2, Forgetting = .97;
        public const float CloseBelow = 4, FarAbove = 10;

        readonly BetaBelief[,] beliefs = new BetaBelief[4, 3];
        public int Updates { get; private set; }

        public RiftAttackLearner()
        {
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 3; b++) beliefs[a, b] = new BetaBelief(PriorAlpha, PriorBeta, Forgetting);
        }

        public static RiftRangeBand Band(float range) => range < CloseBelow ? RiftRangeBand.Close : range > FarAbove ? RiftRangeBand.Far : RiftRangeBand.Mid;

        public BetaBelief Belief(RiftAttackKind kind, RiftRangeBand band) => beliefs[(int)kind, (int)band];

        public void Record(RiftAttackKind kind, RiftRangeBand band, bool hit) { beliefs[(int)kind, (int)band].Observe(hit); Updates++; }

        public double HitChance(RiftAttackKind kind, RiftRangeBand band) => beliefs[(int)kind, (int)band].Mean;

        public void Reset() { foreach (var b in beliefs) b.Reset(); Updates = 0; }
    }
}
