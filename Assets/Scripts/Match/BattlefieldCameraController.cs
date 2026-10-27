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
    /// The opening framing sits <em>inside</em> the tabletop zoom range, a couple of notches short of
    /// <see cref="maxDistance"/>, so a player who scrolls out at the start of a match pulls back before
    /// anything cranes; only past the ceiling does the same input lift the camera overhead.
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

        /// <summary>
        /// Depth the resting camera aims at, in board-local Y — which the board's +90 degree X rotation maps
        /// onto world Z, so this is also the world Z of the point under the cross-hairs at rest.
        /// <para>
        /// Derived, not tuned, and deliberately <em>not</em> <see cref="BattlefieldPresenter.TableCenterY"/>.
        /// The hand fan is parented to the camera, so its top edge is a constant of the lens (fov and the
        /// fan's own geometry) and nothing the rig does to pitch or distance moves it: the board is the only
        /// thing that can get out of its way. Aiming at the table's own centre left the player's four Core
        /// Bench sockets 29.2 pixels behind the fan at 1280x720 — and a bench socket the resting fan covers
        /// is a socket the player cannot click, which retreat needs. Biasing the aim one board unit toward
        /// the player lifts the whole board sixty-odd pixels: the bench clears the fan by 24.7, no socket on
        /// the table reaches the banner or the key legend above it, and the tabletop still spans y 69..623 of
        /// 720, so the fan owns the bottom band and the status layer the top one.
        /// </para>
        /// <para>
        /// BattlefieldCameraControllerTests pins the seam as a contract; the derivation of the numbers is
        /// written up in docs/CARD-FRAME-SPEC.md alongside the co-planarity rule they share a lens with.
        /// </para>
        /// </summary>
        public const float RestFocusZ = -1f;

        [Tooltip("Board point the camera looks at. Doubles as the pan origin: the focus is clamped around " +
                 "wherever this sits, never around the world origin, so moving the board in the editor moves " +
                 "the panning limits with it. It is deliberately not the table's own centre - see RestFocusZ " +
                 "for why the resting shot aims a board unit this side of it.")]
        public Vector3 focus = new Vector3(0f, 0f, RestFocusZ);
        [Range(5f, 89f)] public float pitch = 52f;
        public float yaw = 0f;
        [Tooltip("Opening distance, derived from the tabletop footprint rather than copied from the ceiling. " +
                 "At the shipped 38 degree field of view this frames the 16.5-unit-wide table with about an " +
                 "eleventh of a table edge of air on each side at 16:9.")]
        public float distance = 15f;
        public float fieldOfView = 38f;

        [Header("Zoom (mouse wheel)")]
        public float minDistance = 7f;
        [Tooltip("Deepest zoom-out of the tilted tabletop view, and the point past which the same wheel input " +
                 "starts lifting the camera toward bird's-eye. Deliberately beyond the opening framing: when " +
                 "the opening sat exactly on the ceiling, one stray scroll at the start of a match began " +
                 "craning the camera off the board instead of pulling back. Two notches of headroom means a " +
                 "single accidental scroll now does nothing visible at all.")]
        public float maxDistance = 17f;
        [Tooltip("Distance change per scroll notch. Six or seven notches span the tabletop range; at the old 6 " +
                 "units one notch ate 37% of it, which left the view with effectively two zoom positions.")]
        public float zoomStep = 1.5f;

        [Header("Bird's-eye view")]
        [Tooltip("Distance the crane settles at when fully overhead. Sized by the fit contract pinned in " +
                 "BattlefieldCameraControllerTests: the whole 16.5 x 10.3 tabletop has to read as a plan view " +
                 "at the narrowest aspect the game ships (1.5:1) with no more than 15% of slack in the tight " +
                 "axis. That ceiling is what keeps the tabletop tier squeezed, because fitting the same table " +
                 "at a tilt costs almost the same distance - the two tiers are neighbours by geometry, not by " +
                 "accident, and buying zoom headroom by pushing this number out floats the board in void.")]
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
        [Tooltip("How far past the point where the tabletop exactly fills the frame the focus may still " +
                 "drift, in board units. Half the widest socket on the table, so a board edge never lands " +
                 "flush against the screen border.")]
        public float panSlackX = 0.55f;
        public float panSlackZ = 0.55f;
        [Tooltip("Aspect used to widen the vertical field of view into a horizontal one when no camera is " +
                 "attached, which is the case for a bare test rig. In play mode the real camera's aspect " +
                 "always wins.")]
        public float referenceAspect = 16f / 9f;
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
        /// The board point the pan clamp is built around: the authored <see cref="focus"/>, captured once.
        /// <para>
        /// Public and read-only because it is the one quantity the panning limits are defined against, and a
        /// test that cannot see it has to guess it — which it used to do by restating the table's centre and
        /// silently assuming the two are the same number. They are not, and RestFocusZ is why.
        /// </para>
        /// <para>
        /// Valid once the rig has initialised, which Awake and every public control both do.
        /// </para>
        /// </summary>
        public Vector3 BoardCenter => boardCenter;

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

            // The authored focus is the pan origin, captured once. Clamping around the world origin instead
            // would silently drift the limits the moment anybody nudged the board in the editor.
            boardCenter = new Vector3(focus.x, groundY, focus.z);
            targetFocus = focus;
            targetDistance = Mathf.Clamp(distance, minDistance, birdDistance);
            ClampFocus();
            initialized = true;
        }

        Vector3 boardCenter;

        /// <summary>
        /// Keeps the tabletop inside the frame.
        /// <para>
        /// The bound moves with the zoom, and that is the whole design. Framed wide enough that the table
        /// already fits, there is nothing off-screen to look at and the focus cannot leave its origin; zoom
        /// in, and the slack opens to exactly the amount of board that has been pushed outside the frame,
        /// plus <see cref="panSlackX"/>. A fixed rectangle can only offer one of those two behaviours and the
        /// one this replaces offered neither: at +/-8 units of x it let a 16.5-unit table slide clean out of
        /// view while still refusing to centre on the far corners when zoomed right in.
        /// </para>
        /// </summary>
        void ClampFocus()
        {
            float slackX = Mathf.Max(0f, BattlefieldPresenter.TableWidth * 0.5f - VisibleHalfWidth())
                + Mathf.Max(0f, panSlackX);
            float slackZ = Mathf.Max(0f, BattlefieldPresenter.TableDepth * 0.5f - VisibleHalfDepth())
                + Mathf.Max(0f, panSlackZ);

            targetFocus.x = Mathf.Clamp(targetFocus.x, boardCenter.x - slackX, boardCenter.x + slackX);
            targetFocus.z = Mathf.Clamp(targetFocus.z, boardCenter.z - slackZ, boardCenter.z + slackZ);
            targetFocus.y = groundY;
        }

        /// <summary>Half the frame's height at the focus row, in board units.</summary>
        float VisibleHalfHeight()
        {
            return targetDistance * Mathf.Tan(Mathf.Deg2Rad * fieldOfView * 0.5f);
        }

        /// <summary>Half the frame's width at the focus row, in board units.</summary>
        float VisibleHalfWidth()
        {
            float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : referenceAspect;
            return VisibleHalfHeight() * aspect;
        }

        /// <summary>
        /// Half the frame's extent along the board's depth axis, in board units. The frame is a rectangle in
        /// the camera's view, but on the table its depth axis is foreshortened by the pitch, so the ground it
        /// covers grows as the camera comes down toward the horizon. The divisor is floored well clear of zero
        /// because at a grazing pitch the true answer is unbounded, and an unbounded pan bound is the same bug
        /// as no bound at all.
        /// </summary>
        float VisibleHalfDepth()
        {
            return VisibleHalfHeight() / Mathf.Max(0.2f, Mathf.Sin(Mathf.Deg2Rad * CurrentPitch));
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
