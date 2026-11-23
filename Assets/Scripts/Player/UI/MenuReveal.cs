using UnityEngine;

namespace Skybreak.Player.UI
{
    /// <summary>Fades and slides an element in each time its page opens (unscaled time).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MenuReveal : MonoBehaviour
    {
        public float delay, duration = .45f;
        public Vector2 offset = new Vector2(-28, 0);
        RectTransform rect;
        CanvasGroup group;
        Vector2 home;
        bool captured;
        float shownAt;

        void OnEnable()
        {
            if (!captured)
            {
                rect = (RectTransform)transform; group = GetComponent<CanvasGroup>();
                home = rect.anchoredPosition; captured = true;
            }
            shownAt = Time.unscaledTime;
            Apply(0);
        }

        void Update() => Apply(Mathf.Clamp01((Time.unscaledTime - shownAt - delay) / duration));
        void OnDisable() { if (captured) Apply(1); }

        void Apply(float t)
        {
            float eased = 1 - Mathf.Pow(1 - t, 3);
            group.alpha = eased;
            rect.anchoredPosition = home + offset * (1 - eased);
        }
    }
}
