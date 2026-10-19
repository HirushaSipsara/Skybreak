using System;
using System.Collections.Generic;
namespace Skybreak.Warden.Core
{
    public readonly struct TacticalPoint
    {
        public readonly bool Feasible;
        public readonly double Value;
        public TacticalPoint(bool feasible,double value){Feasible=feasible&&WorldGraph.Finite(value);Value=value;}
    }
    public static class TacticalSearch
    {
        // Bounded random-restart hill climbing. Strict improvement prevents plateaus/cycles.
        // Candidate feasibility includes clearance, reachability and tactical role.
        // No claim of a global optimum: it can miss an unsampled basin.
        public static int Climb(WorldGraph graph,TacticalPoint[] points,int seed,int preferred=-1,int restarts=4,int maxSteps=24)
        {
            if(points.Length!=graph.Count)throw new ArgumentException("Candidate size");
            var feasible=new List<int>();
            for(int i=0;i<points.Length;i++)if(points[i].Feasible)feasible.Add(i);
            if(feasible.Count==0)return -1;
            var random=new Random(seed); int best=-1;
            for(int trial=0;trial<Math.Max(1,restarts);trial++)
            {
                int n=trial==0&&graph.Valid(preferred)&&points[preferred].Feasible?preferred:feasible[random.Next(feasible.Count)];
                for(int step=0;step<maxSteps;step++)
                {
                    int next=n;
                    foreach(var edge in graph.Neighbours(n))
                        if(edge.Open&&points[edge.To].Feasible&&points[edge.To].Value>points[next].Value+1e-9)next=edge.To;
                    if(next==n)break;
                    n=next;
                }
                if(best<0||points[n].Value>points[best].Value)best=n;
            }
            return best;
        }
        public static double[] TravelCosts(WorldGraph g,int start)
        {
            var costs=new double[g.Count]; var done=new bool[g.Count];
            for(int i=0;i<costs.Length;i++)costs[i]=double.PositiveInfinity;
            if(!g.Valid(start))return costs;
            costs[start]=0;var q=new MinQueue();q.Push(start,0);
            while(q.Count>0)
            {
                int n=q.Pop();if(done[n])continue;done[n]=true;
                foreach(var e in g.Neighbours(n))if(e.Open&&costs[n]+e.Cost<costs[e.To])
                {costs[e.To]=costs[n]+e.Cost;q.Push(e.To,costs[e.To]);}
            }
            return costs;
        }
    }
}
