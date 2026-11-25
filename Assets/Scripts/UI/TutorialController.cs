using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Overlink.Net;

namespace Overlink.UI
{
    /// <summary>
    /// Runs the first-run walkthrough. Layout is authored by Overlink/UI/Author Tutorial Screen; this
    /// class only binds the objects and pages through <see cref="TutorialContent.Pages"/>.
    /// <para>
    /// Finishing and skipping both record <c>tutorialCompleted</c>. Closing the app partway through leaves
    /// the flag false, so the tutorial starts again on the next launch. Replay from Settings never clears
    /// the flag; it simply opens this scene.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialController : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] TMP_Text pageTitle;
        [SerializeField] TMP_Text pageBody;
        [SerializeField] TMP_Text pageCounter;

        [Header("Controls")]
        [SerializeField] Button btnBack;
        [SerializeField] Button btnNext;
        [SerializeField] Button btnSkip;

        [Header("Board Highlights")]
        [SerializeField] Outline[] playerActiveHighlights;
        [SerializeField] Outline[] nodeHighlights;

        int index;
        bool finishing;

        void Awake()
        {
            if (!ValidateReferences()) return;

            btnBack.onClick.AddListener(OnBack);
            btnNext.onClick.AddListener(OnNext);
            btnSkip.onClick.AddListener(OnSkip);

            Show(0);
        }

        void OnDestroy()
        {
            if (btnBack != null) btnBack.onClick.RemoveListener(OnBack);
            if (btnNext != null) btnNext.onClick.RemoveListener(OnNext);
            if (btnSkip != null) btnSkip.onClick.RemoveListener(OnSkip);
        }

        void Show(int page)
        {
            index = Mathf.Clamp(page, 0, TutorialContent.Pages.Length - 1);
            TutorialPage current = TutorialContent.Pages[index];

            pageTitle.text = current.Title;
            pageBody.text = current.Body;
            pageCounter.text = $"{index + 1} / {TutorialContent.Pages.Length}";

            SetHighlight(playerActiveHighlights, current.Highlight.HasFlag(TutorialHighlight.PlayerActives));
            SetHighlight(nodeHighlights, current.Highlight.HasFlag(TutorialHighlight.Nodes));

            btnBack.interactable = index > 0;
            bool last = index == TutorialContent.Pages.Length - 1;
            TMP_Text nextLabel = btnNext.GetComponentInChildren<TMP_Text>(true);
            if (nextLabel != null) nextLabel.text = last ? "FINISH" : "NEXT";
        }

        static void SetHighlight(Outline[] outlines, bool on)
        {
            if (outlines == null) return;
            foreach (Outline outline in outlines)
            {
                if (outline != null) outline.enabled = on;
            }
        }

        void OnBack()
        {
            if (index > 0) Show(index - 1);
        }

        void OnNext()
        {
            if (index < TutorialContent.Pages.Length - 1)
            {
                Show(index + 1);
                return;
            }

            Complete();
        }

        void OnSkip() => Complete();

        async void Complete()
        {
            if (finishing) return;
            finishing = true;
            SetControlsInteractable(false);

            try
            {
                PlayerProfileService service = PlayerProfileService.Instance;
                if (service != null && service.CurrentProfile != null)
                {
                    AuthResult result = await service.MarkTutorialCompletedAsync();
                    if (!result.Success)
                    {
                        Debug.LogWarning("[Tutorial] Could not record completion; it will show again next launch.");
                        SetControlsInteractable(true);
                        finishing = false;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Tutorial] Completion save failed: {e.Message}");
                SetControlsInteractable(true);
                finishing = false;
                return;
            }

            AuthFlowController.Instance?.OnTutorialCompleted();
        }

        void SetControlsInteractable(bool value)
        {
            if (btnBack != null) btnBack.interactable = value && index > 0;
            if (btnNext != null) btnNext.interactable = value;
            if (btnSkip != null) btnSkip.interactable = value;
        }

        bool ValidateReferences()
        {
            bool valid = true;
            valid &= Check(pageTitle, nameof(pageTitle));
            valid &= Check(pageBody, nameof(pageBody));
            valid &= Check(pageCounter, nameof(pageCounter));
            valid &= Check(btnBack, nameof(btnBack));
            valid &= Check(btnNext, nameof(btnNext));
            valid &= Check(btnSkip, nameof(btnSkip));
            return valid;
        }

        static bool Check(UnityEngine.Object value, string fieldName)
        {
            if (value != null) return true;

            Debug.LogError($"[{nameof(TutorialController)}] Assign {fieldName} in the Inspector.");
            return false;
        }
    }
}
