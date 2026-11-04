using UnityEngine;
using Overlink.Vfx;

namespace Overlink.UI
{
    /// <summary>
    /// Panel wake: on enable the panel settles from a slightly small scale (and, when a CanvasGroup
    /// is present, fades in) with an overshoot, so overlays like the bundle summary arrive with a
    /// beat instead of popping into existence. Reduced motion shows the panel instantly at rest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiFxPanel : MonoBehaviour
    {
        [SerializeField] float fromScale = 0.96f;
        [SerializeField] float duration = 0.22f;

        CanvasGroup group;

        void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            // The scene builder adds this component at edit time; Awake/OnEnable fire immediately then,
            // and coroutines must never run against editor state.
            if (!Application.isPlaying) return;
            if (!UiVfxGate.Motion)
            {
                transform.localScale = Vector3.one;
                if (group != null) group.alpha = 1f;
                return;
            }

            transform.localScale = Vector3.one * fromScale;
            if (group != null) group.alpha = 0f;
            UiTween.On(transform).Play(null, null, Vector3.one, duration, UiEase.OutBack,
                k =>
                {
                    if (group != null) group.alpha = Mathf.Clamp01(k);
                });
        }

        void OnDisable()
        {
            UiTween tween = GetComponent<UiTween>();
            if (tween != null) tween.Stop();
            transform.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }
    }
}
