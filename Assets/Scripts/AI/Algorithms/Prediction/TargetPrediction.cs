using System;

namespace Skybreak.AI.Algorithms.Prediction
{
    // How a moving target answered an attack, judged from the attacker's point of view.
    public enum MotionResponse { Stay, StrafeLeft, StrafeRight, BackOff, CloseIn, Jump }

    public static class TargetPrediction
    {
        // Exponential smoothing factor for a time step dt and time constant tau: x <- x + alpha * (sample - x).
        // Using 1 - e^(-dt/tau) makes the smoothing independent of the frame rate.
        public static double SmoothingAlpha(double dt, double tau) => tau <= 0 ? 1 : 1 - Math.Exp(-dt / tau);

        // Constant-velocity lead on the ground plane: where the target will be after 'seconds', scaled by
        // kappa (0 = aim at the current position, 1 = full lead) and capped at maxLead metres so a sudden
        // change of direction always escapes.
        public static (double x, double z) Lead(double px, double pz, double vx, double vz, double seconds, double kappa, double maxLead)
        {
            double dx = vx * seconds * kappa, dz = vz * seconds * kappa;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length > maxLead && length > 0) { dx *= maxLead / length; dz *= maxLead / length; }
            return (px + dx, pz + dz);
        }

        // Classifies a displacement during an attack window. radial = movement away from the attacker (+) or towards it (-);
        // lateral = movement to the attacker's right (+) or left (-); rise = highest height above the starting height.
        public static MotionResponse Classify(double radial, double lateral, double rise, double stayThreshold = .8, double jumpThreshold = .6)
        {
            if (rise > jumpThreshold) return MotionResponse.Jump;
            if (Math.Sqrt(radial * radial + lateral * lateral) < stayThreshold) return MotionResponse.Stay;
            if (Math.Abs(radial) >= Math.Abs(lateral)) return radial > 0 ? MotionResponse.BackOff : MotionResponse.CloseIn;
            return lateral > 0 ? MotionResponse.StrafeRight : MotionResponse.StrafeLeft;
        }
    }
}
