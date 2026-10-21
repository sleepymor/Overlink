using UnityEngine;

namespace Overlink.UI
{
    /// <summary>
    /// Keeps runtime diagnostic/account overlays out of the main menu.
    /// The Net layer creates these objects globally, so the menu hides only their
    /// presentation and restores them when the scene is left.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuOverlayGuard : MonoBehaviour
    {
        const string AccountPanelName = "AccountPanel";
        const string ConsoleToggleName = "ConsoleToggle";
        const string ConsolePanelName = "ConsolePanel";

        [SerializeField] float patienceSeconds = 5f;

        GameObject hiddenAccountPanel;
        GameObject hiddenConsoleToggle;
        GameObject hiddenConsolePanel;
        bool gaveUp;

        void Update()
        {
            if (gaveUp || IsComplete())
                return;

            HideIfFound(AccountPanelName, ref hiddenAccountPanel);
            HideIfFound(ConsoleToggleName, ref hiddenConsoleToggle);
            HideIfFound(ConsolePanelName, ref hiddenConsolePanel);

            if (Time.timeSinceLevelLoad > patienceSeconds)
                gaveUp = true;
        }

        void OnDisable()
        {
            Restore(ref hiddenAccountPanel);
            Restore(ref hiddenConsoleToggle);
            Restore(ref hiddenConsolePanel);
        }

        bool IsComplete()
        {
            return hiddenAccountPanel != null
                   && hiddenConsoleToggle != null;
        }

        static void HideIfFound(string objectName, ref GameObject hiddenObject)
        {
            if (hiddenObject != null)
                return;

            var target = GameObject.Find(objectName);
            if (target == null)
                return;

            hiddenObject = target;
            target.SetActive(false);
        }

        static void Restore(ref GameObject hiddenObject)
        {
            if (hiddenObject == null)
                return;

            hiddenObject.SetActive(true);
            hiddenObject = null;
        }
    }
}
