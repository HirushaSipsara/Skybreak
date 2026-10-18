using UnityEngine;

namespace Skybreak.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class CombatAudio : MonoBehaviour
    {
        public CombatSoundBank bank;
        public bool playerForeground;
        public float volume = .55f;
        AudioSource source;
        public int Played { get; private set; }
        public CombatSound LastSound { get; private set; }
        public AudioSource Source => source;
        void Awake()
        {
            source = GetComponent<AudioSource>(); source.playOnAwake = false; source.loop = false;
            source.spatialBlend = playerForeground ? 0 : 1; source.minDistance = 3; source.maxDistance = 32;
            source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0; source.priority = playerForeground ? 80 : 110;
        }
        public void Play(CombatSound sound, float gain = 1)
        {
            var clip = bank != null ? bank.Get(sound) : null; if (clip == null || source == null) return;
            source.PlayOneShot(clip, Mathf.Clamp01(volume * gain)); LastSound = sound; Played++;
        }
    }
}
