using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WardenMotor : MonoBehaviour
    {
        public WardenMap map;
        public float speed=2.8f;
        public int[] Path {get;private set;}=System.Array.Empty<int>();
        public RouteStatus LastResult {get;private set;}=RouteStatus.Invalid;
        public bool Blocked {get;private set;}
        public bool Arrived=>cursor>=Path.Length;
        int cursor,goal=-1,version=-1;
        float retryAt,stuckFor;
        CharacterController body;
        void Awake(){body=GetComponent<CharacterController>();}
        public void Stop(){Path=System.Array.Empty<int>();cursor=0;goal=-1;stuckFor=0;}
        public void Go(int node)
        {
            if(map==null||map.Graph==null||!map.Graph.Valid(node)){Stop();return;}
            if(node==goal&&version==map.Graph.Version&&!Blocked)return;
            if(Time.time<retryAt)return;
            int start=map.AccessibleNode(transform.position);
            var route=Search.AStar(map.Graph,start,node,256);
            LastResult=route.Status;goal=node;version=map.Graph.Version;Blocked=false;stuckFor=0;
            Path=route.Nodes;cursor=0;
            if(route.Status!=RouteStatus.Found){retryAt=Time.time+1;Blocked=true;}
        }
        void Update()
        {
            if(map==null||map.Graph==null||Arrived||body==null||!body.enabled)return;
            if(version!=map.Graph.Version){int destination=goal;Stop();Go(destination);return;}
            Vector3 destinationPoint=map.nodes[Path[cursor]];
            Vector3 delta=destinationPoint-transform.position;
            if(delta.magnitude<.16f){cursor++;return;}
            Vector3 old=transform.position;
            if(map.ClearSegment(old,destinationPoint))body.Move(delta.normalized*Mathf.Min(delta.magnitude,speed*Time.deltaTime));
            if((transform.position-old).sqrMagnitude<.000001f)stuckFor+=Time.deltaTime;else stuckFor=0;
            if(stuckFor>.65f)
            {
                Blocked=true;retryAt=Time.time+.8f;Path=System.Array.Empty<int>();cursor=0;
                map.Refresh();
            }
            Face(delta);
        }
        public void Face(Vector3 direction)
        {
            direction.y=0;
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),220*Time.deltaTime);
        }
        void OnDisable(){Stop();}
    }
}
