using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.Net
{
    /// <summary>Small runtime-created account panel for the demo scene.</summary>
    public sealed class AccountMenuUI : MonoBehaviour
    {
        AccountManager account;
        TextMeshProUGUI status;
        Button googleButton, guestButton, signOutButton;
        GameObject panel;

        void Awake()
        {
            account = AccountManager.Instance != null ? AccountManager.Instance : AccountManager.EnsureInstance();
            account.StatusChanged += SetStatus;
            account.AccountChanged += Refresh;
        }

        void Start()
        {
            BuildPanel();
            SetStatus(account.Status);
            Refresh(account.Kind);
        }

        void OnDestroy()
        {
            if (account == null) return;
            account.StatusChanged -= SetStatus;
            account.AccountChanged -= Refresh;
        }

        void BuildPanel()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("AccountCanvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            panel = new GameObject("AccountPanel");
            panel.transform.SetParent(canvas.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -18f);
            panelRect.sizeDelta = new Vector2(290f, 200f);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.04f, 0.06f, 0.1f, 0.92f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel(panel.transform, "Account");
            title.fontSize = 20f;
            status = CreateLabel(panel.transform, "Not signed in.");
            status.fontSize = 13f;
            googleButton = CreateButton(panel.transform, "Continue with Google", OnGoogleClicked);
            guestButton = CreateButton(panel.transform, "Play as Guest", OnGuestClicked);
            signOutButton = CreateButton(panel.transform, "Sign Out", OnSignOutClicked);
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string value)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = true;
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 22f;
            return text;
        }

        static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label.Replace(" ", ""));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.38f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 28f;
            var text = CreateLabel(go.transform, label);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 14f;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return button;
        }

        void OnGoogleClicked() => Forget(account.SignInWithGoogleAsync());
        void OnGuestClicked() => Forget(account.PlayAsGuestAsync());
        void OnSignOutClicked() => account.SignOut();

        void SetStatus(string value)
        {
            if (status != null) status.text = value;
        }

        void Refresh(AccountKind kind)
        {
            if (googleButton == null) return;
            bool signedIn = kind != AccountKind.SignedOut;
            googleButton.gameObject.SetActive(kind != AccountKind.Google);
            guestButton.gameObject.SetActive(kind == AccountKind.SignedOut);
            signOutButton.gameObject.SetActive(signedIn);
            googleButton.interactable = !account.IsBusy;
            guestButton.interactable = !account.IsBusy;
            signOutButton.interactable = !account.IsBusy;
        }

        static void Forget(Task task) => _ = task;
    }
}
