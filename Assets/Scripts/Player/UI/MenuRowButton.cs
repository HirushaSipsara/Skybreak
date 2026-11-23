using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Skybreak.Player.UI
{
    /// <summary>
    /// Animated menu row: a light bar sweeps in, the label slides right and a chevron
    /// appears while the row is selected. Hovering with the mouse selects the row so
    /// mouse, keyboard and gamepad all share one highlight. Runs on unscaled time because
    /// menus are shown while the game is paused.
    /// </summary>
    public sealed class MenuRowButton : MonoBehaviour, IPointerEnterHandler
    {
        public RectTransform sweep, accentBar;
        public Text label, index, chevron;
        public Color accent = Color.cyan, idleLabel = Color.white, activeLabel = Color.white;
        public float labelX, idleSweep;
        float amount;

        void OnEnable() { amount = IsSelected ? 1 : idleSweep; Apply(); }

        bool IsSelected => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void Update()
        {
            float target = IsSelected ? 1 : idleSweep;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 6);
            Apply();
        }

        void Apply()
        {
            float eased = 1 - (1 - amount) * (1 - amount);
            float pulse = IsSelected ? .85f + Mathf.Sin(Time.unscaledTime * 4) * .15f : 1;
            if (sweep != null) sweep.localScale = new Vector3(eased, 1, 1);
            if (accentBar != null) accentBar.localScale = new Vector3(1, eased * pulse, 1);
            if (label != null)
            {
                label.color = Color.Lerp(idleLabel, activeLabel, eased);
                var p = label.rectTransform.anchoredPosition; p.x = labelX + eased * 14; label.rectTransform.anchoredPosition = p;
            }
            if (index != null) index.color = new Color(accent.r, accent.g, accent.b, .45f + eased * .55f);
            if (chevron != null)
            {
                chevron.color = new Color(accent.r, accent.g, accent.b, eased);
                var p = chevron.rectTransform.anchoredPosition; p.x = -18 + eased * 6; chevron.rectTransform.anchoredPosition = p;
            }
        }
    }
}
