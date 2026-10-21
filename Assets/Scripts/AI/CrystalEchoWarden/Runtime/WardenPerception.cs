using UnityEngine;
using Skybreak.Combat;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    public sealed class WardenPerception : MonoBehaviour
    {
        public WardenMap map;
        public WardenNoiseBus noise;
        public DamageSink target;
        public float sightRange=16,fieldOfView=115;
        public Vector3 eyeOffset=new Vector3(0,.65f,0);
        public BeliefMap Belief {get;private set;}
        public bool Visible {get;private set;}
        public Vector3 LastSeen {get;private set;}
        public int HeardCount {get;private set;}
        public string Evidence {get;private set;}="No evidence";
        public Vector3 Eye=>transform.TransformPoint(eyeOffset);
        long processedNoise;
        float lastTick;
        public bool CanSeeNow()
        {
            if(map==null||target==null||!target.Alive||!target.isActiveAndEnabled)return false;
            Vector3 delta=target.AimPoint-Eye;
            return delta.magnitude<=sightRange&&Vector3.Angle(transform.forward,delta)<=fieldOfView*.5f&&
                !Physics.Linecast(Eye,target.AimPoint,map.environmentMask,QueryTriggerInteraction.Ignore);
        }
        public void Tick()
        {
            if(map==null||map.Graph==null)return;
            if(Belief==null||Belief.Count!=map.Graph.Count){Belief=new BeliefMap(map.Graph.Count);lastTick=Time.time;}
            Belief.Predict(map.Graph,Time.time-lastTick);lastTick=Time.time;
            // Sound bus origin is used only to simulate amplitude at the sensor.
            // The posterior never receives that origin or its node ID.
            if(noise!=null)
            {
                int listener=map.Graph.Nearest(WardenMap.ToPoint(transform.position));
                double[] loss=null;
                foreach(var e in noise.Events)
                {
                    if(e.Id<=processedNoise)continue;
                    processedNoise=e.Id;
                    if(Time.time-e.Time>1)continue;
                    if(loss==null)loss=Search.AcousticLoss(map.Graph,listener);
                    int source=map.Graph.Nearest(WardenMap.ToPoint(e.Origin));
                    if(source<0)continue;
                    double received=e.Loudness-loss[source];
                    if(received<2)continue;
                    // Small deterministic sensor error; repeated runs remain explainable.
                    received+=((e.Id%5)-2)*.12;
                    Belief.Observe(BeliefMap.SoundLikelihood(loss,e.Loudness,received),Time.time);
                    HeardCount++;Evidence="Heard "+e.Kind+"; amplitude "+received.ToString("0.0");
                }
            }
            Visible=CanSeeNow();
            if(Visible)
            {
                LastSeen=target.AimPoint;
                Belief.See(map.Graph.Nearest(WardenMap.ToPoint(target.transform.position)),Time.time);
                Evidence="Direct sight";
            }
            else
            {
                // Clear only regions actually inside the cone and unoccluded.
                // Nodes approximate zones; this is imperfect negative evidence, not certainty.
                for(int i=0;i<map.nodes.Length;i++)
                {
                    Vector3 delta=map.nodes[i]-Eye;
                    if(delta.magnitude<sightRange&&Vector3.Angle(transform.forward,delta)<fieldOfView*.5f&&
                       !Physics.Linecast(Eye,map.nodes[i],map.environmentMask,QueryTriggerInteraction.Ignore))Belief.ClearVisible(i);
                }
            }
        }
    }
}
