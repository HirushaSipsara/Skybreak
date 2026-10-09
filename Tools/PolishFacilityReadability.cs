using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class PolishFacilityReadability
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open saved main map outside Play Mode.");
        var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        foreach(var label in root.GetComponentsInChildren<TextMesh>()){
            var sign=label.transform.parent;
            var plate=sign.Find("Enamel sign plate");
            if(plate==null||sign.name.EndsWith(" / readable"))continue;
            sign.position-=sign.forward*.6f;sign.name+=" / readable";
            var size=plate.localScale;size.y=1.25f;plate.localScale=size;
            var rail=sign.Find("Orange sign rail");rail.localPosition=new Vector3(0,.66f,0);
            label.characterSize=.038f;
        }
        foreach(var area in root.transform.Cast<Transform>()){
            var housing=area.Cast<Transform>().Where(t=>t.name=="Emergency luminaire housing").ToArray();
            var lenses=area.Cast<Transform>().Where(t=>t.name=="Emergency luminaire lens").ToArray();
            var lights=area.Cast<Transform>().Where(t=>t.name=="Local emergency illumination").ToArray();
            foreach(var h in housing){
                var oldForward=h.forward;var lens=lenses.FirstOrDefault(t=>Vector3.Distance(t.position,h.position)<.3f);
                if(lens==null)continue;
                var local=lights.FirstOrDefault(t=>Vector3.Distance(t.position,h.position)<.6f);
                h.rotation*=Quaternion.Euler(0,180,0);h.name="Emergency luminaire housing / inward";
                lens.rotation=h.rotation;lens.position=h.position-h.forward*.16f;
                if(local!=null)local.position=h.position-h.forward*.4f;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Facility signs moved in front of stonework with readable backing plates; emergency lamps face the walkable interior.";
    }
}
