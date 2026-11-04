using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Overlink.Vfx;

namespace Overlink.UI
{
    /// <summary>
    /// Animated stat bar: a filled foreground that eases to the new ratio, a dimmer ghost bar that
    /// lags behind on losses (the classic damage-trail read), a brightness pulse on damage and an
    /// overshoot pop on gains. Both gates apply — the pulse through <see cref="UiVfxGate.Flash"/>,
    /// all motion through <see cref="UiTween"/> — so reduced-motion players get an instant, calm
    /// bar with the same information.
    ///
    /// Authored as two stretched Images (fill type: Filled) under any rect; the widget owns nothing
    /// else, so it drops onto HUD chips, pack meters or shop tracks alike.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiBar : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] Image ghost;
        [SerializeField] float lagDelay = 0.16f;
        [SerializeField] float lagDuration = 0.6f;

        Color fillBase = Color.white;
        float shown;
        float ghostShown;
        Coroutine lag;

        void Awake()
        {
            if (fill != null)
            {
                fillBase = fill.color;
                shown = fill.fillAmount;
                ghostShown = shown;
                if (ghost != null) ghost.fillAmount = shown;
            }
        }

        /// <summary>Runtime wiring for scenes that attach the bar from code (the bay milestone track):
        /// the same contract as the serialized fields, resolved late.</summary>
        public void Bind(Image fillImage, Image ghostImage)
        {
            fill = fillImage;
            ghost = ghostImage;
            if (fill != null)
            {
                fillBase = fill.color;
                shown = fill.fillAmount;
                ghostShown = shown;
                if (ghost != null) ghost.fillAmount = shown;
            }
        }

        public void Set(float current, float max)
        {
            if (fill == null) return;
            float target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            float from = shown;
            bool dropped = target < shown - 0.0001f;
            bool gained = target > shown + 0.0001f;

            UiTween.On(fill.rectTransform).PlayStep(0.35f, UiEase.OutCubic, k =>
            {
                shown = Mathf.Lerp(from, target, k);
                fill.fillAmount = shown;
            });

            if (ghost != null)
            {
                if (dropped) StartLag(ghostShown, target);
                else
                {
                    StopLag();
                    ghostShown = target;
                    ghost.fillAmount = target;
                }
            }

            if (dropped) Pulse();
            else if (gained) Pop();
        }

        public void SetImmediate(float current, float max)
        {
            if (fill == null) return;
            shown = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            ghostShown = shown;
            fill.fillAmount = shown;
            if (ghost != null) ghost.fillAmount = shown;
        }

        /// <summary>What the fill currently shows, for tests and readouts; -1 when unbound.</summary>
        public float FillAmount => fill != null ? fill.fillAmount : -1f;

        void StartLag(float from, float to)
        {
            StopLag();
            lag = StartCoroutine(LagRun(from, to));
        }

        void StopLag()
        {
            if (lag != null)
            {
                StopCoroutine(lag);
                lag = null;
            }
        }

        IEnumerator LagRun(float from, float to)
        {
            yield return new WaitForSecondsRealtime(lagDelay);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, lagDuration);
                float k = UiTween.Apply(UiEase.OutCubic, Mathf.Clamp01(t));
                ghostShown = Mathf.Lerp(from, to, k);
                if (ghost != null) ghost.fillAmount = ghostShown;
                yield return null;
            }

            ghostShown = to;
            if (ghost != null) ghost.fillAmount = to;
            lag = null;
        }

        /// <summary>Brief white-hot brightness bump on loss, gated by the flash preference.</summary>
        void Pulse()
        {
            if (fill == null) return;
            float intensity = UiVfxGate.FlashScale(1f);
            if (intensity <= 0f) return;
            Color hot = Color.Lerp(fillBase, Color.white, 0.75f);
            UiTween.On(fill.rectTransform).PlayStep(0.34f, UiEase.OutCubic, k =>
            {
                float bump = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * intensity;
                fill.color = Color.Lerp(fillBase, hot, bump);
            }, () => fill.color = fillBase);
        }

        void Pop()
        {
            UiTween.On(transform).Play(null, null, new Vector3(1.06f, 1.18f, 1f), 0.12f, UiEase.OutBack, null,
                () => UiTween.On(transform).Play(null, null, Vector3.one, 0.16f, UiEase.OutCubic));
        }

        void OnDisable() => StopLag();
    }
}
