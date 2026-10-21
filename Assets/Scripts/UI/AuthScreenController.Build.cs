using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Builds the Auth screen's widgets at runtime when no prefabricated ones are wired up.
    /// <para>
    /// The Auth screen is the game's entry point and the only screen a player cannot skip, so it has to
    /// work when the scene holds nothing but this component: no serialized references to lose, no prefab
    /// to re-import, no manual Inspector step between "cloned the repo" and "runs". Assign the fields on
    /// the other half of this class and the builder steps aside entirely (see <see cref="IsWired"/>),
    /// which is how a designed look gets swapped in later without touching logic.
    /// </para>
    /// </summary>
    public sealed partial class AuthScreenController
    {
        const float CardWidth = 480f;
        const float CardHeight = 660f;

        /// <summary>True when a hand-wired screen is present and the builder should stay out of the way.</summary>
        bool IsWired()
        {
            return loadingPanel != null && signInPanel != null && setupPanel != null
                   && ageStep != null && identityStep != null;
        }

        void BuildRuntimeUi()
        {
            UiKit.EnsureEventSystem();

            // Always its own canvas: the Auth scene is the first thing a build loads, and reusing a
            // stray canvas left over from a persistent manager would inherit its scaling and sorting.
            var canvas = UiKit.CreateCanvas("Auth Canvas");

            UiKit.Filled("Background", canvas.transform, UiKit.Background);

            var cardGo = UiKit.Child("Card", canvas.transform);
            var card = cardGo.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardGo.AddComponent<Image>().color = UiKit.Card;

            BuildLoadingPanel(card);
            BuildSignInPanel(card);
            BuildSetupPanel(card);
        }

        void BuildLoadingPanel(Transform parent)
        {
            loadingPanel = UiKit.Stack("Loading Panel", parent);
            UiKit.Text("Wordmark", loadingPanel.transform, "OVERLINK", 34f, UiKit.TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);
            loadingText = UiKit.Text("Status", loadingPanel.transform, "Starting…", 16f, UiKit.TextDim,
                TextAlignmentOptions.Center);
        }

        void BuildSignInPanel(Transform parent)
        {
            signInPanel = UiKit.Stack("Sign-In Panel", parent);
            var root = signInPanel.transform;

            titleText = UiKit.Text("Title", root, "Sign in", 30f, UiKit.TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiKit.Text("Subtitle", root, "An account is required to play Overlink.", 14f, UiKit.TextDim,
                TextAlignmentOptions.Center);

            googleButton = UiKit.Button("Google Button", root, "Continue with Google", UiKit.Style.Google)
                .GetComponent<Button>();

            UiKit.Text("Or", root, "—  or use your Overlink account  —", 12f, UiKit.TextDim,
                TextAlignmentOptions.Center);

            usernameInput = UiKit.InputField("Username Input", root, "Username", 20,
                TMP_InputField.ContentType.Standard);
            passwordInput = UiKit.InputField("Password Input", root, "Password", 30,
                TMP_InputField.ContentType.Password);
            confirmPasswordInput = UiKit.InputField("Confirm Password Input", root, "Confirm password", 30,
                TMP_InputField.ContentType.Password);
            confirmPasswordInput.gameObject.SetActive(false);

            submitButton = UiKit.Button("Submit Button", root, "Sign In", UiKit.Style.Primary).GetComponent<Button>();

            toggleModeButton = UiKit.Button("Create Account Button", root, "Create an account", UiKit.Style.Ghost)
                .GetComponent<Button>();
            forgotPasswordButton = UiKit.Button("Forgot Password Button", root, "Forgot password?", UiKit.Style.Ghost)
                .GetComponent<Button>();

            signInError = UiKit.Text("Error", root, "", 14f, UiKit.TextError, TextAlignmentOptions.Center);
            signInError.gameObject.SetActive(false);

            statusText = UiKit.Text("Account Status", root, "", 13f, UiKit.TextDim, TextAlignmentOptions.Center);
        }

        void BuildSetupPanel(Transform parent)
        {
            setupPanel = UiKit.Stack("Setup Panel", parent);
            BuildAgeStep(setupPanel.transform);
            BuildIdentityStep(setupPanel.transform);
        }

        void BuildAgeStep(Transform parent)
        {
            ageStep = UiKit.Stack("Age Step", parent);
            var root = ageStep.transform;

            UiKit.Text("Title", root, "How old are you?", 28f, UiKit.TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiKit.Text("Body", root,
                $"Overlink requires players to be at least {minimumAge}. This is tied to your account, " +
                "not to this device, so you only answer once.",
                14f, UiKit.TextDim, TextAlignmentOptions.Center, FontStyles.Normal, 62f);

            ageInput = UiKit.InputField("Age Input", root, "Your age", 3,
                TMP_InputField.ContentType.IntegerNumber);

            ageError = UiKit.Text("Error", root, "", 14f, UiKit.TextError, TextAlignmentOptions.Center);
            ageError.gameObject.SetActive(false);

            ageContinueButton = UiKit.Button("Continue Button", root, "Continue", UiKit.Style.Primary)
                .GetComponent<Button>();
        }

        void BuildIdentityStep(Transform parent)
        {
            identityStep = UiKit.Stack("Identity Step", parent);
            var root = identityStep.transform;

            UiKit.Text("Title", root, "Choose your ID", 28f, UiKit.TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiKit.Text("Body", root, "This is how other players see and add you.", 14f, UiKit.TextDim,
                TextAlignmentOptions.Center);

            identityPreview = UiKit.Text("Preview", root, "Name#Tag", 34f, UiKit.Accent,
                TextAlignmentOptions.Center, FontStyles.Bold);

            identityUsernameInput = UiKit.InputField("Username Input", root, "Username (3–16 characters)", 16,
                TMP_InputField.ContentType.Standard);

            // Tag and its reroll button sit side by side, so this row is horizontal inside a vertical stack.
            var row = UiKit.Child("Tag Row", root);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = UiKit.RowHeight;

            identityTagInput = UiKit.InputField("Tag Input", row.transform, "TAG (3–5 characters)", 5,
                TMP_InputField.ContentType.Standard);
            // Force upper case as typed rather than on submit, so what the player sees is what is saved.
            identityTagInput.onValidateInput = (_, _, added) =>
                char.IsLower(added) ? char.ToUpperInvariant(added) : added;

            var reroll = UiKit.Button("Random Tag Button", row.transform, "Reroll", UiKit.Style.Ghost);
            // The button already carries a LayoutElement from UiKit; a second one would be ignored.
            // Pin the width and withdraw the stretch claim UiKit gives buttons for vertical stacks.
            var rerollElement = reroll.GetComponent<LayoutElement>();
            rerollElement.flexibleWidth = 0f;
            rerollElement.minWidth = rerollElement.preferredWidth = 86f;
            randomTagButton = reroll.GetComponent<Button>();

            identityError = UiKit.Text("Error", root, "", 14f, UiKit.TextError, TextAlignmentOptions.Center);
            identityError.gameObject.SetActive(false);

            saveIdentityButton = UiKit.Button("Save Button", root, "Save and continue", UiKit.Style.Primary)
                .GetComponent<Button>();
            signOutButton = UiKit.Button("Switch Account Button", root, "Use a different account", UiKit.Style.Danger)
                .GetComponent<Button>();
        }
    }
}
