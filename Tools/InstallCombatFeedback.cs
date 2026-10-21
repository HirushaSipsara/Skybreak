using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak.AI;
using Skybreak.AI.RiftWarden;
using Skybreak.AI.Relay;

public static class InstallCombatFeedback
{
    static CombatSoundBank bank;
    static CombatAudio Audio(GameObject actor,bool player=false)
    {
        var audio=actor.GetComponent<CombatAudio>();if(audio==null)audio=actor.AddComponent<CombatAudio>();
        audio.bank=bank;audio.playerForeground=player;audio.volume=player?.48f:.55f;
        var source=actor.GetComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=player?0:1;
        source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=3;source.maxDistance=32;source.dopplerLevel=0;
        EditorUtility.SetDirty(audio);return audio;
    }
    static void Boss(RiftWardenBrain b)
    {
        b.audioCues=Audio(b.gameObject);var recoil=b.GetComponent<RiftWeaponFeedback>();if(recoil==null)recoil=b.gameObject.AddComponent<RiftWeaponFeedback>();
        recoil.cannonBone=b.leftTarget.transform.parent;b.weaponFeedback=recoil;EditorUtility.SetDirty(b);EditorUtility.SetDirty(recoil);
    }
    static void Relay(RelayBrain r){r.audioCues=Audio(r.gameObject);EditorUtility.SetDirty(r);}
    static void Scene(string path,bool local)
    {
        var scene=EditorSceneManager.OpenScene(path);Boss(UnityEngine.Object.FindAnyObjectByType<RiftWardenBrain>());Relay(UnityEngine.Object.FindAnyObjectByType<RelayBrain>());
        if(local)Audio(UnityEngine.Object.FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>().gameObject,true);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play");bank=AssetDatabase.LoadAssetAtPath<CombatSoundBank>("Assets/Audio/Combat/OwnedAgents/CombatSoundBank.asset");if(bank==null)throw new Exception("Create sound bank first");
        string main="Assets/Scenes/GameLevel/SkybreakMain.unity";if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=main)throw new Exception("Open main scene");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Tools/SceneSnapshots/SkybreakMain_before_combat_audio_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
        string bp="Assets/Prefabs/Enemies/RiftWarden/RiftWarden.prefab";var boss=PrefabUtility.LoadPrefabContents(bp);try{Boss(boss.GetComponent<RiftWardenBrain>());PrefabUtility.SaveAsPrefabAsset(boss,bp);}finally{PrefabUtility.UnloadPrefabContents(boss);}
        string rp="Assets/Prefabs/Companions/Relay/Relay.prefab";var relay=PrefabUtility.LoadPrefabContents(rp);try{Relay(relay.GetComponent<RelayBrain>());PrefabUtility.SaveAsPrefabAsset(relay,rp);}finally{PrefabUtility.UnloadPrefabContents(relay);}
        Scene(main,false);Scene("Assets/LocalCombatLab/OwnedAgentsLab.unity",true);EditorSceneManager.OpenScene(main);AssetDatabase.SaveAssets();
        return "Permanent boss/Relay audio and cannon recoil saved to prefabs/main. Local player audio saved only to excluded lab. One existing camera listener retained.";
    }
}
