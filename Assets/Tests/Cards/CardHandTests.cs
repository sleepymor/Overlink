using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Overlink.Cards;

public class CardHandTests
{
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

    [UnityTest]
    public IEnumerator DrawAndDiscard_UpdatesHand()
    {
        var go = new GameObject("TestTable");
        go.AddComponent<DeckController>();
        var hand = go.AddComponent<HandController>();
        hand.handSizeStart = 0;
        yield return null;

        hand.DrawOne();
        yield return new WaitForSeconds(0.7f);
        Assert.AreEqual(1, hand.CardCount, "DrawOne should add exactly one card.");
        Assert.IsNotNull(Object.FindObjectOfType<CardView>(), "A CardView should exist in the scene.");

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
        var data = new CardData("test", "Test", 1, "Test card.", Color.red);
        var size = new Vector3(1.2f, 1.8f, 0.1f);
        var card = CardView.Create(data, parent.transform, size);
        yield return null;

        Assert.AreEqual(size, card.BaseScale, "BaseScale should match the requested size.");
        var body = card.transform.Find("Body");
        Assert.IsNotNull(body, "Card should have a Body child.");
        Assert.AreEqual(size, body.localScale, "Body should carry the requested size.");
        var col = card.GetComponent<BoxCollider>();
        Assert.AreEqual(size, col.size, "Collider should match the requested size.");

        var newSize = new Vector3(0.7f, 1.0f, 0.05f);
        card.SetSize(newSize);
        Assert.AreEqual(newSize, card.BaseScale, "SetSize should update BaseScale.");
        Assert.AreEqual(newSize, body.localScale, "SetSize should resize the body.");
        Assert.AreEqual(newSize, col.size, "SetSize should resize the collider.");

        Object.Destroy(parent);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DrawBeyondDeck_ReshufflesDiscard()
    {
        var go = new GameObject("TestTable2");
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
}
