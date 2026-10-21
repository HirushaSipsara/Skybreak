using Skybreak.AI;
using Skybreak.AI.Relay;
using Skybreak.AI.RiftWarden;
using Skybreak.Descent;
using UnityEngine;
using UnityEngine.SceneManagement;
using Skybreak.Player;

namespace Skybreak.LocalCombatLab
{
    // Reuse the baseline combat controller for the shared main map.
    public static class MainCombatBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            // Works with domain and scene reload disabled too; never accumulate subscriptions.
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitialScene() { Install(SceneManager.GetActiveScene()); }
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { Install(scene); }
        static void Install(Scene scene)
        {
            if (scene.name != "SkybreakMain") return;
            var arc = Object.FindAnyObjectByType<DescentCharacterController>();
            var boss = Object.FindAnyObjectByType<RiftWardenBrain>();
            var relay = Object.FindAnyObjectByType<RelayBrain>();
            if (arc == null || boss == null || relay == null) return;
            if(Object.FindAnyObjectByType<MissionFlowController>()==null)new GameObject("Skybreak mission flow").AddComponent<MissionFlowController>();
            if(arc.GetComponent<LocalCombatDriver>() != null)return;
            arc.enabled = false;
            var vitals = arc.gameObject.AddComponent<LocalPlayerVitals>();
            var audio = arc.gameObject.AddComponent<CombatAudio>();
            audio.bank = boss.audioCues.bank; audio.playerForeground = true; audio.volume = .48f;
            audio.Source.spatialBlend = 0;
            var driver = arc.gameObject.AddComponent<LocalCombatDriver>();
            driver.cameraView = arc.viewCamera != null ? arc.viewCamera : Camera.main;
            driver.boss = boss; driver.relay = relay; driver.vitals = vitals;
            boss.player = arc.transform; boss.playerVitals = vitals;
            relay.player = arc.transform; relay.playerVitals = vitals;
        }
    }
}
