using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Keeps the Net layer's self-installed account panel off the local match screen.
    /// <para>
    /// <c>AccountManager</c> installs itself via <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c> and
    /// <c>AccountMenuUI</c> then builds a Google / guest sign-in panel into whichever canvas it finds first,
    /// so the panel appears in <b>every</b> scene - including this one, which is a local match against the AI
    /// with no <c>NetworkManager</c>, no lobby and nothing for those buttons to act on.
    /// </para>
    /// <para>
    /// The panel is also mis-typed today: <c>AccountMenuUI.CreateButton</c> never sets a font size, so its
    /// three button labels render at TextMeshPro's 36-point default inside rows the layout budgets 28 pixels
    /// for, and they pile up across the board header. Fixing that belongs to the Net assembly -
    /// <c>Assets/Scripts/Net</c> is off limits from here - so the panel is simply hidden, and only in this
    /// scene. The Net demo scenes keep exactly what they had.
    /// </para>
    /// <para>
    /// Hiding the <c>GameObject</c> is presentation only. <c>AccountManager</c> is untouched: it keeps working,
    /// an in-flight sign-in keeps running and <c>EnsureSignedInAsync</c> behaves identically. Only the buttons
    /// stop being drawn over the tabletop.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NetAccountPanelGuard : MonoBehaviour
    {
        /// <summary>The name <c>AccountMenuUI.BuildPanel</c> gives its panel.</summary>
        public const string AccountPanelName = "AccountPanel";

        [Tooltip("Hide the Net account panel while this scene is loaded. Switch off to exercise a sign-in " +
                 "flow from the Gameplay scene without leaving it.")]
        public bool hideAccountPanel = true;

        [Tooltip("How long to wait for the panel to appear before giving up. It is built in another " +
                 "assembly's Start(), so it may not exist on this component's first frame - and once the wait " +
                 "expires the guard stops searching, because a scene with no Net layer installed would " +
                 "otherwise poll every frame for nothing.")]
        public float patienceSeconds = 5f;

        GameObject hiddenPanel;
        bool gaveUp;

        /// <summary>True while this guard is holding somebody else's panel off screen.</summary>
        public bool IsHiding => hiddenPanel != null;

        void Update()
        {
            if (!hideAccountPanel || IsHiding || gaveUp)
            {
                return;
            }

            if (TryHideAccountPanel())
            {
                return;
            }

            if (Time.timeSinceLevelLoad > patienceSeconds)
            {
                gaveUp = true;
            }
        }

        void OnDisable()
        {
            // The panel belongs to a DontDestroyOnLoad object, so it outlives this scene. Leaving it hidden
            // would strand the sign-in flow in whatever Net scene the player opens next.
            RestoreAccountPanel();
        }

        /// <summary>
        /// Finds the account panel and switches it off. Returns true if the panel is hidden, whether or not
        /// this call is the one that did it, and false if there is no panel to hide yet.
        /// <para>
        /// Public because the panel is created by another assembly's <c>Start()</c>, so the timing is not
        /// ours to predict; the seam lets a test drive the lookup without entering play mode and waiting.
        /// </para>
        /// </summary>
        public bool TryHideAccountPanel()
        {
            if (IsHiding)
            {
                return true;
            }

            // GameObject.Find sees active objects only, which is exactly right here: the panel is on screen by
            // the time this runs, and once hidden it must not be looked up again but remembered by reference.
            GameObject panel = GameObject.Find(AccountPanelName);
            if (panel == null)
            {
                return false;
            }

            hiddenPanel = panel;
            panel.SetActive(false);
            return true;
        }

        /// <summary>Puts the panel back, if this guard was holding it hidden. Safe to call unconditionally.</summary>
        public void RestoreAccountPanel()
        {
            if (hiddenPanel == null)
            {
                return;
            }

            hiddenPanel.SetActive(true);
            hiddenPanel = null;
        }
    }
}
