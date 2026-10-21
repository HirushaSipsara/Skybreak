using Skybreak.AI.Algorithms.Prediction;
using Skybreak.AI.Algorithms.Probability;
using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    // The four attack kinds the player can learn to answer (the core teleport + slam counts as Slam).
    public enum RiftAttackKind { Cannon, Slash, DoubleTeleport, Slam }

    // Layer 1 (model-based, Lecture 03) for an *opponent*: what this particular player tends to do.
    //   velocity          smoothed ground-plane velocity, used to lead shots (layer 2)
    //   preferred range   where the player likes to fight from
    //   strafe bias       -1 always circles to the boss's left ... +1 always to its right
    //   responses         per attack, a Dirichlet belief over Stay / StrafeLeft / StrafeRight / BackOff / CloseIn / Jump,
    //                     learned by watching what the player did between an attack's start and its impact
    //   part pressure     which arm the player has been shooting lately
    //   heals             when the player's health went up (someone healed them)
    public sealed class RiftPlayerModel
    {
        public const float VelocityTau = .25f, RangeAlpha = .05f, StrafeAlpha = .03f, ResponseForgetting = .9f, PressureWindow = 10;

        public Vector3 Velocity { get; private set; }
        public float PreferredRange { get; private set; } = 8;
        public float StrafeBias { get; private set; }
        public int ResponsesObserved { get; private set; }
        public float LastHealAt { get; private set; } = -100;
        public int HealsSeen { get; private set; }
        public RiftAttackKind? OpenAttack => open ? openKind : (RiftAttackKind?)null;
        public MotionResponse LastResponse { get; private set; }

        readonly DirichletCategorical[] responses =
        {
            new DirichletCategorical(6, 1, ResponseForgetting), new DirichletCategorical(6, 1, ResponseForgetting),
            new DirichletCategorical(6, 1, ResponseForgetting), new DirichletCategorical(6, 1, ResponseForgetting),
        };
        readonly float[] partDamage = new float[3];          // decayed damage per part: left arm, right arm, core
        float partDamageAt;
        Vector3 lastPosition; bool hasLast;
        float lastHealth = -1;
        bool open; RiftAttackKind openKind; Vector3 openFrom, openRadial; float openStartY, openMaxY;

        public void Reset()
        {
            Velocity = Vector3.zero; PreferredRange = 8; StrafeBias = 0; ResponsesObserved = 0; HealsSeen = 0; LastHealAt = -100;
            foreach (var r in responses) r.Clear();
            System.Array.Clear(partDamage, 0, partDamage.Length);
            hasLast = false; open = false; lastHealth = -1;
        }

        // Called every frame the player is visible.
        public void Observe(Vector3 player, Vector3 boss, float dt, float now)
        {
            if (hasLast && dt > 0)
            {
                Vector3 v = (player - lastPosition) / dt; v.y = 0;
                if (v.sqrMagnitude < 15 * 15)   // ignore teleports and respawns
                    Velocity += (float)TargetPrediction.SmoothingAlpha(dt, VelocityTau) * (v - Velocity);
            }
            lastPosition = player; hasLast = true;
            Vector3 toPlayer = player - boss; toPlayer.y = 0;
            float range = toPlayer.magnitude;
            PreferredRange += RangeAlpha * (range - PreferredRange) * Mathf.Clamp01(dt * 10);
            if (range > .5f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, toPlayer / range);     // the boss's right, looking at the player
                float lateral = Vector3.Dot(Velocity, right);
                if (Mathf.Abs(lateral) > .5f) StrafeBias += StrafeAlpha * (Mathf.Sign(lateral) - StrafeBias) * Mathf.Clamp01(dt * 10);
            }
            if (open) openMaxY = Mathf.Max(openMaxY, player.y);
        }

        // Player health watch: a rise means somebody healed them.
        public void ObserveHealth(float health, float now)
        {
            if (lastHealth >= 0 && health > lastHealth + .5f) { HealsSeen++; LastHealAt = now; }
            lastHealth = health;
        }

        public void BeginResponse(RiftAttackKind kind, Vector3 player, Vector3 boss)
        {
            open = true; openKind = kind; openFrom = player; openStartY = openMaxY = player.y;
            openRadial = player - boss; openRadial.y = 0;
            openRadial = openRadial.sqrMagnitude > .01f ? openRadial.normalized : Vector3.forward;
        }

        // Called once at the attack's impact: classifies what the player did since the attack started.
        public void EndResponse(RiftAttackKind kind, Vector3 player)
        {
            if (!open || kind != openKind) return;
            open = false;
            Vector3 moved = player - openFrom; moved.y = 0;
            Vector3 right = Vector3.Cross(Vector3.up, openRadial);
            LastResponse = TargetPrediction.Classify(Vector3.Dot(moved, openRadial), Vector3.Dot(moved, right), openMaxY - openStartY);
            responses[(int)kind].Observe((int)LastResponse);
            ResponsesObserved++;
        }

        public double ResponseProbability(RiftAttackKind kind, MotionResponse response) => responses[(int)kind].Probability((int)response);
        public double ResponseCount(RiftAttackKind kind) => responses[(int)kind].Total;
        public MotionResponse LikelyResponse(RiftAttackKind kind) => (MotionResponse)responses[(int)kind].MostLikely();

        public void ObservePartDamage(RiftPart part, float amount, float now)
        {
            DecayParts(now);
            partDamage[(int)part] += amount;
        }

        // Damage the player deals per second, from the decayed per-part totals (steady state: total = rate x window).
        public float DamageRate(float now) { DecayParts(now); return (partDamage[0] + partDamage[1] + partDamage[2]) / PressureWindow; }

        // Damage per part over roughly the last ten seconds (exponentially decayed).
        public float PartPressure(RiftPart part, float now) { DecayParts(now); return partDamage[(int)part]; }

        void DecayParts(float now)
        {
            float dt = now - partDamageAt; partDamageAt = now;
            if (dt <= 0) return;
            float k = Mathf.Exp(-dt / PressureWindow);
            for (int i = 0; i < partDamage.Length; i++) partDamage[i] *= k;
        }
    }
}
