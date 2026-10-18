using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Tabletop camera rig for the gameplay scene. It keeps the Underlords-style framing (a fixed pitch and
    /// yaw looking down at <see cref="focus"/>) while letting the player inspect the board:
    /// <list type="bullet">
    /// <item><description><b>Zoom</b> - mouse scroll wheel moves the camera toward or away from the focus.</description></item>
    /// <item><description><b>Bird's-eye</b> - scrolling out past the last tabletop zoom-out cranes the same
    /// camera up and over: the distance keeps growing while the pitch lifts to <see cref="birdPitch"/>, so
    /// the whole table reads as a plan view instead of being clipped by the zoom ceiling.</description></item>
    /// <item><description><b>Pan</b> - right or middle mouse drag slides the board; WASD / arrow keys nudge it.</description></item>
    /// </list>
    /// The opening framing <em>is</em> the tabletop ceiling (<c>BuildGameplayScene</c> seeds
    /// <see cref="maxDistance"/> from it), so a player who scrolls out at the start of a match goes overhead
    /// rather than watching nothing happen for a notch or two.
    /// This is a <b>view only</b> tool: it never touches match state. The left mouse button is left alone so
    /// card dragging in <c>HandController</c> keeps working.
    /// <para>
    /// Input is read in <see cref="LateUpdate"/> and funneled through the public <see cref="ZoomBy"/>,
    /// <see cref="PanBy"/>, <see cref="SetFocus"/> and <see cref="SnapToTarget"/> methods, so the framing
    /// math is unit-testable without simulating the keyboard or mouse.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class BattlefieldCameraController : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Board point the camera looks at.")]
        public Vector3 focus = new Vector3(0f, 0f, 0.2f);
        [Range(5f, 89f)] public float pitch = 52f;
        public float yaw = 0f;
        public float distance = 15f;
        public float fieldOfView = 38f;

        [Header("Zoom (mouse wheel)")]
        public float minDistance = 6f;
        [Tooltip("Deepest zoom-out of the tilted tabletop view. The scene ships its opening framing here, " +
                 "so scrolling out again does not pull back - it lifts into the bird's-eye view below.")]
        public float maxDistance = 16f;
        [Tooltip("Distance change per scroll notch.")]
        public float zoomStep = 6f;

        [Header("Bird's-eye view")]
        [Tooltip("Distance the crane settles at when fully overhead. Chosen to fit the tabletop rather than " +
                 "fill the screen: at the authored field of view the visible frame is about 12.4 x 18.6 board " +
                 "units at 1.5:1, against a 11.4 x 16.5 tabletop, so the table's depth is the tight axis and " +
                 "the board fills the frame with a small margin at any wider aspect.")]
        public float birdDistance = 18f;
        [Tooltip("Pitch the view settles at when fully overhead. 89 rather than 90 keeps the ground-plane " +
                 "raycast used by drag-panning non-degenerate.")]
        public float birdPitch = 89f;
        [Tooltip("How many scroll notches the lift from the tabletop ceiling to fully overhead takes. More " +
                 "than one, so the crane reads as one continuous move instead of a cut between two cameras.")]
        public float birdTravelNotches = 3f;

        [Tooltip("How quickly the camera eases toward its target. Higher is snappier.")]
        public float smooth = 12f;

        [Header("Pan")]
        [Tooltip("Right or middle mouse drag slides the board; WASD / arrows nudge it.")]
        public float panBoundsXMin = -8f;
        public float panBoundsXMax = 8f;
        public float panBoundsZMin = -5.5f;
        public float panBoundsZMax = 6f;
        public float keyboardPanSpeed = 10f;

        [Header("Ground")]
        [Tooltip("World Y of the board plane used to interpret drags.")]
        public float groundY = 0f;

        Camera cam;
        Vector3 targetFocus;
        float targetDistance;
        bool initialized;

        Vector3 dragFocusStart;
        Vector3 dragAnchorWorld;
        bool dragging;

        /// <summary>Eased board point the camera currently looks at.</summary>
        public Vector3 Focus => focus;

        /// <summary>Eased distance from the focus point to the camera.</summary>
        public float Distance => distance;

        /// <summary>Board point the camera is easing toward.</summary>
        public Vector3 TargetFocus => targetFocus;

        /// <summary>Distance the camera is easing toward.</summary>
        public float TargetDistance => targetDistance;

        /// <summary>
        /// How far the view has craned from the tilted tabletop framing (0) to fully overhead (1).
        /// Everything at or below <see cref="maxDistance"/> is pure tabletop zoom, so this stays at zero there.
        /// <para>
        /// Driven by the <em>eased</em> distance rather than the target, so the pitch lifts in lockstep with
        /// the camera instead of snapping to its final angle one frame early.
        /// </para>
        /// </summary>
        public float BirdBlend
        {
            get
            {
                float span = birdDistance - maxDistance;
                return span <= 0.01f ? 0f : Mathf.Clamp01((distance - maxDistance) / span);
            }
        }

        /// <summary>True once the view has passed the tabletop ceiling and is lifting overhead.</summary>
        public bool IsBirdView => BirdBlend > 1e-3f;

        /// <summary>
        /// The pitch the rig actually renders at: the authored tabletop <see cref="pitch"/> until the zoom
        /// runs past <see cref="maxDistance"/>, easing up to <see cref="birdPitch"/> after that. Smoothstepped
        /// so the camera leaves the tilt gently and settles level gently.
        /// </summary>
        public float CurrentPitch
        {
            get
            {
                float t = BirdBlend;
                return Mathf.Lerp(pitch, birdPitch, t * t * (3f - 2f * t));
            }
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = fieldOfView;
            }

            EnsureInit();
            focus = targetFocus;
            distance = targetDistance;
            PlaceCamera();
        }

        void LateUpdate()
        {
            EnsureInit();

            float dt = Time.unscaledDeltaTime;

            HandleZoom();
            HandleDragPan();
            HandleKeyboardPan(dt);

            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, smooth) * dt);
            focus = Vector3.Lerp(focus, targetFocus, k);
            distance = Mathf.Lerp(distance, targetDistance, k);
            PlaceCamera();
        }

        // ---- Testable view controls -------------------------------------------------

        /// <summary>Zoom by a signed amount in board units: positive moves the camera closer (zooms in).</summary>
        public void ZoomBy(float amount)
        {
            EnsureInit();

            // Past the tabletop ceiling, zooming out stops pulling back and starts lifting: the same wheel
            // input cranes the camera up toward the bird's-eye view. The two legs are clamped against each
            // other rather than allowed to overshoot, so the ceiling is reached exactly from below and left
            // exactly from above, and the transition is symmetric in both directions.
            if (amount < 0f && targetDistance >= maxDistance - 1e-4f)
            {
                targetDistance = Mathf.Min(targetDistance + BirdStep, birdDistance);
                return;
            }

            if (amount > 0f && targetDistance > maxDistance + 1e-4f)
            {
                targetDistance = Mathf.Max(targetDistance - BirdStep, maxDistance);
                return;
            }

            targetDistance = Mathf.Clamp(targetDistance - amount, minDistance, maxDistance);
        }

        /// <summary>Distance travelled per scroll notch while craning between the tabletop and overhead views.</summary>
        public float BirdStep =>
            (birdDistance - maxDistance) / Mathf.Max(0.01f, birdTravelNotches);

        /// <summary>Slide the focus across the board plane by the given world-space delta.</summary>
        public void PanBy(Vector3 boardDelta)
        {
            EnsureInit();
            targetFocus += new Vector3(boardDelta.x, 0f, boardDelta.z);
            ClampFocus();
        }

        /// <summary>Move the focus to a board point (Y is forced onto the board plane).</summary>
        public void SetFocus(Vector3 boardPoint)
        {
            EnsureInit();
            targetFocus = new Vector3(boardPoint.x, groundY, boardPoint.z);
            ClampFocus();
        }

        /// <summary>Apply the current target immediately and reposition the camera (no easing).</summary>
        public void SnapToTarget()
        {
            EnsureInit();
            focus = targetFocus;
            distance = targetDistance;
            PlaceCamera();
        }

        // ---- Input ------------------------------------------------------------------

        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 1e-4f)
            {
                ZoomBy(scroll * zoomStep);
            }
        }

        void HandleDragPan()
        {
            bool held = Input.GetMouseButton(1) || Input.GetMouseButton(2);

            if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && TryGroundPoint(out Vector3 downPoint))
            {
                dragging = true;
                dragAnchorWorld = downPoint;
                dragFocusStart = targetFocus;
            }

            if (dragging && held && TryGroundPoint(out Vector3 currentPoint))
            {
                // Keep the world point grabbed at drag start under the cursor, so the board tracks the mouse.
                Vector3 delta = dragAnchorWorld - currentPoint;
                SetFocus(dragFocusStart + new Vector3(delta.x, 0f, delta.z));
            }

            if (dragging && !held)
            {
                dragging = false;
            }
        }

        void HandleKeyboardPan(float dt)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            {
                return;
            }

            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 move = right * h + forward * v;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            // Scale with zoom so panning feels the same whether close in or zoomed out.
            float speed = keyboardPanSpeed * Mathf.Max(1f, distance / Mathf.Max(0.01f, birdDistance) * 2f);
            PanBy(move * speed * dt);
        }

        void EnsureInit()
        {
            if (initialized)
            {
                return;
            }

            targetFocus = focus;
            targetDistance = Mathf.Clamp(distance, minDistance, birdDistance);
            ClampFocus();
            initialized = true;
        }

        void ClampFocus()
        {
            targetFocus.x = Mathf.Clamp(targetFocus.x, panBoundsXMin, panBoundsXMax);
            targetFocus.z = Mathf.Clamp(targetFocus.z, panBoundsZMin, panBoundsZMax);
            targetFocus.y = groundY;
        }

        bool TryGroundPoint(out Vector3 point)
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
            }

            if (cam != null)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (plane.Raycast(ray, out float d))
                {
                    point = ray.GetPoint(d);
                    return true;
                }
            }

            point = default;
            return false;
        }

        void PlaceCamera()
        {
            Quaternion rot = Quaternion.Euler(CurrentPitch, yaw, 0f);
            Vector3 forward = rot * Vector3.forward;
            Vector3 f = focus;
            f.y = groundY;

            transform.rotation = rot;
            transform.position = f - forward * distance;
        }
    }
}
