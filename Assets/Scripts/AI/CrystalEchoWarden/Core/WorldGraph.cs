using System;
using System.Collections.Generic;

namespace Skybreak.Warden.Core
{
    public readonly struct Point
    {
        public readonly double X, Y, Z;
        public Point(double x, double y, double z) { X=x; Y=y; Z=z; }
        public static double Distance(Point a, Point b) =>
            Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
    }

    // The node topology is authored once; links can change without rebuilding node IDs.
    public sealed class WorldGraph
    {
        public sealed class Link
        {
            public int To { get; }
            public double Cost { get; }
            public bool Open { get; internal set; }
            public double SoundLoss { get; internal set; }
            internal Link(int to, double cost, double loss) { To=to; Cost=cost; SoundLoss=loss; Open=true; }
        }
        readonly List<Point> points = new List<Point>();
        readonly List<List<Link>> edges = new List<List<Link>>();
        public int Count => points.Count;
        public int Version { get; private set; }
        public Point this[int i] => points[i];
        public IReadOnlyList<Link> Neighbours(int i) => edges[i];
        public bool Valid(int i) => i>=0 && i<Count;
        public int Add(Point p)
        {
            if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw new ArgumentException("Invalid node");
            points.Add(p); edges.Add(new List<Link>()); Version++; return Count-1;
        }
        public void Connect(int a, int b, double extraCost=0, double soundLoss=1)
        {
            if (!Valid(a) || !Valid(b) || a==b || extraCost<0 || !Finite(extraCost) || soundLoss<0 || !Finite(soundLoss))
                throw new ArgumentException("Invalid link");
            foreach(var e in edges[a]) if(e.To==b) throw new ArgumentException("Duplicate link");
            double cost=Point.Distance(points[a],points[b])+extraCost;
            if (cost<=0) throw new ArgumentException("Coincident nodes");
            edges[a].Add(new Link(b,cost,soundLoss)); edges[b].Add(new Link(a,cost,soundLoss)); Version++;
        }
        public void SetLink(int a, int b, bool open, double soundLoss)
        {
            if (!Valid(a)||!Valid(b)||!Finite(soundLoss)||soundLoss<0) throw new ArgumentException("Invalid link state");
            bool changed=false, found=false;
            foreach(var e in edges[a]) if(e.To==b) { found=true; changed|=e.Open!=open||e.SoundLoss!=soundLoss; e.Open=open; e.SoundLoss=soundLoss; }
            foreach(var e in edges[b]) if(e.To==a) { e.Open=open; e.SoundLoss=soundLoss; }
            if(!found) throw new ArgumentException("Missing link");
            if(changed) Version++;
        }
        public int Nearest(Point p)
        {
            int best=-1; double distance=double.PositiveInfinity;
            for(int i=0;i<Count;i++) { double d=Point.Distance(p,points[i]); if(d<distance){ best=i; distance=d; } }
            return best;
        }
        public static bool Finite(double x) => !double.IsNaN(x)&&!double.IsInfinity(x);
    }
}

