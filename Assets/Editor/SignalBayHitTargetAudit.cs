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
