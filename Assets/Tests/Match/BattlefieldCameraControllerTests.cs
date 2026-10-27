using NUnit.Framework;
using UnityEngine;
using Overlink.Cards;
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
        public void PanBy_MovesFocusWithinTheFrame()
        {
            // From the pan origin, not from the tabletop's own centre. The two are a board unit apart by
            // design (RestFocusZ), and a nudge of this size only stays inside the clamp when it starts from
            // the point the clamp is actually built around - which is why the rig exposes BoardCenter rather
            // than leaving a test to guess it.
            rig.SnapToTarget();
            rig.SetFocus(rig.BoardCenter);
            rig.SnapToTarget();

            rig.PanBy(new Vector3(0.2f, 0f, 0.3f));
            rig.SnapToTarget();
            Assert.AreEqual(0.2f, rig.Focus.x, 1e-4f);
            Assert.AreEqual(rig.BoardCenter.z + 0.3f, rig.Focus.z, 1e-4f);
        }

        [Test]
        public void PanBound_CollapsesWhenTheTableAlreadyFitsTheFrame()
        {
            // The invariant the old fixed rectangle got exactly backwards. At the opening framing the whole
            // tabletop is inside the frame, so there is nothing off-screen to look at and the focus has no
            // business drifting eight units — which is what panBoundsXMax used to allow on a 16.5-unit table,
            // sliding half the board out of view while pretending to constrain it. What is left is panSlack: a
            // nudge so an edge never lands flush on the screen border.
            rig.SnapToTarget();
            rig.SetFocus(rig.BoardCenter);
            rig.SnapToTarget();

            Assert.Greater(HalfWidthAt(rig), BattlefieldPresenter.TableWidth * 0.5f,
                "Precondition: at the opening distance the table must fit across the frame, or this test is " +
                "measuring the wrong tier.");

            rig.PanBy(new Vector3(1000f, 0f, 1000f));
            rig.SnapToTarget();
            Assert.AreEqual(rig.panSlackX, rig.Focus.x, 1e-4f, "+X should collapse to the slack.");
            Assert.AreEqual(rig.BoardCenter.z + rig.panSlackZ, rig.Focus.z, 1e-4f,
                "+Z should collapse to the slack.");

            rig.PanBy(new Vector3(-2000f, 0f, -2000f));
            rig.SnapToTarget();
            Assert.AreEqual(-rig.panSlackX, rig.Focus.x, 1e-4f, "-X should collapse to the slack.");
            Assert.AreEqual(rig.BoardCenter.z - rig.panSlackZ, rig.Focus.z, 1e-4f,
                "-Z should collapse to the slack.");
        }

        [Test]
        public void PanBound_OpensUpAsTheCameraZoomsIn()
        {
            // The other half of the same rule. Zoomed in, part of the table is outside the frame and the focus
            // has to be able to reach it — that reach is the entire purpose of panning, and a bound that does
            // not move with the zoom can only serve one framing. The old one served neither: it let the board
            // leave the frame when zoomed out and still could not centre the near corners when zoomed in.
            // The walk is on the X axis, so it starts from wherever the rig rests.
            rig.SnapToTarget();

            float previousSlack = -1f;
            for (int notch = 0; notch <= 12; notch++)
            {
                rig.PanBy(new Vector3(1000f, 0f, 0f));
                rig.SnapToTarget();

                float slack = rig.Focus.x;
                Assert.GreaterOrEqual(slack + HalfWidthAt(rig), BattlefieldPresenter.TableWidth * 0.5f - 1e-3f,
                    $"At distance {rig.Distance} the panned edge must reach the table's own edge.");
                Assert.GreaterOrEqual(slack, previousSlack,
                    "The bound must never tighten as the camera comes in.");
                previousSlack = slack;

                rig.PanBy(new Vector3(-1000f, 0f, 0f));
                rig.ZoomBy(rig.zoomStep);
                rig.SnapToTarget();
                if (rig.Distance <= rig.minDistance + 1e-3f)
                {
                    break;
                }
            }

            Assert.LessOrEqual(rig.Distance, rig.minDistance + 1e-3f,
                "The walk should have reached the closest zoom, or it proved nothing.");
            Assert.Greater(previousSlack, 1f,
                "At closest zoom the bound must have opened well past the opening framing's nudge, or the " +
                "side ranks are unreachable.");
        }

        /// <summary>Half the frame's width at the focus row, mirroring the rig's own maths.</summary>
        static float HalfWidthAt(BattlefieldCameraController rig)
        {
            return rig.Distance * Mathf.Tan(rig.fieldOfView * 0.5f * Mathf.Deg2Rad) * rig.referenceAspect;
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
            // A point inside the pan clamp about the authored origin, so what this test measures is the pose
            // formula and not the clamp quietly moving the target underneath it.
            const float AimZ = -0.8f;

            rig.SnapToTarget();
            rig.SetFocus(new Vector3(0f, 0f, AimZ));
            rig.SnapToTarget();
            Assert.AreEqual(AimZ, rig.Focus.z, 1e-4f, "The aim point must survive the clamp intact.");

            float rad = rig.pitch * Mathf.Deg2Rad;
            Vector3 expected = new Vector3(
                0f,
                rig.distance * Mathf.Sin(rad),
                AimZ - rig.distance * Mathf.Cos(rad));

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
            // The ceiling is the last zoom-out before the crane, so nothing about the view may change until the
            // player actually scrolls past it: no pitch drift, no blend, no half-raised camera. Reaching it from
            // the opening now takes two notches, which is the headroom that stops one accidental scroll from
            // lifting the camera off the board at the start of a match.
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
            // nearly on the vertical through the focus rather than on the tilted arc. Aimed off-centre first,
            // from inside the pan clamp so it is the rig being measured and not the clamp moving underneath.
            rig.SnapToTarget();
            rig.SetFocus(new Vector3(0f, 0f, rig.BoardCenter.z + 0.35f));
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
        public void BirdView_CannotPanBecauseTheWholeTableIsAlreadyInView()
        {
            // The overhead tier is sized to fit the entire tabletop, so there is nothing off-screen to pan to
            // and the bound collapses to the slack. The fixed rectangle this replaces let the board drift
            // clean out of the plan view, which is the one thing a plan view cannot afford.
            rig.SnapToTarget();
            rig.SetFocus(rig.BoardCenter);
            for (int i = 0; i < 20; i++)
            {
                rig.ZoomBy(-rig.zoomStep);
            }
            rig.SnapToTarget();
            Assert.IsTrue(rig.IsBirdView, "Precondition: the view should be overhead.");

            rig.PanBy(new Vector3(1000f, 0f, 1000f));
            rig.SnapToTarget();
            Assert.AreEqual(rig.panSlackX, rig.Focus.x, 1e-4f,
                "Overhead the frame covers the table's width, so the lateral bound should be collapsed.");
            Assert.AreEqual(rig.BoardCenter.z + rig.panSlackZ, rig.Focus.z, 1e-4f,
                "Overhead the frame covers the table's depth too, so the depth bound should collapse as well.");
        }

        // ---- The resting shot ---------------------------------------------------------
        // The fan is parented to the camera, so its screen footprint is a constant of the lens: no amount of
        // pitching, zooming or panning moves it off the sockets it lands on. The board is the only thing that
        // can get out of its way, and RestFocusZ is the one dial that does it - which makes the seam between
        // the two a contract worth pinning, for the same reason the overhead fit is.
        //
        // The fan's dials and its placement inside the camera are mirrored from Gameplay.unity, and from
        // CardHandTests, which is where the row's own congruence is pinned. Restated rather than imported
        // because what this needs is the row and the lens together: the fan's top edge in screen terms is a
        // property of both, and neither file owns it alone.
        const float ShippedFanSpacing = 0.74f;
        const float ShippedArcHeight = 0.18f;
        const float ShippedMaxYaw = 14f;
        const float ShippedHandDepth = 6.7f;
        const float ShippedHandY = -1.65f;

        [Test]
        public void RestFraming_LeavesThePlayersOwnBenchClearOfTheRestingFan()
        {
            // Heights are compared as fractions of the frame, so the contract reads the same at 720p and at
            // 4K: 0 is the bottom edge, 1 the top.
            float halfFovTan = Mathf.Tan(rig.fieldOfView * 0.5f * Mathf.Deg2Rad);

            // The face's own half-extents, from the canonical size rather than a copy of it: the lateral one
            // matters as much as the vertical, because a rolled card tops out on a CORNER. Getting these two
            // mixed up is exactly how this test first shipped, and it did not fail quietly - it failed the
            // seam assertion, which is what a contract is for.
            Vector2 halfFace = new Vector2(CardFaceView.CanonicalFaceSize.x, CardFaceView.CanonicalFaceSize.y) * 0.5f;

            // Highest point any card in the resting five reaches, in the camera's own plane. The fan rolls
            // about the view axis, so the top of the row is a corner on the wing cards and an edge midpoint
            // on the centre one, and only comparing all five settles which.
            float fanTopCamY = float.NegativeInfinity;
            for (int i = 0; i < 5; i++)
            {
                HandController.ComputeSlot(i, 5, ShippedFanSpacing, ShippedArcHeight, ShippedMaxYaw,
                    0f, true, out Vector3 slot, out Quaternion roll);
                foreach (Vector2 top in new[] {
                    new Vector2(0f, halfFace.y), halfFace, new Vector2(-halfFace.x, halfFace.y) })
                {
                    float y = ShippedHandY + slot.y + (roll * new Vector3(top.x, top.y, 0f)).y;
                    fanTopCamY = Mathf.Max(fanTopCamY, y);
                }
            }
            float fanTop = 0.5f + fanTopCamY / ShippedHandDepth / (2f * halfFovTan);

            // The nearest edge of the player's own Bench rank, on the ground, projected the same way. The
            // board root is rotated +90 degrees about X, so board-local Y is world Z.
            float benchWorldZ = BattlefieldPresenter.CorePlayerBenchY - BattlefieldPresenter.CoreBenchSize.y * 0.5f;
            float dz = benchWorldZ - BattlefieldCameraController.RestFocusZ;
            float pitchRad = rig.pitch * Mathf.Deg2Rad;
            float depth = rig.distance + dz * Mathf.Cos(pitchRad);
            float height = dz * Mathf.Sin(pitchRad);
            float benchNear = 0.5f + height / depth / (2f * halfFovTan);

            Assert.GreaterOrEqual(benchNear - fanTop, 0.02f,
                $"The resting fan covers the player's own Bench sockets (seam {benchNear - fanTop:0.###} of " +
                "the frame height). Retreat makes that rank a destination the player has to be able to click, " +
                "and the fan is camera-parented - only RestFocusZ can open this seam.");

            // And the other direction: the bias may not shove the far edge of the tabletop out of the shot,
            // which is what a player who never scrolls would see as a board with no top to it. This is the
            // FRAME bound. The status layer's top band is the tighter one in practice - at the shipped HUD
            // layout the key legend's glyph floor sits at 0.874 of the frame, which is what rules out a
            // deeper bias than the authored one - but that number belongs to BattlefieldHud, and Stage 3 is
            // reflowing it for the button rail. Pinning it here would mean editing it twice.
            float farWorldZ = BattlefieldPresenter.TableCenterY + BattlefieldPresenter.TableDepth * 0.5f;
            float fdz = farWorldZ - BattlefieldCameraController.RestFocusZ;
            float farTop = 0.5f + fdz * Mathf.Sin(pitchRad)
                / (rig.distance + fdz * Mathf.Cos(pitchRad)) / (2f * halfFovTan);

            Assert.LessOrEqual(farTop, 0.95f,
                $"The tabletop's far edge sits at {farTop:0.###} of the frame height, so the board is being " +
                "pushed out of the resting shot by the same bias that clears the fan.");
        }

        [Test]
        public void RestFraming_PanOriginIsTheAuthoredRestBias()
        {
            // The clamp is built around the point the rig actually rests on, so the shot a player opens in and
            // the limits they can pan within cannot drift apart. This is the property the pan-bound tests used
            // to get by assuming the origin was the table's own centre, which it no longer is.
            rig.SnapToTarget();
            Assert.AreEqual(BattlefieldCameraController.RestFocusZ, rig.BoardCenter.z, 1e-4f,
                "The pan origin should be the authored rest bias.");
            Assert.AreNotEqual(BattlefieldPresenter.TableCenterY, BattlefieldCameraController.RestFocusZ,
                "And it is deliberately not the tabletop's centre - RestFocusZ's doc says why.");
        }
    }
}
