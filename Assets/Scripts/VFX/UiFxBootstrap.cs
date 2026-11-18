using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Applies the button micro-animation kit to every uGUI Button in every loaded scene, so hover
    /// pops, accent rails and press squashes reach all screens (menus, shop, reveal, binder, HUD
    /// panels) without each scene authoring the component by hand. Runs once per domain load and
    /// re-decorates on every scene load; buttons that already carry <see cref="UiFxButton"/> — or
    /// that opt out by name — are skipped.
    /// </summary>
    public static class UiFxBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Decorate();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Decorate();
        }

        public static void Decorate()
        {
            Button[] buttons = Object.FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.GetComponent<UiFxButton>() != null || OptedOut(button)) continue;
                UiFxButton effect = button.gameObject.AddComponent<UiFxButton>();
                if (IsBattlePrimaryAction(button)) effect.ShowAccentRails = false;
            }
        }

        static bool IsBattlePrimaryAction(Button button)
        {
            if (button.name != "ConfirmLineupButton" && button.name != "EndPhaseButton"
                && button.name != "DoneBlockingButton") return false;
            Transform parent = button.transform.parent;
            return parent != null && parent.name == "PhaseControls" && parent.parent != null
                && parent.parent.name == "Battlefield HUD";
        }

        /// <summary>
        /// Name-based opt-out. A full-screen dismiss scrim is a Button too, and decorating one would
        /// scale the whole screen past its bounds and add hover accents across everything, so
        /// scrim affordances stay undecorated by convention.
        /// </summary>
        static bool OptedOut(Button button)
        {
            return button.name.IndexOf("Scrim", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
