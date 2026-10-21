using System;
using System.Collections.Generic;

namespace Skybreak.Warden.Core
{
    public enum RouteStatus { Found, Unreachable, BudgetExceeded, Invalid }
    public sealed class Route
    {
        public RouteStatus Status;
        public int[] Nodes=Array.Empty<int>();
        public double Cost=double.PositiveInfinity;
        public int Expanded;
    }
    internal sealed class MinQueue
    {
        struct Entry { public int Node; public double Priority; }
        readonly List<Entry> heap=new List<Entry>();
        public int Count=>heap.Count;
        public void Push(int node,double priority)
        {
            var e=new Entry{Node=node,Priority=priority}; heap.Add(e); int i=heap.Count-1;
            while(i>0) { int p=(i-1)/2; if(heap[p].Priority<=priority) break; heap[i]=heap[p]; i=p; }
            heap[i]=e;
        }
        public int Pop()
        {
            int result=heap[0].Node; var last=heap[heap.Count-1]; heap.RemoveAt(heap.Count-1);
            if(heap.Count==0)return result;
            int i=0;
            while(i*2+1<heap.Count) { int c=i*2+1; if(c+1<heap.Count&&heap[c+1].Priority<heap[c].Priority)c++;
                if(heap[c].Priority>=last.Priority)break; heap[i]=heap[c]; i=c; }
            heap[i]=last; return result;
        }
    }
    public static class Search
    {
        // Consistent h: every movement edge costs at least Euclidean distance.
        // Costs/topology are held constant for this bounded synchronous query.
        public static Route AStar(WorldGraph g,int start,int goal,int budget=512)
        {
            var result=new Route{Status=RouteStatus.Invalid};
            if(g==null||!g.Valid(start)||!g.Valid(goal)||budget<1)return result;
            var scores=new double[g.Count]; var parents=new int[g.Count]; var closed=new bool[g.Count];
            for(int i=0;i<g.Count;i++){scores[i]=double.PositiveInfinity; parents[i]=-1;}
            var open=new MinQueue(); scores[start]=0; open.Push(start,Point.Distance(g[start],g[goal]));
            while(open.Count>0)
            {
                int n=open.Pop(); if(closed[n])continue;
                if(result.Expanded>=budget){result.Status=RouteStatus.BudgetExceeded; return result;}
                result.Expanded++; closed[n]=true;
                if(n==goal)
                {
                    var path=new List<int>();
                    for(int p=n;p!=-1;p=parents[p])path.Add(p);
                    path.Reverse(); result.Nodes=path.ToArray(); result.Cost=scores[n]; result.Status=RouteStatus.Found; return result;
                }
                foreach(var e in g.Neighbours(n))
                {
                    if(!e.Open||closed[e.To])continue;
                    double tentative=scores[n]+e.Cost;
                    if(tentative>=scores[e.To])continue;
                    scores[e.To]=tentative; parents[e.To]=n;
                    open.Push(e.To,tentative+Point.Distance(g[e.To],g[goal]));
                }
            }
            result.Status=RouteStatus.Unreachable; return result;
        }
        // Closed doors retain attenuated sound links. They are impassable for movement.
        public static double[] AcousticLoss(WorldGraph g,int listener)
        {
            var loss=new double[g.Count]; var closed=new bool[g.Count];
            for(int i=0;i<loss.Length;i++)loss[i]=double.PositiveInfinity;
            if(!g.Valid(listener))return loss;
            var q=new MinQueue(); loss[listener]=0; q.Push(listener,0);
            while(q.Count>0)
            {
                int n=q.Pop(); if(closed[n])continue; closed[n]=true;
                foreach(var e in g.Neighbours(n))
                {
                    double next=loss[n]+e.SoundLoss;
                    if(next>=loss[e.To])continue;
                    loss[e.To]=next; q.Push(e.To,next);
                }
            }
            return loss;
        }
    }
}
