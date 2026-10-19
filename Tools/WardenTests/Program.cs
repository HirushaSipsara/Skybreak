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
        Console.WriteLine("TOTAL "+passed);
    }
}
