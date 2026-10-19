using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Overlink.Core;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// Point-and-click needs two things the drag path never needed: a hit-test broad enough to cover every
    /// object on the table (not just the four sockets a card may be dropped into), and a card-to-tile map so a
    /// list of legal targets from <see cref="CombatLegality"/> can be painted as glowing sockets.
    /// <para>
    /// Both rest on the same contract the drop targeting already rests on - <b>tile names are the interface</b>
    /// - because the board is a hierarchy an editor is allowed to regroup. Nothing here asserts where a tile is;
    /// it asserts what a tile <em>means</em>, which is the part that must not drift when the layout changes.
    /// </para>
    /// </summary>
    public class PointerTargetingTests
    {
        // ---- The name contract ------------------------------------------------------------

        /// <summary>Every object the click layer is allowed to recognise, spelled as the builder spells it.</summary>
        static IEnumerable<string> AllTileNames()
        {
            foreach (string side in new[] { "Play", "Opp" })
            {
                yield return side + "CoreActive0";

                for (int i = 0; i < Board.CoreBenchSlots; i++)
                {
                    yield return $"{side}CoreBench{i}";
                }

                yield return side + "Protocol";

                for (int n = 0; n < Board.NodeCount; n++)
                {
                    yield return $"Node{side}Active{n}_0";

                    for (int i = 0; i < Board.NodeBenchSlots; i++)
                    {
                        yield return $"Node{side}Bench{n}_{i}";
                    }
                }
            }

            for (int n = 0; n < Board.NodeCount; n++)
            {
                yield return $"NodePlate{n}";
            }

            yield return "DeckPile";
            yield return "DiscardPile";
        }

        [Test]
        public void TileOwnership_EveryObjectOnTheTableIdentifiesItself()
        {
            // A board regrouped by hand still clicks: identity comes from the name, not from where the tile
            // sits in the hierarchy.
            foreach (string name in AllTileNames())
            {
                Assert.IsTrue(BattlefieldPresenter.TryParseTileOwnership(name,
                        out PlayerIndex owner, out BattlefieldPresenter.TileRole role, out int nodeIndex),
                    $"{name} must be recognised, or it is invisible to the pointer.");

                switch (role)
                {
                    case BattlefieldPresenter.TileRole.Socket:
                        Assert.AreEqual(name.StartsWith("Play") || name.StartsWith("NodePlay")
                            ? PlayerIndex.Player : PlayerIndex.Opponent, owner, $"{name}'s owner.");
                        Assert.AreEqual(name.StartsWith("Node") ? TrailingNode(name) : -1, nodeIndex,
                            $"{name}'s node.");
                        break;

                    case BattlefieldPresenter.TileRole.Protocol:
                        Assert.AreEqual(name == "PlayProtocol" ? PlayerIndex.Player : PlayerIndex.Opponent, owner);
                        break;

                    case BattlefieldPresenter.TileRole.Plate:
                        Assert.AreEqual(TrailingNode(name), nodeIndex, "A plate must know which Node it is.");
                        break;

                    case BattlefieldPresenter.TileRole.Pile:
                        Assert.AreEqual(-1, nodeIndex, "A pile is in no battlefield.");
                        break;

                    default:
                        Assert.Fail($"{name} parsed but was given the role {role}, which the view drops.");
                        break;
                }
            }
        }

        [Test]
        public void TileOwnership_BoardFurnitureIsNotClickable()
        {
            // The tabletop, the header and the two status strips are decoration as far as picking goes - the
            // Core counters are handled separately, by rect, because they have no cube to tint.
            foreach (string name in new[]
                     {
                         "Table", "Tabletop", "Header", "ControlsHint", "PStatus", "OStatus", "Status",
                         "Core", "Nodes", "Bench", "Player", "Opponent", "Piles", "Body", "Label",
                         "VictoryBanner", null, "", "   ", "SomethingElse"
                     })
            {
                Assert.IsFalse(BattlefieldPresenter.TryParseTileOwnership(name, out _, out _, out _),
                    $"{name ?? "<null>"} is furniture and must not parse.");
            }
        }

        [Test]
        public void TileOwnership_IsStrictlyBroaderThanTheDropContract()
        {
            // The narrow contract decides where a dragged card may land; the broad one decides what a click can
            // reach. If a tile ever parsed as a drop target without also parsing as a click target, the drop
            // highlight would be lighting something the view cannot name.
            foreach (string name in AllTileNames())
            {
                if (!BattlefieldPresenter.TryParseSlotIdentity(name, out _, out _, out _))
                {
                    continue;
                }

                Assert.IsTrue(BattlefieldPresenter.TryParseTileOwnership(name, out _, out _, out _),
                    $"{name} is a drop target, so it must also be a click target.");
            }

            // And the other way round: nothing the opponent owns may ever be a drop target, however clickable.
            foreach (string name in AllTileNames())
            {
                if (!BattlefieldPresenter.TryParseTileOwnership(name, out PlayerIndex owner, out var role, out _)
                    || role != BattlefieldPresenter.TileRole.Socket || owner != PlayerIndex.Opponent)
                {
                    continue;
                }

                Assert.IsFalse(BattlefieldPresenter.TryParseSlotIdentity(name, out _, out _, out _),
                    $"{name} belongs to the opponent: clickable, never deployable.");
            }
        }

        static int TrailingNode(string name)
        {
            // NodePlate3 -> 3; NodePlayBench2_1 -> 2; PlayCoreBench1 -> -1 (no node). Node indices are single
            // digits by construction (Board.NodeCount is 4), so the first digit after the prefix is the answer.
            if (!name.StartsWith("Node"))
            {
                return -1;
            }

            for (int i = 4; i < name.Length; i++)
            {
                if (char.IsDigit(name[i]))
                {
                    return name[i] - '0';
                }
            }

            return -1;
        }

        // ---- The geometry --------------------------------------------------------------------

        static BattlefieldPresenter BuildThrowawayBoard(out Transform board)
        {
            var go = new GameObject("PointerTargetingTestPresenter");
            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();
            board = presenter.transform.Find("Battlefield");
            Assert.IsNotNull(board, "BuildBoard must create the board root.");
            return presenter;
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

        /// <summary>The centre of a named tile, in board-root coordinates.</summary>
        static Vector3 CentreOf(Transform board, string tileName)
        {
            Transform tile = FindDeep(board, tileName);
            Assert.IsNotNull(tile, $"{tileName} must exist on the board.");
            Transform body = tile.Find("Body");
            Assert.IsNotNull(body, $"{tileName} must have a Body child.");
            return board.InverseTransformPoint(body.position);
        }

        [Test]
        public void FindHit_AtEachTileCentreResolvesThatTile()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                foreach (string name in AllTileNames())
                {
                    BoardHit hit = presenter.FindHitInBoardSpace(CentreOf(board, name));

                    Assert.IsNotNull(hit, $"The centre of {name} must be clickable.");
                    Assert.AreEqual(name, hit.Tile.Name, $"A click at {name}'s centre resolved elsewhere.");
                    Assert.IsTrue(BattlefieldPresenter.TryParseTileOwnership(name,
                        out _, out BattlefieldPresenter.TileRole role, out _), $"{name} must parse.");
                    Assert.AreEqual(ExpectedKind(name, role), hit.Kind, $"{name}'s kind.");
                }
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        static BoardHitKind ExpectedKind(string name, BattlefieldPresenter.TileRole role)
        {
            switch (role)
            {
                case BattlefieldPresenter.TileRole.Plate: return BoardHitKind.Plate;
                case BattlefieldPresenter.TileRole.Protocol: return BoardHitKind.Protocol;
                case BattlefieldPresenter.TileRole.Pile:
                    return name == "DeckPile" ? BoardHitKind.DeckPile : BoardHitKind.DiscardPile;
                default: return BoardHitKind.Socket;
            }
        }

        [Test]
        public void FindHit_OnTheCoreCountersResolvesThemAsCores()
        {
            // The opponent's counter is the click target for an unblocked direct attack (GDD 2.1.6 Step 4), so
            // it has to be reachable even though it is a text strip rather than a socket.
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                Vector3 ours = board.InverseTransformPoint(FindDeep(board, "PStatus").position);
                Vector3 theirs = board.InverseTransformPoint(FindDeep(board, "OStatus").position);

                BoardHit player = presenter.FindHitInBoardSpace(ours);
                Assert.IsNotNull(player, "The player's Core counter must be clickable, if only to read it.");
                Assert.AreEqual(BoardHitKind.PlayerCore, player.Kind);

                BoardHit opponent = presenter.FindHitInBoardSpace(theirs);
                Assert.IsNotNull(opponent, "The opponent's Core counter must be clickable: it is an attack target.");
                Assert.AreEqual(BoardHitKind.OpponentCore, opponent.Kind);
                Assert.AreEqual(PlayerIndex.Opponent, opponent.Owner);
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void FindHit_OffTheTableResolvesNothing()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                Assert.IsNull(presenter.FindHitInBoardSpace(new Vector3(500f, 500f, 0f)),
                    "Empty space is not a click target, so a stray click cancels rather than inspecting.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void FindHit_ReportsTheCardStandingInTheSocket()
        {
            // The whole point of the second hit-test: a click on a unit has to reach the Core instance behind
            // the tile, or the details panel would be reading a picture rather than the truth.
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = NewMatch();
                CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Probe Striker");

                presenter.Repaint(state);

                BoardHit hit = presenter.FindHitInBoardSpace(CentreOf(board, "PlayCoreActive0"));
                Assert.IsNotNull(hit);
                Assert.AreSame(unit, hit.Card, "The socket must report the unit standing in it.");
                Assert.AreSame(hit.Tile, presenter.TileOf(unit),
                    "The tile the click resolved and the tile the card maps to must be one object, or a mark " +
                    "would light somewhere other than what was clicked.");

                BoardHit empty = presenter.FindHitInBoardSpace(CentreOf(board, "PlayCoreBench2"));
                Assert.IsNotNull(empty);
                Assert.IsNull(empty.Card, "An empty socket carries no card.");
                Assert.AreEqual(PlayerIndex.Player, empty.Owner);
                Assert.IsTrue(empty.IsOwnSocket);

                BoardHit theirs = presenter.FindHitInBoardSpace(CentreOf(board, "OppCoreActive0"));
                Assert.AreEqual(PlayerIndex.Opponent, theirs.Owner);
                Assert.IsFalse(theirs.IsOwnSocket, "A click on the enemy's socket is never one of ours.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        // ---- The shipped asset -------------------------------------------------------------
        // Everything above proves that a <em>procedurally built</em> board clicks. The scene does not ship a
        // procedural board: it ships Assets/Prefabs/Battlefield.prefab, and BuildBoard() <em>binds</em> to it by
        // name rather than rebuilding. So the asset is under test in its own right. A hand-edit that overlaps two
        // sockets, renames one, or bolts a collider onto one is invisible to every test above and shows up in
        // play as a click that lands on the wrong card - which is exactly the failure mode a player has no way to
        // describe. Same assertions, run against the thing the game actually loads.

        const string BattlefieldPrefabPath = "Assets/Prefabs/Battlefield.prefab";

        /// <summary>
        /// Loads the shipped prefab under a throwaway presenter and returns the board root, asserting the bind
        /// succeeded. A failed bind is destroyed and rebuilt from scratch, so the surviving instance <em>is</em>
        /// the proof: if these tests ever start passing by rebuilding, they stop testing the asset.
        /// </summary>
        static Transform BindShippedPrefab(out BattlefieldPresenter presenter)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldPrefabPath);
            Assert.IsNotNull(prefab, $"{BattlefieldPrefabPath} is what Gameplay.unity loads. Rebuild it from " +
                "Overlink > Battlefields > Build Battlefield Prefab.");

            var host = new GameObject("ShippedPrefabTestHost");
            GameObject board = Object.Instantiate(prefab);
            board.name = "Battlefield";
            board.transform.SetParent(host.transform, false);

            presenter = host.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();

            Assert.IsTrue(board != null,
                "The presenter discarded the shipped hierarchy and rebuilt it, so the bind failed and this " +
                "test proved nothing. TryBindBoard()'s name index is the place to look.");
            return board.transform;
        }

        [Test]
        public void ShippedPrefab_HoldsEveryTileTheClickLayerNames()
        {
            BattlefieldPresenter presenter;
            Transform board = BindShippedPrefab(out presenter);
            try
            {
                // One loop, two failures distinguished: a missing tile is silent invisibility to the pointer,
                // and a tile with no Body child has no footprint to project a click onto.
                foreach (string name in AllTileNames())
                {
                    Assert.IsNotNull(FindDeep(board, name),
                        $"{name} is absent from the shipped prefab, so it can never be clicked.");
                    Assert.IsNotNull(FindDeep(board, name).Find("Body"),
                        $"{name} has no Body child, so it has no footprint for a click to land on.");
                }
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ShippedPrefab_EveryTileCentreResolvesThatTile()
        {
            // The overlap check, expressed as the symptom that matters: two sockets whose baked rectangles
            // intersect steal each other's clicks, and which one wins depends on iteration order rather than on
            // anything a designer could see.
            BattlefieldPresenter presenter;
            Transform board = BindShippedPrefab(out presenter);
            try
            {
                foreach (string name in AllTileNames())
                {
                    BoardHit hit = presenter.FindHitInBoardSpace(CentreOf(board, name));

                    Assert.IsNotNull(hit, $"The centre of {name} is not clickable in the shipped prefab.");
                    Assert.AreEqual(name, hit.Tile.Name,
                        $"A click at {name}'s centre resolved to {hit.Tile.Name} - the two sockets overlap, so " +
                        "one of them is unreachable.");
                }
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ShippedPrefab_CarriesNoCollidersAnywhere()
        {
            // Not incidental: the hand drag has to raycast <em>through</em> the board to reach the play line, and
            // the click layer deliberately projects onto the board plane instead of using Physics.Raycast. One
            // collider on one socket is enough to break a drag that starts over it, and it would break it only in
            // play, where the geometry is a prefab instance nobody is looking at.
            BattlefieldPresenter presenter;
            Transform board = BindShippedPrefab(out presenter);
            try
            {
                Collider[] found = board.GetComponentsInChildren<Collider>(true);
                if (found.Length == 0)
                {
                    return;
                }

                var names = new System.Text.StringBuilder();
                for (int i = 0; i < found.Length; i++)
                {
                    if (i > 0) names.Append(", ");
                    names.Append(found[i].gameObject.name);
                }

                Assert.Fail($"Board tiles are painted, not simulated. Colliders on them: {names}");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        // ---- Marks ---------------------------------------------------------------------------

        [Test]
        public void ShowAttack_LightsTheAttackerItsTargetsAndTheCore()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = NewMatch();
                CardInstance attacker = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Attacker");
                CardInstance mirror = Place(state, PlayerIndex.Opponent, SlotType.CoreActive, 0, -1, "Mirror");
                CardInstance offNode = Place(state, PlayerIndex.Opponent, SlotType.NodeActive, 0, 2, "Far");
                presenter.Repaint(state);

                presenter.ShowAttack(attacker, new List<CardInstance> { mirror, offNode }, true);

                Assert.AreEqual(BattlefieldPresenter.PickMark.Selected, presenter.TileOf(attacker).Mark);
                Assert.AreEqual(BattlefieldPresenter.PickMark.AttackTarget, presenter.TileOf(mirror).Mark);
                Assert.AreEqual(BattlefieldPresenter.PickMark.AttackTarget, presenter.TileOf(offNode).Mark);
                Assert.IsTrue(presenter.CoreTargetMarked, "A direct Core hit must be lit as a target too.");

                presenter.ClearMarks();
                Assert.AreEqual(BattlefieldPresenter.PickMark.None, presenter.TileOf(attacker).Mark);
                Assert.AreEqual(BattlefieldPresenter.PickMark.None, presenter.TileOf(mirror).Mark);
                Assert.IsFalse(presenter.CoreTargetMarked,
                    "Clearing must be total: a leftover glow is a promise nobody intends to keep.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ShowBlocks_LightsTheBlockersAndTheAttacksTheyAnswer()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = NewMatch();
                CardInstance blocker = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Blocker");
                CardInstance attacker = Place(state, PlayerIndex.Opponent, SlotType.CoreActive, 0, -1, "Raider");
                presenter.Repaint(state);

                presenter.ShowBlocks(new List<CardInstance> { blocker }, null);
                Assert.AreEqual(BattlefieldPresenter.PickMark.Blocker, presenter.TileOf(blocker).Mark);

                presenter.ShowBlockPick(blocker, new List<CardInstance> { attacker });
                Assert.AreEqual(BattlefieldPresenter.PickMark.Selected, presenter.TileOf(blocker).Mark,
                    "A picked-up blocker is the player's own commitment, so it reads as selected.");
                Assert.AreEqual(BattlefieldPresenter.PickMark.BlockTarget, presenter.TileOf(attacker).Mark);
                Assert.IsFalse(presenter.CoreTargetMarked,
                    "Blocking never offers the Core, so a leftover Core glow would be a stale mark.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void MarkCards_IgnoresAUnitTheBoardDoesNotHave()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = NewMatch();
                CardInstance on = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "On Board");
                presenter.Repaint(state);

                // A unit that died during the same Combat step still has its instance in the caller's list; the
                // view must report that it lit nothing rather than throw or light a ghost socket.
                var ghost = new CardInstance(UnitDef("ghost", 2, 2), PlayerIndex.Opponent);

                Assert.AreEqual(1, presenter.MarkCards(new List<CardInstance> { on, ghost },
                    BattlefieldPresenter.PickMark.AttackTarget));
                Assert.AreEqual(0, presenter.MarkCards(null, BattlefieldPresenter.PickMark.AttackTarget));
                Assert.IsNull(presenter.TileOf(ghost));
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        // ---- Details ------------------------------------------------------------------------------

        [Test]
        public void ShowCard_PutsTheWholeCardInTheReader()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            BattlefieldHud hud = NewHud();
            try
            {
                presenter.hud = hud;
                MatchState state = NewMatch();
                CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Probe Striker");
                unit.Definition.AbilityText = "Deal 1 damage to the Core when it attacks.";
                unit.Definition.Keywords = new[] { Keyword.SummoningDelay };

                // HasSummoningDelay is latched from the definition at construction, so a card that gains the
                // keyword afterwards has to be told directly - which is also what the status line reads.
                unit.HasSummoningDelay = true;
                presenter.Repaint(state);

                presenter.ShowCard(unit, state, "YOUR CORE ACTIVE");

                Assert.IsTrue(hud.IsReaderVisible, "A card click must open the reader.");
                Assert.AreEqual("Probe Striker", hud.ReaderTitle);
                Assert.AreEqual("YOUR CORE ACTIVE", hud.ReaderFoot);
                StringAssert.Contains("UNIT", hud.ReaderBody);
                StringAssert.Contains("ATK 2", hud.ReaderBody);
                StringAssert.Contains("Deal 1 damage to the Core when it attacks.", hud.ReaderBody);
                StringAssert.Contains("[SummoningDelay]", hud.ReaderBody,
                    "Keywords are the difference between a card that works and a card that looks broken.");
                StringAssert.Contains("SUMMONING DELAY", hud.ReaderBody,
                    "The status line must say why the unit is not attacking this step.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ShowCard_ForANonCombatCardLeavesOutTheStats()
        {
            // A Protocol or a Relay printed as "ATK 0 / HP 0" reads as a bug, not as a card with no stats
            // (GDD 4.15.1).
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            BattlefieldHud hud = NewHud();
            try
            {
                presenter.hud = hud;
                MatchState state = NewMatch();

                // A Protocol is authored with no stats (GDD 4.15.1), so the details text must not print the
                // line at all rather than print it as zeroes.
                CardInstance protocol = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1,
                    "Probe Protocol", CardType.Protocol);
                protocol.Definition.Atk = 0;
                protocol.Definition.Hp = 0;
                presenter.Repaint(state);

                presenter.ShowCard(protocol, state, "YOUR PROTOCOL");

                StringAssert.Contains("PROTOCOL", hud.ReaderBody, "The type line leads.");
                Assert.IsFalse(hud.ReaderBody.Contains("ATK"), "A Protocol has no ATK to print.");
                Assert.IsFalse(hud.ReaderBody.Contains("HP"), "Nor HP.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ShowPileEntry_ReadsTheDiscardInFull()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            BattlefieldHud hud = NewHud();
            try
            {
                presenter.hud = hud;
                MatchState state = NewMatch();
                CardInstance spent = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Spent Card");

                presenter.ShowPileEntry("DISCARD", spent, 1, 9);

                Assert.AreEqual("Spent Card", hud.ReaderTitle, "The discard is public knowledge.");
                StringAssert.Contains("DISCARD  2 / 9", hud.ReaderFoot,
                    "and the browser says where in the pile the player is standing.");
                StringAssert.Contains("ATK 2", hud.ReaderBody);
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ShowDeck_ReportsTheCountAndRefusesToRevealTheOrder()
        {
            // The deck is the one clickable object on the table that will not open. Deck-Out is a victory
            // condition, so the count is information the player is owed; what is next is not.
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            BattlefieldHud hud = NewHud();
            try
            {
                presenter.hud = hud;
                MatchState state = NewMatch();
                CardInstance top = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Top Card");

                presenter.ShowDeck(7);

                Assert.AreEqual("DECK", hud.ReaderTitle, "titled by the stack, never by a card inside it");
                StringAssert.Contains("7 cards left", hud.ReaderBody);
                Assert.IsFalse(hud.ReaderBody.Contains("Top Card"),
                    "browsing the deck must never name what is coming");

                presenter.ShowDeck(0);
                StringAssert.Contains("Deck-Out", hud.ReaderBody,
                    "an empty deck is a loss condition, so saying so is the point of opening it");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void ClearDetails_TakesTheReaderOffScreen()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            BattlefieldHud hud = NewHud();
            try
            {
                presenter.hud = hud;
                MatchState state = NewMatch();
                CardInstance unit = Place(state, PlayerIndex.Player, SlotType.CoreActive, 0, -1, "Attacker");
                presenter.Repaint(state);

                presenter.ShowCard(unit, state, "YOUR CORE ACTIVE");
                Assert.IsTrue(hud.IsReaderVisible);

                presenter.ClearDetails();
                Assert.IsFalse(hud.IsReaderVisible, "A cancelled click must close the reader, not grey it out.");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        // ---- Descriptions ---------------------------------------------------------------------------

        [Test]
        public void BoardHit_DescribesItsSpotInPlayerWords()
        {
            SlotType which = SlotType.NodeBench;
            var socket = new BoardHit
            {
                Kind = BoardHitKind.Socket,
                Owner = PlayerIndex.Player,
                SlotType = which,
                SlotIndex = 1,
                NodeIndex = 2
            };
            Assert.AreEqual("YOUR NODE 3 BENCH 2", socket.Describe(),
                "Node and slot indices are zero-based in Core and one-based for the player.");

            var core = new BoardHit { Kind = BoardHitKind.OpponentCore, Owner = PlayerIndex.Opponent };
            Assert.AreEqual("OPPONENT CORE", core.Describe());
            Assert.AreEqual("DECK", new BoardHit { Kind = BoardHitKind.DeckPile }.Describe());
            Assert.AreEqual("NODE 4", new BoardHit { Kind = BoardHitKind.Plate, NodeIndex = 3 }.Describe());
            Assert.AreEqual("ENEMY PROTOCOL",
                new BoardHit { Kind = BoardHitKind.Protocol, Owner = PlayerIndex.Opponent }.Describe());
        }

        // ---- fixtures ---------------------------------------------------------------------------------

        const string TestHudName = BattlefieldHud.RootName + " (Pointer)";

        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();
        }

        BattlefieldHud NewHud()
        {
            var go = new GameObject(TestHudName, typeof(RectTransform));
            spawned.Add(go);
            var hud = go.AddComponent<BattlefieldHud>();
            hud.Build();
            return hud;
        }

        static MatchState NewMatch()
        {
            var state = new MatchState();
            state.InitializeMatch(Deck(), Deck());
            return state;
        }

        static List<CardDefinition> Deck()
        {
            var list = new List<CardDefinition>();
            for (int i = 0; i < 20; i++)
            {
                list.Add(UnitDef("deck_" + i, 1, 1));
            }

            return list;
        }

        static CardInstance Place(MatchState state, PlayerIndex owner, SlotType type, int index, int node,
            string name, CardType cardType = CardType.Unit)
        {
            BoardSlot slot = state.GetPlayer(owner).Board.GetSlot(type, index, node);
            Assert.IsNotNull(slot, $"{type}[{index}] node {node} must exist.");

            var unit = new CardInstance(UnitDef(name, 2, 2), owner)
            {
                State = UnitState.Ready
            };
            unit.Definition.Type = cardType;
            slot.Card = unit;
            return unit;
        }

        static CardDefinition UnitDef(string name, int atk, int hp)
        {
            return new CardDefinition
            {
                Id = "card_fsti_" + (System.Math.Abs(name.GetHashCode()) % 900 + 100),
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 2,
                Atk = atk,
                Hp = hp
            };
        }
    }
}
