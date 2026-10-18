using UnityEngine;

namespace Overlink.Net
{
    /// <summary>
    /// Non-secret Google OAuth settings. The client ID is safe to ship in a client, but the
    /// OAuth consent screen and redirect URIs must also be configured in Google Cloud.
    /// </summary>
    [CreateAssetMenu(menuName = "Overlink/Google Auth Settings", fileName = "GoogleAuthSettings")]
    public sealed class GoogleAuthSettings : ScriptableObject
    {
        [Tooltip("Google OAuth client ID. Leave empty until the Google provider is configured.")]
        public string clientId;
    }
}
