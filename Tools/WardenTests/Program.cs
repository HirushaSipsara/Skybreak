using System;
using Skybreak.Warden.Core;
static class Program
{
    static int passed;
    static void Check(bool ok, string name) { if(!ok) throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
    static void Main()
    {
        var g=new WorldGraph(); g.Add(new Point(0,0,0)); g.Add(new Point(3,0,4)); g.Connect(0,1);
        Check(g.Neighbours(0)[0].Cost==5,"edge cost respects Euclidean lower bound");
        int v=g.Version; g.SetLink(0,1,false,8); Check(g.Version==v+1&&!g.Neighbours(1)[0].Open,"door change updates both directions");
        g.SetLink(0,1,false,8); Check(g.Version==v+1,"unchanged door does not invalidate routes");
        Check(new WorldGraph().Nearest(new Point())==-1,"empty graph is safe");
        SearchAndBeliefs();
        DecisionsAndTactics();
        RegressionAndStress();
        MovementAnimations();
        SyntaxChecks.Run();
        Console.WriteLine("TOTAL "+passed);
    }
    static void MovementAnimations()
    {
        Check(MotionChoice.Choose(true,false,false,false,0,0)==MotionClip.Idle,"blocked stationary actor uses idle despite a requested path");
        Check(MotionChoice.Choose(true,false,false,false,2.8,0)==MotionClip.Glide,"actual forward movement selects glide");
        Check(MotionChoice.Choose(true,false,false,false,1,-180)==MotionClip.TurnLeft&&MotionChoice.Choose(true,false,false,false,1,180)==MotionClip.TurnRight,"turn direction selects matching bank clip");
        Check(MotionChoice.Choose(true,false,false,true,2,0)==MotionClip.Retreat&&MotionChoice.Choose(true,false,false,true,0,0)==MotionClip.Idle,"retreat animation requires real movement");
        Check(MotionChoice.Choose(true,true,true,false,2,180)==MotionClip.Attack,"attack wind-up takes visual priority over hit and movement");
        Check(MotionChoice.Choose(false,true,true,true,2,180)==MotionClip.Death,"death overrides all animation states");
        Check(MotionChoice.Choose(true,false,true,false,2,0)==MotionClip.Hit,"non-attacking damage selects hit reaction");
        Check(MotionChoice.TryName("CEW_Rig|CEW_Glide",out var named)&&named==MotionClip.Glide&&!MotionChoice.TryName("__preview__",out _),"FBX take prefixes normalize and unrelated clips are rejected");
        Check(MotionChoice.Loops(MotionClip.Idle)&&!MotionChoice.Loops(MotionClip.Death)&&!MotionChoice.Loops(MotionClip.Attack),"locomotion loops while one-shot actions hold their final pose");
    }
    static void RegressionAndStress()
    {
        var random=new Random(87);
        for(int trial=0;trial<150;trial++)
        {
            var graph=new WorldGraph();
            for(int i=0;i<24;i++)graph.Add(new Point(i%6,0,i/6));
            for(int i=0;i<24;i++)
            {
                if(i%6<5)graph.Connect(i,i+1,random.NextDouble()*3,1);
                if(i<18)graph.Connect(i,i+6,random.NextDouble()*3,1);
            }
            for(int i=0;i<20;i++)
            {
                int a=random.Next(23),neighbour=a%6<5?a+1:a+6;
                if(graph.Valid(neighbour))graph.SetLink(a,neighbour,random.NextDouble()>.4,2);
            }
            int start=random.Next(24),goal=random.Next(24);
            double expected=TacticalSearch.TravelCosts(graph,start)[goal];
            var found=Search.AStar(graph,start,goal);
            if(double.IsInfinity(expected))
            {if(found.Status!=RouteStatus.Unreachable)throw new Exception("A* unreachable mismatch");}
            else if(found.Status!=RouteStatus.Found||Math.Abs(expected-found.Cost)>1e-8)
                throw new Exception("A* and uniform-cost disagree");
            if(found.Status==RouteStatus.Found)
                for(int k=1;k<found.Nodes.Length;k++)
                {
                    bool legal=false;
                    foreach(var e in graph.Neighbours(found.Nodes[k-1]))if(e.To==found.Nodes[k]&&e.Open)legal=true;
                    if(!legal)throw new Exception("Invalid route edge");
                }
        }
        Check(true,"150 weighted dynamic graphs: A* equals uniform-cost optimum and uses valid edges");
        var d=new Decision();
        var s=new Situation{Alive=true,Health=1,HasIntercept=true,ObjectiveDanger=1,Confidence=1};
        d.Choose(s,0);
        s.Visible=true;s.InRange=true;
        Check(d.Choose(s,.01)==Intent.Attack,"in-range defence interrupts interception even during commitment");
        s.Health=.1;s.HasRetreat=true;s.RetreatQuality=1;
        Check(d.Choose(s,.01)==Intent.Retreat,"critical retreat still overrides objective combat");
        var b=new BeliefMap(3);
        for(int i=0;i<1000;i++){b.Observe(new[]{1e-30,1.0,1e-30},i);b.ClearVisible(1);}
        double sum=b[0]+b[1]+b[2];
        Check(Math.Abs(sum-1)<1e-8&&!double.IsNaN(sum),"extreme repeated evidence remains finite");
        bool rejected=false;
        try{b.Observe(new[]{1.0,double.NaN,0},1);}catch(ArgumentException){rejected=true;}
        Check(rejected,"invalid sensor likelihood is rejected");
        var hysteresis=new Decision();
        var hs=new Situation{Alive=true,Health=1,CanPatrol=true};
        hysteresis.Choose(hs,0);hs.ObjectiveDanger=.45;
        Check(hysteresis.Choose(hs,.01)==Intent.Patrol,"small utility change respects commitment");
        hs.HasIntercept=true;hs.ObjectiveDanger=1;
        Check(hysteresis.Choose(hs,.02)==Intent.Intercept,"urgent alarm interrupts commitment");
        var g=new WorldGraph();
        for(int z=0;z<12;z++)for(int x=0;x<12;x++)g.Add(new Point(x*2,0,z*2));
        for(int i=0;i<144;i++){if(i%12<11)g.Connect(i,i+1);if(i<132)g.Connect(i,i+12);}
        var minds=new[]{new Decision(),new Decision(),new Decision(),new Decision()};
        var beliefs=new[]{new BeliefMap(144),new BeliefMap(144),new BeliefMap(144),new BeliefMap(144)};
        var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int tick=0;tick<300;tick++)for(int a=0;a<4;a++)
        {
            var loss=Search.AcousticLoss(g,a);
            beliefs[a].Predict(g,.25);
            beliefs[a].Observe(BeliefMap.SoundLikelihood(loss,30,10),tick*.25);
            var route=Search.AStar(g,a,beliefs[a].MostLikely,256);
            if(route.Status!=RouteStatus.Found)throw new Exception("Stress route failed");
            minds[a].Choose(new Situation{Alive=true,Health=1,Confidence=1,HasInvestigation=true},tick*.25);
        }
        watch.Stop();
        Check(true,"four independent cores complete 1200 combined hearing/decision/path cycles");
        Console.WriteLine("CORE TIMING: "+watch.Elapsed.TotalMilliseconds.ToString("0.0")+" ms / 1200 cycles; excludes Unity physics, rendering and other agents.");
    }
    static void DecisionsAndTactics()
    {
        var d=new Decision(); var s=new Situation{Alive=true,Health=1,CanPatrol=true};
        Check(d.Choose(s,0)==Intent.Patrol,"quiet patrol");
        s.Visible=true;s.InRange=true;s.Confidence=1;
        Check(d.Choose(s,1)==Intent.Attack,"visible in-range target permits attack");
        s.Visible=false;s.InRange=false;s.HasInvestigation=true;
        Check(d.Choose(s,1.01)==Intent.Investigate,"lost target cancels attack despite commitment");
        s.HasRetreat=true;s.Health=.1;s.RetreatQuality=1;
        Check(d.Choose(s,1.02)==Intent.Retreat,"critical health interrupts commitment");
        s.Alive=false;
        Check(d.Choose(s,1.03)==Intent.Disabled,"death overrides every utility");
        s=new Situation{Alive=true,Health=1,HasIntercept=true,ObjectiveDanger=1};
        Check(d.Choose(s,2)==Intent.Intercept,"objective alarm selects interception");
        s=new Situation{Alive=true,Health=1,HasInvestigation=true,Confidence=0,CanPatrol=true};
        Check(d.Choose(s,3)==Intent.Patrol,"expired evidence returns to patrol");
        var g=new WorldGraph();for(int i=0;i<6;i++)g.Add(new Point(i,0,0));
        for(int i=0;i<5;i++)g.Connect(i,i+1);
        var points=new[]{new TacticalPoint(true,1),new TacticalPoint(true,2),new TacticalPoint(true,1),
            new TacticalPoint(true,4),new TacticalPoint(true,8),new TacticalPoint(false,999)};
        Check(TacticalSearch.Climb(g,points,42,0,30)==4,"random restarts escape inferior local peak");
        Check(TacticalSearch.Climb(g,new TacticalPoint[6],1)==-1,"no feasible cover returns no target");
        Check(TacticalSearch.Climb(g,points,42,0,30)==TacticalSearch.Climb(g,points,42,0,30),"tactics deterministic with seeded randomness");
        g.SetLink(2,3,false,4);
        Check(double.IsPositiveInfinity(TacticalSearch.TravelCosts(g,0)[4]),"unreachable tactical points excluded by travel cost");
    }
    static void SearchAndBeliefs()
    {
        var g=new WorldGraph();
        foreach(var p in new[]{new Point(0,0,0),new Point(1,0,0),new Point(2,0,0),new Point(1,0,1)})g.Add(p);
        g.Connect(0,1,0,2); g.Connect(1,2,0,2); g.Connect(0,3,0,3); g.Connect(3,2,0,3);
        Check(Search.AStar(g,0,2).Cost==2,"A* shortest route");
        g.SetLink(1,2,false,10);
        Check(Search.AStar(g,0,2).Nodes[1]==3,"A* replans around closed door");
        Check(Search.AStar(g,0,2,1).Status==RouteStatus.BudgetExceeded,"budget exhaustion is distinct from no path");
        g.SetLink(3,2,false,10);
        Check(Search.AStar(g,0,2).Status==RouteStatus.Unreachable,"sealed goal returns unreachable");
        Check(Search.AStar(g,0,0).Nodes.Length==1,"start equals goal");
        Check(Search.AStar(g,-1,2).Status==RouteStatus.Invalid,"invalid endpoints");
        Check(Search.AcousticLoss(g,0)[2]==12,"sound takes least attenuated route through closed door");
        var b=new BeliefMap(4); b.Observe(new[]{.6,.2,.1,.1},1);
        Check(Math.Abs(b[0]-.6)<1e-9,"Bayesian update matches hand calculation");
        b.Observe(new[]{.01,.9,.05,.04},2);
        Check(b.MostLikely==1,"conflicting new evidence can overturn prior");
        b.See(2,3); b.ClearVisible(2);
        Check(b[2]<.98,"visible empty region reduces belief");
        for(int i=0;i<100;i++)b.Predict(g,.2);
        double sum=0; for(int i=0;i<4;i++)sum+=b[i];
        Check(Math.Abs(sum-1)<1e-9,"probabilities stay normalized after diffusion");
        Check(b.Confidence(100)<.001,"stale evidence expires");
        var likelihood=BeliefMap.SoundLikelihood(new[]{2.0,2.0,10.0},12,10);
        Check(likelihood[0]==likelihood[1]&&likelihood[2]<likelihood[0],"equal acoustic paths stay ambiguous");
        var single=new BeliefMap(1); single.See(0,0);
        Check(single[0]==1&&single.Concentration==1,"single-zone belief is safe");
    }
}
