using Skybreak.AI.Disruptor;
using Skybreak.AI.SawanIS.VaultHound;
using Skybreak.Warden;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Skybreak.AI
{
    /// <summary>
    /// Gives the main map's enemy bots the Rift Warden's glowing-parts look (no extra lights).
    /// Relay already drives its own glow, so it is left as authored.
    /// </summary>
    public static class BotGlowBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitialScene() => Install(SceneManager.GetActiveScene());

        static void SceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        static void Install(Scene scene)
        {
            if (scene.name != "SkybreakMain") return;
            foreach (var bot in Find<WardenBrain>()) Add(bot.gameObject, 2.2f, 1.4f);
            foreach (var bot in Find<VaultHoundBrain>()) Add(bot.gameObject, 2f, 1.2f);
            foreach (var bot in Find<DisruptorBrain>())
            {
                if (!Add(bot.gameObject, 2f, 1.3f)) continue;
                // The Disruptor animates its own glow material; brighten that colour instead.
                var visuals = bot.GetComponent<DisruptorVisuals>();
                if (visuals != null) visuals.glowColour *= 1.6f;
            }
        }

        static T[] Find<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        static bool Add(GameObject bot, float boost, float accent)
        {
            if (bot.GetComponent<BotGlow>() != null) return false;
            var glow = bot.AddComponent<BotGlow>();
            glow.emissionBoost = boost; glow.accentStrength = accent;
            return true;
        }
    }
}
