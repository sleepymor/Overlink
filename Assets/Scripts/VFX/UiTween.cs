using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Overlink.Vfx
{
    public enum UiEase
    {
        Linear,
        OutCubic,
        InCubic,
        InOutSine,
        OutBack,
        OutElastic
    }

    /// <summary>
    /// The UI-side generalization of <c>Overlink.Cards.CardTween</c> (which stays untouched for the
    /// card row): the same transform channels plus a per-frame step callback, so colours, fill
    /// amounts, anchored positions and any other scalar can ride the same eased curve without a
    /// coroutine per widget.
    ///
    /// Reduced motion is honoured centrally here: when <see cref="UiVfxGate.Motion"/> is off, Play
    /// snaps straight to the end state and still fires the step at 1, so callers never branch on the
    /// accessibility setting themselves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiTween : MonoBehaviour
    {
        Coroutine active;
        readonly List<Coroutine> steps = new List<Coroutine>();

        public bool IsPlaying => active != null || steps.Count > 0;

        /// <summary>Gets or adds the tween driver on a target, so one-shots need no authored component.</summary>
        public static UiTween On(Transform target)
        {
            if (target == null) return null;
            UiTween tween = target.GetComponent<UiTween>();
            return tween != null ? tween : target.gameObject.AddComponent<UiTween>();
        }

        public void Play(Vector3? pos, Quaternion? rot, Vector3? scale, float duration, UiEase ease,
            Action<float> onStep = null, Action onDone = null)
        {
            StopTransform();
            if (duration <= 0f || !UiVfxGate.Motion || !gameObject.activeInHierarchy)
            {
                Settle(pos, rot, scale);
                onStep?.Invoke(1f);
                onDone?.Invoke();
                return;
            }

            active = StartCoroutine(Run(pos, rot, scale, duration, ease, onStep, onDone));
        }

        /// <summary>Runs an eased scalar curve with no transform channel at all — pure callback tween.
        /// Step channels are independent of each other and of the transform channel, so a counter roll
        /// and a scale pop can ride the same widget at once.</summary>
        public void PlayStep(float duration, UiEase ease, Action<float> onStep, Action onDone = null)
        {
            if (duration <= 0f || !UiVfxGate.Motion || !gameObject.activeInHierarchy)
            {
                onStep?.Invoke(1f);
                onDone?.Invoke();
                return;
            }

            Coroutine routine = null;
            routine = StartCoroutine(StepRun(duration, ease, onStep, () => steps.Remove(routine), onDone));
            steps.Add(routine);
        }

        public void Stop()
        {
            StopTransform();
            StopSteps();
        }

        /// <summary>Transform-channel-only stop, used by <see cref="Play"/> so a scale pop on a widget
        /// can never murder the step callbacks (counter rolls, bar fills) riding the same tween.</summary>
        void StopTransform()
        {
            if (active != null)
            {
                StopCoroutine(active);
                active = null;
            }
        }

        public void StopSteps()
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null) StopCoroutine(steps[i]);
            }

            steps.Clear();
        }

        void OnDisable() => Stop();

        void Settle(Vector3? pos, Quaternion? rot, Vector3? scale)
        {
            if (pos.HasValue) transform.localPosition = pos.Value;
            if (rot.HasValue) transform.localRotation = rot.Value;
            if (scale.HasValue) transform.localScale = scale.Value;
        }

        IEnumerator Run(Vector3? pos, Quaternion? rot, Vector3? scale, float duration, UiEase ease,
            Action<float> onStep, Action onDone)
        {
            Vector3 p0 = transform.localPosition;
            Quaternion r0 = transform.localRotation;
            Vector3 s0 = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float k = Apply(ease, Mathf.Clamp01(t));
                if (pos.HasValue) transform.localPosition = Vector3.LerpUnclamped(p0, pos.Value, k);
                if (rot.HasValue) transform.localRotation = Quaternion.SlerpUnclamped(r0, rot.Value, k);
                if (scale.HasValue) transform.localScale = Vector3.LerpUnclamped(s0, scale.Value, k);
                onStep?.Invoke(k);
                yield return null;
            }

            Settle(pos, rot, scale);
            onStep?.Invoke(1f);
            active = null;
            onDone?.Invoke();
        }

        IEnumerator StepRun(float duration, UiEase ease, Action<float> onStep, Action cleanup, Action onDone)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                onStep?.Invoke(Apply(ease, Mathf.Clamp01(t)));
                yield return null;
            }

            onStep?.Invoke(1f);
            cleanup?.Invoke();
            onDone?.Invoke();
        }

        public static float Apply(UiEase ease, float t)
        {
            switch (ease)
            {
                case UiEase.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case UiEase.InCubic:
                    return t * t * t;
                case UiEase.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case UiEase.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case UiEase.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                default:
                    return t;
            }
        }
    }
}
