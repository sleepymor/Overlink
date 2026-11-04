using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Overlink.Vfx;

namespace Overlink.UI
{
    /// <summary>
    /// Hover/click micro-animation for any uGUI Button: a brief overshoot pop on hover, a squash on
    /// press, and an additive sheen sweep that fades in while hovered. Keyboard/gamepad focus counts
    /// as hover, so a pad player navigating a menu gets the same beat (this replaces the old
    /// <c>UiButtonFeedback</c>, whose focus response was the one thing the pointer-only kit lacked).
    /// The sheen is a stretched child Image on the shared <c>UiSheen</c> material — per-button state
    /// lives in that child's vertex alpha (the shader multiplies by it), so no per-button material
    /// instances are created and batching is untouched.
    ///
    /// All motion routes through <see cref="UiTween"/>, which settles instantly when the player has
    /// reduced motion enabled; the sheen additionally obeys the flash gate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiFxButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        const float HoverScale = 1.035f;
        const float PressScale = 0.965f;

        Image sheen;
        RectTransform rect;
        bool hovering;
        bool pressing;
        bool focused;

        void Awake()
        {
            rect = transform as RectTransform;
            sheen = EnsureSheen(transform);
            ApplySheenAlpha(0f);
        }

        /// <summary>Stretched additive child; raycast off so it never steals the button's own input.</summary>
        internal static Image EnsureSheen(Transform button)
        {
            Transform existing = button.Find("Sheen");
            if (existing != null) return existing.GetComponent<Image>();

            GameObject go = new GameObject("Sheen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(button, false);
            RectTransform child = (RectTransform)go.transform;
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.material = UiVfxMaterials.Sheen;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        void ApplySheenAlpha(float alpha)
        {
            if (sheen == null) return;
            float gated = UiVfxGate.FlashScale(alpha);
            Color color = sheen.color;
            color.a = gated;
            sheen.color = color;
            sheen.enabled = gated > 0.001f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            UiTween.On(rect).Play(null, null, Vector3.one * HoverScale, 0.14f, UiEase.OutBack);
            FadeSheen(1f, 0.18f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (!pressing && !focused) UiTween.On(rect).Play(null, null, Vector3.one, 0.16f, UiEase.OutCubic);
            if (!focused) FadeSheen(0f, 0.22f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressing = true;
            UiTween.On(rect).Play(null, null, Vector3.one * PressScale, 0.07f, UiEase.InCubic);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressing = false;
            float target = hovering || focused ? HoverScale : 1f;
            UiTween.On(rect).Play(null, null, Vector3.one * target, 0.18f, UiEase.OutBack);
        }

        public void OnSelect(BaseEventData eventData)
        {
            focused = true;
            UiTween.On(rect).Play(null, null, Vector3.one * HoverScale, 0.14f, UiEase.OutBack);
            FadeSheen(1f, 0.18f);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focused = false;
            if (!hovering && !pressing) UiTween.On(rect).Play(null, null, Vector3.one, 0.16f, UiEase.OutCubic);
            if (!hovering) FadeSheen(0f, 0.22f);
        }

        void FadeSheen(float alpha, float duration)
        {
            if (sheen == null) return;
            float from = sheen.color.a;
            float to = UiVfxGate.FlashScale(alpha);
            UiTween.On(sheen.rectTransform).PlayStep(duration, UiEase.OutCubic,
                k => ApplySheenAlpha(Mathf.Lerp(from, to, k)));
        }

        void OnDisable()
        {
            hovering = false;
            pressing = false;
            focused = false;
            if (rect != null) rect.localScale = Vector3.one;
            ApplySheenAlpha(0f);
        }
    }
}
