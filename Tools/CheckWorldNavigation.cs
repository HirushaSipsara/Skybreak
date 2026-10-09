using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Skybreak.PhysicsInteraction;
public static class CheckWorldNavigation
{
    static string Dir=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/WorldFinish");
    static NavMeshQueryFilter Filter=>new NavMeshQueryFilter{agentTypeID=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>().agentTypeID,areaMask=NavMesh.AllAreas};
    static Vector3 Sample(Vector3 p){if(!NavMesh.SamplePosition(p,out var hit,1.1f,Filter)||Mathf.Abs(hit.position.y-p.y)>.4f)throw new Exception("No ground navigation at "+p);return hit.position;}
    static NavMeshPath Route(Vector3 a,Vector3 b){var path=new NavMeshPath();NavMesh.CalculatePath(Sample(a),Sample(b),Filter,path);return path;}
    static void DoorPaths(bool open){foreach(var d in UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>()){
        var west=d.transform.position.x<40;var a=new Vector3(west?19:67,0,west?54:46);var b=new Vector3(west?27:59,0,west?54:46);var path=Route(a,b);
        if((path.status==NavMeshPathStatus.PathComplete)!=open)throw new Exception(d.name+" expected open="+open+" path="+path.status);
        if(open&&!d.IsOpen||!open&&!d.IsClosed)throw new Exception("Gate animation not finished "+d.name);
    }}
    public static string ClosedThenOpen(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode");DoorPaths(false);var notes=new List<string>{"Closed gates: 2/2 reject complete paths from their holding room to the hall."};
        var route=new[]{new Vector3(0,0,-4),new Vector3(0,0,16),new Vector3(21,0,21),new Vector3(21,0,35),new Vector3(9,0,35),new Vector3(9,0,46),new Vector3(42,0,50),new Vector3(42,0,66),new Vector3(42,0,84.5f)};
        int corners=0;
        for(int i=0;i<route.Length-1;i++){var path=Route(route[i],route[i+1]);if(path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Main route blocked "+i+": "+path.status);corners+=path.corners.Length;}
        notes.Add("Main route: "+(route.Length-1)+"/"+(route.Length-1)+" legs complete through court, winding passage, security hall and boss chamber; "+corners+" total path corners.");
        int checkedCover=0;foreach(var col in GameObject.Find("09 / Dungeon decoration").GetComponentsInChildren<Collider>().Where(c=>c.bounds.min.y<.15f&&c.bounds.size.y>.55f&&c.bounds.size.x>.6f&&c.bounds.size.z>.6f)){
            var p=col.bounds.center;p.y=.05f;if(NavMesh.SamplePosition(p,out var hit,.08f,Filter))throw new Exception("Navigation crosses solid cover "+col.name+" "+hit.position);checkedCover++;
        }
        notes.Add("Solid ground-level cover centres excluded: "+checkedCover+".");File.WriteAllLines(Path.Combine(Dir,"NavigationChecks.txt"),notes);
        foreach(var door in UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>())door.Open();return string.Join("\n",notes)+"\nGates opening for next check.";
    }
    public static string OpenThenClose(){DoorPaths(true);File.AppendAllText(Path.Combine(Dir,"NavigationChecks.txt"),"Open gates: 2/2 complete holding-room paths after the animated blocker lifts.\n");foreach(var d in UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>())d.Close();return "Both room paths open; gates closing for repeat validation.";}
    public static string ConfirmClosed(){DoorPaths(false);File.AppendAllText(Path.Combine(Dir,"NavigationChecks.txt"),"Reclosed gates: 2/2 room paths blocked again.\nNo permanent NavMeshAgents added; this validates route planning and live door carving, not final agent brains.\n");return "Both gates reclosed and navigation blocked again.";}
}
