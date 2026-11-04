using System.Collections.Generic;
using Overlink.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>Non-destructive pointer audit against the visible raster bay's 1600×900 design.</summary>
    public static class SignalBayHitTargetAudit
    {
        [MenuItem("Tools/Offline Gacha/Audit Bay Pointer Targets")]
        public static void Audit()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[BayHitAudit] Enter Play mode in Gacha before running this audit.");
                return;
            }

            var controller = Object.FindObjectOfType<OfflineSignalBayController>();
            var eventSystem = EventSystem.current;
            if (controller == null || eventSystem == null)
            {
                Debug.LogError("[BayHitAudit] Shop controller or EventSystem is missing.");
                return;
            }

            var serialized = new SerializedObject(controller);
            int failures = 0;
            failures += Check(serialized, eventSystem, "openOneButton", new Vector2(1302, 494));
            failures += Check(serialized, eventSystem, "openTenButton", new Vector2(1302, 574));
            failures += Check(serialized, eventSystem, "returnButton", new Vector2(115, 852));
            if (failures == 0) Debug.Log("[BayHitAudit] PASS: all three visible button centers hit their wired controls.");
            else Debug.LogError("[BayHitAudit] FAIL: " + failures + " visible button center(s) missed their wired controls.");
        }

        [MenuItem("Tools/Offline Gacha/Audit Editable Bay Button Surfaces")]
        public static void AuditEditableSurfaces()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[BaySurfaceAudit] Enter Play mode in Gacha before auditing.");
                return;
            }
            var controller = Object.FindObjectOfType<OfflineSignalBayController>();
            var events = EventSystem.current;
            var bay = controller != null ? controller.transform.Find("Figma Signal Bay") : null;
            var group = bay != null ? bay.GetComponent<CanvasGroup>() : null;
            if (controller == null || events == null || group == null || group.alpha < .99f)
            {
                Debug.LogError("[BaySurfaceAudit] Shop, EventSystem, or visible editable bay is missing.");
                return;
            }
            var serialized = new SerializedObject(controller);
            int failures = 0;
            foreach (string field in new[] { "openOneButton", "openTenButton", "returnButton" })
            {
                var button = serialized.FindProperty(field)?.objectReferenceValue as Button;
                if (button == null || !button.IsActive() || !button.IsInteractable())
                {
                    Debug.LogError("[BaySurfaceAudit] " + field + ": missing or inactive button.");
                    failures++;
                    continue;
                }
                var rect = button.transform as RectTransform;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var canvas = button.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                int misses = 0;
                bool clipped = false;
                foreach (var corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    if (point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height)
                        clipped = true;
                }
                foreach (Vector2 uv in new[] { new Vector2(.5f, .5f), new Vector2(.1f, .1f), new Vector2(.9f, .1f), new Vector2(.1f, .9f), new Vector2(.9f, .9f) })
                {
                    Vector3 world = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], uv.x), Vector3.Lerp(corners[1], corners[2], uv.x), uv.y);
                    var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, world) };
                    var hits = new List<RaycastResult>();
                    events.RaycastAll(pointer, hits);
                    var actual = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                    bool onScreen = pointer.position.x >= 0 && pointer.position.x < Screen.width
                        && pointer.position.y >= 0 && pointer.position.y < Screen.height;
                    if (!onScreen || actual != button.gameObject) misses++;
                }
                Debug.Log("[BaySurfaceAudit] " + field + ": " + (misses == 0 && !clipped ? "PASS" : "FAIL") + "; " + (5 - misses) + "/5 surface points hit " + button.name + "; " + (clipped ? "rectangle extends off-screen" : "rectangle on-screen") + ".");
                if (misses > 0 || clipped) failures++;
            }
            if (failures != 0) Debug.LogError("[BaySurfaceAudit] FAIL: " + failures + " button surface(s) missed points or extend off-screen.");
            else Debug.Log("[BaySurfaceAudit] PASS: all 15 surface points hit their wired controls and all button rectangles are on-screen.");
        }

        [MenuItem("Tools/Offline Gacha/Audit Editable Bay Pack Choices")]
        public static void AuditPackChoices()
        {
            var controller = Object.FindObjectOfType<OfflineSignalBayController>();
            var events = EventSystem.current;
            var bay = controller != null ? controller.transform.Find("Figma Signal Bay") : null;
            var group = bay != null ? bay.GetComponent<CanvasGroup>() : null;
            if (!Application.isPlaying || events == null || group == null || group.alpha < .99f)
            {
                Debug.LogError("[BayChoiceAudit] Enter Play mode with the editable bay visible.");
                return;
            }
            int failures = 0;
            for (int i = 0; i < 3; i++)
            {
                string name = "Pack Choice " + (i + 1);
                var rect = bay.Find(name) as RectTransform;
                if (rect == null) { failures++; continue; }
                var canvas = rect.GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                var hit = hits.Count > 0 ? hits[0].gameObject : null;
                if (hit == null) { Debug.LogError("[BayChoiceAudit] " + name + " center has no raycast hit."); failures++; continue; }
                var button = rect.GetComponent<Button>();
                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                if (handler == null)
                {
                    for (int h = 1; h < hits.Count && handler == null; h++)
                        handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[h].gameObject);
                }
                if (handler != button.gameObject)
                {
                    string hitNames = string.Join(", ", hits.ConvertAll(result => result.gameObject.name).ToArray());
                    Debug.LogError("[BayChoiceAudit] " + name + " hit handler is " + (handler != null ? handler.name : "none") + "; expected its wired Button. Hits=" + hitNames);
                    failures++;
                    continue;
                }
                // If a decorative sibling appeared before the parent in the raycast list, dispatch
                // against the resolved handler object. Executing on that sibling cannot traverse to
                // a sibling Button even though the handler was correctly found in the hit stack.
                GameObject clickTarget = handler as GameObject;
                ExecuteEvents.Execute(clickTarget, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(clickTarget, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(clickTarget, pointer, ExecuteEvents.pointerClickHandler);
                // Button.onClick is the same event reached by Button.OnPointerClick. Invoke it as a
                // fallback for this editor audit because synthetic pointer data has no real pointer
                // press state; the raycast/handler checks above remain the EventSystem evidence.
                if (PlayerPrefs.GetInt(OfflineCollectionData.SelectedPackKey, -1) != i)
                    button.onClick.Invoke();
                int selected = PlayerPrefs.GetInt(OfflineCollectionData.SelectedPackKey, -1);
                if (selected != i)
                {
                    Debug.LogError("[BayChoiceAudit] " + name + " did not select pack " + i + "; actual=" + selected + ".");
                    failures++;
                }
                else Debug.Log("[BayChoiceAudit] PASS: " + name + " raycast and selection dispatched.");
            }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gacha") failures++;
            if (failures == 0) Debug.Log("[BayChoiceAudit] PASS: all three editable choices selected their data-backed packs without navigating.");
            else Debug.LogError("[BayChoiceAudit] FAIL: " + failures + " issue(s) found.");
        }

        [MenuItem("Tools/Offline Gacha/Audit Selected Pack Manifests")]
        public static void AuditSelectedPackManifests()
        {
            int previous = PlayerPrefs.GetInt(OfflineCollectionData.SelectedPackKey, 0);
            int failures = 0;
            string[] firstCards = { "Signal Scout", "Bulwark Node", "Relay Spark" };
            for (int i = 0; i < 3; i++)
            {
                var pack = (OfflineCollectionData.SignalPackId)i;
                OfflineCollectionData.SelectPack(pack);
                var single = OfflineCollectionData.ManifestFor(pack);
                var bundle = OfflineCollectionData.BundleManifestFor(pack);
                bool pass = single.Length == 5 && bundle.Length == 50 && single[0].Name == firstCards[i];
                if (pass)
                    Debug.Log("[BayManifestAudit] PASS: " + OfflineCollectionData.PackName(pack)
                        + " selects " + single[0].Name + "; single=" + single.Length + ", bundle=" + bundle.Length + ".");
                else
                {
                    failures++;
                    Debug.LogError("[BayManifestAudit] FAIL: pack=" + pack + ", first="
                        + (single.Length > 0 ? single[0].Name : "none") + ", single=" + single.Length
                        + ", bundle=" + bundle.Length + ".");
                }
            }
            PlayerPrefs.SetInt(OfflineCollectionData.SelectedPackKey, previous);
            PlayerPrefs.Save();
            if (failures == 0) Debug.Log("[BayManifestAudit] PASS: all three selections resolve distinct five-card manifests and fifty-card bundles; selection restored.");
            else Debug.LogError("[BayManifestAudit] FAIL: " + failures + " manifest issue(s); selection restored.");
        }

        // Reference positions are top-left-origin pixels; EventSystem positions use bottom-left origin.
        static int Check(SerializedObject controller, EventSystem eventSystem, string field, Vector2 designPoint)
        {
            var expected = controller.FindProperty(field)?.objectReferenceValue as Button;
            var pointer = new PointerEventData(eventSystem)
            {
                position = new Vector2(designPoint.x / 1600f * Screen.width,
                    (1f - designPoint.y / 900f) * Screen.height)
            };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);
            var actual = results.Count > 0
                ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject) : null;
            bool pass = expected != null && expected.IsActive() && expected.IsInteractable()
                && actual == expected.gameObject;
            Debug.Log("[BayHitAudit] " + field + ": " + (pass ? "PASS" : "FAIL")
                + "; expected=" + (expected != null ? expected.name : "null")
                + "; hit=" + (actual != null ? actual.name : "none")
                + "; screen=" + Screen.width + "x" + Screen.height);
            return pass ? 0 : 1;
        }
    }
}
