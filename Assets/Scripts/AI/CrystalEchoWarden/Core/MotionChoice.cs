using System;

namespace Skybreak.Warden.Core
{
    public enum MotionClip { Idle, Glide, TurnLeft, TurnRight, Retreat, Attack, Hit, Death }

    // Presentation selection only. This never selects a gameplay action or moves the actor.
    public static class MotionChoice
    {
        public static MotionClip Choose(bool alive, bool attack, bool hit, bool retreat, double speed, double turnRate)
        {
            if (!alive) return MotionClip.Death;
            if (attack) return MotionClip.Attack;
            if (hit) return MotionClip.Hit;
            if (!WorldGraph.Finite(speed)) speed = 0;
            if (!WorldGraph.Finite(turnRate)) turnRate = 0;
            if (speed > .08 && retreat) return MotionClip.Retreat;
            if (Math.Abs(turnRate) > 70) return turnRate < 0 ? MotionClip.TurnLeft : MotionClip.TurnRight;
            return speed > .08 ? MotionClip.Glide : MotionClip.Idle;
        }

        public static bool Loops(MotionClip clip) => clip != MotionClip.Attack && clip != MotionClip.Hit && clip != MotionClip.Death;

        public static bool TryName(string imported, out MotionClip clip)
        {
            clip = MotionClip.Idle;
            if (string.IsNullOrEmpty(imported)) return false;
            int marker = imported.LastIndexOf("CEW_", StringComparison.Ordinal);
            if (marker < 0) return false;
            return Enum.TryParse(imported.Substring(marker + 4), out clip) && Enum.IsDefined(typeof(MotionClip), clip);
        }
    }
}
