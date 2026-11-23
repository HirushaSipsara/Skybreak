using UnityEngine;
using UnityEngine.UI;

namespace Skybreak.Player.UI
{
    /// <summary>Slow aether motes drifting upward across a full-screen rect (unscaled time).</summary>
    public sealed class MenuMotes : MonoBehaviour
    {
        RectTransform[] motes;
        Image[] images;
        Vector3[] state; // x: horizontal 0..1, y: speed, z: phase
        float[] height;
        Color tint;

        public void Build(Sprite glow, Color color, int count, int seed)
        {
            tint = color;
            var random = new System.Random(seed);
            motes = new RectTransform[count]; images = new Image[count]; state = new Vector3[count]; height = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>(); image.sprite = glow; image.raycastTarget = false;
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(.5f, .5f);
                float size = 4 + (float)random.NextDouble() * 10;
                rect.sizeDelta = new Vector2(size, size);
                motes[i] = rect; images[i] = image;
                state[i] = new Vector3((float)random.NextDouble(), .012f + (float)random.NextDouble() * .035f, (float)random.NextDouble() * 10);
                height[i] = (float)random.NextDouble();
            }
        }

        void Update()
        {
            if (motes == null) return;
            var area = ((RectTransform)transform).rect;
            float time = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = 0; i < motes.Length; i++)
            {
                height[i] += state[i].y * dt;
                if (height[i] > 1.05f) { height[i] = -.05f; state[i].x = Mathf.Repeat(state[i].x + .37f, 1); }
                float sway = Mathf.Sin(time * .4f + state[i].z) * .015f;
                motes[i].anchoredPosition = new Vector2((state[i].x + sway) * area.width, height[i] * area.height);
                float twinkle = .35f + .65f * Mathf.Abs(Mathf.Sin(time * .9f + state[i].z * 3));
                float edge = Mathf.Clamp01(Mathf.Min(height[i], 1 - height[i]) * 6);
                images[i].color = new Color(tint.r, tint.g, tint.b, tint.a * twinkle * edge);
            }
        }
    }
}
