using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Applies the button micro-animation kit to every uGUI Button in every loaded scene, so hover
    /// pops, press squashes and sheen sweeps reach all screens (menus, shop, reveal, binder, HUD
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
                button.gameObject.AddComponent<UiFxButton>();
            }
        }

        /// <summary>
        /// Name-based opt-out. A full-screen dismiss scrim is a Button too, and decorating one would
        /// scale the whole screen past its bounds and sweep an additive sheen across everything, so
        /// scrim affordances stay undecorated by convention.
        /// </summary>
        static bool OptedOut(Button button)
        {
            return button.name.IndexOf("Scrim", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
