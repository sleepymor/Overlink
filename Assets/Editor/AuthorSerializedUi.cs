using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Authors the small pieces of UI which must exist before Play mode. Runtime controllers bind
    /// these objects; they are never allowed to create the screen foundation as a fallback.
    /// </summary>
    public static class AuthorSerializedUi
    {
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        const string AuthPath = "Assets/Scenes/Auth.unity";
        const string SettingsPath = "Assets/Scenes/Settings.unity";
        const string GachaPath = "Assets/Scenes/Gacha.unity";

        // Auth uses the same tactical-network tokens as the supplied Main Menu reference.
        // Keep these here, rather than in a runtime fallback, so the authored scene remains the
        // source of truth and the screen is immediately inspectable in the Editor.
        static readonly Color Background = new Color32(6, 13, 20, 255);
        static readonly Color Scrim = new Color(0.015f, 0.025f, 0.045f, 0.94f);
        static readonly Color Card = new Color32(15, 29, 41, 255);
        static readonly Color Control = new Color32(21, 38, 51, 255);
        static readonly Color Accent = new Color32(41, 223, 242, 255);
        static readonly Color Amber = new Color32(255, 200, 87, 255);
        static readonly Color Bright = new Color32(221, 232, 237, 255);
        static readonly Color Dim = new Color32(130, 150, 169, 255);
        static readonly Color Hairline = new Color(0.18f, 0.30f, 0.36f, 0.72f);

        [MenuItem("Overlink/UI/Author Serialized UI Foundations")]
        public static void AuthorAll()
        {
            AuthorScene(AuthPath, AuthorAuth);
            AuthorScene(MainMenuPath, AuthorMainMenu);
            AuthorScene(SettingsPath, AuthorSettings);
            AuthorScene(GachaPath, AuthorSignalBayChoices);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SerializedUI] Authored Main Menu profile, Settings toggles, and Signal Bay visibility.");
        }

        [MenuItem("Overlink/UI/Author Auth Screen")]
        public static void AuthorAuthOnly()
        {
            AuthorScene(AuthPath, AuthorAuth);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SerializedUI] Auth screen authored and saved.");
        }

        public static void BatchAuthorAll()
        {
            AuthorAll();
        }

        static void AuthorScene(string path, Action<Scene> author)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError("[SerializedUI] Missing scene: " + path);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            author(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AuthorMainMenu(Scene scene)
        {
            GameObject root = FindSceneObject(scene, "MainMenu");
            Transform canvas = root != null ? root.transform.Find("Canvas") : FindSceneTransform(scene, "Canvas");
            if (canvas == null)
            {
                Debug.LogError("[SerializedUI] Main Menu Canvas is missing.");
                return;
            }

            GameObject panel = FindOrCreate("Profile Panel", canvas, false);
            Stretch(panel.GetComponent<RectTransform>());
            AddImage(panel, Scrim, false);

            GameObject scrim = FindOrCreate("Scrim", panel.transform);
            Stretch(scrim.GetComponent<RectTransform>());
            Image scrimImage = AddImage(scrim, Scrim, true);
            Button scrimButton = GetOrAdd<Button>(scrim);
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.targetGraphic = scrimImage;

            GameObject card = FindOrCreate("Card", panel.transform);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            Anchor(cardRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(500f, 500f));
            AddImage(card, Card, true);
            UiCardBorder(card);
            GameObject content = FindOrCreate("Content", card.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(.08f, .08f);
            contentRect.anchorMax = new Vector2(.92f, .92f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

            CreateText(content.transform, "Title", "IDENTITY PROFILE", 27f, Bright,
                new Vector2(.0f, .82f), new Vector2(1f, 1f), TextAlignmentOptions.Center);
            CreateIdentityRow(content.transform, "Username", "CALLSIGN", .60f);
            CreateIdentityRow(content.transform, "Tag", "TAG", .43f);
            CreateIdentityRow(content.transform, "Full ID", "FULL ID", .26f);
            CreateText(content.transform, "Copy Feedback", string.Empty, 13f, Accent,
                new Vector2(0f, .16f), new Vector2(1f, .24f), TextAlignmentOptions.Center);
            CreateButton(content.transform, "Close Button", "CLOSE", .5f, .08f, 150f, 42f, Accent);

            panel.SetActive(false);
            Debug.Log("[SerializedUI] Main Menu profile dialog authored at Canvas/Profile Panel.");
        }

        static void AuthorAuth(Scene scene)
        {
            GameObject controllerObject = FindSceneObject(scene, "Auth Screen");
            if (controllerObject == null)
            {
                Debug.LogError("[SerializedUI] Auth Screen controller object is missing.");
                return;
            }

            GameObject eventSystem = FindSceneObject(scene, "EventSystem");
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
            }

            Transform canvas = FindSceneTransform(scene, "Auth Canvas");
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Auth Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Canvas canvasComponent = canvasObject.GetComponent<Canvas>();
                canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasComponent.sortingOrder = 20;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                canvas = canvasObject.transform;
            }

            GameObject background = FindOrCreate("Background", canvas);
            Stretch(background.GetComponent<RectTransform>());
            AddImage(background, Background, false);
            AuthorAuthBackdrop(background.transform);

            // The form intentionally lives to the right of the composition. The empty left field is
            // part of the reference language: it gives the logo and signal copy room to breathe and
            // keeps the credential task visually distinct from the world behind it.
            GameObject card = FindOrCreate("Card", canvas);
            Anchor(card.GetComponent<RectTransform>(), new Vector2(.72f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(660f, 820f));
            AddImage(card, Card, false);
            UiCardBorder(card);
            CreateText(card.transform, "Card eyebrow", "SECURE LINK / AUTH NODE 01", 11f, Accent,
                new Vector2(.09f, .91f), new Vector2(.91f, .95f), TextAlignmentOptions.MidlineLeft);
            CreateLine(card.transform, "Card header line", new Vector2(.09f, .895f), new Vector2(.91f, .895f), Hairline);

            GameObject loading = CreatePanel(card.transform, "Loading Panel");
            TextMeshProUGUI loadingText = CreateText(loading.transform, "Status", "ESTABLISHING SECURE LINK...", 15f, Dim,
                new Vector2(.08f, .37f), new Vector2(.92f, .48f), TextAlignmentOptions.Center);
            CreateLogo(loading.transform, "Wordmark", new Vector2(.12f, .57f), new Vector2(.88f, .78f));
            CreateText(loading.transform, "Loading caption", "TACTICAL NETWORK // ONLINE", 10f, Accent,
                new Vector2(.1f, .50f), new Vector2(.9f, .55f), TextAlignmentOptions.Center);

            GameObject signIn = CreatePanel(card.transform, "Sign-In Panel");
            TextMeshProUGUI title = CreateText(signIn.transform, "Title", "SIGN IN", 31f, Bright,
                new Vector2(.09f, .81f), new Vector2(.91f, .89f), TextAlignmentOptions.MidlineLeft);
            CreateText(signIn.transform, "Subtitle", "AUTHENTICATE TO ACCESS THE SIGNAL ARCHIVE", 11f, Dim,
                new Vector2(.09f, .76f), new Vector2(.91f, .81f), TextAlignmentOptions.MidlineLeft);
            Button google = CreateAuthButton(signIn.transform, "Google Button", "CONTINUE WITH GOOGLE", .09f, .67f, .82f, .065f, Accent);
            TextMeshProUGUI googleNote = CreateText(signIn.transform, "Google Unavailable Note",
                "GOOGLE AUTHENTICATION IS UNAVAILABLE IN THIS BUILD. USE AN OVERLINK ACCOUNT BELOW.",
                11f, Dim, new Vector2(.09f, .61f), new Vector2(.91f, .67f), TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI divider = CreateText(signIn.transform, "Or", "OR  //  OVERLINK CREDENTIALS", 10f, Accent,
                new Vector2(.09f, .59f), new Vector2(.91f, .635f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField username = CreateInput(signIn.transform, "Username Input", "USERNAME", .505f);
            TMP_InputField password = CreateInput(signIn.transform, "Password Input", "PASSWORD", .425f);
            TMP_InputField confirm = CreateInput(signIn.transform, "Confirm Password Input", "CONFIRM PASSWORD", .345f);
            TextMeshProUGUI passwordHint = CreateText(signIn.transform, "Password Hint", "8–64 CHARACTERS  //  UPPER  //  LOWER  //  NUMBER  //  SYMBOL", 10f, Dim,
                new Vector2(.09f, .30f), new Vector2(.91f, .34f), TextAlignmentOptions.MidlineLeft);
            Button submit = CreateAuthButton(signIn.transform, "Submit Button", "SIGN IN  //  ESTABLISH LINK", .09f, .205f, .82f, .07f, Accent);
            Button toggle = CreateAuthButton(signIn.transform, "Create Account Button", "CREATE ACCOUNT", .09f, .115f, .39f, .055f, Control);
            Button forgot = CreateAuthButton(signIn.transform, "Forgot Password Button", "PASSWORD HELP", .52f, .115f, .39f, .055f, Control);
            TextMeshProUGUI signInError = CreateText(signIn.transform, "Error", string.Empty, 13f, new Color32(255, 125, 116, 255),
                new Vector2(.09f, .055f), new Vector2(.91f, .105f), TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI status = CreateText(signIn.transform, "Account Status", string.Empty, 13f, Dim,
                new Vector2(.09f, .015f), new Vector2(.91f, .05f), TextAlignmentOptions.MidlineLeft);

            GameObject setup = CreatePanel(card.transform, "Setup Panel");
            GameObject age = CreatePanel(setup.transform, "Age Step");
            CreateText(age.transform, "Title", "AGE VERIFICATION", 30f, Bright, new Vector2(.09f, .80f), new Vector2(.91f, .89f), TextAlignmentOptions.MidlineLeft);
            CreateText(age.transform, "Body", "OVERLINK REQUIRES PLAYERS TO BE AT LEAST 13. THIS CHECK IS LINKED TO YOUR ACCOUNT AND ONLY HAPPENS ONCE.", 12f, Dim, new Vector2(.09f, .64f), new Vector2(.91f, .78f), TextAlignmentOptions.MidlineLeft);
            CreateText(age.transform, "Age label", "ACCESS CLEARANCE / AGE", 10f, Accent, new Vector2(.09f, .57f), new Vector2(.91f, .61f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField ageInput = CreateInput(age.transform, "Age Input", "YOUR AGE", .49f);
            TextMeshProUGUI ageError = CreateText(age.transform, "Error", string.Empty, 13f, new Color32(255, 125, 116, 255), new Vector2(.09f, .36f), new Vector2(.91f, .44f), TextAlignmentOptions.MidlineLeft);
            Button ageContinue = CreateAuthButton(age.transform, "Continue Button", "CONTINUE  //  VERIFY", .09f, .25f, .82f, .07f, Accent);

            GameObject identity = CreatePanel(setup.transform, "Identity Step");
            CreateText(identity.transform, "Title", "IDENTITY REGISTRATION", 28f, Bright, new Vector2(.09f, .80f), new Vector2(.91f, .89f), TextAlignmentOptions.MidlineLeft);
            CreateText(identity.transform, "Body", "THIS IS HOW OTHER OPERATORS SEE AND ADD YOU.", 11f, Dim, new Vector2(.09f, .75f), new Vector2(.91f, .80f), TextAlignmentOptions.MidlineLeft);
            CreateText(identity.transform, "Preview label", "ACTIVE CALLSIGN", 10f, Accent, new Vector2(.09f, .67f), new Vector2(.91f, .71f), TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI preview = CreateText(identity.transform, "Preview", "Name#Tag", 34f, Bright, new Vector2(.09f, .59f), new Vector2(.91f, .68f), TextAlignmentOptions.MidlineLeft);
            TMP_InputField identityUsername = CreateInput(identity.transform, "Username Input", "USERNAME / 3–16 CHARACTERS", .505f);
            TMP_InputField identityTag = CreateInput(identity.transform, "Tag Input", "TAG / 3–5 CHARACTERS", .425f);
            Button randomTag = CreateAuthButton(identity.transform, "Random Tag Button", "REROLL", .69f, .345f, .22f, .052f, Control);
            TMP_InputField email = CreateInput(identity.transform, "Email Input", "EMAIL ADDRESS", .32f);
            CreateText(identity.transform, "Email Purpose", "USED FOR ACCOUNT CONTACT AND FUTURE PURCHASES. NEVER SHOWN TO OTHER PLAYERS.", 10f, Dim, new Vector2(.09f, .245f), new Vector2(.91f, .30f), TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI identityError = CreateText(identity.transform, "Error", string.Empty, 13f, new Color32(255, 125, 116, 255), new Vector2(.09f, .18f), new Vector2(.91f, .235f), TextAlignmentOptions.MidlineLeft);
            Button save = CreateAuthButton(identity.transform, "Save Button", "SAVE IDENTITY  //  CONTINUE", .09f, .095f, .82f, .07f, Accent);
            Button signOut = CreateAuthButton(identity.transform, "Switch Account Button", "USE A DIFFERENT ACCOUNT", .09f, .025f, .82f, .05f, Control);

            confirm.gameObject.SetActive(false);
            passwordHint.gameObject.SetActive(false);
            googleNote.gameObject.SetActive(false);
            signInError.gameObject.SetActive(false);
            ageError.gameObject.SetActive(false);
            identityError.gameObject.SetActive(false);
            signIn.SetActive(false);
            setup.SetActive(false);
            loading.SetActive(true);

            SerializedObject serialized = new SerializedObject(controllerObject.GetComponent("AuthScreenController"));
            SetRef(serialized, "loadingPanel", loading);
            SetRef(serialized, "signInPanel", signIn);
            SetRef(serialized, "setupPanel", setup);
            SetRef(serialized, "ageStep", age);
            SetRef(serialized, "identityStep", identity);
            SetRef(serialized, "loadingText", loadingText);
            SetRef(serialized, "titleText", title);
            SetRef(serialized, "statusText", status);
            SetRef(serialized, "signInError", signInError);
            SetRef(serialized, "usernameInput", username);
            SetRef(serialized, "passwordInput", password);
            SetRef(serialized, "confirmPasswordInput", confirm);
            SetRef(serialized, "passwordHint", passwordHint);
            SetRef(serialized, "googleButton", google);
            SetRef(serialized, "googleDivider", divider);
            SetRef(serialized, "googleUnavailableNote", googleNote);
            SetRef(serialized, "submitButton", submit);
            SetRef(serialized, "toggleModeButton", toggle);
            SetRef(serialized, "forgotPasswordButton", forgot);
            SetRef(serialized, "ageInput", ageInput);
            SetRef(serialized, "ageError", ageError);
            SetRef(serialized, "ageContinueButton", ageContinue);
            SetRef(serialized, "identityUsernameInput", identityUsername);
            SetRef(serialized, "identityTagInput", identityTag);
            SetRef(serialized, "identityEmailInput", email);
            SetRef(serialized, "identityPreview", preview);
            SetRef(serialized, "identityError", identityError);
            SetRef(serialized, "randomTagButton", randomTag);
            SetRef(serialized, "saveIdentityButton", save);
            SetRef(serialized, "signOutButton", signOut);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[SerializedUI] Auth Canvas and all Auth controls authored and assigned.");
        }

        static void AuthorAuthBackdrop(Transform parent)
        {
            // Quiet technical ornamentation: enough structure to make the auth screen feel authored,
            // but low contrast so the credential task remains the visual priority.
            CreateLine(parent, "Top rule", new Vector2(.02f, .94f), new Vector2(.98f, .94f), Hairline);
            CreateLine(parent, "Bottom rule", new Vector2(.02f, .055f), new Vector2(.98f, .055f), Hairline);
            CreateLine(parent, "Center rule", new Vector2(.055f, .12f), new Vector2(.055f, .86f), new Color(0.12f, 0.22f, 0.28f, .45f));

            CreateLogo(parent, "Brand mark", new Vector2(.07f, .78f), new Vector2(.30f, .87f));
            CreateText(parent, "Brand caption", "TACTICAL NETWORK // ONLINE", 11f, Bright,
                new Vector2(.10f, .73f), new Vector2(.38f, .77f), TextAlignmentOptions.MidlineLeft);
            CreateText(parent, "Brand status", "AUTHENTICATION NODE / 01", 10f, Accent,
                new Vector2(.10f, .705f), new Vector2(.38f, .735f), TextAlignmentOptions.MidlineLeft);

            CreateText(parent, "Signal title", "SIGNAL ARCHIVE", 11f, Accent,
                new Vector2(.07f, .14f), new Vector2(.35f, .18f), TextAlignmentOptions.MidlineLeft);
            CreateText(parent, "Signal name", "SECURE HANDSHAKE", 28f, Bright,
                new Vector2(.07f, .095f), new Vector2(.48f, .145f), TextAlignmentOptions.MidlineLeft);
            CreateLine(parent, "Signal accent", new Vector2(.07f, .082f), new Vector2(.105f, .082f), Accent);
            CreateText(parent, "Signal detail", "A VERIFIED LINK IS REQUIRED TO ENTER OVERLINK.", 10f, Dim,
                new Vector2(.12f, .068f), new Vector2(.48f, .095f), TextAlignmentOptions.MidlineLeft);
            CreateText(parent, "Version", "OVERLINK / AUTH  •  v2026.11.9", 10f, Dim,
                new Vector2(.72f, .065f), new Vector2(.94f, .095f), TextAlignmentOptions.MidlineRight);
        }

        static void UiCardBorder(GameObject card)
        {
            Outline outline = card.GetComponent<Outline>() ?? card.AddComponent<Outline>();
            outline.effectColor = Hairline;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        static Image CreateLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject go = FindOrCreate(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (Mathf.Approximately(anchorMin.y, anchorMax.y)) rect.sizeDelta = new Vector2(rect.sizeDelta.x, 1f);
            if (Mathf.Approximately(anchorMin.x, anchorMax.x)) rect.sizeDelta = new Vector2(1f, rect.sizeDelta.y);
            Image image = AddImage(go, color, false);
            image.raycastTarget = false;
            return image;
        }

        static Image CreateLogo(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = FindOrCreate(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            TextMeshProUGUI oldText = go.GetComponent<TextMeshProUGUI>();
            if (oldText != null) UnityEngine.Object.DestroyImmediate(oldText);

            Image image = AddImage(go, Color.white, false);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Branding/OverlinkLogoWordmarkWhite.png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static void AuthorSettings(Scene scene)
        {
            Transform panel = FindSceneTransform(scene, "SettingsPanel");
            if (panel == null)
            {
                Debug.LogError("[SerializedUI] SettingsPanel is missing.");
                return;
            }

            GameObject settingsControllerObject = FindSceneObjectWithComponent<MonoBehaviour>(scene, "SettingsController");
            SerializedObject serialized = settingsControllerObject != null
                ? new SerializedObject(settingsControllerObject.GetComponent("SettingsController"))
                : null;

            Toggle postFx = CreateToggle(panel, "PostFxToggle", "POST FX", -135f);
            Toggle shake = CreateToggle(panel, "ReduceShakeToggle", "REDUCE SHAKE", -190f);
            Toggle flash = CreateToggle(panel, "ReduceFlashToggle", "REDUCE FLASH", -245f);
            Toggle motion = CreateToggle(panel, "ReducedMotionToggle", "REDUCED MOTION", -300f);

            if (serialized != null)
            {
                serialized.FindProperty("postFxToggle").objectReferenceValue = postFx;
                serialized.FindProperty("reduceShakeToggle").objectReferenceValue = shake;
                serialized.FindProperty("reduceFlashToggle").objectReferenceValue = flash;
                serialized.FindProperty("reducedMotionToggle").objectReferenceValue = motion;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log("[SerializedUI] Settings accessibility toggles authored and assigned.");
        }

        static void AuthorSignalBayChoices(Scene scene)
        {
            Transform bay = FindSceneTransform(scene, "Figma Signal Bay");
            if (bay == null)
            {
                Debug.LogError("[SerializedUI] Figma Signal Bay hierarchy is missing.");
                return;
            }

            // The editable bay is the authored composition. The old full-screen reference remains
            // available as a rollback asset, but must not be rendered in the shipped scene.
            CanvasGroup bayGroup = bay.GetComponent<CanvasGroup>();
            if (bayGroup != null)
            {
                bayGroup.alpha = 1f;
                bayGroup.interactable = true;
                bayGroup.blocksRaycasts = true;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(image.sprite);
                if (assetPath.EndsWith("SignalBayBase.png", StringComparison.OrdinalIgnoreCase))
                {
                    image.sprite = null;
                    image.color = new Color(0f, 0f, 0f, 0f);
                    image.raycastTarget = false;
                    Debug.Log("[SerializedUI] Disabled active SignalBayBase.png composition on " + image.name + ".");
                }
            }

            // PackScreen is the legacy full-screen image host. Keep the GameObject as a rollback
            // marker, but remove its sprite from the saved scene so the authored hierarchy is the only
            // rendered composition even if the importer does not report the source path above.
            Transform legacyPackScreen = FindSceneTransform(scene, "PackScreen");
            Image legacyImage = legacyPackScreen != null ? legacyPackScreen.GetComponent<Image>() : null;
            if (legacyImage != null && legacyImage.sprite != null)
            {
                string legacyPath = AssetDatabase.GetAssetPath(legacyImage.sprite);
                if (legacyPath.EndsWith("SignalBayBase.png", StringComparison.OrdinalIgnoreCase)
                    || legacyImage.sprite.name.Equals("SignalBayBase", StringComparison.OrdinalIgnoreCase))
                {
                    legacyImage.sprite = null;
                    legacyImage.color = new Color(0f, 0f, 0f, 0f);
                    legacyImage.raycastTarget = false;
                    Debug.Log("[SerializedUI] Removed legacy SignalBayBase sprite from PackScreen.");
                }
            }
            Debug.Log("[SerializedUI] Signal Bay pack-choice buttons authored on all three choices.");
        }

        static Toggle CreateToggle(Transform parent, string name, string label, float y)
        {
            GameObject root = FindOrCreate(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(230f, y), new Vector2(250f, 38f));
            Image background = AddImage(root, Control, true);
            Toggle toggle = GetOrAdd<Toggle>(root);
            toggle.targetGraphic = background;
            toggle.transition = Selectable.Transition.ColorTint;

            GameObject checkObject = FindOrCreate("Checkmark", root.transform);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            Anchor(checkRect, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(10f, 0f), new Vector2(16f, 16f));
            Image check = AddImage(checkObject, Accent, false);
            toggle.graphic = check;

            GameObject labelObject = FindOrCreate("Label", root.transform);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(38f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(labelObject);
            text.text = label;
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Bold;
            text.color = Bright;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return toggle;
        }

        static GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = FindOrCreate(name, parent);
            Stretch(panel.GetComponent<RectTransform>());
            return panel;
        }

        static TMP_InputField CreateInput(Transform parent, string name, string placeholder, float y)
        {
            GameObject root = FindOrCreate(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            // These values are normalized bounds, not an anchor plus a pivot. The old helper call
            // collapsed the field to a sliver because Anchor interpreted (.9, y) as the pivot.
            rect.anchorMin = new Vector2(.09f, y);
            rect.anchorMax = new Vector2(.91f, y);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 48f);
            rect.localScale = Vector3.one;
            Image image = AddImage(root, Control, true);
            TMP_InputField input = GetOrAdd<TMP_InputField>(root);
            input.targetGraphic = image;
            GameObject textArea = FindOrCreate("Text Area", root.transform);
            RectTransform areaRect = textArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(16f, 4f);
            areaRect.offsetMax = new Vector2(-16f, -4f);
            GetOrAdd<RectMask2D>(textArea);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(FindOrCreate("Text", textArea.transform));
            Stretch(text.rectTransform);
            text.color = Bright;
            text.fontSize = 18f;
            text.font = DisplayFont;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            TextMeshProUGUI hint = GetOrAdd<TextMeshProUGUI>(FindOrCreate("Placeholder", textArea.transform));
            Stretch(hint.rectTransform);
            hint.text = placeholder;
            hint.color = Dim;
            hint.fontSize = 18f;
            hint.font = DisplayFont;
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            hint.enableWordWrapping = false;
            hint.raycastTarget = false;
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = name.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0
                ? TMP_InputField.ContentType.Password
                : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            return input;
        }

        static void SetRef(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }

        static void CreateIdentityRow(Transform parent, string name, string caption, float y)
        {
            GameObject row = FindOrCreate(name + " Row", parent);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y + .12f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            CreateText(row.transform, "Caption", caption, 14f, Dim, Vector2.zero, new Vector2(.26f, 1f), TextAlignmentOptions.MidlineLeft);
            CreateText(row.transform, "Value", "—", 16f, Bright, new Vector2(.28f, 0f), new Vector2(.72f, 1f), TextAlignmentOptions.MidlineLeft);
            CreateButton(row.transform, "Copy Button", "COPY", .78f, .0f, 74f, 30f, Accent);
        }

        static Button CreateButton(Transform parent, string name, string label, float x, float y, float width, float height, Color color)
        {
            GameObject go = FindOrCreate(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(x, y), new Vector2(x, y), Vector2.zero, new Vector2(width, height));
            Image image = AddImage(go, color, true);
            Button button = GetOrAdd<Button>(go);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            CreateText(go.transform, "Label", label, 14f, Color.white, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            return button;
        }

        static Button CreateAuthButton(Transform parent, string name, string label, float x, float y,
            float width, float height, Color color)
        {
            GameObject go = FindOrCreate(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x + width, y + height);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            Image image = AddImage(go, color, true);
            Button button = GetOrAdd<Button>(go);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            CreateText(go.transform, "Label", label, 16f, color == Accent ? Background : Bright, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center);
            return button;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
        {
            GameObject go = FindOrCreate(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(go);
            text.text = value;
            text.fontSize = size;
            text.font = DisplayFont;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

        static TMP_FontAsset DisplayFont
        {
            get
            {
                if (displayFont == null)
                    displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/ChakraPetch-Bold.asset");
                return displayFont;
            }
        }

        static TMP_FontAsset displayFont;

        static GameObject FindOrCreate(string name, Transform parent, bool active = true)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.SetActive(active);
                return existing.gameObject;
            }
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.SetActive(active);
            return go;
        }

        static Image AddImage(GameObject go, Color color, bool raycast)
        {
            Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindChild(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindSceneTransform(Scene scene, string name)
        {
            GameObject found = FindSceneObject(scene, name);
            return found != null ? found.transform : null;
        }

        static GameObject FindChild(Transform root, string name)
        {
            if (root.name == name) return root.gameObject;
            foreach (Transform child in root)
            {
                GameObject found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static GameObject FindSceneObjectWithComponent<T>(Scene scene, string componentName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                    if (component.GetType().Name == componentName) return component.gameObject;
            }
            return null;
        }

    }
}
