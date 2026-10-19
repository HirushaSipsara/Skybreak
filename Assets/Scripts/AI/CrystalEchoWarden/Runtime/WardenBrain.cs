using UnityEngine;
using Skybreak.Combat;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    [RequireComponent(typeof(WardenMotor),typeof(WardenPerception))]
    public sealed class WardenBrain : DamageSink
    {
        public WardenMap map;
        public WardenObjective objective;
        public float maximumHealth=90,attackRange=8,attackDamage=10,attackCooldown=1.8f,windupSeconds=.6f;
        public DecisionWeights weights=new DecisionWeights();
        public bool showDebug=true;
        public override Allegiance Team=>Allegiance.Hostile;
        public override float Health=>health;
        public override float MaximumHealth=>maximumHealth;
        public override Vector3 AimPoint=>transform.position+Vector3.up*.25f;
        public Decision Mind {get;private set;}
        public int Goal {get;private set;}=-1;
        public int Shots {get;private set;}
        public bool WindingUp=>windupAt>=0;
        public double LastTickMilliseconds {get;private set;}
        public string Status {get;private set;}="Waiting for navigation";
        public WardenPerception Senses {get;private set;}
        public WardenMotor Motor {get;private set;}
        public event System.Action<Vector3,Vector3> Fired;
        float health,nextThink,attackReady,windupAt=-1;
        int home=-1,patrolCursor,planSeed=17;
        double[] travel;
        void Awake()
        {
            health=maximumHealth;Mind=new Decision(weights);
            Senses=GetComponent<WardenPerception>();Motor=GetComponent<WardenMotor>();
            nextThink=Time.time+(Mathf.Abs(GetInstanceID())%5)*.04f;
        }
        public override void ReceiveDamage(float amount,Transform source)
        {
            if(!Alive||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return;
            health=Mathf.Max(0,health-amount);
            if(!Alive)
            {
                Motor.Stop();windupAt=-1;
                Mind.Choose(new Situation{Alive=false},Time.time);
                Status="Defeated";
            }
            // Damage is not an exact-location sensor; a gunshot event supplies hearing.
        }
        void Update()
        {
            if(!Alive)return;
            if(Time.time>=nextThink)
            {
                nextThink=Time.time+.25f;
                var watch=System.Diagnostics.Stopwatch.StartNew();
                Think();
                watch.Stop();LastTickMilliseconds=watch.Elapsed.TotalMilliseconds;
            }
            ResolveAttack();
            if(Mind.Current==Intent.Guard||Mind.Current==Intent.Patrol&&Motor.Arrived)
                transform.Rotate(0,30*Time.deltaTime,0);
        }
        public void Think()
        {
            if(!Alive){Motor.Stop();return;}
            if(map==null||map.Graph==null){Motor.Stop();Status="Navigation unavailable";return;}
            Motor.map=map;Senses.map=map;Senses.Tick();
            int here=map.AccessibleNode(transform.position);
            if(here<0){Motor.Stop();Status="No collision-free connection to graph";return;}
            if(home<0)home=here;
            travel=TacticalSearch.TravelCosts(map.Graph,here);
            var belief=Senses.Belief;
            int investigate=-1;double highest=-1;
            for(int i=0;i<travel.Length;i++)
                if(WorldGraph.Finite(travel[i])&&belief[i]>highest){highest=belief[i];investigate=i;}
            int likely=belief.MostLikely;
            Vector3 threat=Senses.Visible?Senses.LastSeen:map.nodes[likely];
            double confidence=belief.Confidence(Time.time);
            int guard=home;
            if(objective!=null&&objective.Available)
            {
                int node=map.Graph.Nearest(WardenMap.ToPoint(objective.transform.position));
                if(WorldGraph.Finite(travel[node]))guard=node;
            }
            float danger=objective!=null?objective.Danger:0;
            // Only observed player proximity can raise objective danger.
            if(objective!=null&&objective.Available&&Senses.Visible)
                danger=Mathf.Max(danger,1-Mathf.Clamp01(Vector3.Distance(threat,objective.transform.position)/9));
            int flank=-1,retreat=-1;double flankValue=0,retreatValue=0;
            if(confidence>.08)
            {
                var flanks=new TacticalPoint[map.Graph.Count];var retreats=new TacticalPoint[map.Graph.Count];
                Vector3 approach=transform.position-threat;approach.y=0;
                for(int i=0;i<map.nodes.Length;i++)
                {
                    Vector3 p=map.nodes[i];float range=Vector3.Distance(p,threat);
                    bool reachable=WorldGraph.Finite(travel[i])&&map.ClearSegment(p,p);
                    bool covered=Physics.Linecast(p+Vector3.up*.4f,threat,map.environmentMask,QueryTriggerInteraction.Ignore);
                    double side=Mathf.Clamp01(Vector3.Angle(approach,p-threat)/90);
                    double cheap=1-Mathf.Clamp01((float)travel[i]/25);
                    double quality=.4*side+.3*(covered?1:0)+.2*(1-Mathf.Clamp01(Mathf.Abs(range-6)/6))+.1*cheap;
                    flanks[i]=new TacticalPoint(reachable&&range>=3&&range<=11&&side>.35&&travel[i]>.7,quality);
                    retreats[i]=new TacticalPoint(reachable&&covered&&range>=4,
                        .6+.25*Mathf.Clamp01(range/15)+.15*cheap);
                }
                flank=TacticalSearch.Climb(map.Graph,flanks,planSeed++,Goal);
                retreat=TacticalSearch.Climb(map.Graph,retreats,planSeed++,Goal);
                if(flank>=0)flankValue=flanks[flank].Value;
                if(retreat>=0)retreatValue=retreats[retreat].Value;
            }
            int intercept=-1;
            if(danger>.25&&objective!=null&&objective.Available)
            {
                // Predict a route from the most likely zone, never from the hidden live Transform.
                var predicted=Search.AStar(map.Graph,likely,guard,256);
                double best=double.NegativeInfinity, elapsed=0;
                for(int k=0;k<predicted.Nodes.Length;k++)
                {
                    int n=predicted.Nodes[k];
                    if(k>0)elapsed+=Point.Distance(map.Graph[predicted.Nodes[k-1]],map.Graph[n])/5;
                    if(!WorldGraph.Finite(travel[n]))continue;
                    double interceptScore=elapsed-travel[n]/Mathf.Max(.1f,Motor.speed);
                    if(interceptScore>best){best=interceptScore;intercept=n;}
                }
                if(intercept<0)intercept=guard;
            }
            bool visible=Senses.Visible;
            float distance=visible?Vector3.Distance(Senses.Eye,Senses.LastSeen):float.PositiveInfinity;
            var situation=new Situation {
                Alive=Alive, Health=health/Mathf.Max(1,maximumHealth),Visible=visible,InRange=visible&&distance<=attackRange,
                Confidence=confidence,Concentration=belief.Concentration,ObjectiveDanger=danger,
                HasInvestigation=investigate>=0,HasFlank=flank>=0,HasRetreat=retreat>=0,HasIntercept=intercept>=0,
                FlankQuality=flankValue,RetreatQuality=retreatValue,CanPatrol=true,
                DistanceCost=investigate>=0?Mathf.Clamp01((float)travel[investigate]/25):1
            };
            var intent=Mind.Choose(situation,Time.time);
            int selected=guard;
            switch(intent)
            {
                case Intent.Attack:
                    Motor.Stop();Goal=-1;Status=Mind.Reason;return;
                case Intent.Pursue:
                    selected=map.Graph.Nearest(WardenMap.ToPoint(Senses.LastSeen));break;
                case Intent.Investigate: selected=investigate;break;
                case Intent.Flank: selected=flank;break;
                case Intent.Retreat: selected=retreat;break;
                case Intent.Intercept: selected=intercept;break;
                case Intent.Patrol:
                    if(Goal<0||Motor.Arrived||Motor.Blocked)
                    {
                        for(int k=0;k<map.nodes.Length;k++)
                        {
                            patrolCursor=(patrolCursor+1)%map.nodes.Length;
                            if(WorldGraph.Finite(travel[patrolCursor])&&travel[patrolCursor]>1&&Vector3.Distance(map.nodes[patrolCursor],map.nodes[guard])<9)break;
                        }
                        selected=patrolCursor;
                    }else selected=Goal;
                    break;
            }
            if(selected<0||!WorldGraph.Finite(travel[selected]))selected=guard;
            Goal=selected;Motor.Go(selected);
            Status=Mind.Reason+" / node "+selected+" / "+Motor.LastResult;
            // Face the suspected region at a search destination and let real vision clear it.
            if(Motor.Arrived&&intent==Intent.Investigate)transform.Rotate(0,45,0);
        }
        void ResolveAttack()
        {
            bool valid=Mind.Current==Intent.Attack&&Senses.CanSeeNow()&&
                Vector3.Distance(Senses.Eye,Senses.target.AimPoint)<=attackRange;
            if(!valid){windupAt=-1;return;}
            Motor.Face(Senses.target.AimPoint-transform.position);
            if(Time.time<attackReady)return;
            if(windupAt<0){windupAt=Time.time;return;}
            if(Time.time-windupAt<windupSeconds)return;
            // Recheck exact ray at discharge. No damage through cover, no queued shots after death.
            Vector3 from=Senses.Eye,to=Senses.target.AimPoint;
            if(!Physics.Linecast(from,to,map.environmentMask,QueryTriggerInteraction.Ignore))
            {
                // Actors can intercept the beam. Never shoot through a teammate.
                if(Physics.Raycast(from,(to-from).normalized,out var hit,(to-from).magnitude+.2f,~0,QueryTriggerInteraction.Ignore))
                {
                    var receiver=hit.collider.GetComponentInParent<DamageSink>();
                    if(receiver==Senses.target){receiver.ReceiveDamage(attackDamage,transform);Shots++;Fired?.Invoke(from,to);}
                }
            }
            attackReady=Time.time+attackCooldown;windupAt=-1;
        }
        void OnDrawGizmosSelected()
        {
            if(map==null||Senses==null||Senses.Belief==null)return;
            for(int i=0;i<map.nodes.Length;i++)
            {
                Gizmos.color=Color.Lerp(new Color(0,.2f,.3f),Color.yellow,(float)Senses.Belief[i]);
                Gizmos.DrawSphere(map.nodes[i],.08f+(float)Senses.Belief[i]*.6f);
            }
            if(Motor!=null)
            {
                Gizmos.color=Color.cyan;
                for(int i=1;i<Motor.Path.Length;i++)Gizmos.DrawLine(map.nodes[Motor.Path[i-1]],map.nodes[Motor.Path[i]]);
            }
        }
        protected override void OnDisable(){base.OnDisable();if(Motor!=null)Motor.Stop();windupAt=-1;}
    }
}
