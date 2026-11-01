using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Small, view-only response for buttons. It works for both pointer and keyboard/gamepad focus and
    /// animates in unscaled time so a paused/settings overlay still feels responsive.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] float hoverScale = 1.025f;
        [SerializeField] float pressedScale = 0.985f;
        [SerializeField] float response = 18f;

        Button button;
        Vector3 restScale;
        float targetScale = 1f;

        void Awake()
        {
            button = GetComponent<Button>();
            restScale = transform.localScale;
        }

        void OnEnable()
        {
            targetScale = 1f;
            if (button == null) button = GetComponent<Button>();
            restScale = transform.localScale;
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(1f, response) * Time.unscaledDeltaTime);
            float current = transform.localScale.x / Mathf.Max(0.001f, restScale.x);
            transform.localScale = restScale * Mathf.Lerp(current, targetScale, k);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);
        public void OnSelect(BaseEventData eventData) => SetHovered(true);
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && button.interactable) targetScale = pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData) => SetHovered(true);

        void SetHovered(bool hovered)
        {
            targetScale = hovered && button != null && button.interactable ? hoverScale : 1f;
        }
    }
}
