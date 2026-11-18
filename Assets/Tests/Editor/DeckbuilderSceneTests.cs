using NUnit.Framework;
using Overlink.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.Tests.Editor
{
    public class DeckbuilderSceneTests
    {
        [Test]
        public void Scene_HasUsableCanvasAndInputSystem()
        {
            const string path = "Assets/Scenes/Deckbuilder.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Canvas canvas = null;
                EventSystem events = null;
                DeckbuilderController controller = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
                    if (events == null) events = root.GetComponentInChildren<EventSystem>(true);
                    if (controller == null) controller = root.GetComponentInChildren<DeckbuilderController>(true);
                }

                Assert.That(controller, Is.Not.Null, "The runtime deckbuilder must be attached to the scene.");
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.transform.lossyScale.x, Is.Not.EqualTo(0f));
                Assert.That(canvas.transform.lossyScale.y, Is.Not.EqualTo(0f));
                Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
                Assert.That(events, Is.Not.Null);
                Assert.That(events.GetComponent<BaseInputModule>(), Is.Not.Null);
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
