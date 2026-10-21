using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak.Warden;

public static class ConfigureCrystalPatrols
{
    const string Report="Docs/IS/CrystalEchoWarden/MainMap/OrbitChecks.txt";
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||!scene.path.EndsWith("SkybreakMain.unity"))throw new Exception("Open the main scene in Edit Mode.");
        var encounter=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        var map=encounter.map;
        if(encounter.wardens.Length!=4)throw new Exception("Expected four crystal guards.");
        // Rebuild the additions so this operation is safe to repeat.
        int originalCount=encounter.wardens.Where(w=>w.patrolRoute.Length>0).Select(w=>w.patrolRoute.Min()).DefaultIfEmpty(map.nodes.Length).Min();
        var nodes=map.nodes.Take(originalCount).Select(p=>new Vector3(p.x,1.95f,p.z)).ToList();
        var edges=map.connections.Where(e=>e.a<originalCount&&e.b<originalCount).ToList();
        Physics.SyncTransforms();
        for(int guard=0;guard<encounter.wardens.Length;guard++)
        {
            var w=encounter.wardens[guard];var center=w.objective.transform.position;center.y=1.95f;
            var ring=new Vector3[16];int start=nodes.Count;
            for(int i=0;i<ring.Length;i++)
            {
                float angle=i*Mathf.PI*2/ring.Length;
                ring[i]=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.8f;
            }
            for(int i=0;i<ring.Length;i++)
                if(!map.ClearSegment(ring[i],ring[(i+1)%ring.Length]))throw new Exception("Crystal "+(guard+1)+" orbit obstructed at "+i);
            nodes.AddRange(ring);
            for(int i=0;i<ring.Length;i++)
            {
                edges.Add(new WardenMap.Connection{a=start+i,b=start+(i+1)%ring.Length});
                for(int n=0;n<originalCount;n++)
                    if(Vector3.Distance(ring[i],nodes[n])<5.1f&&map.ClearSegment(ring[i],nodes[n]))
                        edges.Add(new WardenMap.Connection{a=start+i,b=n});
            }
            w.patrolRoute=Enumerable.Range(start,ring.Length).ToArray();
            // Only quiet patrol uses this loop; retain Sachini's authored movement speed.
            int first=guard*4;
            w.transform.position=ring[first];
            w.transform.rotation=Quaternion.LookRotation(ring[(first+1)%ring.Length]-ring[first]);
            w.GetComponent<WardenMotor>().speed=2.8f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(w);
            PrefabUtility.RecordPrefabInstancePropertyModifications(w.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(w.GetComponent<WardenMotor>());
        }
        if(nodes.Count>256)throw new Exception("Navigation node limit exceeded.");
        map.nodes=nodes.ToArray();map.connections=edges.ToArray();map.Build();
        if(map.Graph==null)throw new Exception(map.ValidationError);
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        string result="Four checked 16-point quiet patrol loops, radius 1.8m, root height 1.95m, speed 2.8m/s. Original utility selection, tactical goals and stationary charged attack retained. Graph: "+nodes.Count+" nodes.\n";
        File.WriteAllText(Report,result);return result;
    }
    public static string StartCheck()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
        var e=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        var driver=UnityEngine.Object.FindAnyObjectByType<Skybreak.LocalCombatLab.LocalCombatDriver>();
        driver.ResetLab();
        bool driverEnabled=driver.enabled,relayEnabled=driver.relay.enabled;
        driver.enabled=false;driver.relay.enabled=false;
        var ws=e.wardens;
        var angles=new float[4];var total=new float[4];var maxRadiusError=new float[4];var heightError=new float[4];
        var oldTargets=ws.Select(w=>w.Senses.target).ToArray();
        var oldNoise=ws.Select(w=>w.Senses.noise).ToArray();
        for(int i=0;i<4;i++){ws[i].Senses.target=null;ws[i].Senses.noise=null;var d=ws[i].transform.position-ws[i].objective.transform.position;angles[i]=Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg;}
        double end=EditorApplication.timeSinceStartup+20;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
            for(int i=0;i<4;i++)
            {
                var d=ws[i].transform.position-ws[i].objective.transform.position;
                float angle=Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg;
                total[i]+=Mathf.DeltaAngle(angles[i],angle);angles[i]=angle;
                maxRadiusError[i]=Mathf.Max(maxRadiusError[i],Mathf.Abs(new Vector2(d.x,d.z).magnitude-1.8f));
                heightError[i]=Mathf.Max(heightError[i],Mathf.Abs(ws[i].transform.position.y-1.95f));
            }
            if(EditorApplication.timeSinceStartup<end)return;
            EditorApplication.update-=tick;
            var lines=new List<string>();
            for(int i=0;i<4;i++)
            {
                bool pass=total[i]>360&&maxRadiusError[i]<.25f&&heightError[i]<.05f&&ws[i].Alive;
                lines.Add((pass?"PASS":"FAIL")+": crystal "+(i+1)+" travel="+total[i].ToString("F1")+" degrees; radius error="+maxRadiusError[i].ToString("F3")+"m; height error="+heightError[i].ToString("F3")+"m; "+ws[i].Status);
                ws[i].Senses.target=oldTargets[i];
                ws[i].Senses.noise=oldNoise[i];
            }
            driver.enabled=driverEnabled;driver.relay.enabled=relayEnabled;driver.ResetLab();
            File.AppendAllLines(Report,lines);
        };
        EditorApplication.update+=tick;
        return "Started 20-second live orbit check for all four Wardens.";
    }
}
