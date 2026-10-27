using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Overlink.Cards;
using Overlink.Core;

public class CardHandTests
{
    // The shipped HandRoot placement. The hand hangs in the camera's own plane, low and close enough that a
    // row of cards reads as a hand held up to the lens rather than a wall filling it. Mirrored from
    // Gameplay.unity (and BuildGameplayScene's constants), because a rig built at some convenient depth
    // proves nothing about the framing the player actually sees. The depth is the closest one that keeps the
    // row's loss at the bottom edge under a quarter of a card: the outer card's lower corner lands at
    // camera-space y = -2.662 whatever the depth, the frame reaches only to -tan(fov/2) * z, so the clipped
    // amount is 2.662 - 0.3443 * z and asking that to stay under 0.25 * 1.4615 puts z at 6.67 or more.
    // Shipped at 6.7, which hangs 24.3 percent of the outer cards and 5 percent of the centre one.
    const float ShippedHandY = -1.65f;
    const float ShippedHandDepth = 6.7f;
    static Vector3 ShippedHandPlacement => new Vector3(0f, ShippedHandY, ShippedHandDepth);

    // The shipped fan's own three dials. They are not independent: a folding fan is cards radiating from
    // one rivet, so the roll at the outer card and the spacing between neighbours fix how far the centre
    // card has to rise. Tuning one without the other is what made the row drift out of alignment, so
    // FanSlots_NeighbourPairsAreCongruent pins the agreement rather than the numbers.
    const float ShippedFanSpacing = 0.74f;
    const float ShippedArcHeight = 0.18f;
    const float ShippedMaxYaw = 14f;

    [Test]
    public void FanSlots_AreSymmetric()
    {
        HandController.ComputeSlot(0, 5, 0.78f, 0.35f, 16f, -2.6f,
            out Vector3 leftPos, out Quaternion leftRot);
        HandController.ComputeSlot(4, 5, 0.78f, 0.35f, 16f, -2.6f,
            out Vector3 rightPos, out Quaternion rightRot);

        Assert.AreEqual(leftPos.x, -rightPos.x, 1e-5f, "Outer slots should mirror on X.");
        Assert.AreEqual(leftPos.y, rightPos.y, 1e-5f, "Outer slots should share arc height.");
        float yawMirrorError = Mathf.DeltaAngle(leftRot.eulerAngles.z, -rightRot.eulerAngles.z);
        Assert.AreEqual(0f, yawMirrorError, 0.01f, "Outer slots should mirror fan yaw.");
    }

    [Test]
    public void FanSlots_CenterCardSitsHighest()
    {
        HandController.ComputeSlot(0, 5, 0.78f, 0.35f, 16f, -2.6f,
            out Vector3 edgePos, out _);
        HandController.ComputeSlot(2, 5, 0.78f, 0.35f, 16f, -2.6f,
            out Vector3 centerPos, out _);

        Assert.Greater(centerPos.y, edgePos.y, "Center card should ride the top of the arc.");
        Assert.AreEqual(0f, centerPos.x, 1e-5f, "Center card should sit at x=0.");
    }

    [Test]
    public void FanSlots_NeighbourPairsAreCongruent()
    {
        // A folding fan is one object, and it only reads as one if every card tucks behind its neighbour by
        // the same amount - the whole length of the row and at every height. That is congruence of the
        // neighbouring PAIRS, not uniformity of the gaps between centres, and the two come apart as soon as
        // the cards are rolled: a card rotated about its own middle shares an edge with its neighbour that
        // opens at one corner and pinches shut at the other, so the slice across a name banner differs from
        // pair to pair and grows toward the ends of the row, where the roll is largest.
        //
        // Folding the row onto one rivet makes every pair an exact rotation of the next, which is why the
        // fan pivots. This pins the property, and pins that the unpivoted row does not have it.
        AssertCongruent(ShippedFanSpacing, ShippedMaxYaw, true, 1e-4f, "the pivoted fan");
        AssertCongruent(ShippedFanSpacing, ShippedMaxYaw, false, -0.02f, "the unpivoted row");

        // And the pivot must not quietly resize the row: the outer cards still land on the half-row width
        // the spacing implies, so turning the pivot on changes how the cards overlap and nothing else.
        const float halfRow = ShippedFanSpacing * 2f;
        HandController.ComputeSlot(0, 5, ShippedFanSpacing, ShippedArcHeight, ShippedMaxYaw, 0f, true,
            out Vector3 left, out _);
        HandController.ComputeSlot(4, 5, ShippedFanSpacing, ShippedArcHeight, ShippedMaxYaw, 0f, true,
            out Vector3 right, out _);
        Assert.AreEqual(-halfRow, left.x, 1e-3f, "the left end of a pivoted row keeps its width");
        Assert.AreEqual(halfRow, right.x, 1e-3f, "and so does the right end");

        // The rise is no longer a free dial: a rivet at r = halfRow / sin(yaw) leaves the outer cards
        // halfRow * tan(yaw / 2) below the centre one. That is what the hand's width and roll work out to,
        // and it is the number arcHeight used to carry by hand - the reason an independently tuned arc, at
        // 0.35 beside a 16 degree roll, described a fan that could not exist.
        HandController.ComputeSlot(2, 5, ShippedFanSpacing, 0f, ShippedMaxYaw, 0f, true,
            out Vector3 centre, out _);
        float rise = centre.y - left.y;
        float implied = halfRow * Mathf.Tan(0.5f * ShippedMaxYaw * Mathf.Deg2Rad);
        Assert.AreEqual(implied, rise, 1e-3f,
            $"a {ShippedMaxYaw} degree roll of a {halfRow * 2f:0.##}-wide row can only rise by " +
            $"{implied:0.###}, so arcHeight has to agree with it or the fan is not a fan");
        Assert.AreEqual(ShippedArcHeight, rise, 0.01f,
            "and the scene's arcHeight is kept in step with that, so it still documents the real shape " +
            "even though the pivot ignores it");
    }

    [Test]
    public void NamePlates_AreCongruentToo_WhateverTheNameLengthsAre()
    {
        // The rivet makes the FRAMES congruent, and that was only half of what the eye was reporting. The
        // name plate is the one element on a card that straddles the overlap with its neighbour, and
        // shrink-to-fit sized each name against its own text: measured on the shipped pool at the shipped
        // lens, one hand set its five plates at 71.0, 76.7, 85.5, 96.0 and 96.0 point, with the glyph
        // block tops at canvas y 490, 493, 495, 496 and 490. So the type changed size AND moved along the
        // row at the same time, growing toward the ends - which is exactly "the layout shifting", and it
        // survived the pivot because it is content, not geometry.
        //
        // Single-line zones therefore keep one em and squeeze sideways instead (CondenseToFitWidth). The
        // cap height and the baseline stop being variables, and a long name was already a visibly wider
        // name, so spending width costs nothing the eye is tracking.
        string[] names = { "Volley", "Interdiction Net" };   // shortest vs longest in the shipped pool
        var holders = new GameObject[2];
        var label = new TextMeshPro[2];
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                holders[i] = new GameObject("NamePlateFace_" + i);
                CardFaceView face = holders[i].AddComponent<CardFaceView>();
                face.Bind(new CardFaceData("card_test_" + i, names[i], CardType.Unit, Faction.Fsti,
                    Rarity.Common, 3, null, 2, 3, 0, "Test."), null);
                label[i] = face.NameValue;
                Assert.IsNotNull(label[i], $"{names[i]}: the face must expose a name plate");
            }

            Assert.AreEqual(label[0].fontSize, label[1].fontSize, 1e-4f,
                "every name plate prints at one point size, so no card in a hand is set smaller than " +
                "the card it tucks behind");
            Assert.AreEqual(label[0].transform.localScale.y, label[1].transform.localScale.y, 1e-5f,
                "and the vertical scale is that one em, so the cap height and the baseline match across " +
                "the whole row");

            float plate = label[1].rectTransform.rect.width;
            float widest = WidestLine(label[1]);
            Assert.Greater(widest, plate,
                "the long name really does overflow the plate at one em, or this test is vacuous");

            // The squeeze is a horizontal scale, so the rendered width in em units has to come back to
            // the plate width exactly - it fits, and it fits without the em having moved.
            float squeezed = widest * label[1].transform.localScale.x / label[1].transform.localScale.y;
            Assert.AreEqual(plate, squeezed, 1e-3f,
                "the long name fits its plate exactly, having been squeezed rather than shrunk");

            Assert.AreEqual(label[0].transform.localScale.x, label[0].transform.localScale.y, 1e-5f,
                "a name that already fits is left unsqueezed, so the common case stays honest type");
        }
        finally
        {
            for (int i = 0; i < holders.Length; i++)
            {
                if (holders[i] != null) Object.Destroy(holders[i]);
            }
        }
    }

    /// <summary>
    /// The widest laid-out line of a label, in the same rect units StyleLabel authors the plate in, so
    /// the two are comparable without a scale conversion: the plate is written as
    /// pointSize x (plate px / em px) and the mesh comes back in pointSize units too.
    /// </summary>
    static float WidestLine(TextMeshPro text)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        Assert.IsNotNull(info, $"{text.name}: TMP must have laid the text out");
        float widest = 0f;
        for (int i = 0; i < info.lineCount; i++)
        {
            Extents extents = info.lineInfo[i].lineExtents;
            float width = extents.max.x - extents.min.x;
            if (width > widest) widest = width;
        }
        return widest;
    }

    /// <summary>
    /// Asserts that every neighbouring pair of a five card fan is an exact rotation of every other one.
    /// A positive <paramref name="tolerance"/> demands that congruence; a negative one demands the
    /// opposite, so the check cannot pass by being vacuous on the row it is meant to condemn.
    /// </summary>
    static void AssertCongruent(float spacing, float yaw, bool pivoted, float tolerance, string label)
    {
        const int count = 5;
        var poses = new Vector3[count];
        var rots = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            HandController.ComputeSlot(i, count, spacing, 0.35f, yaw, 0f, pivoted, out poses[i], out rots[i]);
        }

        // The first pair is the reference; congruence means the rest match it in the cards' own frames,
        // which is the only frame in which "the same overlap" means anything.
        Vector3 reference = RelativePose(poses[0], rots[0], poses[1], rots[1]);
        float referenceRoll = RelativeRoll(rots[0], rots[1]);
        float worst = 0f;
        for (int i = 1; i < count - 1; i++)
        {
            Vector3 rel = RelativePose(poses[i], rots[i], poses[i + 1], rots[i + 1]);
            float spread = Mathf.Max(Mathf.Abs(rel.x - reference.x), Mathf.Abs(rel.y - reference.y));
            worst = Mathf.Max(spread, Mathf.Abs(RelativeRoll(rots[i], rots[i + 1]) - referenceRoll));
        }

        if (tolerance >= 0f)
        {
            Assert.LessOrEqual(worst, tolerance,
                $"{label}: neighbouring pairs disagree by {worst:0.####} of a card, so the overlap is not " +
                "the same along the row and the cards read as drifting out of alignment.");
        }
        else
        {
            Assert.Greater(worst, -tolerance,
                $"{label}: expected the pairs to DISAGREE (that is the fault the rivet fixes), so the " +
                "congruence assertion above would prove nothing if this row were also congruent.");
        }
    }

    static Vector3 RelativePose(Vector3 aPos, Quaternion aRot, Vector3 bPos, Quaternion bRot)
    {
        return Quaternion.Inverse(aRot) * (bPos - aPos);
    }

    static float RelativeRoll(Quaternion aRot, Quaternion bRot)
    {
        return Mathf.DeltaAngle(0f, (Quaternion.Inverse(aRot) * bRot).eulerAngles.z);
    }

    [UnityTest]
    public IEnumerator DrawAndDiscard_UpdatesHand()
    {
        var go = new GameObject("TestTable");
        // These rigs live in whatever scene the runner happened to enter Play mode from. When that
        // is MainMenu, AuthFlowController loads a scene a second into play, and a Single-mode load
        // destroys every dynamically created object in the project. Pinning the rig to
        // DontDestroyOnLoad keeps the test from being nuked by ambient game startup.
        Object.DontDestroyOnLoad(go);
        go.AddComponent<DeckController>();
        var hand = go.AddComponent<HandController>();
        hand.handSizeStart = 0;
        yield return null;

        hand.DrawOne();
        yield return new WaitForSeconds(0.7f);
        Assert.AreEqual(1, hand.CardCount, "DrawOne should add exactly one card.");
        Assert.IsNotNull(go.GetComponentInChildren<CardView>(),
            "The drawn card should exist as a descendant of the hand.");

        hand.DiscardLast();
        yield return new WaitForSeconds(0.7f);
        Assert.AreEqual(0, hand.CardCount, "DiscardLast should empty the hand.");

        Object.Destroy(go);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CustomSize_PropagatesToBodyAndCollider()
    {
        var parent = new GameObject("SizeParent");
        Object.DontDestroyOnLoad(parent);
        var data = new CardData("test", "Test", 1, "Test card.", Color.red);
        var size = new Vector3(1.2f, 1.8f, 0.1f);
        var card = CardView.Create(data, parent.transform, size);
        yield return null;

        Assert.AreEqual(Vector3.one, card.BaseScale, "Root animation scale should rest at one.");
        var body = card.transform.Find(CardView.LegacyBodyName);
        Assert.IsNotNull(body, "A non-presentation card should have a legacy Body cube child.");
        Assert.AreEqual(size, body.localScale, "Legacy body should carry the requested size.");
        var col = card.GetComponent<BoxCollider>();
        Assert.AreEqual(size, col.size, "Collider should match the requested size.");

        var newSize = new Vector3(0.7f, 1.0f, 0.05f);
        card.SetSize(newSize);
        Assert.AreEqual(Vector3.one, card.BaseScale, "SetSize should not disturb the root animation scale.");
        Assert.AreEqual(newSize, body.localScale, "SetSize should resize the body.");
        Assert.AreEqual(newSize, col.size, "SetSize should resize the collider.");

        Object.Destroy(parent);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DrawBeyondDeck_ReshufflesDiscard()
    {
        var go = new GameObject("TestTable2");
        Object.DontDestroyOnLoad(go);
        var deck = go.AddComponent<DeckController>();
        var hand = go.AddComponent<HandController>();
        hand.handSizeStart = 0;
        yield return null;

        int drawn = 0;
        for (int i = 0; i < 12; i++)
        {
            int before = deck.DrawCount;
            hand.DrawOne();
            if (hand.CardCount > drawn) drawn = hand.CardCount;
            hand.DiscardLast();
            yield return new WaitForSeconds(0.7f);
            if (before == 0 && deck.DrawCount == 0 && deck.DiscardCount == 0) break;
        }

        Assert.Greater(drawn, 0, "Should have drawn at least one card.");
        Object.Destroy(go);
        yield return null;
    }

    // ---------------------------------------------------------------------------------------------
    // Played-card presentation.
    //
    // The gameplay board is pitched 52 degrees, so a card left lying in playSlot is read edge-on for
    // the whole resolve. HandController can instead fly it to a transform - the camera rig - and hold
    // it square to the lens. These tests pin that contract, and pin the tabletop path the preview
    // scenes still depend on, because the same method decides both.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The gameplay arrangement in miniature: a hand laid flat on a table and a camera pitched over
    /// it, using the same numbers <c>BuildGameplayScene</c> authors (90 degree root, 52 degree pitch,
    /// 38 degree field of view). The caller owns <paramref name="table"/> and the camera for teardown.
    /// </summary>
    static HandController CreatePitchedRig(out GameObject table, out Camera cam,
        bool cameraFirst = false)
    {
        // The hand captures its camera in Awake (cam = Camera.main), and the pointer path - hover,
        // drag, drop - dereferences it from then on. So a test that drives BeginDrag has to put the
        // tagged camera in place BEFORE the hand wakes. Every other rig test wants the opposite:
        // with cam null, Update bails at its first line and an ambient cursor sitting over the game
        // view cannot lift a card out from under a position assertion. Hence the flag, not a reorder.
        cam = cameraFirst ? CreateRigCamera() : null;

        table = new GameObject("PitchedTable");
        // Pinned for the same reason the rigs above are: an ambient scene load would otherwise
        // destroy the hand mid-coroutine.
        Object.DontDestroyOnLoad(table);
        table.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        table.AddComponent<DeckController>();
        var hand = table.AddComponent<HandController>();
        hand.handSizeStart = 0;
        hand.autoDraw = false;

        if (!cameraFirst) cam = CreateRigCamera();

        hand.playCamera = cam.transform;
        hand.playCardOffset = new Vector3(0f, 0.22f, 4.2f);
        hand.playCardScale = 1.15f;
        // Long enough that the presentation is settled while the assertions run; the discard test
        // shortens it so it can watch the hand-off.
        hand.playCardHoldSeconds = 5f;
        return hand;
    }

    /// <summary>
    /// The pitched observation rig of <c>CreatePitchedRig</c>, split out so a test can choose whether
    /// it exists before the hand's Awake runs. Tagged MainCamera because that is what Camera.main
    /// matches on - an untagged camera would leave the hand blind and every pointer assertion would
    /// pass for the wrong reason.
    /// </summary>
    static Camera CreateRigCamera()
    {
        var rig = new GameObject("PitchedRig");
        Object.DontDestroyOnLoad(rig);
        rig.tag = "MainCamera";
        Camera cam = rig.AddComponent<Camera>();
        cam.fieldOfView = 38f;
        Quaternion rot = Quaternion.Euler(52f, 0f, 0f);
        rig.transform.rotation = rot;
        rig.transform.position = new Vector3(0f, 0f, 0.35f) - rot * Vector3.forward * 16f;
        return cam;
    }

    static void DestroyRig(GameObject table, Camera cam)
    {
        Object.Destroy(table);
        if (cam != null) Object.Destroy(cam.gameObject);
    }

    /// <summary>
    /// Parents a holder under <paramref name="cam"/> so a hand can be laid out in the camera's own plane
    /// instead of on the table - the arrangement that turns the fan back into a folding fan, because the
    /// fan's arc and yaw were written for a hand seen head-on.
    /// </summary>
    static Transform CreateHandHolder(Camera cam, Vector3 localPos)
    {
        var holder = new GameObject("HandHolder");
        Object.DontDestroyOnLoad(holder);
        holder.transform.SetParent(cam.transform, false);
        holder.transform.localPosition = localPos;
        holder.transform.localRotation = Quaternion.identity;
        return holder.transform;
    }

    /// <summary>
    /// Drives the hand's private play path the way releasing a drag above the play line does. There is
    /// no public "play this card" seam - the drag is the only caller - so a test that wants the real
    /// resolve animation has to reach for it. If this ever fails to resolve, the hand's play path was
    /// renamed and the tests below are no longer proving anything about the animation.
    /// </summary>
    static void PlayCardNow(HandController hand, CardView card)
    {
        MethodInfo play = typeof(HandController).GetMethod("PlayCard",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(play,
            "HandController.PlayCard is the path a released drag takes; without it these tests " +
            "would not be exercising the played-card animation at all.");
        play.Invoke(hand, new object[] { card });
    }

    [UnityTest]
    public IEnumerator PlayedCard_HoldsInFrontOfTheCamera_AndSquaresToTheLens()
    {
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        yield return null;

        hand.DrawOne();
        yield return new WaitForSeconds(0.6f);
        CardView card = table.GetComponentInChildren<CardView>();
        Assert.IsNotNull(card, "DrawOne should have produced a hand card.");

        PlayCardNow(hand, card);
        yield return new WaitForSeconds(0.45f);

        Assert.AreEqual(cam.transform, card.transform.parent,
            "With playCamera assigned, a played card belongs to the camera - not the tabletop.");
        Vector3 wanted = cam.transform.TransformPoint(hand.playCardOffset);
        Assert.AreEqual(wanted.x, card.transform.position.x, 0.01f, "Presented card X in camera space.");
        Assert.AreEqual(wanted.y, card.transform.position.y, 0.01f, "Presented card Y in camera space.");
        Assert.AreEqual(wanted.z, card.transform.position.z, 0.01f, "Presented card Z in camera space.");

        // A card face is built on its local -Z side, so that axis has to line up with the view axis.
        // Measuring it against the direction to the camera's *position* would be the wrong test:
        // playCardOffset lifts the card 0.22 off the axis, which tilts that vector by 3 degrees while
        // leaving the face perfectly square to the screen - and square to the screen is what reads.
        Vector3 face = -card.transform.forward;
        Assert.Greater(Vector3.Dot(face, -cam.transform.forward), 0.9999f,
            "The card face must line up with the view axis; edge-on or backwards is unreadable.");
        Assert.Less(Quaternion.Angle(Quaternion.identity, card.transform.localRotation), 0.01f,
            "The presented pose is identity in camera space, so nothing else can rotate it off the lens.");

        // The sign of playCardOffset is worth its own check. A card behind the lens still has a face
        // pointing at the camera, so only the frustum coordinates distinguish the two.
        Vector3 vp = cam.WorldToViewportPoint(card.transform.position);
        Assert.Greater(vp.z, cam.nearClipPlane,
            "The presented card has to sit in front of the near clip plane, not behind the camera.");
        Assert.IsTrue(vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f,
            "The presented card has to be inside the viewport, got " + vp + ".");

        float expected = card.BaseScale.x * hand.playCardScale;
        Assert.AreEqual(expected, card.transform.localScale.x, 1e-4f, "Presented scale from playCardScale.");

        DestroyRig(table, cam);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayedCard_KeepsItsPlaceOnScreen_WhenTheCameraPansAndZooms()
    {
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        yield return null;

        hand.DrawOne();
        yield return new WaitForSeconds(0.6f);
        CardView card = table.GetComponentInChildren<CardView>();

        PlayCardNow(hand, card);
        yield return new WaitForSeconds(0.45f);
        Vector3 before = cam.WorldToViewportPoint(card.transform.position);

        // What BattlefieldCameraController does on a pan and on the wheel: move and swing the rig.
        cam.transform.position += new Vector3(1.4f, 0.6f, -2f);
        cam.transform.Rotate(6f, 9f, 0f);
        yield return null;
        Vector3 after = cam.WorldToViewportPoint(card.transform.position);

        // The whole point of parenting it: the card is pinned to the view, so it never slides across
        // the board underneath while the player is reading it.
        Assert.Less(Vector3.Distance(before, after), 0.001f,
            "A camera-attached card must hold its screen position through a pan and a zoom.");

        DestroyRig(table, cam);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayedCard_SurrendersTheCamera_ForTheDiscardFlight()
    {
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        // Both tweens are stretched so the three stages - presented, handed back, destroyed - can be
        // sampled with real margins instead of landing on a frame boundary and flaking.
        hand.resolveDuration = 0.8f;
        hand.playCardHoldSeconds = 0.4f;
        yield return null;

        hand.DrawOne();
        yield return new WaitForSeconds(0.6f);
        CardView card = table.GetComponentInChildren<CardView>();
        Collider collider = card.GetComponent<Collider>();
        Assert.IsNotNull(collider, "A hand card needs its pickup collider for the drag raycast.");

        PlayCardNow(hand, card);

        // Centred in front of the lens, the card is out of the hand already, so its collider would
        // only swallow the raycast that picks up the cards behind it. This is true from the frame the
        // attach happens on, well before the tween settles.
        Assert.AreEqual(cam.transform, card.transform.parent, "Attached for the presentation.");
        Assert.IsFalse(collider.enabled,
            "A presented card must not shadow the hand's pickup raycast.");

        // Tween in ends at 0.8, the hold ends at 1.2, the discard flight ends at 2.0.
        yield return new WaitForSeconds(0.9f);
        Assert.IsFalse(collider.enabled, "Still presented, still no collider.");

        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(table.transform, card.transform.parent,
            "discardPile is a point in the hand's own plane, so the card has to stop tracking the " +
            "camera before the discard flight can be expressed in it.");
        Assert.IsTrue(collider.enabled,
            "The collider comes back with the card, so anything handed back to the hand is pickable.");

        yield return new WaitForSeconds(1.6f);
        Assert.IsTrue(card == null, "The discard flight ends by destroying the view.");

        DestroyRig(table, cam);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TabletopPlay_LandsOnPlaySlot_WhenNoCameraIsAssigned()
    {
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        // The preview scenes leave playCamera empty: there is no board to read past, so the card has
        // to stay in the fan's own plane. This is the regression guard for that path.
        hand.playCamera = null;
        yield return null;

        hand.DrawOne();
        yield return new WaitForSeconds(0.6f);
        CardView card = table.GetComponentInChildren<CardView>();
        Vector3 slotWorld = hand.playSlot.position;

        PlayCardNow(hand, card);
        yield return new WaitForSeconds(0.45f);

        Assert.AreEqual(table.transform, card.transform.parent,
            "With no playCamera the card never leaves the hand's plane - the tabletop path is intact.");
        Assert.AreEqual(slotWorld.x, card.transform.position.x, 0.01f, "Played card lands on playSlot.");
        Assert.AreEqual(slotWorld.y, card.transform.position.y, 0.01f, "Played card lands on playSlot.");
        Assert.AreEqual(slotWorld.z, card.transform.position.z, 0.01f, "Played card lands on playSlot.");
        Assert.AreEqual(1.35f, card.transform.localScale.x, 1e-4f,
            "The tabletop resolve keeps its historic 1.35 enlargement.");
        // Lying flat on a root pitched 90 degrees: the face still points up out of the table.
        Assert.Greater(Vector3.Dot(-card.transform.forward, Vector3.up), 0.999f,
            "A tabletop card faces up out of the board, exactly as it always did.");

        DestroyRig(table, cam);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayPresentation_DetachIsGatedOnTheCardActuallyBeingAttached()
    {
        // EndPlayPresentation must be a no-op for a card that never went to the camera: DiscardLast
        // sends ordinary hand cards straight to the pile. Asserted on the guard's own terms, so a
        // future refactor that reparents unconditionally shows up as a failure rather than as a card
        // silently adopted by the camera and left floating there.
        var rig = new GameObject("GuardOnlyTable");
        rig.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var hand = rig.AddComponent<HandController>();
        var camGo = new GameObject("GuardOnlyRig");
        var cam = camGo.AddComponent<Camera>();
        hand.playCamera = cam.transform;

        var card = CardView.Create(new CardData("g", "Guard", 1, "Guard card.", Color.white),
            rig.transform, hand.cardSize);

        MethodInfo end = typeof(HandController).GetMethod("EndPlayPresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(end, "EndPlayPresentation is the counterpart of the play attach.");

        end.Invoke(hand, new object[] { card });
        Assert.AreEqual(rig.transform, card.transform.parent,
            "A card that never reached the camera must not be reparented by the detach path.");

        Object.Destroy(rig);
        Object.Destroy(camGo);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HandSpan_ClosesTheRowUpPastTheCorridor_InsteadOfLettingItGrowForever()
    {
        // Nothing in Core caps the hand size, so the view is the only thing that can keep a seven-card
        // hand inside the space the table leaves it. Asserted through SetHand rather than through the
        // private spacing helper, because the corridor is only real if the laid-out cards obey it.
        var table = new GameObject("SpanTable");
        Object.DontDestroyOnLoad(table);
        table.AddComponent<DeckController>();
        var hand = table.AddComponent<HandController>();
        hand.handSizeStart = 0;
        hand.autoDraw = false;
        hand.fanSpacing = 1.08f;
        hand.maxHandSpan = 4.16f;
        yield return null;

        var many = new System.Collections.Generic.List<CardData>();
        for (int i = 0; i < 7; i++)
        {
            many.Add(new CardData("s" + i, "Span " + i, 1, "Squeeze probe.", Color.white));
        }
        hand.SetHand(many);
        yield return new WaitForSeconds(0.6f);

        float half = hand.maxHandSpan * 0.5f;
        float outermost = 0f;
        // Measured off the hand's own card list rather than this transform's children: with a handRoot
        // the cards are not children of the component at all, and a loop over the wrong parent would
        // silently find nothing and pass.
        foreach (CardView card in hand.Cards)
        {
            outermost = Mathf.Max(outermost, Mathf.Abs(card.transform.localPosition.x));
        }

        Assert.AreEqual(7, hand.CardCount, "SetHand should have laid out all seven cards.");
        Assert.Greater(outermost, 0f, "The hand must actually contain laid-out cards to measure.");
        Assert.LessOrEqual(outermost, half + 0.001f,
            "Seven cards at the preferred spacing would reach x=" + (3 * hand.fanSpacing) +
            "; the corridor has to close the row up instead of letting it overrun the table.");
        // Filling the corridor exactly is what distinguishes a squeeze from a clamp: the row is as wide
        // as the table allows and no wider, so the cards stay as readable as the space permits.
        Assert.AreEqual(half, outermost, 0.001f,
            "A seven-card hand should fill the corridor rather than sit inside it unused.");

        Object.Destroy(table);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HandRoot_LaysTheFanInCameraSpace_StillSquaredToTheLens()
    {
        // The whole point of a handRoot: the cards leave the pitched table and lie in the screen plane,
        // where the fan's arc and yaw behave as drawn. Asserted on the holder's own local poses, because
        // those are what the fan math writes, and on the face axis, because a fan that squared itself to
        // the board instead of the lens would look identical in a still shot and read at an angle.
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        Transform holder = CreateHandHolder(cam, ShippedHandPlacement);
        hand.handRoot = holder;
        hand.fanSpacing = 0.68f;
        hand.arcHeight = 0.35f;
        hand.maxYaw = 16f;
        hand.handY = 0f;
        yield return null;

        var three = new System.Collections.Generic.List<CardData>();
        for (int i = 0; i < 3; i++)
        {
            three.Add(new CardData("h" + i, "Hand " + i, 1, "Fan probe.", Color.white));
        }
        hand.SetHand(three);
        yield return new WaitForSeconds(0.7f);

        for (int i = 0; i < 3; i++)
        {
            CardView card = hand.Cards[i];
            Assert.AreEqual(holder, card.transform.parent,
                "Card " + i + " belongs to the hand's card space, not the pitched table it was authored on.");
            // The face is built on the card's local -Z; -cam.forward points back out of the lens.
            Assert.Greater(Vector3.Dot(-card.transform.forward, -cam.transform.forward), 0.999f,
                "Roll about Z is all the fan's yaw is allowed to be, so the face stays square to the " +
                "lens at every slot. Card " + i + " is off by " +
                Vector3.Angle(-card.transform.forward, -cam.transform.forward) + " degrees.");
        }

        // The arc and the mirror: middle card on top, outer cards level and rolled the other way.
        Assert.AreEqual(0.35f, hand.Cards[1].transform.localPosition.y, 0.01f,
            "The middle card rides the top of the arc, in the screen plane this time rather than up-board.");
        Assert.AreEqual(0f, hand.Cards[0].transform.localPosition.y, 0.01f,
            "The outer cards sit on the hand's baseline.");
        Assert.AreEqual(-0.68f, hand.Cards[0].transform.localPosition.x, 0.01f, "Fan spacing.");
        Assert.AreEqual(0.68f, hand.Cards[2].transform.localPosition.x, 0.01f, "Fan spacing mirrors.");
        // Folded into the signed range first: localEulerAngles reports a -16 degree roll as 344.
        Assert.AreEqual(16f, Mathf.DeltaAngle(0f, hand.Cards[0].transform.localEulerAngles.z), 0.5f,
            "Outer card rolls one way.");
        Assert.AreEqual(-16f, Mathf.DeltaAngle(0f, hand.Cards[2].transform.localEulerAngles.z), 0.5f,
            "and the other the other.");

        DestroyRig(table, cam);
        Object.Destroy(holder.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HandRoot_HoverMovesTheCardTowardTheViewer_NotThroughTheLens()
    {
        // Hover and drag both push the card to a fixed negative local Z, a convention inherited from a
        // hand lying on a table rotated 90 degrees, where -Z is up out of the board. In a camera plane
        // -Z is back toward the lens, so the sign still means "toward the player" - but only because the
        // hand hangs well in front of the near plane. Pinned here, because a holder placed at z < 0.4
        // would send a hovered card clean through the back of the camera and it would simply vanish.
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        Transform holder = CreateHandHolder(cam, ShippedHandPlacement);
        hand.handRoot = holder;
        hand.handY = 0f;
        yield return null;

        hand.AddCard(new CardData("hv", "Hover Probe", 1, "Hover probe.", Color.white));
        yield return new WaitForSeconds(0.6f);
        CardView card = hand.Cards[0];

        float restZ = cam.transform.InverseTransformPoint(card.transform.position).z;

        MethodInfo hover = typeof(HandController).GetMethod("Hover",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(hover, "Hover is what the pointer raise calls; the lift is not reachable otherwise.");
        hover.Invoke(hand, new object[] { card });
        yield return new WaitForSeconds(0.35f);

        float hoverZ = cam.transform.InverseTransformPoint(card.transform.position).z;
        Assert.Less(hoverZ, restZ,
            "A hovered card has to come toward the player, not sink away from them.");
        Assert.Greater(hoverZ, cam.nearClipPlane,
            "and it has to stay in front of the near plane, or the hover makes the card disappear.");

        DestroyRig(table, cam);
        Object.Destroy(holder.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HandRoot_PickupTravelsOutToTheDragPlane_OneFrameAtATime()
    {
        // Lifting a card a whole unit is imperceptible when it lies on a table sixteen units away, and it
        // is the exact same number that makes a card lunge at the lens when the hand hangs five from it -
        // depth is only a fraction of the card's own size there. The pickup used to write the drag depth
        // straight into the transform, so grabbing a card jumped it forward before the pointer had moved.
        // It travels instead, and this pins the first frame of that travel plus the legacy instant leg the
        // tabletop previews still depend on.
        // cameraFirst: the pickup dereferences the hand's camera, so unlike every other rig test
        // this one needs the camera to exist before the hand's Awake captured Camera.main.
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam, cameraFirst: true);
        Transform holder = CreateHandHolder(cam, ShippedHandPlacement);
        hand.handRoot = holder;
        hand.handY = 0f;
        hand.hoverDepth = -0.16f;
        hand.dragDepth = -0.3f;
        hand.dragPickupSeconds = 1f;
        yield return null;

        hand.AddCard(new CardData("pk", "Pickup Probe", 1, "Pickup probe.", Color.white));
        yield return new WaitForSeconds(0.6f);
        CardView card = hand.Cards[0];

        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo beginDrag = typeof(HandController).GetMethod("BeginDrag", flags);
        MethodInfo updateDrag = typeof(HandController).GetMethod("UpdateDrag", flags);
        Assert.IsNotNull(beginDrag, "The pickup is the pointer path under test; nothing else reaches it.");
        Assert.IsNotNull(updateDrag, "and the drag is what advances it, frame by frame.");
        Assert.IsNotNull(typeof(HandController).GetField("cam", flags)?.GetValue(hand),
            "The pickup reads the pointer through the hand's camera, so a rig that builds the camera "
            + "after the hand leaves cam null and BeginDrag fails with a bare NullReferenceException "
            + "that reads like a product fault. Build it cameraFirst.");

        // Whatever plane the pointer found the card on - the resting one, or the shallower lift a hover
        // left it at - is where the leg has to start, so it is read rather than assumed.
        float fromZ = card.transform.localPosition.z;
        beginDrag.Invoke(hand, new object[] { card });
        updateDrag.Invoke(hand, null);
        float firstZ = card.transform.localPosition.z;

        Assert.LessOrEqual(firstZ, fromZ,
            "The pickup moves toward the drag plane, never away from it.");
        Assert.Greater(Mathf.Abs(firstZ - hand.dragDepth), Mathf.Abs(fromZ - hand.dragDepth) * 0.5f,
            "and one frame of it is a step, not the whole leg - arriving at once is the lunge this pins.");

        // The eased leg is bounded by Clamp01, so it can only ever settle ON the drag plane. What matters
        // to the previews is that switching the easing off restores the old single-frame arrival exactly.
        hand.dragPickupSeconds = 0f;
        updateDrag.Invoke(hand, null);
        Assert.AreEqual(hand.dragDepth, card.transform.localPosition.z, 1e-4f,
            "With no easing the drag holds the card on the drag plane immediately, as it always did.");

        DestroyRig(table, cam);
        Object.Destroy(holder.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HandRoot_DiscardFlightLeavesCameraSpaceForTheBoardPlane()
    {
        // The discard pile names a place on the board, and its localPosition is only meaningful in the
        // component's own plane. A card discarded straight out of a screen-fixed hand - no play, no
        // presentation - therefore has to be brought back to that plane first, or the flight tweens to a
        // board coordinate read as a camera coordinate and the card shrinks at some arbitrary point in
        // front of the lens.
        HandController hand = CreatePitchedRig(out GameObject table, out Camera cam);
        Transform holder = CreateHandHolder(cam, ShippedHandPlacement);
        hand.handRoot = holder;
        hand.handY = 0f;
        yield return null;

        hand.AddCard(new CardData("df", "Discard Probe", 1, "Discard probe.", Color.white));
        yield return new WaitForSeconds(0.6f);
        CardView card = hand.Cards[0];
        Assert.AreEqual(holder, card.transform.parent, "precondition: the card is in camera space.");

        hand.DiscardLast();
        yield return new WaitForSeconds(0.08f);

        Assert.AreEqual(table.transform, card.transform.parent,
            "The discard flight is expressed in the board plane, so the card must be back in it.");
        Collider col = card.GetComponent<Collider>();
        Assert.IsNotNull(col);
        Assert.IsTrue(col.enabled,
            "A card handed back to the board is pickable again, exactly as one that never left.");
        Assert.AreEqual(0, hand.CardCount, "and it is no longer part of the hand.");

        yield return new WaitForSeconds(0.6f);
        // `card == null` rather than Assert.IsNull: boxing to object resolves the comparison against
        // System.Object, not Unity's overloaded operator, so a destroyed view reads as alive to NUnit and
        // the assertion would pass for the wrong reason. Same idiom as the discard test above.
        Assert.IsTrue(card == null,
            "The discard flight ends by destroying the view; a surviving card means the flight " +
            "never landed.");

        DestroyRig(table, cam);
        Object.Destroy(holder.gameObject);
        yield return null;
    }
}
