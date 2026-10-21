using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Skybreak.Presentation
{
    // Manual graph time keeps combat markers aligned with the actual animation, including slow frames.
    public sealed class TimedClipPresenter : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip[] clips;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClip current;
        bool loop;
        public float TimeInClip { get; private set; }
        public float Duration => current != null ? current.length : 0;
        public string CurrentClip => current != null ? current.name : "";
        // Presentation offsets must be removed before evaluating or replacing the animation pose.
        public event System.Action BeforePoseEvaluation;
        public void Play(string suffix, bool looping = false)
        {
            AnimationClip selected = null;
            foreach (var clip in clips) if (clip != null && clip.name.EndsWith(suffix)) { selected = clip; break; }
            if (selected == null) { Debug.LogError("Missing boss animation: " + suffix, this); return; }
            BeforePoseEvaluation?.Invoke();
            if (graph.IsValid()) graph.Destroy();
            current = selected; loop = looping; TimeInClip = 0;
            graph = PlayableGraph.Create("Rift Warden animation"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, selected); playable.SetApplyFootIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Boss pose", animator); output.SetSourcePlayable(playable);
            animator.applyRootMotion = false; graph.Play(); graph.Evaluate(0);
        }
        public void Advance(float dt)
        {
            if (!graph.IsValid() || current == null) return;
            BeforePoseEvaluation?.Invoke();
            TimeInClip += dt;
            playable.SetTime(loop ? TimeInClip % current.length : Mathf.Min(TimeInClip, current.length));
            graph.Evaluate(0);
        }
        public void Seek(float seconds)
        {
            if (!graph.IsValid() || current == null) return;
            BeforePoseEvaluation?.Invoke();
            TimeInClip = Mathf.Clamp(seconds, 0, current.length);
            playable.SetTime(TimeInClip); graph.Evaluate(0);
        }
        void OnDisable() { BeforePoseEvaluation?.Invoke(); if (graph.IsValid()) graph.Destroy(); }
    }
}
