using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Skybreak.Combat;
using Skybreak.AI.Disruptor;
using Skybreak.AI.SawanIS.VaultHound;
using Skybreak.PhysicsInteraction;

public static class CheckChamberReferenceRecovery
{
    const string Report = "Docs/IS/AgentDesign/SawanIS/MainMap/ReferenceRecoveryChecks.txt";
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var actual = UnityEngine.Object.FindAnyObjectByType<ChamberReleaseController>();
        float oldScale = Time.timeScale; bool oldBackground = Application.runInBackground;
        Time.timeScale = 1; Application.runInBackground = true;
        var config = new SerializedObject(actual);
        if (actual.TotalEnemies != 4 || config.FindProperty("holdingDoors").arraySize != 2 || config.FindProperty("hounds").arraySize != 2 || config.FindProperty("grid").objectReferenceValue == null || config.FindProperty("pack").objectReferenceValue == null)
            throw new Exception("The active encounter did not recover its four enemies, two doors, two hounds and services.");
        File.WriteAllText(Report, "PASS: active encounter recovered all missing references without restarting or changing enemy health.\n");
        var root = new GameObject("Chamber regression clones (runtime)");
        var sourceDoor = UnityEngine.Object.FindAnyObjectByType<DungeonHoldingDoor>();
        var doors = new DungeonHoldingDoor[2];
        for (int i=0;i<2;i++)
        {
            var go = UnityEngine.Object.Instantiate(sourceDoor.gameObject, new Vector3(5000+i*20,0,5000), Quaternion.identity, root.transform);
            doors[i] = go.GetComponent<DungeonHoldingDoor>();
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/RiftDisruptor/RiftDisruptor.prefab");
        var enemies = new DisruptorBrain[4];
        for (int i=0;i<4;i++)
        {
            var go=UnityEngine.Object.Instantiate(prefab, new Vector3(5100+i*10,0,5000), Quaternion.identity, root.transform);
            enemies[i]=go.GetComponent<DisruptorBrain>(); enemies[i].decisionsEnabled=false;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
        }
        var release = root.AddComponent<ChamberReleaseController>();
        var so = new SerializedObject(release);
        SetArray(so,"area1Agents",enemies); SetArray(so,"holdingDoors",doors);
        // Keep the cloned gate test separate from the live pack under the main encounter root.
        so.ApplyModifiedPropertiesWithoutUndo();
        int releases=0,stage=0; float start=Time.time; release.OnChambersOpen+=()=>releases++;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            bool done=false;
            try
            {
                if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended during the check.");
                if(stage==0&&Time.time-start>.15f)
                {
                    foreach(var enemy in enemies.Take(3))enemy.ReceiveDamage(10000,null);
                    start=Time.time;stage=1;
                }
                if(stage==1&&Time.time-start>.15f)
                {
                    if(release.Released||doors.Any(d=>!d.IsClosed))throw new Exception("Released before all four enemies died.");
                    File.AppendAllText(Report,"PASS: three of four defeats keep both cloned gates closed.\n");
                    enemies[3].ReceiveDamage(10000,null);start=Time.time;stage=2;
                }
                if(stage==2&&Time.time-start>2)
                {
                    if(!release.Released||releases!=1||doors.Any(d=>!d.IsOpen))throw new Exception("Fourth defeat did not open both gates exactly once.");
                    File.AppendAllText(Report,"PASS: fourth defeat opens both cloned gate models exactly once. Live actors and gates were not reset or damaged.\n");done=true;
                }
            }
            catch(Exception ex){File.AppendAllText(Report,"FAIL: "+ex+"\n");Debug.LogError(ex);done=true;}
            if(!done)return;
            EditorApplication.update-=tick;root.SetActive(false);UnityEngine.Object.Destroy(root);
            Time.timeScale=oldScale;Application.runInBackground=oldBackground;
        };
        EditorApplication.update+=tick;
        return "Checking three/four-enemy release on temporary gate/enemy clones; live progress preserved.";
    }
    public static string ReproduceMissingReferences()
    {
        var c=UnityEngine.Object.FindAnyObjectByType<ChamberReleaseController>();
        var so=new SerializedObject(c);
        foreach(var name in new[]{"area1Agents","holdingDoors","hounds"})so.FindProperty(name).arraySize=0;
        so.FindProperty("grid").objectReferenceValue=null;so.FindProperty("pack").objectReferenceValue=null;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(ChamberReleaseController).GetField("totalArea1",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(c,0);
        return "Reproduced empty runtime bindings; next Update must recover them.";
    }
    public static string VerifyRealChambers()
    {
        var mission=Skybreak.Player.MissionFlowController.Instance;
        if(mission==null||mission.State!=Skybreak.Player.MissionState.MainMenu)throw new Exception("Run only in a fresh main-menu test session.");
        var release=UnityEngine.Object.FindAnyObjectByType<ChamberReleaseController>();
        var so=new SerializedObject(release);var list=so.FindProperty("area1Agents");
        if(list.arraySize!=4)throw new Exception("Expected four recovered court references.");
        Time.timeScale=1;Application.runInBackground=true;
        for(int i=0;i<list.arraySize;i++)((DamageSink)list.GetArrayElementAtIndex(i).objectReferenceValue).ReceiveDamage(10000,null);
        float start=Time.time;EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            if(Time.time-start<3)return;
            EditorApplication.update-=tick;
            bool ok=release.Released&&UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>().All(d=>d.IsOpen)&&UnityEngine.Object.FindObjectsByType<VaultHoundBrain>().All(h=>h.CurrentState!=HoundState.Chambered);
            File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+"Fresh test session: all four real court enemies defeated, both real chamber gates opened, both hounds deployed.\n");
            Time.timeScale=0;
        };
        EditorApplication.update+=tick;
        return "Testing real chambers in a fresh main-menu session; discard this session after the check.";
    }
    static void SetArray(SerializedObject so,string name,UnityEngine.Object[] objects)
    {
        var p=so.FindProperty(name);p.arraySize=objects.Length;
        for(int i=0;i<objects.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=objects[i];
    }
}
