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

        static readonly Color Scrim = new Color(0.015f, 0.025f, 0.045f, 0.92f);
        static readonly Color Card = new Color(0.055f, 0.09f, 0.14f, 1f);
        static readonly Color Control = new Color(0.035f, 0.062f, 0.105f, 1f);
        static readonly Color Accent = new Color(0.435f, 0.69f, 0.97f, 1f);
        static readonly Color Bright = new Color(0.925f, 0.941f, 0.965f, 1f);
        static readonly Color Dim = new Color(0.51f, 0.588f, 0.663f, 1f);

        [MenuItem("Overlink/UI/Author Serialized UI Foundations")]
        public static void AuthorAll()
        {
            AuthorScene(AuthPath, AuthorAuth);
            AuthorScene(MainMenuPath, AuthorMainMenu);
            AuthorScene(SettingsPath, AuthorSettings);
            AuthorScene(GachaPath, AuthorSignalBayChoices);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SerializedUI] Authored Main Menu profile, Settings toggles, and Signal Bay pack-choice buttons.");
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
            Anchor(cardRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(440f, 400f));
            AddImage(card, Card, true);
            GameObject content = FindOrCreate("Content", card.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(.08f, .08f);
            contentRect.anchorMax = new Vector2(.92f, .92f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

            CreateText(content.transform, "Title", "PLAYER ID", 26f, Bright,
                new Vector2(.0f, .82f), new Vector2(1f, 1f), TextAlignmentOptions.Center);
            CreateIdentityRow(content.transform, "Username", "USERNAME", .60f);
            CreateIdentityRow(content.transform, "Tag", "TAG", .43f);
            CreateIdentityRow(content.transform, "Full ID", "FULL ID", .26f);
            CreateText(content.transform, "Copy Feedback", string.Empty, 13f, Accent,
                new Vector2(0f, .16f), new Vector2(1f, .24f), TextAlignmentOptions.Center);
            CreateButton(content.transform, "Close Button", "CLOSE", .02f, .14f, 130f, 40f, Accent);

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
            AddImage(background, new Color32(8, 14, 24, 255), false);

            GameObject card = FindOrCreate("Card", canvas);
            Anchor(card.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(480f, 660f));
            AddImage(card, new Color32(18, 31, 47, 255), false);

            GameObject loading = CreatePanel(card.transform, "Loading Panel");
            TextMeshProUGUI loadingText = CreateText(loading.transform, "Status", "Starting...", 16f, Dim,
                new Vector2(.08f, .42f), new Vector2(.92f, .58f), TextAlignmentOptions.Center);
            CreateText(loading.transform, "Wordmark", "OVERLINK", 34f, Bright,
                new Vector2(.08f, .58f), new Vector2(.92f, .75f), TextAlignmentOptions.Center);

            GameObject signIn = CreatePanel(card.transform, "Sign-In Panel");
            TextMeshProUGUI title = CreateText(signIn.transform, "Title", "Sign in", 30f, Bright,
                new Vector2(.08f, .86f), new Vector2(.92f, .96f), TextAlignmentOptions.Center);
            CreateText(signIn.transform, "Subtitle", "An account is required to play Overlink.", 14f, Dim,
                new Vector2(.08f, .80f), new Vector2(.92f, .86f), TextAlignmentOptions.Center);
            Button google = CreateButton(signIn.transform, "Google Button", "Continue with Google", .12f, .71f, .76f, .07f, Accent);
            TextMeshProUGUI googleNote = CreateText(signIn.transform, "Google Unavailable Note",
                "Google sign-in isn't part of the browser build. Play here with an Overlink account below, or download the desktop version to sign in with Google.",
                12f, Dim, new Vector2(.08f, .66f), new Vector2(.92f, .74f), TextAlignmentOptions.Center);
            TextMeshProUGUI divider = CreateText(signIn.transform, "Or", "—  or use your Overlink account  —", 12f, Dim,
                new Vector2(.08f, .64f), new Vector2(.92f, .70f), TextAlignmentOptions.Center);
            TMP_InputField username = CreateInput(signIn.transform, "Username Input", "Username", .53f);
            TMP_InputField password = CreateInput(signIn.transform, "Password Input", "Password", .45f);
            TMP_InputField confirm = CreateInput(signIn.transform, "Confirm Password Input", "Confirm password", .37f);
            TextMeshProUGUI passwordHint = CreateText(signIn.transform, "Password Hint", "8-64 characters, with upper, lower, number and symbol.", 11f, Dim,
                new Vector2(.08f, .32f), new Vector2(.92f, .38f), TextAlignmentOptions.Center);
            Button submit = CreateButton(signIn.transform, "Submit Button", "Sign In", .12f, .23f, .76f, .07f, Accent);
            Button toggle = CreateButton(signIn.transform, "Create Account Button", "Create an account", .12f, .15f, .37f, .06f, Control);
            Button forgot = CreateButton(signIn.transform, "Forgot Password Button", "Forgot password?", .51f, .15f, .37f, .06f, Control);
            TextMeshProUGUI signInError = CreateText(signIn.transform, "Error", string.Empty, 14f, new Color32(255, 110, 110, 255),
                new Vector2(.08f, .08f), new Vector2(.92f, .14f), TextAlignmentOptions.Center);
            TextMeshProUGUI status = CreateText(signIn.transform, "Account Status", string.Empty, 13f, Dim,
                new Vector2(.08f, .01f), new Vector2(.92f, .07f), TextAlignmentOptions.Center);

            GameObject setup = CreatePanel(card.transform, "Setup Panel");
            GameObject age = CreatePanel(setup.transform, "Age Step");
            CreateText(age.transform, "Title", "How old are you?", 28f, Bright, new Vector2(.08f, .80f), new Vector2(.92f, .92f), TextAlignmentOptions.Center);
            CreateText(age.transform, "Body", "Overlink requires players to be at least 13. This is tied to your account, not to this device, so you only answer once.", 14f, Dim, new Vector2(.08f, .65f), new Vector2(.92f, .80f), TextAlignmentOptions.Center);
            TMP_InputField ageInput = CreateInput(age.transform, "Age Input", "Your age", .50f);
            TextMeshProUGUI ageError = CreateText(age.transform, "Error", string.Empty, 14f, new Color32(255, 110, 110, 255), new Vector2(.08f, .35f), new Vector2(.92f, .43f), TextAlignmentOptions.Center);
            Button ageContinue = CreateButton(age.transform, "Continue Button", "Continue", .12f, .22f, .76f, .08f, Accent);

            GameObject identity = CreatePanel(setup.transform, "Identity Step");
            CreateText(identity.transform, "Title", "Choose your ID", 28f, Bright, new Vector2(.08f, .86f), new Vector2(.92f, .96f), TextAlignmentOptions.Center);
            CreateText(identity.transform, "Body", "This is how other players see and add you.", 14f, Dim, new Vector2(.08f, .80f), new Vector2(.92f, .86f), TextAlignmentOptions.Center);
            TextMeshProUGUI preview = CreateText(identity.transform, "Preview", "Name#Tag", 34f, Accent, new Vector2(.08f, .68f), new Vector2(.92f, .78f), TextAlignmentOptions.Center);
            TMP_InputField identityUsername = CreateInput(identity.transform, "Username Input", "Username (3-16 characters)", .58f);
            TMP_InputField identityTag = CreateInput(identity.transform, "Tag Input", "TAG (3-5 characters)", .50f);
            Button randomTag = CreateButton(identity.transform, "Random Tag Button", "Reroll", .68f, .41f, .20f, .06f, Control);
            TMP_InputField email = CreateInput(identity.transform, "Email Input", "Email address", .40f);
            CreateText(identity.transform, "Email Purpose", "Used to contact you about your account and future purchases. Other players never see it.", 11f, Dim, new Vector2(.08f, .31f), new Vector2(.92f, .39f), TextAlignmentOptions.Center);
            TextMeshProUGUI identityError = CreateText(identity.transform, "Error", string.Empty, 14f, new Color32(255, 110, 110, 255), new Vector2(.08f, .23f), new Vector2(.92f, .31f), TextAlignmentOptions.Center);
            Button save = CreateButton(identity.transform, "Save Button", "Save and continue", .12f, .13f, .76f, .07f, Accent);
            Button signOut = CreateButton(identity.transform, "Switch Account Button", "Use a different account", .12f, .05f, .76f, .06f, Control);

            confirm.gameObject.SetActive(false);
            passwordHint.gameObject.SetActive(false);
            googleNote.gameObject.SetActive(false);
            signInError.gameObject.SetActive(false);
            ageError.gameObject.SetActive(false);
            identityError.gameObject.SetActive(false);
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

            for (int i = 1; i <= 3; i++)
            {
                Transform choice = bay.Find("Pack Choice " + i);
                if (choice == null)
                {
                    Debug.LogError("[SerializedUI] Missing serialized pack choice: Pack Choice " + i);
                    continue;
                }

                Button button = choice.GetComponent<Button>() ?? choice.gameObject.AddComponent<Button>();
                Image image = choice.GetComponent<Image>() ?? choice.gameObject.AddComponent<Image>();
                image.raycastTarget = true;
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                foreach (Graphic graphic in choice.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = graphic.transform == choice;
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
            Anchor(rect, new Vector2(.1f, y), new Vector2(.9f, y), new Vector2(0f, 0f), new Vector2(0f, 48f));
            Image image = AddImage(root, Control, true);
            TMP_InputField input = GetOrAdd<TMP_InputField>(root);
            input.targetGraphic = image;
            GameObject textArea = FindOrCreate("Text Area", root.transform);
            RectTransform areaRect = textArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(16f, 4f);
            areaRect.offsetMax = new Vector2(-16f, -4f);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(FindOrCreate("Text", textArea.transform));
            text.color = Bright;
            text.fontSize = 18f;
            text.raycastTarget = false;
            TextMeshProUGUI hint = GetOrAdd<TextMeshProUGUI>(FindOrCreate("Placeholder", textArea.transform));
            hint.text = placeholder;
            hint.color = Dim;
            hint.fontSize = 18f;
            hint.raycastTarget = false;
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = hint;
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
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

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
