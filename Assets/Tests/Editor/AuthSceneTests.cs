using NUnit.Framework;
using Overlink.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.Tests.Editor
{
    public class AuthSceneTests
    {
        [Test]
        public void AuthScene_HasWiredReadableControlsAndMaskedPasswords()
        {
            const string path = "Assets/Scenes/Auth.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                AuthScreenController controller = null;
                Canvas canvas = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (controller == null) controller = root.GetComponentInChildren<AuthScreenController>(true);
                    if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
                }
                Assert.That(controller, Is.Not.Null);
                Assert.That(canvas, Is.Not.Null);
                var data = new SerializedObject(controller);
                string[] fields = { "loadingPanel", "signInPanel", "setupPanel", "ageStep", "identityStep",
                    "loadingText", "titleText", "statusText", "signInError", "usernameInput", "passwordInput",
                    "confirmPasswordInput", "submitButton", "toggleModeButton", "forgotPasswordButton",
                    "ageInput", "ageError", "ageContinueButton", "identityUsernameInput", "identityTagInput",
                    "identityEmailInput", "identityPreview", "identityError", "randomTagButton", "saveIdentityButton", "signOutButton" };
                foreach (string field in fields)
                    Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

                foreach (string field in new[] { "passwordInput", "confirmPasswordInput" })
                    Assert.That(((TMP_InputField)data.FindProperty(field).objectReferenceValue).inputType,
                        Is.EqualTo(TMP_InputField.InputType.Password), field);
                foreach (string field in new[] { "usernameInput", "passwordInput", "confirmPasswordInput", "ageInput",
                    "identityUsernameInput", "identityTagInput", "identityEmailInput" })
                {
                    var input = (TMP_InputField)data.FindProperty(field).objectReferenceValue;
                    Assert.That(input.GetComponent<RectTransform>().rect.width, Is.GreaterThan(100f), field);
                    Assert.That(input.textViewport, Is.Not.Null, field);
                    Assert.That(input.textComponent, Is.Not.Null, field);
                }
                Assert.That(canvas.transform.Find("Background/Brand mark").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(((GameObject)data.FindProperty("loadingPanel").objectReferenceValue).activeSelf, Is.True);
                Assert.That(((GameObject)data.FindProperty("signInPanel").objectReferenceValue).activeSelf, Is.False);
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
