using UnityEngine;

namespace Overlink.UI
{
    /// <summary>Compatibility partial for the serialized Auth scene.</summary>
    public sealed partial class AuthScreenController
    {
        bool IsWired()
        {
            return loadingPanel != null && signInPanel != null && setupPanel != null
                && ageStep != null && identityStep != null && loadingText != null
                && titleText != null && statusText != null && signInError != null
                && usernameInput != null && passwordInput != null && confirmPasswordInput != null
                && submitButton != null && toggleModeButton != null && forgotPasswordButton != null
                && ageInput != null && ageError != null && ageContinueButton != null
                && identityUsernameInput != null && identityTagInput != null && identityEmailInput != null
                && identityPreview != null && identityError != null && randomTagButton != null
                && saveIdentityButton != null && signOutButton != null;
        }
    }
}
