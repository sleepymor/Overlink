using System.IO;
using Unity.Services.Authentication;
using UnityEngine;

namespace Overlink.Net
{
    /// <summary>
    /// Development aids for getting the account state back to zero.
    /// <para>
    /// These reset the <i>local</i> session and cache only. Whatever is stored in Cloud Save belongs to
    /// the account and stays on the service, so a player whose profile is corrupt server-side still
    /// needs the Dashboard to clear it.
    /// </para>
    /// </summary>
    public static class AccountResetUtility
    {
        public static void ResetAccount()
        {
            Debug.Log("[AccountReset] Starting full account reset...");

            // Sign out from Unity Authentication
            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[AccountReset] Signing out from Unity Authentication...");
                AuthenticationService.Instance.SignOut(true);
            }

            // Clear PlayerPrefs (settings, cached data)
            Debug.Log("[AccountReset] Clearing PlayerPrefs...");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // Clear Unity Authentication cache files
            ClearAuthenticationCache();

            // Reset the in-memory account and profile state so the next scene sees a fresh install.
            if (AccountManager.Instance != null) AccountManager.Instance.SignOut();
            PlayerProfileService.Instance?.ClearProfile();

            Debug.Log("[AccountReset] Account reset complete. Restart Play mode or reload the scene.");
        }

        static void ClearAuthenticationCache()
        {
            // Unity Authentication stores session tokens in Application.persistentDataPath
            // The exact path structure varies by platform
            string persistentPath = Application.persistentDataPath;
            
            // Common cache locations
            string[] cachePaths = new[]
            {
                Path.Combine(persistentPath, "unity.cloud_environment"),
                Path.Combine(persistentPath, "unity.cloud_userid"),
                Path.Combine(persistentPath, "unity.services.core"),
                Path.Combine(persistentPath, "Unity", "UnityServicesCore"),
            };

            foreach (string path in cachePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                        Debug.Log($"[AccountReset] Deleted cache file: {path}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[AccountReset] Could not delete {path}: {e.Message}");
                    }
                }

                if (Directory.Exists(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                        Debug.Log($"[AccountReset] Deleted cache directory: {path}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[AccountReset] Could not delete {path}: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Less aggressive: just sign out and clear the session token, but keep PlayerPrefs.
        /// </summary>
        public static void SignOutAndClearSession()
        {
            Debug.Log("[AccountReset] Signing out and clearing session...");

            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
            }

            if (AccountManager.Instance != null)
            {
                AccountManager.Instance.SignOut();
            }

            Debug.Log("[AccountReset] Sign-out complete.");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Overlink/Account/Reset Account (Full)", priority = 100)]
        static void MenuResetAccount()
        {
            if (UnityEditor.EditorUtility.DisplayDialog(
                "Reset Account",
                "This will:\n" +
                "• Sign out from Unity Authentication\n" +
                "• Delete ALL PlayerPrefs (settings, progress)\n" +
                "• Clear authentication cache files\n" +
                "• Force a fresh start\n\n" +
                "Continue?",
                "Reset Everything",
                "Cancel"))
            {
                ResetAccount();
                UnityEditor.EditorUtility.DisplayDialog("Reset Complete", 
                    "Account reset complete. Restart Play mode or reload the scene.", "OK");
            }
        }

        [UnityEditor.MenuItem("Overlink/Account/Sign Out (Keep PlayerPrefs)", priority = 101)]
        static void MenuSignOut()
        {
            SignOutAndClearSession();
            UnityEditor.EditorUtility.DisplayDialog("Signed Out", 
                "Signed out successfully. PlayerPrefs were preserved.", "OK");
        }

        [UnityEditor.MenuItem("Overlink/Account/Show Account Info", priority = 102)]
        static void MenuShowAccountInfo()
        {
            if (AuthenticationService.Instance == null)
            {
                UnityEditor.EditorUtility.DisplayDialog("Account Info", 
                    "Unity Services not initialized.", "OK");
                return;
            }

            var auth = AuthenticationService.Instance;
            string info = $"Signed In: {auth.IsSignedIn}\n";
            
            if (auth.IsSignedIn)
            {
                info += $"Player ID: {auth.PlayerId}\n";
                info += $"Session Token Exists: {auth.SessionTokenExists}\n";
                
                if (AccountManager.Instance != null)
                {
                    info += $"Account Type: {AccountManager.Instance.Kind}\n";
                    info += $"Player Name: {AccountManager.Instance.PlayerName}\n";
                    info += $"Status: {AccountManager.Instance.Status}\n";
                }
            }
            else
            {
                info += "Not signed in.";
            }

            UnityEditor.EditorUtility.DisplayDialog("Account Info", info, "OK");
        }
#endif
    }
}
