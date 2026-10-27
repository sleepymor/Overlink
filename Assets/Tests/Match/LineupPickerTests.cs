using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Overlink.Core;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The opening lineup is chosen by dragging a card from the fan onto a lit Active socket, and no test can
    /// drag: the bridge has no pointer simulation and Unity's EditMode has no mouse. What that leaves behind is
    /// the two seams the gesture would otherwise be trusted to hold, pinned here instead.
    /// <para>
    /// <b>The mapping.</b> A nomination is an <c>int</c> per Active socket in battlefield order, and the engine
    /// reads that array back as <see cref="BoardSlot"/> positions (<c>MatchEngine.ActiveSlotsInBattlefieldOrder</c>,
    /// already pinned by <c>OpeningLineupTests</c>). The front end walks the same order backwards, socket to
    /// index. Those two halves agree by construction and nothing but a test can keep them agreeing, so a
    /// disagreement - which would field the whole lineup one socket to the side, or drop a Unit onto a socket
    /// the player never lit - fails here rather than in a match nobody can explain.
    /// </para>
    /// <para>
    /// <b>The preview.</b> A nominated Unit is painted on the socket as a substitution inside the normal paint
    /// pass, because it is not on the board yet. The substitution has to be invisible everywhere else: inert
    /// once setup closes, inert on the opponent's half, and stripped of the summoning mark an offered card
    /// genuinely carries but the lineup explicitly waives. Each of those is a lie the board could tell a player
    /// about a card they were about to lose, so each gets its own test.
    /// </para>
    /// </summary>
    public class LineupPickerTests
    {
        // ---- The encoding -----------------------------------------------------------------

        /// <summary>
        /// A nomination array is one int per Active socket, and <c>0</c> means "leave this battlefield open".
        /// That only works while no real card can carry id 0, which the counter's start guarantees and nothing
        /// else in the codebase asserts. If it ever regressed, a player dropping a Unit onto a socket would
        /// watch it vanish: the drop would be recorded as an empty socket and the confirm would field nothing.
        /// </summary>
        [Test]
        public void ZeroInstanceIdIsReservedForTheEmptySocketMarker()
        {
            CardInstance first = NewInstance("card_sentinel_a", PlayerIndex.Player);
            CardInstance second = NewInstance("card_sentinel_b", PlayerIndex.Opponent);

            Assert.Greater(first.InstanceId, 0, "id 0 belongs to the empty marker, not to a card");
            Assert.Greater(second.InstanceId, first.InstanceId, "the counter only ever rises");
        }

        // ---- Socket to index ---------------------------------------------------------------

        [Test]
        public void NominationIndex_EveryActiveSocketMapsOnceInBattlefieldOrder()
        {
            Board board = SetupMatch().GetPlayer(PlayerIndex.Player).Board;
            var mapped = new bool[ConfirmLineupCommand.RequiredLength];

            for (int i = 0; i < Board.CoreActiveSlots; i++)
            {
                int index = GameCoordinator.NominationIndexFor(board.CoreActive[i]);
                Assert.AreEqual(i, index, $"Core Active {i} indexes the first {Board.CoreActiveSlots} slots");
                Assert.IsFalse(mapped[index], $"{index} is claimed twice");
                mapped[index] = true;
            }

            for (int n = 0; n < Board.NodeCount; n++)
            {
                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    int index = GameCoordinator.NominationIndexFor(board.NodeActive[n][i]);
                    Assert.AreEqual(Board.CoreActiveSlots + n * Board.NodeActiveSlots + i, index,
                        $"Node {n + 1} Active {i} follows the Core rank, in node order");
                    Assert.IsTrue(index >= 0 && index < mapped.Length, $"{index} is inside the array");
                    Assert.IsFalse(mapped[index], $"{index} is claimed twice");
                    mapped[index] = true;
                }
            }

            Assert.IsTrue(mapped.All(x => x),
                "every nomination index is reachable from a socket, or the confirm can never be given that slot");
        }

        /// <summary>
        /// The five Actives are the only places an opening Unit can stand: a Bench is not in any battlefield and
        /// the Protocol socket hosts neither. A mapper that accepted one of these would let a drop nominate a
        /// socket the engine then refuses to fill, and the whole lineup would be rejected on confirm.
        /// <para>
        /// Ownership is deliberately not part of this mapping - it identifies a socket by shape. Which half of
        /// the table a drop may name is the drop probe's business, because that is the layer that knows what the
        /// player can actually see lit.
        /// </para>
        /// </summary>
        [Test]
        public void NominationIndex_NoSocketOutsideTheActivesMaps()
        {
            Board board = SetupMatch().GetPlayer(PlayerIndex.Player).Board;

            for (int i = 0; i < Board.CoreBenchSlots; i++)
            {
                Assert.AreEqual(-1, GameCoordinator.NominationIndexFor(board.CoreBench[i]),
                    $"Core Bench {i} is reserve, not a battlefield");
            }

            for (int n = 0; n < Board.NodeCount; n++)
            {
                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    Assert.AreEqual(-1, GameCoordinator.NominationIndexFor(board.NodeBench[n][i]),
                        $"Node {n + 1} Bench {i} is reserve, not a battlefield");
                }
            }

            // The Protocol is not in the slot system at all: SlotType has no Protocol member, and the board holds
            // its occupant as a bare CardInstance. So there is no socket to nominate onto, and a probe aimed at
            // the Protocol tile resolves to nothing before this mapper ever runs - which is the assertion below,
            // rather than a lookup that cannot be written.
            Assert.IsNull(board.GetSlot(SlotType.CoreActive, Board.CoreActiveSlots, -1),
                "an index past the last Active resolves to nothing instead of throwing");
            Assert.AreEqual(-1, GameCoordinator.NominationIndexFor(null), "no slot, no index");
        }

        // ---- The preview -------------------------------------------------------------------

        [Test]
        public void LineupPreview_EachNominatedUnitPaintsOnItsOwnSocket()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = SetupMatch();
                List<CardInstance> offer = state.GetPlayer(PlayerIndex.Player).LineupOffer;
                Assert.AreEqual(MatchState.LineupOfferSize, offer.Count, "a full offer to spread across the sockets");

                // Deliberately sparse and deliberately not in offer order: the Core rank, Node 2 and Node 4.
                // A paint that walked the array by position rather than by index would show up here as the
                // second card landing on Node 1, which the player left open on purpose.
                int[] ids = new int[ConfirmLineupCommand.RequiredLength];
                ids[0] = offer[0].InstanceId;
                ids[2] = offer[1].InstanceId;
                ids[4] = offer[2].InstanceId;

                presenter.SetLineupPreview(ids);
                presenter.Repaint(state);

                Assert.AreSame(offer[0], CardAt(presenter, board, "PlayCoreActive0"), "the Core rank");
                Assert.AreSame(offer[1], CardAt(presenter, board, "NodePlayActive1_0"), "Node 2's Active");
                Assert.AreSame(offer[2], CardAt(presenter, board, "NodePlayActive3_0"), "Node 4's Active");
                Assert.IsNull(CardAt(presenter, board, "NodePlayActive0_0"), "Node 1 was left open");
                Assert.IsNull(CardAt(presenter, board, "NodePlayActive2_0"), "Node 3 was left open");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        /// <summary>
        /// An offered card <em>is</em> <see cref="UnitState.Summoned"/> - that is what a fresh
        /// <see cref="CardInstance"/> starts life as - and <c>CardText</c> prints "(S)" for it. <c>CompleteSetup</c>
        /// readies everything it fields and waives the delay outright, so a preview carrying the mark would
        /// advertise a summoning delay the lineup explicitly refuses to give, on a card the player is deciding
        /// whether to commit to. The control half matters: a real Summoned body still prints it, so the
        /// suppression is the preview's and not a suffix that stopped working.
        /// </summary>
        [Test]
        public void LineupPreview_PrintsNoStateMarkWhileARealBodyStillDoes()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = SetupMatch();
                CardInstance offered = state.GetPlayer(PlayerIndex.Player).LineupOffer[0];
                Assert.AreEqual(UnitState.Summoned, offered.State, "the premise: an offer is a fresh instance");

                int[] ids = new int[ConfirmLineupCommand.RequiredLength];
                ids[0] = offered.InstanceId;
                presenter.SetLineupPreview(ids);
                presenter.Repaint(state);

                string preview = LabelAt(presenter, board, "PlayCoreActive0");
                Assert.IsTrue(preview.StartsWith(offered.Definition.Name),
                    $"the preview still says which card it is, got \"{preview}\"");
                StringAssert.DoesNotContain("(S)", preview, "a nomination is not a body in delay");
                StringAssert.DoesNotContain("(E)", preview, "and it has not acted, because it cannot act yet");

                Place(state, PlayerIndex.Opponent, SlotType.CoreActive, 0, -1, "Late Arrival",
                    UnitState.Summoned);
                presenter.Repaint(state);

                StringAssert.Contains("(S)", LabelAt(presenter, board, "OppCoreActive0"),
                    "a real unit in delay still says so");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void LineupPreview_NeverPaintsTheOpponentsHalf()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = SetupMatch();
                CardInstance offered = state.GetPlayer(PlayerIndex.Player).LineupOffer[0];

                // The same array, read against the enemy Core: the socket index means "the nominating seat's
                // Core rank", and the opponent's Core is not the player's.
                int[] ids = new int[ConfirmLineupCommand.RequiredLength];
                ids[0] = offered.InstanceId;
                presenter.SetLineupPreview(ids);
                presenter.Repaint(state);

                Assert.IsNull(CardAt(presenter, board, "OppCoreActive0"),
                    "the player's offer cannot be previewed on the enemy side");
                Assert.AreSame(offered, CardAt(presenter, board, "PlayCoreActive0"),
                    "and the same id still paints at home");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        /// <summary>
        /// What a reroll leaves behind. The Units the player had nominated go back into the deck, so a stale id
        /// reaching the board would preview a card the offer no longer holds - one the player can click, and
        /// <c>BattlefieldInput</c> would happily send to the details panel as though it were on the table.
        /// </summary>
        [Test]
        public void LineupPreview_IgnoresAnIdTheOfferNoLongerHolds()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = SetupMatch();

                // The opponent's offered instance: a real id, on a real card, in nobody's player offer.
                int foreign = state.GetPlayer(PlayerIndex.Opponent).LineupOffer[0].InstanceId;
                int[] ids = new int[ConfirmLineupCommand.RequiredLength];
                ids[0] = foreign;
                ids[1] = int.MaxValue;

                presenter.SetLineupPreview(ids);
                presenter.Repaint(state);

                Assert.IsNull(CardAt(presenter, board, "PlayCoreActive0"), "an id from the enemy offer is inert");
                Assert.IsNull(CardAt(presenter, board, "NodePlayActive0_0"), "and so is an id that names nothing");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void LineupPreview_ClearingItTakesTheUnitsOffTheSockets()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = SetupMatch();
                List<CardInstance> offer = state.GetPlayer(PlayerIndex.Player).LineupOffer;

                int[] ids = new int[ConfirmLineupCommand.RequiredLength];
                ids[0] = offer[0].InstanceId;
                presenter.SetLineupPreview(ids);
                presenter.Repaint(state);
                Assert.IsNotNull(CardAt(presenter, board, "PlayCoreActive0"), "the preview is up");

                // The coordinator's reroll path: the whole array goes away before the new offer is shown.
                presenter.SetLineupPreview(null);
                presenter.Repaint(state);

                Assert.IsNull(CardAt(presenter, board, "PlayCoreActive0"), "a cleared preview paints nothing");
                Assert.IsNull(CardAt(presenter, board, "NodePlayActive0_0"), "and no socket keeps a stale unit");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        /// <summary>
        /// The guard that outlives every other assertion here. Once setup closes the nomination means nothing:
        /// what the player chose is on the board, what they left is in the deck, and a preview still naming a
        /// card that went back would put two truths on one table. The stale array below names exactly such a
        /// card - left unchosen, returned to the deck - on the socket the confirm left open.
        /// </summary>
        [Test]
        public void LineupPreview_StepsAsideTheMomentSetupCloses()
        {
            BattlefieldPresenter presenter = BuildThrowawayBoard(out Transform board);
            try
            {
                MatchState state = SetupMatch();
                List<CardInstance> mine = state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList();
                List<CardInstance> theirs = state.GetPlayer(PlayerIndex.Opponent).LineupOffer.ToList();

                var fielded = new int[ConfirmLineupCommand.RequiredLength];
                fielded[0] = mine[0].InstanceId;
                var engine = new MatchEngine(state);
                Assert.AreEqual(CommandResult.Success,
                    engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, fielded)));
                Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new ConfirmLineupCommand(
                    PlayerIndex.Opponent, IdsOf(theirs))));
                Assert.IsFalse(state.SetupInProgress, "both seats confirmed, so the match is open");

                int[] stale = new int[ConfirmLineupCommand.RequiredLength];
                stale[1] = mine[1].InstanceId;
                presenter.SetLineupPreview(stale);
                presenter.Repaint(state);

                Assert.IsNull(CardAt(presenter, board, "NodePlayActive0_0"),
                    "the returned card is in the deck now, and must not also stand on Node 1");
                Assert.AreSame(mine[0], CardAt(presenter, board, "PlayCoreActive0"),
                    "the Unit that was fielded paints as the body it has become");
            }
            finally
            {
                Object.DestroyImmediate(presenter.gameObject);
            }
        }

        // ---- Helpers -----------------------------------------------------------------------

        /// <summary>A seeded match sitting in setup, with a full offer on both seats.</summary>
        static MatchState SetupMatch()
        {
            var state = new MatchState(1234);
            state.InitializeSetup(CheapUnits(12), CheapUnits(12));
            return state;
        }

        static CardInstance NewInstance(string id, PlayerIndex owner)
        {
            return new CardInstance(new CardDefinition
            {
                Id = id,
                Name = "Sentinel",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 2,
                Atk = 2,
                Hp = 2
            }, owner);
        }

        static List<CardDefinition> CheapUnits(int count)
        {
            var list = new List<CardDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new CardDefinition
                {
                    Id = "card_pick_" + i,
                    Name = "Pick Unit " + i,
                    Faction = Faction.Fsti,
                    Rarity = Rarity.Common,
                    Type = CardType.Unit,
                    Cost = 2,
                    Atk = 2,
                    Hp = 2
                });
            }

            return list;
        }

        static int[] IdsOf(List<CardInstance> offer)
        {
            var ids = new int[ConfirmLineupCommand.RequiredLength];
            for (int i = 0; i < ids.Length && i < offer.Count; i++)
            {
                ids[i] = offer[i].InstanceId;
            }

            return ids;
        }

        static BattlefieldPresenter BuildThrowawayBoard(out Transform board)
        {
            var go = new GameObject("LineupPickerTestPresenter");
            BattlefieldPresenter presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();
            board = presenter.transform.Find("Battlefield");
            Assert.IsNotNull(board, "BuildBoard must create the board root.");
            return presenter;
        }

        static CardInstance CardAt(BattlefieldPresenter presenter, Transform board, string tileName)
        {
            BoardHit hit = presenter.FindHitInBoardSpace(CentreOf(board, tileName));
            Assert.IsNotNull(hit, $"{tileName} must be clickable.");
            Assert.AreEqual(tileName, hit.Tile.Name, $"the probe at {tileName}'s centre resolved elsewhere");
            return hit.Card;
        }

        static string LabelAt(BattlefieldPresenter presenter, Transform board, string tileName)
        {
            BoardHit hit = presenter.FindHitInBoardSpace(CentreOf(board, tileName));
            Assert.IsNotNull(hit, $"{tileName} must be clickable.");
            return hit.Tile.Label.text;
        }

        static CardInstance Place(MatchState state, PlayerIndex owner, SlotType type, int index, int node,
            string name, UnitState unitState)
        {
            BoardSlot slot = state.GetPlayer(owner).Board.GetSlot(type, index, node);
            Assert.IsNotNull(slot, $"{type}[{index}] node {node} must exist.");

            var unit = new CardInstance(new CardDefinition
            {
                Id = "card_placed_" + name,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 2,
                Atk = 2,
                Hp = 2
            }, owner)
            {
                State = unitState
            };
            slot.Card = unit;
            return unit;
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
    }
}
