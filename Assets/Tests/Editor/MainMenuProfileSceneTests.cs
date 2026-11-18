using NUnit.Framework;
using Overlink.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.Tests.Editor
{
    public class MainMenuProfileSceneTests
    {
        [Test]
        public void ProfilePanel_HasAllBindableControlsInAuthoredHierarchy()
        {
            const string path = "Assets/Scenes/MainMenu.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                MainMenuController controller = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (controller == null) controller = root.GetComponentInChildren<MainMenuController>(true);
                Assert.That(controller, Is.Not.Null);
                var panel = controller.transform.Find("Canvas/Profile Panel");
                Assert.That(panel, Is.Not.Null);
                foreach (string row in new[] { "Username Row", "Tag Row", "Full ID Row" })
                {
                    var value = panel.Find("Card/Content/" + row + "/Value");
                    var copy = panel.Find("Card/Content/" + row + "/Copy Button");
                    Assert.That(value != null ? value.GetComponent<TextMeshProUGUI>() : null, Is.Not.Null, row);
                    Assert.That(copy != null ? copy.GetComponent<Button>() : null, Is.Not.Null, row);
                }
                var close = panel.Find("Card/Content/Close Button");
                var scrim = panel.Find("Scrim");
                Assert.That(close != null ? close.GetComponent<Button>() : null, Is.Not.Null);
                Assert.That(scrim != null ? scrim.GetComponent<Button>() : null, Is.Not.Null);
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
