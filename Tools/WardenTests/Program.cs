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
        SyntaxChecks.Run();
        Console.WriteLine("TOTAL "+passed);
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
