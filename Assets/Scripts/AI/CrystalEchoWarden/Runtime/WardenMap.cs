using System;
using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    public sealed class WardenMap : MonoBehaviour
    {
        [Serializable] public struct Connection { public int a,b; public float extraCost; }
        public Vector3[] nodes=Array.Empty<Vector3>();
        public Connection[] connections=Array.Empty<Connection>();
        public LayerMask environmentMask=~(1<<8);
        [Min(.1f)] public float clearanceRadius=.7f;
        public WorldGraph Graph {get;private set;}
        float nextRefresh;
        public string ValidationError {get;private set;}
        public void Build()
        {
            try {
                if(nodes.Length==0||nodes.Length>256)throw new ArgumentException("Use 1 to 256 navigation nodes");
                var g=new WorldGraph();
                foreach(var p in nodes)g.Add(ToPoint(p));
                foreach(var e in connections)g.Connect(e.a,e.b,e.extraCost,Math.Max(.1,Vector3.Distance(nodes[e.a],nodes[e.b])*.8));
                Graph=g; ValidationError=null; Refresh();
            } catch(Exception e) { Graph=null; ValidationError=e.Message; Debug.LogError("Warden graph: "+e.Message,this); }
        }
        void Awake(){ if(nodes.Length>0)Build(); }
        void Update(){if(Graph!=null&&Time.time>=nextRefresh){nextRefresh=Time.time+.4f;Refresh();}}
        public void Refresh()
        {
            if(Graph==null)return;
            foreach(var e in connections)
            {
                bool open=ClearSegment(nodes[e.a],nodes[e.b]);
                Graph.SetLink(e.a,e.b,open,Math.Max(.1,Vector3.Distance(nodes[e.a],nodes[e.b])*.8)+(open?0:12));
            }
        }
        public bool ClearSegment(Vector3 from,Vector3 to)
        {
            Vector3 offset=Vector3.up*.2f, delta=to-from;
            if(Physics.CheckSphere(from+offset,clearanceRadius,environmentMask,QueryTriggerInteraction.Ignore) ||
               Physics.CheckSphere(to+offset,clearanceRadius,environmentMask,QueryTriggerInteraction.Ignore))return false;
            return delta.sqrMagnitude<.0001f || !Physics.SphereCast(from+offset,clearanceRadius,delta.normalized,delta.magnitude,environmentMask,QueryTriggerInteraction.Ignore);
        }
        public int AccessibleNode(Vector3 from)
        {
            if(Graph==null)return -1;
            int best=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<nodes.Length;i++)
            {
                float d=(nodes[i]-from).sqrMagnitude;
                if(d<distance&&ClearSegment(from,nodes[i])){best=i;distance=d;}
            }
            return best;
        }
        public static Point ToPoint(Vector3 p)=>new Point(p.x,p.y,p.z);
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan;
            foreach(var n in nodes)Gizmos.DrawWireSphere(n,.12f);
            foreach(var e in connections)if(e.a>=0&&e.a<nodes.Length&&e.b>=0&&e.b<nodes.Length)Gizmos.DrawLine(nodes[e.a],nodes[e.b]);
        }
    }
}
