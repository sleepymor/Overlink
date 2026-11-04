using System;
using TMPro;
using UnityEngine;
using Overlink.Vfx;

namespace Overlink.UI
{
    /// <summary>
    /// Rolling number readout for gacha counters (credits, loyalty, revealed cards). The displayed
    /// value eases toward the target through <see cref="UiTween"/>'s callback channel while a
    /// formatter closure composes the surrounding copy, so labels like "REVEALED 3 / 5" roll without
    /// the controller rewriting strings per frame. Increases answer with a small overshoot pop;
    /// reduced motion snaps straight to the target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UiCounter : MonoBehaviour
    {
        TMP_Text label;
        Func<int, string> formatter = v => v.ToString();
        int shown;
        int target;

        // Controllers bind from their own Awake, which can run before this component's — the label
        // must resolve lazily or the seed write (e.g. milestone "0 / 100", whose value never rolls)
        // is silently dropped and the node keeps its builder placeholder text.
        TMP_Text Label
        {
            get
            {
                if (label == null) label = GetComponent<TMP_Text>();
                return label;
            }
        }

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        /// <summary>Attaches the composer and seeds the readout without animating.</summary>
        public void Bind(Func<int, string> format, int initial)
        {
            if (format != null) formatter = format;
            shown = initial;
            target = initial;
            Write();
        }

        public void Set(int value, float duration = 0.55f)
        {
            if (Label == null) return;
            target = value;
            if (shown == target)
            {
                Write();
                return;
            }

            int from = shown;
            bool increased = target > from;
            UiTween.On(transform).PlayStep(duration, UiEase.OutCubic, k =>
            {
                shown = Mathf.RoundToInt(Mathf.Lerp(from, target, k));
                Write();
            }, () =>
            {
                shown = target;
                Write();
            });

            if (increased) Pop();
        }

        public void SetImmediate(int value)
        {
            shown = value;
            target = value;
            Write();
        }

        void Pop()
        {
            UiTween.On(transform).Play(null, null, Vector3.one * 1.12f, 0.12f, UiEase.OutBack, null,
                () => UiTween.On(transform).Play(null, null, Vector3.one, 0.16f, UiEase.OutCubic));
        }

        void Write()
        {
            TMP_Text text = Label;
            if (text != null) text.text = formatter(shown);
        }

        void OnDisable()
        {
            UiTween tween = GetComponent<UiTween>();
            if (tween != null) tween.StopSteps();
        }
    }
}
