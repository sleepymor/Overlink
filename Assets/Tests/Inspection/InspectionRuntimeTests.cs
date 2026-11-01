using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Overlink.Cards;
using Overlink.Core;
using Overlink.Match;
using UnityEngine;
using UnityEngine.TestTools;

public class InspectionRuntimeTests
{
    [UnityTest]
    public IEnumerator BoardCardClick_OpensVisiblePreviewOnFirstClick()
    {
        var cameraObject = new GameObject("Inspection test camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        var match = new GameObject("Inspection test match");
        var coordinator = match.AddComponent<GameCoordinator>();
        coordinator.enabled = false;
        var presenter = match.AddComponent<BattlefieldPresenter>();
        presenter.enabled = false;
        var input = match.AddComponent<BattlefieldInput>();
        input.enabled = false;
        input.presenter = presenter;
        input.coordinator = coordinator;
        var state = new MatchState();
        typeof(GameCoordinator).GetField("matchState", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(coordinator, state);
        var definition = new CardDefinition { Id = "inspect", Name = "Inspect probe", Type = CardType.Unit,
            Cost = 1, Atk = 2, Hp = 4, AbilityText = "Test full ability." };
        var card = new CardInstance(definition, PlayerIndex.Opponent);
        state.Players[1].Board.GetSlot(SlotType.CoreActive, 0, -1).Card = card;
        var data = new CardData(definition.Id, definition.Name, definition.Cost, definition.AbilityText, Color.white);
        data.AttachSource(definition, card);
        presenter.cardDataResolver = _ => data;
        presenter.hud = BattlefieldHud.Ensure();
        presenter.BuildBoard();
        presenter.Repaint(state);
        yield return null;

        var tile = presenter.TileOf(card);
        Assert.IsNotNull(tile);
        camera.transform.position = tile.Body.transform.position - Vector3.forward * 8f;
        camera.transform.rotation = Quaternion.identity;
        Vector2 screen = camera.WorldToScreenPoint(tile.Body.transform.position);
        BoardHit hit = presenter.FindHitAt(camera, screen);
        Assert.IsNotNull(hit);
        Assert.AreSame(card, hit.Card, "The projected click must resolve the deployed card.");
        typeof(BattlefieldInput).GetMethod("HandleBoardClick", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(input, new object[] { hit });
        Assert.IsTrue(presenter.hud.ReaderShowsCard, "The first click must open the card preview.");
        Assert.IsNotNull(presenter.inspectRoot);
        var preview = presenter.inspectRoot.GetComponentInChildren<CardView>();
        Assert.IsNotNull(preview);
        Assert.Greater(camera.WorldToViewportPoint(preview.transform.position).z, camera.nearClipPlane,
            "The preview must be in front of the lens.");
        Assert.IsTrue(presenter.hud.TryGetReaderCardBox(out Vector2 center, out _));
        Assert.IsTrue(presenter.IsPointerOverInspect(center));

        Object.Destroy(match);
        Object.Destroy(cameraObject);
        Object.Destroy(presenter.hud.gameObject);
        yield return null;
    }
}
