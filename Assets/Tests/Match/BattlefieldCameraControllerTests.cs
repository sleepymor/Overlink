using NUnit.Framework;
using UnityEngine;
using Overlink.Match;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The gameplay camera rig funnels all input through public methods, so the zoom clamping, pan
    /// clamping and framing math can be verified without simulating the keyboard or mouse.
    /// </summary>
    public class BattlefieldCameraControllerTests
    {
        GameObject go;
        BattlefieldCameraController rig;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("CameraRigTest");
            rig = go.AddComponent<BattlefieldCameraController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ZoomBy_ClampsBetweenMinAndMaxDistance()
        {
            rig.SnapToTarget();
            Assert.AreEqual(15f, rig.Distance, 1e-4f, "Default distance should be 15.");

            rig.ZoomBy(1000f); // zoom in far past the minimum
            rig.SnapToTarget();
            Assert.AreEqual(rig.minDistance, rig.Distance, 1e-4f, "Zoom in should clamp to minDistance.");

            rig.ZoomBy(-1000f); // zoom out far past the maximum
            rig.SnapToTarget();
            Assert.AreEqual(rig.maxDistance, rig.Distance, 1e-4f, "Zoom out should clamp to maxDistance.");
        }

        [Test]
        public void PanBy_MovesFocusAndClampsToBounds()
        {
            rig.SetFocus(Vector3.zero);

            rig.PanBy(new Vector3(1f, 0f, 2f));
            rig.SnapToTarget();
            Assert.AreEqual(1f, rig.Focus.x, 1e-4f);
            Assert.AreEqual(2f, rig.Focus.z, 1e-4f);

            rig.PanBy(new Vector3(1f, 5f, 1f)); // Y is ignored; X/Z run past the bounds
            rig.PanBy(new Vector3(1000f, 0f, 1000f));
            rig.SnapToTarget();
            Assert.AreEqual(rig.panBoundsXMax, rig.Focus.x, 1e-4f, "Pan should clamp to the +X bound.");
            Assert.AreEqual(rig.panBoundsZMax, rig.Focus.z, 1e-4f, "Pan should clamp to the +Z bound.");

            rig.PanBy(new Vector3(-1000f, 0f, -1000f));
            rig.SnapToTarget();
            Assert.AreEqual(rig.panBoundsXMin, rig.Focus.x, 1e-4f, "Pan should clamp to the -X bound.");
            Assert.AreEqual(rig.panBoundsZMin, rig.Focus.z, 1e-4f, "Pan should clamp to the -Z bound.");
        }

        [Test]
        public void Focus_AlwaysRestsOnTheBoardPlane()
        {
            rig.PanBy(new Vector3(3f, 7f, 4f));
            rig.SnapToTarget();
            Assert.AreEqual(rig.groundY, rig.Focus.y, 1e-4f, "Focus should stay on the board plane.");
        }

        [Test]
        public void SnapToTarget_PlacesCameraAtPitchDistanceFraming()
        {
            rig.SetFocus(new Vector3(0f, 0f, 0.2f));
            rig.SnapToTarget();

            float rad = rig.pitch * Mathf.Deg2Rad;
            Vector3 expected = new Vector3(
                0f,
                rig.distance * Mathf.Sin(rad),
                0.2f - rig.distance * Mathf.Cos(rad));

            Assert.AreEqual(expected.x, go.transform.position.x, 1e-3f);
            Assert.AreEqual(expected.y, go.transform.position.y, 1e-3f);
            Assert.AreEqual(expected.z, go.transform.position.z, 1e-3f);
            Assert.AreEqual(rig.pitch, go.transform.rotation.eulerAngles.x, 1e-2f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, go.transform.rotation.eulerAngles.y), 1e-2f);
        }

        [Test]
        public void ZoomingIn_MovesCameraCloserToFocus()
        {
            rig.SnapToTarget();
            float before = Vector3.Distance(go.transform.position, rig.Focus);

            rig.ZoomBy(-4f); // ZoomBy positive = closer, so negative moves away first...
            rig.SnapToTarget();
            float away = Vector3.Distance(go.transform.position, rig.Focus);
            Assert.Greater(away, before, "Zooming out should increase the camera distance.");

            rig.ZoomBy(6f);
            rig.SnapToTarget();
            float back = Vector3.Distance(go.transform.position, rig.Focus);
            Assert.Less(back, away, "Zooming in should decrease the camera distance.");
        }

        // ---- Bird's-eye view -----------------------------------------------------------

        [Test]
        public void BirdView_AtTheTabletopCeilingIsStillTheTiltedView()
        {
            // The opening framing IS the deepest zoom-out, so nothing about the view may change until the
            // player actually scrolls past it: no pitch drift, no blend, no half-raised camera.
            rig.ZoomBy(-1000f);
            rig.SnapToTarget();

            Assert.AreEqual(rig.maxDistance, rig.Distance, 1e-4f, "Zoom out should stop at the ceiling.");
            Assert.IsFalse(rig.IsBirdView, "The ceiling itself is still the tabletop view.");
            Assert.AreEqual(0f, rig.BirdBlend, 1e-4f);
            Assert.AreEqual(rig.pitch, rig.CurrentPitch, 1e-4f, "The authored pitch must survive intact.");
        }

        [Test]
        public void BirdView_ScrollingOutPastTheCeilingCranesToOverhead()
        {
            rig.ZoomBy(-1000f);
            rig.SnapToTarget();

            int notches = 0;
            while (rig.TargetDistance < rig.birdDistance - 1e-3f && notches < 40)
            {
                rig.ZoomBy(-rig.zoomStep);
                notches++;
            }

            rig.SnapToTarget();
            Assert.AreEqual(Mathf.CeilToInt(rig.birdTravelNotches), notches,
                "The lift should take exactly the authored number of scroll notches.");
            Assert.AreEqual(rig.birdDistance, rig.Distance, 1e-3f, "The crane should settle at birdDistance.");
            Assert.AreEqual(1f, rig.BirdBlend, 1e-3f);
            Assert.AreEqual(rig.birdPitch, rig.CurrentPitch, 1e-2f,
                "Fully craned, the pitch should read as looking straight down.");
        }

        [Test]
        public void BirdView_CraningIsMonotonicAndNeverOvershoots()
        {
            rig.ZoomBy(-1000f);
            rig.SnapToTarget();

            float previousPitch = rig.CurrentPitch;
            for (int i = 0; i < 12; i++)
            {
                rig.ZoomBy(-rig.zoomStep);
                rig.SnapToTarget();

                Assert.GreaterOrEqual(rig.Distance, rig.maxDistance, "The crane never dips back below the ceiling.");
                Assert.LessOrEqual(rig.Distance, rig.birdDistance, "The crane never flies past overhead.");
                Assert.GreaterOrEqual(rig.CurrentPitch, previousPitch,
                    "The camera must only ever lift as the player scrolls out.");
                previousPitch = rig.CurrentPitch;
            }
        }

        [Test]
        public void BirdView_OverheadCameraSitsAboveTheFocusNotBehindIt()
        {
            // The point of the second tier is to see the whole table from above, so the camera has to end up
            // nearly on the vertical through the focus rather than on the tilted arc.
            rig.SetFocus(new Vector3(0f, 0f, 0.35f));
            rig.ZoomBy(-1000f);
            for (int i = 0; i < 12; i++)
            {
                rig.ZoomBy(-rig.zoomStep);
            }
            rig.SnapToTarget();

            Vector3 above = go.transform.position - rig.Focus;
            Assert.Greater(Vector3.Dot(above.normalized, Vector3.up), 0.99f,
                "Overhead, the camera should sit almost directly above the point it looks at.");
            Assert.AreEqual(rig.distance, above.magnitude, 1e-2f, "The distance is the camera-to-focus span.");
        }

        [Test]
        public void BirdView_OverheadFrameFitsTheTabletopSnugly()
        {
            // Design lock: birdDistance exists to show the whole tabletop at once and not much else. The frame
            // has to be larger than the board in both axes at the narrowest aspect the game is played at
            // (1.5:1, i.e. 3:2), and the board's own dimensions are read from the presenter rather than copied
            // here, so retuning the table retunes the overhead tier's contract with it.
            const float NarrowestAspect = 1.5f;
            float TableWidth = BattlefieldPresenter.TableWidth;
            float TableDepth = BattlefieldPresenter.TableDepth;

            float visibleHeight = 2f * rig.birdDistance * Mathf.Tan(rig.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * NarrowestAspect;

            Assert.Greater(visibleHeight, TableDepth,
                "Overhead, the table must fit top to bottom.");
            Assert.Greater(visibleWidth, TableWidth,
                "Overhead, the table must fit side to side even at a narrow aspect.");

            // ...and the tight axis must not be wasteful. A plan view that floats the board in a field of void
            // reads as zoomed *out*, which is the opposite of what lifting the camera is supposed to buy.
            float slack = Mathf.Min(visibleHeight / TableDepth - 1f, visibleWidth / TableWidth - 1f);
            Assert.LessOrEqual(slack, 0.15f,
                "The overhead tier should fit the table, not hover above it.");
        }

        [Test]
        public void BirdView_ScrollingInDescendsBackToTheCeilingThenResumesTabletopZoom()
        {
            // Symmetry matters: a player who went overhead to see the board must be able to get back to the
            // exact opening framing by scrolling the same way in, without skipping past it.
            rig.ZoomBy(-1000f);
            for (int i = 0; i < 12; i++)
            {
                rig.ZoomBy(-rig.zoomStep);
            }
            rig.SnapToTarget();
            Assert.IsTrue(rig.IsBirdView, "Precondition: the view should be overhead.");

            for (int i = 0; i < Mathf.CeilToInt(rig.birdTravelNotches); i++)
            {
                rig.ZoomBy(rig.zoomStep);
            }
            rig.SnapToTarget();

            Assert.AreEqual(rig.maxDistance, rig.Distance, 1e-3f,
                "Zooming in from overhead should land on the ceiling, not fly past it.");
            Assert.AreEqual(0f, rig.BirdBlend, 1e-3f);
            Assert.AreEqual(rig.pitch, rig.CurrentPitch, 1e-3f, "The tilted framing must come back intact.");

            rig.ZoomBy(rig.zoomStep);
            rig.SnapToTarget();
            Assert.Less(rig.Distance, rig.maxDistance,
                "One more notch in should resume ordinary tabletop zooming.");
        }

        [Test]
        public void BirdView_PanBoundsAreUnchangedByTheCrane()
        {
            // Panning overhead is how you inspect a far corner of the board, so the crane must not quietly
            // tighten or loosen the limits that apply in the tilted view.
            rig.SetFocus(new Vector3(0f, 0f, 0.35f));
            for (int i = 0; i < 12; i++)
            {
                rig.ZoomBy(-rig.zoomStep);
            }
            rig.SnapToTarget();
            Assert.IsTrue(rig.IsBirdView, "Precondition: the view should be overhead.");

            rig.PanBy(new Vector3(1000f, 0f, 1000f));
            rig.SnapToTarget();
            Assert.AreEqual(rig.panBoundsXMax, rig.Focus.x, 1e-4f);
            Assert.AreEqual(rig.panBoundsZMax, rig.Focus.z, 1e-4f);
        }
    }
}
