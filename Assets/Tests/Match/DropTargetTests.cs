using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Overlink.Core;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// Drag-and-drop deployment rests on one contract: every player socket tile must name exactly the Core
    /// <see cref="BoardSlot"/> it renders, and a play nominated to that slot must land there rather than
    /// wherever the auto-deploy spread happened to prefer. Before targeting existed, a unit could only ever
    /// reach the first free slot, so "drop it on my Bench" was physically impossible - these tests lock the
    /// seam that fixed it.
    /// </summary>
    public class DropTargetTests
    {
        // ---- The tile-name to slot contract -------------------------------------------

        /// <summary>
        /// Every hosting socket on the player's half, spelled the way the builder and the prefab name it.
        /// 1 Core Active + 4 Core Bench + 4 Nodes x (1 Active + 2 Bench) = 17 tiles.
        /// </summary>
        static IEnumerable<string> PlayerSocketTileNames()
        {
            for (int i = 0; i < Board.CoreActiveSlots; i++)
            {
                yield return $"PlayCoreActive{i}";
            }

            for (int i = 0; i < Board.CoreBenchSlots; i++)
            {
                yield return $"PlayCoreBench{i}";
            }

            for (int n = 0; n < Board.NodeCount; n++)
            {
                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    yield return $"NodePlayActive{n}_{i}";
                }

                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    yield return $"NodePlayBench{n}_{i}";
                }
            }
        }

        [Test]
        public void SlotIdentity_EveryPlayerSocketTileParses()
        {
            foreach (string name in PlayerSocketTileNames())
            {
                Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity(name, out _, out _, out _),
                    $"{name} names a player socket and must parse as one.");
            }
        }

        [Test]
        public void SlotIdentity_PlayerSocketTilesResolveToSeventeenDistinctSlots()
        {
            // A naming drift that collapsed two tiles onto the same slot would silently make one socket
            // unreachable, so the count of *distinct* results is the assertion that matters.
            var seen = new HashSet<string>();
            foreach (string name in PlayerSocketTileNames())
            {
                Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity(name,
                    out SlotType type, out int index, out int nodeIndex), $"{name} must parse.");
                Assert.IsTrue(seen.Add($"{type}:{index}:{nodeIndex}"),
                    $"{name} duplicates the slot {type}:{index}:{nodeIndex}.");
            }

            int expected = Board.CoreActiveSlots + Board.CoreBenchSlots
                + Board.NodeCount * (Board.NodeActiveSlots + Board.NodeBenchSlots);
            Assert.AreEqual(expected, seen.Count, "The tile names must cover every player socket exactly once.");
        }

        [Test]
        public void SlotIdentity_NodeTileSplitsNodeAndSlotIndices()
        {
            // "NodePlayActive2_1" carries two numbers: the node first, then the slot. Getting the order
            // backwards would send a drop on Node 2 to Node 1 and nothing else would notice.
            Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity("NodePlayBench3_1",
                out SlotType type, out int index, out int nodeIndex));
            Assert.AreEqual(SlotType.NodeBench, type);
            Assert.AreEqual(3, nodeIndex, "The leading number is the node.");
            Assert.AreEqual(1, index, "The trailing number is the slot.");

            Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity("NodePlayActive0_0",
                out type, out index, out nodeIndex));
            Assert.AreEqual(SlotType.NodeActive, type);
            Assert.AreEqual(0, nodeIndex);
            Assert.AreEqual(0, index, "A double zero must not be swallowed by the digit scan.");
        }

        [Test]
        public void SlotIdentity_CoreSlotsCarryNoNodeIndex()
        {
            Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity("PlayCoreBench2",
                out SlotType type, out int index, out int nodeIndex));
            Assert.AreEqual(SlotType.CoreBench, type);
            Assert.AreEqual(2, index);
            Assert.AreEqual(-1, nodeIndex, "The Core is shared and has no node of its own.");
        }

        [TestCase("OppCoreActive0")]
        [TestCase("OppCoreBench3")]
        [TestCase("OppProtocol")]
        [TestCase("NodeOppActive2_0")]
        [TestCase("NodeOppBench0_1")]
        [TestCase("NodePlate1")]
        [TestCase("PlayProtocol")]
        [TestCase("Header")]
        [TestCase("ControlsHint")]
        [TestCase("Table")]
        [TestCase("")]
        [TestCase(null)]
        public void SlotIdentity_NonTargetsDoNotParse(string name)
        {
            // Opponent sockets, Node plates, the Protocol tiles and the board furniture are never drop
            // targets. Parsing one as a slot would let a unit be deployed onto the opponent's half.
            Assert.IsFalse(BattlefieldPresenter.TryParseSlotIdentity(name, out _, out _, out _),
                $"{name ?? "<null>"} is not a player socket and must not parse.");
        }

        [Test]
        public void SlotIdentity_ParsedSlotsResolveOnARealBoard()
        {
            var state = new MatchState();
            Board board = state.Players[0].Board;

            foreach (string name in PlayerSocketTileNames())
            {
                BattlefieldPresenter.TryParseSlotIdentity(name,
                    out SlotType type, out int index, out int nodeIndex);

                BoardSlot slot = board.GetSlot(type, index, nodeIndex);
                Assert.IsNotNull(slot, $"{name} must resolve to a live slot.");
                Assert.AreEqual(type, slot.Type);
                Assert.AreEqual(index, slot.Index);
                Assert.AreEqual(nodeIndex, slot.NodeIndex);
            }
        }

        // ---- The engine honours a nominated slot ---------------------------------------

        [Test]
        public void PlayCard_UnitNominatedToCoreBenchStaysThereOnceTheActiveIsFilled()
        {
            // The complaint this whole feature answers: a unit dragged at the Bench used to land in the
            // first free slot instead. Targeting must be honoured, socket for socket.
            MatchState state = CreateMainPhaseState(out CardInstance card);
            Occupy(state.Players[0].Board.CoreActive[0]);
            var engine = new MatchEngine(state);

            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreBench, 2));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreSame(card, state.Players[0].Board.CoreBench[2].Card);
            Assert.AreNotSame(card, state.Players[0].Board.CoreActive[0].Card,
                "Targeting the Bench must not steal the Active slot.");
        }

        [Test]
        public void PlayCard_UnitNominatedToANodeBenchStaysThereOnceThatNodesActiveIsFilled()
        {
            MatchState state = CreateMainPhaseState(out CardInstance card);
            Occupy(state.Players[0].Board.NodeActive[3][0]);
            var engine = new MatchEngine(state);

            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.NodeBench, 1, 3));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreSame(card, state.Players[0].Board.NodeBench[3][1].Card);
            Assert.IsTrue(state.Players[0].Board.NodeBench[3][0].IsFree(),
                "Only the nominated socket of that node receives the unit.");
        }

        [Test]
        public void PlayCard_UnitNominatedToAnEmptyCoreBenchIsPromotedToTheVacantActive()
        {
            // Not a bug but a rule, and the one that makes "I deployed to my Bench and it moved" look like a
            // broken game: ResolveDestructionAndPromotion runs after every command and never lets an Active
            // socket stay empty, so a Bench drop with a vacant Active is pulled straight up. The play is
            // still legal - refusing it would cost the player their action - so the drop target stays lit
            // and the engine decides where the unit actually ends up.
            MatchState state = CreateMainPhaseState(out CardInstance card);
            var engine = new MatchEngine(state);

            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreBench, 2));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreSame(card, state.Players[0].Board.CoreActive[0].Card,
                "The vacant Active promotes the newly benched unit immediately.");
            Assert.IsTrue(state.Players[0].Board.CoreBench[2].IsFree());
        }

        [Test]
        public void PlayCard_UnitNominatedToAnEmptyNodeBenchIsPromotedToTheVacantNodeActive()
        {
            MatchState state = CreateMainPhaseState(out CardInstance card);
            var engine = new MatchEngine(state);

            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.NodeBench, 1, 3));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreSame(card, state.Players[0].Board.NodeActive[3][0].Card);
            Assert.IsTrue(state.Players[0].Board.NodeBench[3][1].IsFree());
        }

        [Test]
        public void PlayCard_RelayNominatedToACoreSocketIsRefused()
        {
            // Relays live on Nodes only. The gate pre-checks this so the player is told "cannot be deployed
            // to that socket" instead of watching the unit vanish somewhere else.
            MatchState state = CreateMainPhaseState(out CardInstance card, CardType.Relay);
            var engine = new MatchEngine(state);

            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreBench, 0));

            Assert.AreEqual(CommandResult.InvalidTarget, result);
            Assert.IsTrue(state.Players[0].Board.CoreBench[0].IsFree());
            Assert.AreEqual(1, state.Players[0].Hand.Count, "A refused play returns the card to the hand.");
        }

        [Test]
        public void PlayCard_NominatedSlotAlreadyOccupiedIsRefused()
        {
            MatchState state = CreateMainPhaseState(out CardInstance card);
            Occupy(state.Players[0].Board.CoreBench[1]);

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreBench, 1));

            Assert.AreEqual(CommandResult.SlotOccupied, result);
            Assert.IsNotNull(state.Players[0].Board.CoreBench[1].Card);
            Assert.AreNotSame(card, state.Players[0].Board.CoreBench[1].Card,
                "The occupant keeps its socket; a refused play displaces nothing.");
        }

        [Test]
        public void BoardSlot_IsFreeIsTheNegationOfIsOccupied()
        {
            // The drop highlight trusts IsFree() to decide whether a socket may light up.
            var state = new MatchState();
            BoardSlot slot = state.Players[0].Board.CoreBench[0];

            Assert.IsTrue(slot.IsFree());
            Assert.IsFalse(slot.IsOccupied());

            slot.Card = new CardInstance(CreateDefinition("u", "Unit", CardType.Unit), PlayerIndex.Player);

            Assert.IsFalse(slot.IsFree());
            Assert.IsTrue(slot.IsOccupied());
        }

        // ---- The board's geometry ------------------------------------------------------

        /// <summary>The board furniture the gameplay scene actually instantiates, bound by tile name.</summary>
        const string BattlefieldPrefabPath = "Assets/Prefabs/Battlefield.prefab";

        /// <summary>
        /// Builds a fresh board on a throwaway GameObject so the hit-test can be exercised without entering
        /// play mode. The presenter is handed no coordinator: drop targeting is pure geometry and needs no
        /// match state, which is exactly the separation the view is supposed to keep.
        /// </summary>
        static BattlefieldPresenter BuildThrowawayBoard(out Transform board)
        {
            var go = new GameObject("DropTargetTestPresenter");
            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();
            board = presenter.transform.Find("Battlefield");
            Assert.IsNotNull(board, "BuildBoard must create the board root.");
            return presenter;
        }

        /// <summary>
        /// The cube of a named tile, however deeply it is nested. The board groups its sockets (Core /
        /// Node / Player / Bench ranks), so a test that walked direct children would break every time the
        /// layout is reorganized - and the layout is exactly the thing that is allowed to change freely.
        /// Only the tile <em>names</em> are contractual; their parents are not.
        /// </summary>
        static Transform SocketBody(Transform board, string tileName)
        {
            Transform tile = FindDeep(board, tileName);
            return tile != null ? tile.Find("Body") : null;
        }

        /// <summary>Diagnostic for a failed bind: what the host object actually ended up holding.</summary>
        static string ChildNames(Transform root)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < root.childCount; i++)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
                sb.Append('\'').Append(root.GetChild(i).name).Append('\'');
            }
            return sb.Length == 0 ? "(none)" : sb.ToString();
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Asserts the grouping the editor relies on: every battlefield is one selectable object and every
        /// rank is its own, so moving a Bench moves its sockets and their captions together.
        /// </summary>
        static void AssertGrouped(Transform board)
        {
            Assert.IsNotNull(FindDeep(board, "Core"), "The Core battlefield must be its own group.");
            Assert.IsNotNull(FindDeep(board, "Nodes"), "The four Nodes must be grouped together.");
            Assert.IsNotNull(FindDeep(board, "Bench"), "Each Bench rank must be its own group.");

            // A socket carries its own caption, so dragging one socket never strands its text.
            Transform bench0 = FindDeep(board, "PlayCoreBench0");
            Assert.IsNotNull(bench0, "PlayCoreBench0 must exist.");
            Assert.IsNotNull(bench0.Find("Label"), "A socket's caption must be a child of the socket.");
            Assert.IsNotNull(bench0.Find("Body"), "A socket's cube must be a child of the socket.");

            // The event log is screen-space UI (BattlefieldHud) now, so the tabletop keeps the column it used
            // to reserve for eight wrapped world-space text lines that shrank with the camera.
            Assert.IsNull(FindDeep(board, "LogPanel"), "The log belongs to the HUD, not the board.");
            Assert.IsNull(FindDeep(board, "Log0"), "The log belongs to the HUD, not the board.");
        }

        [Test]
        public void FindDropTarget_AtEachPlayerSocketCentreResolvesThatSocket()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                AssertGrouped(board);

                foreach (string name in PlayerSocketTileNames())
                {
                    Transform body = SocketBody(board, name);
                    Assert.IsNotNull(body, $"{name} must have a Body child.");

                    BoardSlot slot = presenter.FindDropTarget(body.position);
                    Assert.IsNotNull(slot, $"The centre of {name} is a drop target.");
                    Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity(name,
                        out SlotType type, out int index, out int nodeIndex));
                    Assert.AreEqual(type, slot.Type, $"Wrong slot type for {name}.");
                    Assert.AreEqual(index, slot.Index, $"Wrong slot index for {name}.");
                    Assert.AreEqual(nodeIndex, slot.NodeIndex, $"Wrong node index for {name}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void FindDropTarget_OnOpponentSocketsResolvesNothing()
        {
            // You deploy to your own ranks only. Resolving an opponent tile would offer a play the engine
            // refuses, which reads as a broken drag rather than as a rule.
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                foreach (string name in new[] { "OppCoreActive0", "OppCoreBench2", "NodeOppBench1_0" })
                {
                    Transform body = SocketBody(board, name);
                    Assert.IsNotNull(body, $"{name} must exist on the board.");
                    Assert.IsNull(presenter.FindDropTarget(body.position),
                        $"{name} belongs to the opponent and is never a drop target.");
                }
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void FindDropTarget_OnThePilesResolvesNothing()
        {
            // The deck and the discard are printed onto the table so cards come from and go somewhere, but
            // they are not sockets: nothing is played TO them from the hand, so offering them as a target
            // would promise a play the engine refuses.
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                foreach (string name in new[] { "DeckPile", "DiscardPile" })
                {
                    Assert.IsFalse(BattlefieldPresenter.TryParseSlotIdentity(name,
                        out _, out _, out _), $"{name} must not parse as a hosting socket.");

                    Transform body = SocketBody(board, name);
                    Assert.IsNotNull(body, $"{name} must be real geometry on the table.");
                    Assert.IsNotNull(FindDeep(board, name).Find("Label"),
                        $"{name} must carry its own caption.");
                    Assert.IsNull(presenter.FindDropTarget(body.position),
                        $"{name} is never a drop target.");
                }
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void FindDropTarget_BindsTheShippedPrefabAndResolvesEveryPlayerSocket()
        {
            // The scene does not build its board: it binds one from Assets/Prefabs/Battlefield.prefab, which
            // is a different code path (FindTile rather than MakeTile). The slot identity is keyed off the
            // tile NAME precisely so both paths agree, and this is the test that proves the shipped prefab
            // still spells those names the way the contract expects.
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldPrefabPath);
            Assert.IsNotNull(prefab, $"The gameplay scene relies on {BattlefieldPrefabPath}.");

            var presenterGO = new GameObject("PrefabBindPresenter");
            var presenter = presenterGO.AddComponent<BattlefieldPresenter>();
            // PrefabUtility, exactly as BuildGameplayScene puts the board into the scene. Object.Instantiate
            // would name it "Battlefield(Clone)", which is not what the presenter is asked to bind.
            var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(presenterGO.transform, false);

            Transform bound = presenterGO.transform.Find("Battlefield");
            Assert.IsNotNull(bound,
                "Instantiate must keep the prefab root's name. Direct children of the host: " +
                ChildNames(presenterGO.transform));
            int boundId = bound.GetInstanceID();

            try
            {
                presenter.BuildBoard();

                Transform board = presenterGO.transform.Find("Battlefield");
                Assert.IsNotNull(board);
                Assert.AreEqual(boundId, board.GetInstanceID(),
                    "BuildBoard must bind the prefab hierarchy instead of tearing it down and rebuilding.");

                AssertGrouped(board);

                foreach (string name in PlayerSocketTileNames())
                {
                    Transform body = SocketBody(board, name);
                    Assert.IsNotNull(body, $"{name} must exist in the shipped prefab.");

                    BoardSlot slot = presenter.FindDropTarget(body.position);
                    Assert.IsNotNull(slot, $"{name} must be a drop target on the bound board.");
                    Assert.IsTrue(BattlefieldPresenter.TryParseSlotIdentity(name,
                        out SlotType type, out int index, out int nodeIndex));
                    Assert.AreEqual(type, slot.Type, $"Wrong slot type for {name}.");
                    Assert.AreEqual(index, slot.Index, $"Wrong slot index for {name}.");
                    Assert.AreEqual(nodeIndex, slot.NodeIndex, $"Wrong node index for {name}.");
                }

                Assert.IsNull(presenter.FindDropTarget(SocketBody(board, "OppCoreBench0").position),
                    "A bound board must still refuse the opponent's half.");
            }
            finally
            {
                Object.DestroyImmediate(presenterGO);
            }
        }

        [Test]
        public void FindDropTarget_AwayFromTheBoardResolvesNothing()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                Assert.IsNull(presenter.FindDropTarget(board.position + new Vector3(0f, 40f, 0f)),
                    "A point high above the table is over nothing.");
                Assert.IsNull(presenter.FindDropTarget(board.position + new Vector3(40f, 0f, 0f)),
                    "A point far off the side of the table is over nothing.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ClearDropHighlight_IsSafeBeforeAndAfterATarget()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                presenter.FindDropTarget(SocketBody(board, "PlayCoreBench0").position);
                presenter.ClearDropHighlight();
                presenter.FindDropTarget(board.position + new Vector3(0f, 40f, 0f));
                presenter.ClearDropHighlight();
                Assert.Pass("Clearing the highlight twice, and with nothing highlighted, must not throw.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        // ---- Fixtures ------------------------------------------------------------------

        /// <summary>
        /// Stands a living unit up in a socket behind the engine's back, so the promotion rule that follows
        /// every play has somewhere else to go and the nominated Bench can keep what it was given.
        /// </summary>
        static void Occupy(BoardSlot slot)
        {
            slot.Card = new CardInstance(
                CreateDefinition("occupant", "Occupant", CardType.Unit), PlayerIndex.Player);
        }

        static MatchState CreateMainPhaseState(out CardInstance card, CardType type = CardType.Unit)
        {
            var state = new MatchState();
            card = new CardInstance(CreateDefinition("test_unit", "Test Unit", type), PlayerIndex.Player);
            state.Players[0].Hand.Add(card);
            state.CurrentPhase = TurnPhase.Main;
            return state;
        }

        static CardDefinition CreateDefinition(string id, string name, CardType type)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = type,
                Cost = 2,
                Atk = type == CardType.Unit ? 2 : 0,
                Hp = type == CardType.Unit ? 2 : 0,
                Shield = 0,
                Keywords = System.Array.Empty<Keyword>(),
                Effects = System.Array.Empty<EffectDescriptor>()
            };
        }
    }
}
