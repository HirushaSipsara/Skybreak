using System;
namespace Skybreak.Warden.Core
{
    public enum Intent { Guard, Patrol, Investigate, Pursue, Intercept, Flank, Retreat, Attack, Disabled }
    public struct Situation
    {
        public bool Alive, Visible, InRange, HasInvestigation, HasFlank, HasRetreat, HasIntercept, CanPatrol;
        public double Health, Confidence, Concentration, ObjectiveDanger, DistanceCost, FlankQuality, RetreatQuality;
    }
    [Serializable]
    public sealed class DecisionWeights
    {
        public double GuardBase=.20, GuardDanger=.38, PatrolBase=.32;
        public double InvestigateConfidence=.58, InvestigateInformation=.16, TravelPenalty=.12;
        public double PursueBase=.60, PursueConfidence=.18;
        public double InterceptBase=.58, InterceptDanger=.40;
        public double FlankBase=.52, FlankQuality=.32;
        public double RetreatBase=.65, RetreatNeed=.35;
        public double AttackBase=.82, AttackHealth=.08;
        public double SwitchMargin=.08, CommitmentSeconds=.65;
    }
    public sealed class Decision
    {
        public readonly double[] Scores=new double[9];
        public Intent Current {get;private set;}=Intent.Guard;
        public double ChangedAt {get;private set;}=double.NegativeInfinity;
        public string Reason {get;private set;}="Guarding";
        readonly DecisionWeights weights;
        public Decision(DecisionWeights weights=null) {this.weights=weights??new DecisionWeights();}
        static double C(double v)=>WorldGraph.Finite(v)?Math.Max(0,Math.Min(1,v)):0;
        public Intent Choose(Situation s,double now)
        {
            for(int i=0;i<Scores.Length;i++)Scores[i]=double.NegativeInfinity;
            if(!s.Alive){Current=Intent.Disabled;Reason="Defeated";return Current;}
            var w=weights;
            double confidence=C(s.Confidence),danger=C(s.ObjectiveDanger),health=C(s.Health),cost=C(s.DistanceCost);
            Scores[(int)Intent.Guard]=w.GuardBase+w.GuardDanger*danger;
            if(s.CanPatrol)Scores[(int)Intent.Patrol]=w.PatrolBase*(1-confidence);
            if(s.HasInvestigation&&confidence>.08)
                Scores[(int)Intent.Investigate]=w.InvestigateConfidence*confidence+w.InvestigateInformation*(1-C(s.Concentration))-w.TravelPenalty*cost;
            if(s.Visible)Scores[(int)Intent.Pursue]=w.PursueBase+w.PursueConfidence*confidence-w.TravelPenalty*cost;
            if(s.HasIntercept&&danger>.25)Scores[(int)Intent.Intercept]=w.InterceptBase+w.InterceptDanger*danger;
            if(s.HasFlank&&confidence>.25)
                Scores[(int)Intent.Flank]=w.FlankBase+w.FlankQuality*C(s.FlankQuality)-w.TravelPenalty*cost;
            if(s.HasRetreat&&health<.35&&confidence>.08)
                Scores[(int)Intent.Retreat]=w.RetreatBase+w.RetreatNeed*(1-health)+.05*C(s.RetreatQuality);
            if(s.Visible&&s.InRange)Scores[(int)Intent.Attack]=w.AttackBase+w.AttackHealth*health;
            int best=0;
            for(int i=1;i<(int)Intent.Disabled;i++)if(Scores[i]>Scores[best])best=i;
            bool feasible=!double.IsNegativeInfinity(Scores[(int)Current]);
            bool urgent=(Intent)best==Intent.Retreat || ((Intent)best==Intent.Intercept&&danger>.8);
            // Invalid actions are never retained for hysteresis. Urgent changes bypass commitment.
            if(feasible&&!urgent&&best!=(int)Current&&
               (now-ChangedAt<w.CommitmentSeconds||Scores[best]<Scores[(int)Current]+w.SwitchMargin))best=(int)Current;
            if(Current!=(Intent)best){Current=(Intent)best;ChangedAt=now;}
            Reason=Current+" selected at "+Scores[best].ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
            return Current;
        }
    }
}
