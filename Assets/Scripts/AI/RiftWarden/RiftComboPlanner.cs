using System;
using System.Collections.Generic;
using Skybreak.AI.Algorithms.Adversarial;
using Skybreak.AI.Algorithms.Prediction;

namespace Skybreak.AI.RiftWarden
{
    public enum RiftMove { Cannon, Slash, DoubleTeleport, Approach, Hold }

    // Abstract fight state for planning: where the player is (range band), how long until each attack is ready,
    // and which arms the boss still has.
    public struct RiftFightState
    {
        public RiftRangeBand band;
        public float cannonIn, bladeIn, teleportIn;   // seconds until ready (0 = ready)
        public bool cannonArm, bladeArm;
    }

    // Layer 4 (Lecture 10, expectimax): the boss's model of the duel.
    //   MAX     the boss picks a move
    //   CHANCE  the player answers with Stay / StrafeLeft / StrafeRight / BackOff / CloseIn / Jump, with the
    //           probabilities the player model learned for that attack; the attack hits with the learner's hit chance
    //   reward  expected damage dealt (% of player health) - riskWeight x expected damage taken while committed
    //   next    range band after the answer (backing off moves the player one band out, closing in one band in; the
    //           rear teleport always ends close), cooldowns advanced by the move's duration
    // A depth-2 search therefore finds two-move lines such as "cannon, they strafe and stay mid, then teleport behind".
    public sealed class RiftComboPlanner : IExpectimaxModel<RiftFightState, RiftMove>
    {
        public int depth = 2;
        public float riskWeight = 1;          // set by the phase (step 5)

        // durations include the 1.15 s recovery
        public const float CannonTime = 2.95f, SlashTime = 2.92f, TeleportTime = 3.47f, ApproachTime = 1.5f, HoldTime = .5f;
        static readonly MotionResponse[] Responses = (MotionResponse[])Enum.GetValues(typeof(MotionResponse));

        RiftPlayerModel model; RiftAttackLearner learner; WardenTuning tuning;
        float damageTakenPerSecond, partMaximum;

        public ExpectimaxResult<RiftMove> Last { get; private set; }
        public string PlannedLine { get; private set; } = "";

        public ExpectimaxResult<RiftMove> Plan(RiftFightState state, RiftPlayerModel playerModel, RiftAttackLearner attackLearner, WardenTuning t,
            float takenPerSecond, float armMaximum, Func<RiftMove, double> rootBonus)
        {
            model = playerModel; learner = attackLearner; tuning = t; damageTakenPerSecond = takenPerSecond; partMaximum = Math.Max(1, armMaximum);
            Last = Expectimax.Decide(this, state, depth, false, rootBonus);
            PlannedLine = Last.HasAction ? Describe(state, Last.Best) : "no move";
            return Last;
        }

        // ---------------------------------------------------------------- the game model
        public IEnumerable<RiftMove> Actions(RiftFightState s)
        {
            if (s.cannonArm && s.cannonIn <= 0) yield return RiftMove.Cannon;
            if (s.bladeArm && s.bladeIn <= 0 && s.band == RiftRangeBand.Close) yield return RiftMove.Slash;
            if (s.bladeArm && s.teleportIn <= 0 && s.band != RiftRangeBand.Close) yield return RiftMove.DoubleTeleport;
            if (s.band != RiftRangeBand.Close) yield return RiftMove.Approach;
            yield return RiftMove.Hold;
        }

        public IEnumerable<(double probability, double reward, RiftFightState next)> Outcomes(RiftFightState s, RiftMove move)
        {
            switch (move)
            {
                case RiftMove.Approach:
                    yield return (1, -Risk(move), Advance(Closer(s), ApproachTime)); yield break;
                case RiftMove.Hold:
                    yield return (1, -Risk(move), Advance(s, HoldTime)); yield break;
            }
            var kind = Kind(move);
            double hit = learner.HitChance(kind, s.band);
            double reward = hit * Payoff(kind) - Risk(move);
            foreach (var r in Responses)
            {
                double p = model.ResponseProbability(kind, r);
                var next = s;
                next.band = move == RiftMove.DoubleTeleport ? RiftRangeBand.Close : Shift(s.band, r);
                next = Advance(next, Duration(move));
                if (move == RiftMove.Cannon) next.cannonIn = tuning.cannonCooldown - Duration(move);
                if (move == RiftMove.Slash) next.bladeIn = tuning.bladeCooldown - Duration(move);
                if (move == RiftMove.DoubleTeleport) next.teleportIn = tuning.teleportCooldown - Duration(move);
                yield return (p, reward, next);
            }
        }

        // At the depth limit: the best attack that is (or soon will be) available, discounted by the wait.
        public double Evaluate(RiftFightState s)
        {
            double best = 0;
            if (s.cannonArm) best = Math.Max(best, Discount(s.cannonIn) * learner.HitChance(RiftAttackKind.Cannon, s.band) * Payoff(RiftAttackKind.Cannon));
            if (s.bladeArm && s.band == RiftRangeBand.Close) best = Math.Max(best, Discount(s.bladeIn) * learner.HitChance(RiftAttackKind.Slash, s.band) * Payoff(RiftAttackKind.Slash));
            if (s.bladeArm && s.band != RiftRangeBand.Close) best = Math.Max(best, Discount(s.teleportIn) * learner.HitChance(RiftAttackKind.DoubleTeleport, s.band) * Payoff(RiftAttackKind.DoubleTeleport));
            return .5 * best;   // half weight: a future opportunity, not a certain hit
        }

        // ---------------------------------------------------------------- helpers
        public static RiftAttackKind Kind(RiftMove m) => m == RiftMove.Cannon ? RiftAttackKind.Cannon : m == RiftMove.Slash ? RiftAttackKind.Slash : RiftAttackKind.DoubleTeleport;
        public double Payoff(RiftAttackKind kind) => (kind == RiftAttackKind.Cannon ? 3 * tuning.cannonDamageFraction : tuning.meleeDamageFraction) * 100;
        static float Duration(RiftMove m) => m == RiftMove.Cannon ? CannonTime : m == RiftMove.Slash ? SlashTime : m == RiftMove.DoubleTeleport ? TeleportTime : m == RiftMove.Approach ? ApproachTime : HoldTime;
        // Extra damage the boss expects to take during a move compared with standing still, as a percentage of an arm's
        // health: the player's recent damage rate x the move's duration x (its exposure - 1). The boss is shot whatever it
        // does, so only the difference matters: the rear teleport (player must turn around) and moving earn a bonus, and
        // holding still is no safer than attacking.
        double Risk(RiftMove m) => riskWeight * damageTakenPerSecond * Duration(m) * (Exposure(m) - 1) / partMaximum * 100;
        public static float Exposure(RiftMove m) => m == RiftMove.DoubleTeleport ? .5f : m == RiftMove.Slash ? .9f : m == RiftMove.Approach ? .8f : 1f;
        static double Discount(float wait) => Math.Pow(.85, Math.Max(0, wait));
        static RiftFightState Advance(RiftFightState s, float t) { s.cannonIn = Math.Max(0, s.cannonIn - t); s.bladeIn = Math.Max(0, s.bladeIn - t); s.teleportIn = Math.Max(0, s.teleportIn - t); return s; }
        static RiftFightState Closer(RiftFightState s) { s.band = s.band == RiftRangeBand.Far ? RiftRangeBand.Mid : RiftRangeBand.Close; return s; }
        static RiftRangeBand Shift(RiftRangeBand b, MotionResponse r) =>
            r == MotionResponse.BackOff ? (b == RiftRangeBand.Close ? RiftRangeBand.Mid : RiftRangeBand.Far) :
            r == MotionResponse.CloseIn ? (b == RiftRangeBand.Far ? RiftRangeBand.Mid : RiftRangeBand.Close) : b;

        // "Cannon -> they likely strafe left (41%) -> then double teleport": the most probable branch of the chosen move
        // and the best follow-up in it.
        string Describe(RiftFightState s, RiftMove first)
        {
            if (first == RiftMove.Approach || first == RiftMove.Hold) return first.ToString();
            var kind = Kind(first);
            MotionResponse likely = MotionResponse.Stay; double best = -1; RiftFightState after = s;
            foreach (var (p, _, next) in OutcomesWithResponse(s, first))
                if (p.p > best) { best = p.p; likely = p.r; after = next; }
            var follow = Expectimax.Decide(this, after, 1);
            return $"{first} -> they likely {likely} ({best:P0}) -> then {(follow.HasAction ? follow.Best.ToString() : "nothing")}";
        }

        IEnumerable<((double p, MotionResponse r), double reward, RiftFightState next)> OutcomesWithResponse(RiftFightState s, RiftMove move)
        {
            int i = 0;
            foreach (var o in Outcomes(s, move)) yield return ((o.probability, Responses[i++]), o.reward, o.next);
        }
    }
}
