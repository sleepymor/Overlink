using System;
using System.Collections;
using UnityEngine;

namespace Overlink.Cards
{
    public enum CardEase { Linear, OutCubic, OutBack, InOutSine }

    [DisallowMultipleComponent]
    public class CardTween : MonoBehaviour
    {
        Coroutine active;

        public bool IsPlaying => active != null;

        public void Play(Vector3? pos, Quaternion? rot, Vector3? scale,
            float duration, CardEase ease, Action onDone = null)
        {
            Stop();
            if (duration <= 0f)
            {
                if (pos.HasValue) transform.localPosition = pos.Value;
                if (rot.HasValue) transform.localRotation = rot.Value;
                if (scale.HasValue) transform.localScale = scale.Value;
                onDone?.Invoke();
                return;
            }
            active = StartCoroutine(Run(pos, rot, scale, duration, ease, onDone));
        }

        public void Stop()
        {
            if (active != null)
            {
                StopCoroutine(active);
                active = null;
            }
        }

        void OnDisable() => Stop();

        IEnumerator Run(Vector3? pos, Quaternion? rot, Vector3? scale,
            float duration, CardEase ease, Action onDone)
        {
            Vector3 p0 = transform.localPosition;
            Quaternion r0 = transform.localRotation;
            Vector3 s0 = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = Apply(ease, Mathf.Clamp01(t));
                if (pos.HasValue) transform.localPosition = Vector3.LerpUnclamped(p0, pos.Value, k);
                if (rot.HasValue) transform.localRotation = Quaternion.SlerpUnclamped(r0, rot.Value, k);
                if (scale.HasValue) transform.localScale = Vector3.LerpUnclamped(s0, scale.Value, k);
                yield return null;
            }
            active = null;
            onDone?.Invoke();
        }

        public static float Apply(CardEase ease, float t)
        {
            switch (ease)
            {
                case CardEase.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case CardEase.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case CardEase.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                default:
                    return t;
            }
        }
    }
}
