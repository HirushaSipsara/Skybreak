using UnityEngine;

namespace Skybreak.AI
{
    public enum CombatSound { PlayerShot, CannonShot, Teleport, Slash, Hit, Jump, DoubleJump, Land, ArmBreak, CoreOpen, Defeat, Heal }
    [CreateAssetMenu(menuName = "Skybreak/Combat sound bank")]
    public sealed class CombatSoundBank : ScriptableObject
    {
        public AudioClip[] clips;
        public AudioClip Get(CombatSound sound) => clips != null && (int)sound < clips.Length ? clips[(int)sound] : null;
    }
}
