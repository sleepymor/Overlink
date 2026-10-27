using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Overlink.Core.Tests
{
    /// <summary>
    /// The opening lineup, the coin flip, the Combat-phase retreat and the [Activate] gate.
    /// <para>
    /// These four rules arrived together because they share one thing: each of them is a *permission* the game
    /// grants before the first turn is live or during the one step of a turn that has no main-phase safety net.
    /// That makes them the easiest place in the ruleset for a front end and the engine to disagree, so the
    /// mirror tests at the bottom of this file assert the same verdict twice — once from
    /// <see cref="CombatLegality"/>, which a button or an AI consults before a move exists, and once from
    /// <see cref="MatchEngine"/>, which is the only thing allowed to make the move happen. Agreement alone is
    /// not the assertion: every case also states which way it should go, because two halves that both say "no"
    /// agree perfectly and ship a dead button.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class OpeningLineupTests
    {
        // ------------------------------------------------------------------
        //  The offer: searched for, not taken off the top
        // ------------------------------------------------------------------

        [Test]
        public void InitializeSetup_OfferIsFiveUnitsUnderTheCostCeiling()
        {
            var state = new MatchState(7);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));

            foreach (PlayerIndex who in new[] { PlayerIndex.Player, PlayerIndex.Opponent })
            {
                PlayerState player = state.GetPlayer(who);
                Assert.AreEqual(MatchState.LineupOfferSize, player.LineupOffer.Count, $"{who} offer size");
                Assert.IsTrue(player.LineupOffer.All(c => c.Definition.Type == CardType.Unit),
                    $"{who} offer holds Units only");
                Assert.IsTrue(player.LineupOffer.All(c => c.Definition.Cost <= MatchState.LineupMaxCost),
                    $"{who} offer is inside the cost ceiling");
            }
        }

        [Test]
        public void InitializeSetup_SkipsOverpricedCardsAndKeepsTheRestOfTheDeckInOrder()
        {
            // Alternating a legal Unit and one the ceiling forbids. A top-of-deck draw would have produced three
            // eligible cards and two the ceiling forbids; the search has to walk past the expensive ones instead,
            // and everything it walks past - or never reaches - stays where it was, in order, so the rest of the
            // deck is still the true top of deck.
            var interleaved = new List<CardDefinition>();
            for (int i = 0; i < 10; i++)
            {
                interleaved.Add(Unit($"cheap_{i}", 1));
                interleaved.Add(Unit($"pricey_{i}", 9));
            }

            var state = new MatchState(3);
            state.InitializeSetup(interleaved, interleaved.ToList());

            PlayerState player = state.GetPlayer(PlayerIndex.Player);
            Assert.AreEqual(5, player.LineupOffer.Count);
            Assert.IsTrue(player.LineupOffer.All(c => c.Definition.Cost == 1), "no overpriced card was offered");

            // "The first five eligible" is not the input order: InitializeSetup shuffles before it deals, so the
            // offer is the front of whatever the shuffle produced. That sequence is recoverable without knowing
            // the RNG - the same seed over the same twenty cards, all of them eligible, deals its offer from the
            // front of the identical shuffle and leaves the rest in the deck, so offer-then-deck spells the
            // shuffled deck out exactly.
            var allEligible = new List<CardDefinition>();
            foreach (CardDefinition definition in interleaved)
            {
                allEligible.Add(Unit(definition.Id, 1));
            }

            var witness = new MatchState(3);
            witness.InitializeSetup(allEligible, allEligible.ToList());
            PlayerState witnessPlayer = witness.GetPlayer(PlayerIndex.Player);
            List<string> shuffled = witnessPlayer.LineupOffer.Select(c => c.DefinitionId)
                .Concat(witnessPlayer.Deck.Select(c => c.DefinitionId)).ToList();

            List<string> expectedOffer = shuffled.Where(id => id.StartsWith("cheap_")).Take(5).ToList();
            CollectionAssert.AreEqual(expectedOffer, player.LineupOffer.Select(c => c.DefinitionId).ToList(),
                "the search walked to the front of the shuffled deck and took the first five it was allowed to");

            List<string> expectedRemaining = shuffled.Where(id => !expectedOffer.Contains(id)).ToList();
            CollectionAssert.AreEqual(expectedRemaining, player.Deck.Select(c => c.DefinitionId).ToList(),
                "and the rest of the deck keeps that same order, skipped cards and unread tail included");
        }

        [Test]
        public void InitializeSetup_SkipsCheapNonUnitsBecauseTheLineupIsABody()
        {
            var deck = new List<CardDefinition>();
            for (int i = 0; i < 8; i++)
            {
                deck.Add(Ability($"trick_{i}", 1));
            }

            for (int i = 0; i < 6; i++)
            {
                deck.Add(Unit($"body_{i}", 1));
            }

            var state = new MatchState(5);
            state.InitializeSetup(deck, deck.ToList());

            List<CardInstance> offer = state.GetPlayer(PlayerIndex.Player).LineupOffer;
            Assert.AreEqual(5, offer.Count);
            Assert.IsTrue(offer.All(c => c.Definition.Type == CardType.Unit),
                "an Ability cannot occupy an Active socket, so it is not a lineup candidate however cheap it is");
        }

        [Test]
        public void InitializeSetup_ShortOfferDoesNotDeadlockWhenTheDeckHasFewEligibleUnits()
        {
            // The ceiling is a filter, so a deck built wrong shows up here rather than at the first attack.
            var deck = new List<CardDefinition>
            {
                Unit("body_0", 1),
                Unit("body_1", 2),
                Ability("trick_0", 1),
                Unit("body_2", 9)
            };

            var state = new MatchState(11);
            state.InitializeSetup(deck, deck.ToList());

            Assert.AreEqual(2, state.GetPlayer(PlayerIndex.Player).LineupOffer.Count,
                "only the two affordable Units qualify");
            Assert.IsTrue(state.SetupInProgress, "and setup still opens - a short offer is a weak start, not an error");
        }

        [Test]
        public void InitializeSetup_HoldsTheOfferApartFromTheHandAndLeavesNothingPlayable()
        {
            var state = new MatchState(13);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));

            Assert.AreEqual(0, state.GetPlayer(PlayerIndex.Player).Hand.Count,
                "an offered card is not in play and not in hand - it is in neither, until it is fielded or sent back");
            Assert.AreEqual(0, state.GetPlayer(PlayerIndex.Opponent).Hand.Count);
            Assert.IsTrue(state.SetupInProgress);
            Assert.AreEqual(TurnPhase.Upkeep, state.CurrentPhase);
            Assert.AreEqual(1, state.TurnNumber);
        }

        [Test]
        public void SetupInProgress_RefusesEveryCommandThatIsNotSetupOrConcession()
        {
            var state = new MatchState(17);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);
            CardInstance someUnit = state.GetPlayer(PlayerIndex.Player).LineupOffer[0];

            // The board the rest of the rules would apply to does not exist yet: no hand, no fielded units, no
            // first Upkeep. A phase that "advanced" during setup would also silently consume the first turn, so
            // the refusal has to be total rather than a per-command judgement call.
            Assert.AreEqual(CommandResult.InvalidPhase,
                engine.ExecuteCommand(new PlayCardCommand(PlayerIndex.Player, someUnit.InstanceId, SlotType.CoreActive, 0)),
                "a lineup card cannot be played as a card");
            Assert.AreEqual(CommandResult.InvalidPhase, engine.ExecuteCommand(new PassPhaseCommand(PlayerIndex.Player)));
            Assert.AreEqual(CommandResult.InvalidPhase, engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player)));
            Assert.AreEqual(CommandResult.InvalidPhase, engine.ExecuteCommand(new DrawCardCommand(PlayerIndex.Player)));
            Assert.AreEqual(CommandResult.InvalidPhase,
                engine.ExecuteCommand(new ActivateAbilityCommand(PlayerIndex.Player, someUnit.InstanceId)));
            Assert.AreEqual(CommandResult.InvalidPhase, engine.ExecuteCommand(new SwitchPositionCommand(
                PlayerIndex.Player, someUnit.InstanceId, SlotType.CoreBench, 0)));
            Assert.AreEqual(CommandResult.InvalidPhase,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, 1, 2)));
            Assert.IsTrue(state.SetupInProgress, "and none of it made any progress");
        }

        [Test]
        public void SetupInProgress_StillAcceptsAConcession()
        {
            // GDD 2.1.8 condition 4: conceding is a private decision, legal at any point in the match - and a
            // player who hates the five cards they were offered has to be able to leave with that rather than
            // being made to field them first.
            var state = new MatchState(19);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Player)));
            Assert.AreEqual(MatchResult.OpponentWin, state.Result);
        }

        // ------------------------------------------------------------------
        //  Confirming a lineup
        // ------------------------------------------------------------------

        [Test]
        public void ConfirmLineup_FieldsInBattlefieldOrderAndStartsTheMatchOnTheSecondConfirmation()
        {
            var state = new MatchState(23);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);
            state.FlipCoin();

            List<CardInstance> mine = state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList();
            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, Ids(mine))));

            // One player's lineup is not a match. The opponent may still reroll, so nothing about the opening
            // hand, the NP bonus or the first Upkeep may have happened yet.
            Assert.IsTrue(state.SetupInProgress, "the opponent has not confirmed");
            Assert.AreEqual(0, state.GetPlayer(PlayerIndex.Player).Hand.Count);

            List<CardInstance> theirs = state.GetPlayer(PlayerIndex.Opponent).LineupOffer.ToList();
            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Opponent, Ids(theirs))));

            Assert.IsFalse(state.SetupInProgress, "both lineups in, setup closed");
            Assert.AreEqual(5, state.GetPlayer(PlayerIndex.Player).Hand.Count);
            Assert.AreEqual(5, state.GetPlayer(PlayerIndex.Opponent).Hand.Count);

            Board board = state.GetPlayer(PlayerIndex.Player).Board;
            Assert.AreEqual(mine[0], board.CoreActive[0].Card, "the first nomination is the Core Active");
            for (int node = 0; node < Board.NodeCount; node++)
            {
                Assert.AreEqual(mine[node + 1], board.NodeActive[node][0].Card, $"Node {node + 1} Active");
            }

            Assert.AreEqual(0, state.GetPlayer(PlayerIndex.Player).LineupOffer.Count,
                "every offered card was either fielded or sent back");
            Assert.AreEqual(state.FirstPlayer, state.ActivePlayer);
            Assert.AreEqual(TurnPhase.Upkeep, state.CurrentPhase);
        }

        [Test]
        public void ConfirmLineup_FieldedUnitsAreReadyForTheFirstCombat()
        {
            // The opening lineup is not a deployment, so [Summoning Delay] has nothing to delay: it is a cost for
            // putting a body on the board during a turn, and the lineup is put on the board before turn 1 exists.
            // CompleteSetup therefore readies every opening unit explicitly, for both seats. Leaning on the
            // first Upkeep instead would have readied only the coin winner's, because OnUpkeepPhase touches the
            // active player's board - and a unit that is not Ready cannot block, so the losing seat would have
            // watched five Ready bodies swing at a 3-durability Core with nothing able to intercept. That is not
            // a tempo disadvantage, it is an unavoidable loss, which is why this is a rule and not a nicety.
            //
            // No card in the prototype pool prints the keyword, so the behaviour is unreachable in play today -
            // and it is pinned here because a comment would not stop the next person from "fixing" it by fielding
            // the lineup as Summoned.
            //
            // Every card in this deck is delayed, rather than a mix with an assertion on the Core Active: which
            // five of a shuffled deck reach the offer is the shuffler's business, and a test that happened to
            // probe a delayed card would pass or fail on the seed rather than on the rule.
            var deck = new List<CardDefinition>();
            for (int i = 0; i < 8; i++)
            {
                deck.Add(Unit($"delayed_{i}", 1, 2, 2, new[] { Keyword.SummoningDelay }));
            }

            var state = new MatchState(29);
            state.InitializeSetup(deck, deck.ToList());
            var engine = new MatchEngine(state);
            state.FlipCoin();

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new ConfirmLineupCommand(
                PlayerIndex.Player, Ids(state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList()))));
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new ConfirmLineupCommand(
                PlayerIndex.Opponent, Ids(state.GetPlayer(PlayerIndex.Opponent).LineupOffer.ToList()))));

            foreach (PlayerIndex who in new[] { PlayerIndex.Player, PlayerIndex.Opponent })
            {
                List<CardInstance> fielded = state.GetPlayer(who).Board.GetAllUnits(who);
                Assert.AreEqual(MatchState.LineupOfferSize, fielded.Count, $"{who} fielded the whole offer");
                foreach (CardInstance unit in fielded)
                {
                    Assert.IsTrue(unit.HasKeyword(Keyword.SummoningDelay),
                        $"{unit.Definition.Name} prints the keyword...");
                    Assert.IsFalse(unit.HasSummoningDelay,
                        "...but the first Upkeep clears the delay flag, so the opening lineup is not a deployment");
                    Assert.IsTrue(unit.CanAct(), $"{unit.Definition.Name} can act on turn 1");
                }
            }
        }

        [Test]
        public void ConfirmLineup_LeavesANominatedSocketOpenRatherThanDeadlockingSetup()
        {
            var state = new MatchState(31);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);
            state.FlipCoin();

            List<CardInstance> mine = state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList();
            int[] partial =
            {
                mine[0].InstanceId, 0, mine[2].InstanceId, 0, mine[4].InstanceId
            };

            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, partial)));

            Board board = state.GetPlayer(PlayerIndex.Player).Board;
            Assert.AreEqual(mine[0], board.CoreActive[0].Card);
            Assert.IsNull(board.NodeActive[0][0].Card, "Node 1 left open on purpose");
            Assert.AreEqual(mine[2], board.NodeActive[1][0].Card);
            Assert.IsNull(board.NodeActive[2][0].Card);
            Assert.AreEqual(mine[4], board.NodeActive[3][0].Card);
            Assert.AreEqual(2, state.GetPlayer(PlayerIndex.Player).LineupOffer.Count,
                "the two unfielded cards are still offered, and go back to the deck when setup closes");
        }

        [Test]
        public void ConfirmLineup_RejectsACardThatWasNeverOffered()
        {
            var state = new MatchState(37);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            // A card in the opponent's deck, a card in nobody's offer: the id is real, the card is not ours to
            // field. InvalidTarget rather than InvalidCommand because the shape of the nomination is fine and
            // its content is not.
            int stolen = state.GetPlayer(PlayerIndex.Opponent).Deck[0].InstanceId;
            int[] ids = { stolen, 0, 0, 0, 0 };

            Assert.AreEqual(CommandResult.InvalidTarget,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, ids)));
            Assert.IsFalse(state.GetPlayer(PlayerIndex.Player).LineupConfirmed,
                "a rejected nomination leaves the player's setup open, so they can try again");
        }

        [Test]
        public void ConfirmLineup_RejectsTheSameCardInTwoSockets()
        {
            var state = new MatchState(41);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            int only = state.GetPlayer(PlayerIndex.Player).LineupOffer[0].InstanceId;
            int[] ids = { only, only, 0, 0, 0 };

            Assert.AreEqual(CommandResult.InvalidCommand,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, ids)));
            Assert.AreEqual(0, state.GetPlayer(PlayerIndex.Player).Board.GetAllUnits(PlayerIndex.Player).Count,
                "and nothing was half-placed: the whole nomination is validated before the first card moves");
        }

        [Test]
        public void ConfirmLineup_RejectsAnArrayOfTheWrongLength()
        {
            var state = new MatchState(43);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            List<CardInstance> mine = state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList();
            Assert.AreEqual(5, ConfirmLineupCommand.RequiredLength,
                "the Core Active plus one Active per Node");

            Assert.AreEqual(CommandResult.InvalidCommand, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Player, mine.Take(3).Select(c => c.InstanceId).ToArray())),
                "too short - a truncated array would silently leave the tail sockets empty");
            Assert.AreEqual(CommandResult.InvalidCommand, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Player, new int[0])), "empty");
        }

        [Test]
        public void ConfirmLineup_TwiceIsRejectedBecauseTheLineupIsAlreadyOnTheBoard()
        {
            var state = new MatchState(47);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);
            state.FlipCoin();

            List<CardInstance> mine = state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList();
            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, Ids(mine))));

            Assert.AreEqual(CommandResult.InvalidCommand,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, Ids(mine))),
                "a confirmed lineup is fielded, not provisional");
        }

        // ------------------------------------------------------------------
        //  Rerolls, priced by the economy
        // ------------------------------------------------------------------

        [Test]
        public void RerollLineup_CostsTwoNPReplacesTheWholeOfferAndEscalates()
        {
            var state = new MatchState(53);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            PlayerState player = state.GetPlayer(PlayerIndex.Player);
            List<int> before = player.LineupOffer.Select(c => c.InstanceId).ToList();
            int npBefore = player.CurrentNP;

            Assert.AreEqual(MatchState.LineupRerollBaseCost, state.LineupRerollCost(PlayerIndex.Player));
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player)));

            Assert.AreEqual(npBefore - MatchState.LineupRerollBaseCost, player.CurrentNP, "2 NP gone");
            Assert.AreEqual(1, player.LineupRerolls);
            Assert.AreEqual(MatchState.LineupRerollBaseCost + 1, state.LineupRerollCost(PlayerIndex.Player),
                "the next one is 3");
            Assert.AreEqual(MatchState.LineupOfferSize, player.LineupOffer.Count, "a full replacement offer");
            Assert.AreNotEqual(before, player.LineupOffer.Select(c => c.InstanceId).ToList(),
                "the same five in the same order would mean the reroll did nothing");
        }

        [Test]
        public void RerollLineup_ConservesEveryCardBecauseTheOldOfferGoesBackIntoTheDeck()
        {
            var state = new MatchState(59);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            PlayerState player = state.GetPlayer(PlayerIndex.Player);
            int total = player.Deck.Count + player.LineupOffer.Count;

            engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player));
            Assert.AreEqual(total, player.Deck.Count + player.LineupOffer.Count,
                "a reroll moves cards between two piles and creates neither");

            engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player));
            Assert.AreEqual(total, player.Deck.Count + player.LineupOffer.Count);
        }

        [Test]
        public void RerollLineup_IsPricedOutOfReachByTheOpeningNPRatherThanByACounter()
        {
            // 3 NP at setup: one reroll at 2 leaves 1, and the next is 3. So the cap on rerolls is the same NP
            // the rest of the game spends, which is the point of escalating the price instead of counting tries.
            var state = new MatchState(61);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            PlayerState player = state.GetPlayer(PlayerIndex.Player);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player)));
            Assert.AreEqual(1, player.CurrentNP);

            Assert.AreEqual(CommandResult.InsufficientNP,
                engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player)), "3 NP is more than they have");
            Assert.AreEqual(1, player.LineupRerolls, "and a refused reroll does not bill the escalation");
        }

        [Test]
        public void RerollLineup_IsIndependentPerPlayerAndImpossibleAfterConfirming()
        {
            var state = new MatchState(67);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);
            state.FlipCoin();

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player)));
            Assert.AreEqual(0, state.GetPlayer(PlayerIndex.Opponent).LineupRerolls,
                "the escalation is per player, not a shared counter");

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Player, Ids(state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList()))));

            Assert.AreEqual(CommandResult.InvalidCommand,
                engine.ExecuteCommand(new RerollLineupCommand(PlayerIndex.Player)),
                "a fielded lineup cannot be thrown back");
        }

        // ------------------------------------------------------------------
        //  The coin flip and what it costs the loser
        // ------------------------------------------------------------------

        [Test]
        public void FlipCoin_AwardsTheGoingSecondNPToTheLoserAndNothingToTheWinner()
        {
            var state = new MatchState(71);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            PlayerIndex first = state.FlipCoin();
            Assert.AreEqual(first, state.FirstPlayer);
            Assert.AreEqual(first, state.ActivePlayer, "the coin winner is who the board is now set to act");

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Player, Ids(state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList()))));
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Opponent, Ids(state.GetPlayer(PlayerIndex.Opponent).LineupOffer.ToList()))));

            // Winner: 3 starting NP, then an Upkeep that gains the 3 base and NOTHING for the Nodes, because a full
            // lineup fields every Node Active for both seats and a contested Node pays neither. Loser: 3 + the
            // going-second bonus, and no Upkeep yet, because it is not their turn. So the opening is as poor in NP
            // as a match that began with an empty board, which is the intended shape - see
            // OpeningLineup_FieldsEveryNodeSoNeitherSideControlsAnyOfThem.
            Assert.AreEqual(PlayerState.StartingNP + 3, state.GetPlayer(first).CurrentNP,
                $"{first} opened first, on the base income alone");
            Assert.AreEqual(PlayerState.StartingNP + MatchState.SecondPlayerNPBonus,
                state.GetOpponent(first).CurrentNP, $"{state.GetOpponent(first)} opened second");
        }

        [Test]
        public void FlipCoin_IsIdempotentBecauseReDecidingFirstPlayerIsNotAFormality()
        {
            var state = new MatchState(73);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));

            PlayerIndex first = state.FlipCoin();
            Assert.AreEqual(first, state.FlipCoin(), "a second call returns the same answer instead of tossing again");
            Assert.AreEqual(first, state.FirstPlayer);
        }

        [Test]
        public void FlipCoin_SpreadsBothWaysAcrossSeedsAndIsNotConstant()
        {
            // A "coin flip" that always lands the same way is worse than no coin flip, because the +1 NP then
            // silently always goes to the same seat. Ten seeds is enough to catch a constant and cheap enough
            // to belong in a unit test; the stream is shared with the shuffles, so this also proves the flip is
            // reading the match RNG rather than its own.
            var players = new List<bool>();
            for (int seed = 0; seed < 10; seed++)
            {
                var state = new MatchState(seed);
                state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
                players.Add(state.FlipCoin() == PlayerIndex.Player);
            }

            Assert.IsTrue(players.Any(x => x), "some seeds open with the player");
            Assert.IsTrue(players.Any(x => !x), "and some open with the opponent");
        }

        [Test]
        public void FlipCoin_OutsideSetupIsRefusedRatherThanAnsweringPlayer()
        {
            var live = new MatchState(79);
            live.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            Assert.Throws<System.InvalidOperationException>(() => live.FlipCoin(),
                "the match is already running, so there is no first turn left to decide");

            var bare = new MatchState(83);
            Assert.Throws<System.InvalidOperationException>(() => bare.FlipCoin(),
                "and a state nobody initialised has not been offered a coin flip either");
        }

        [Test]
        public void CompleteSetup_FlipsTheCoinItselfWhenTheCallerNeverDid()
        {
            // A front end that forgets the toss must not get a silent "Player goes first" - that would hand the
            // tempo and the +1 NP to whoever happens to be index 0, and the banner would say nothing was
            // decided. So the toss is taken here, and it is a real one.
            var state = new MatchState(89);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            var engine = new MatchEngine(state);

            Assert.IsFalse(state.CoinFlipped);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Player, Ids(state.GetPlayer(PlayerIndex.Player).LineupOffer.ToList()))));
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Opponent, Ids(state.GetPlayer(PlayerIndex.Opponent).LineupOffer.ToList()))));

            Assert.IsTrue(state.CoinFlipped, "the match cannot start without a first player");
            Assert.AreEqual(state.FirstPlayer, state.ActivePlayer);
            Assert.AreEqual(PlayerState.StartingNP + MatchState.SecondPlayerNPBonus,
                state.GetOpponent(state.FirstPlayer).CurrentNP, "and the bonus followed the toss it was tied to");
        }

        // ------------------------------------------------------------------
        //  What a full lineup does to the Nodes: it contests all four of them
        // ------------------------------------------------------------------

        [Test]
        public void OpeningLineup_FieldsEveryNodeSoNeitherSideControlsAnyOfThem()
        {
            // The consequence that decides the opening. Node control is uncontested occupation, so a lineup that
            // fields all four Node Actives for BOTH seats leaves every Node contested: no NP income above the base
            // 3, no Core multipliers, and no 3-of-4 tally anywhere on the board. Under the looser reading this is
            // the moment the match was over - both sides sat at 4 of 4 controlled Nodes and GDD 2.1.8 awarded it to
            // whoever won the coin. Holding a Node is now the prerequisite for being paid, not the payment.
            var state = new MatchState(191);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            CompleteLineups(state);

            for (int node = 0; node < Board.NodeCount; node++)
            {
                Assert.IsTrue(state.GetPlayer(PlayerIndex.Player).Board.HoldsNode(node, PlayerIndex.Player),
                    $"both seats fielded Node {node + 1}");
                Assert.IsTrue(state.GetPlayer(PlayerIndex.Opponent).Board.HoldsNode(node, PlayerIndex.Opponent));
            }

            Assert.AreEqual(0, state.CountControlledNodes(PlayerIndex.Player),
                "and holding a Node the opponent also holds pays nothing");
            Assert.AreEqual(0, state.CountControlledNodes(PlayerIndex.Opponent));
            Assert.AreEqual(MatchResult.None, state.Result, "and nobody has won");
            Assert.IsNull(state.ResultReason);
        }

        [Test]
        public void NodeDomination_FiresFromTheSecondTurnOnceBothOpeningsAreBehind()
        {
            // The exemption is one turn, not a disabling. Three Nodes held at the first Upkeep of the *second*
            // round is exactly the race GDD 2.1.8 describes, and it has to resolve as written.
            var state = new MatchState(193);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));

            Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 0);
            Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 1);
            Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 2);

            Assert.AreEqual(MatchResult.None, state.Result, "three Nodes on turn 1 is not a win");

            // Hand over twice: the opponent's whole turn passes, and the Player's second Upkeep is the first
            // check allowed to fire.
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player)));
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Opponent)));

            Assert.AreEqual(2, state.TurnNumber);
            Assert.AreEqual(MatchResult.PlayerWin, state.Result, "and now domination resolves as the GDD words it");
            Assert.AreEqual("NODE DOMINATION", state.ResultReason);
        }

        [Test]
        public void OpeningLineup_NodeMultipliersStayUnresolvedWhileBothSidesHoldTheNodes()
        {
            // Node 1 grants +1 ATK and Node 2 +1 HP to a Core Active (GDD 2.1.7), and a full lineup fields both of
            // those Nodes against the opponent's, so at the open the Core reads exactly its printed numbers. The
            // buff is the reward for a trade that cleared the other seat's holder, which is what makes the two
            // Nodes worth fighting over instead of worth filling.
            var state = new MatchState(197);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            CompleteLineups(state);

            for (int i = 0; i < 2; i++)
            {
                PlayerIndex who = i == 0 ? PlayerIndex.Player : PlayerIndex.Opponent;
                CardInstance core = state.GetPlayer(who).Board.CoreActive[0].Card;
                Assert.AreEqual(0, core.NodeAtkBonus, $"{who}'s Core takes no Node 1 bonus while Node 1 is contested");
                Assert.AreEqual(0, core.NodeHpBonus, $"{who}'s Core takes no Node 2 bonus while Node 2 is contested");
                Assert.AreEqual(2, core.CurrentAtk, $"{who}'s Core Active reads its printed 2 ATK");
                Assert.AreEqual(2, core.CurrentHp);
            }
        }

        [Test]
        public void CompleteSetup_ResolvesTheNodesAPartialLineupLeftUncontested()
        {
            // The refresh inside CompleteSetup, justified under the contest rule. Declining a socket is legal, so a
            // seat can field one body and leave three Nodes open - uncontested, therefore immediately the opponent's,
            // and their Core Active has to be buffed before turn 1 rather than after the first death. Without the
            // refresh the card face and the number the resolver uses disagree across the whole opening turn, and the
            // only two things that ever recomputed them were a unit dying and a position switch.
            var state = new MatchState(199);
            var engine = new MatchEngine(state);
            state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));

            PlayerState shy = state.GetPlayer(PlayerIndex.Player);
            PlayerState full = state.GetPlayer(PlayerIndex.Opponent);
            int[] onlyCore = new int[ConfirmLineupCommand.RequiredLength];
            onlyCore[0] = shy.LineupOffer[0].InstanceId;

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Player, onlyCore)));
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new ConfirmLineupCommand(PlayerIndex.Opponent, Ids(full.LineupOffer.ToList()))));

            CardInstance core = full.Board.CoreActive[0].Card;
            Assert.IsNotNull(full.Board.NodeActive[0][0].Card, "the full seat fielded Node 1");
            Assert.IsNull(shy.Board.NodeActive[0][0].Card, "and the partial seat left it open");
            Assert.AreEqual(1, core.NodeAtkBonus, "so Node 1 is uncontested and its ATK bonus is live at the open");
            Assert.AreEqual(1, core.NodeHpBonus, "and so is Node 2's HP bonus");
            Assert.AreEqual(3, core.CurrentAtk, "a 2-ATK body reads as 3 the moment the match opens");
            Assert.AreEqual(3, core.CurrentHp);

            Assert.AreEqual(MatchResult.None, state.Result,
                "but three uncontested Nodes still do not win on turn 1 - the exemption covers the seat that " +
                "clicked a partial confirm rather than a player who earned the win");
        }

        // ------------------------------------------------------------------
        //  Seeded randomness
        // ------------------------------------------------------------------

        [Test]
        public void SameSeed_DealsTheIdenticalSetupAndOpeningHand()
        {
            // Instance ids come from a process-wide counter, so a replay is compared by definition id and order
            // - which is what a player would actually notice, and what netplay would have to agree on.
            List<CardDefinition> deck = DeckOf(cheapUnits(12));
            var a = Replay(deck);
            var b = Replay(deck);

            CollectionAssert.AreEqual(
                a.GetPlayer(PlayerIndex.Player).LineupOffer.Select(c => c.DefinitionId).ToList(),
                b.GetPlayer(PlayerIndex.Player).LineupOffer.Select(c => c.DefinitionId).ToList(),
                "the offer is reproducible");

            Assert.AreEqual(a.FirstPlayer, b.FirstPlayer, "and so is the coin");

            CompleteLineups(a);
            CompleteLineups(b);

            CollectionAssert.AreEqual(
                a.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.DefinitionId).ToList(),
                b.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.DefinitionId).ToList(),
                "and so is the opening hand");
        }

        [Test]
        public void DifferentSeeds_DoNotAllDealTheSameOpeningHand()
        {
            // The mirror image of the test above, and the one that would have caught the bug it fixes: before
            // this, "the mulliganed card is not back in hand" had been deleted because it was a one-in-twenty-six
            // coin flip against an unseeded shuffler. A seed that always produced the same deck would have made
            // the deterministic version equally useless, so determinism on its own is not the property.
            var hands = new List<string>();
            for (int seed = 0; seed < 8; seed++)
            {
                var state = new MatchState(seed);
                state.InitializeSetup(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
                CompleteLineups(state);
                hands.Add(string.Join(",", state.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.DefinitionId)));
            }

            Assert.Greater(hands.Distinct().Count(), 1, "eight seeds cannot all deal the same eight cards");
        }

        [Test]
        public void Mulligan_IsReproducibleForAGivenSeed()
        {
            // The legacy opening path, kept because GDD 2.1.3 still describes it. What seeding bought is the
            // ability to say what a mulligan *does* rather than that it usually changes something.
            List<CardDefinition> deck = DeckOf(cheapUnits(12));

            var first = new MatchState(97);
            first.InitializeMatch(deck, deck.ToList());
            var firstEngine = new MatchEngine(first);
            List<int> discarded = first.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.InstanceId).ToList();
            Assert.AreEqual(CommandResult.Success,
                firstEngine.ExecuteCommand(new MulliganCommand(PlayerIndex.Player, discarded.ToArray())));

            var second = new MatchState(97);
            second.InitializeMatch(deck, deck.ToList());
            var secondEngine = new MatchEngine(second);
            List<int> discardedAgain = second.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.InstanceId).ToList();
            Assert.AreEqual(CommandResult.Success,
                secondEngine.ExecuteCommand(new MulliganCommand(PlayerIndex.Player, discardedAgain.ToArray())));

            CollectionAssert.AreEqual(
                first.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.DefinitionId).ToList(),
                second.GetPlayer(PlayerIndex.Player).Hand.Select(c => c.DefinitionId).ToList(),
                "the same seed mulligans into the same hand");
        }

        // ------------------------------------------------------------------
        //  Retreat: the second thing an Active unit may do on a Combat step
        // ------------------------------------------------------------------

        [Test]
        public void Retreat_TradesPlacesForOneNPAndExhaustsBothHalvesOfTheExchange()
        {
            var state = new MatchState(101);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance active = Ready(state, PlayerIndex.Player, 3, 3, SlotType.CoreActive);
            CardInstance bench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);
            int npBefore = state.GetPlayer(PlayerIndex.Player).CurrentNP;

            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId)));

            Board board = state.GetPlayer(PlayerIndex.Player).Board;
            Assert.AreEqual(bench, board.CoreActive[0].Card, "the bench unit is in the fight");
            Assert.AreEqual(active, board.CoreBench[0].Card, "and the fighter is resting");
            Assert.AreEqual(UnitState.Exhausted, active.State, "the outgoing unit gave up its action");
            Assert.AreEqual(UnitState.Exhausted, bench.State,
                "and the incoming unit arrives tired, or Combat would be a free extra attacker for 1 NP");
            Assert.AreEqual(npBefore - MatchState.RetreatNP, state.GetPlayer(PlayerIndex.Player).CurrentNP);
        }

        [Test]
        public void Retreat_IsCombatPhaseOnly()
        {
            // The Main-phase move into an empty socket is a different rule with a different price and a different
            // effect on the units, and it is still SwitchPosition. Retreat is one of the two Combat options the
            // user asked for, so its gate is Combat and nothing else.
            foreach (TurnPhase phase in new[] { TurnPhase.Upkeep, TurnPhase.Draw, TurnPhase.Main,
                     TurnPhase.End, TurnPhase.Cleanup })
            {
                var state = new MatchState(103);
                var engine = new MatchEngine(state);
                state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
                state.CurrentPhase = phase;

                CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
                CardInstance bench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);

                Assert.AreEqual(CommandResult.InvalidPhase,
                    engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId)),
                    $"no retreating in {phase}");
            }
        }

        [Test]
        public void Retreat_RejectsAUnitThatAlreadyActedAndRefusesToShuffleTwice()
        {
            var state = new MatchState(107);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            CardInstance bench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);

            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId)));

            // The unit that came in is Exhausted, so it cannot retreat straight back out. The rule is
            // self-limiting without a counter of any kind.
            Assert.AreEqual(CommandResult.UnitCannotAct,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, bench.InstanceId, active.InstanceId)),
                "and no infinite shuffle");
        }

        [Test]
        public void Retreat_RejectsAFreshDeploymentOnEitherSide()
        {
            // GDD 2.1.5: Summoning Delay bars a unit from attacking or switching, and a retreat switches. It
            // bars the *incoming* unit too, or a freshly played body could be laundered into an Active socket
            // the turn it lands and swing immediately.
            var state = new MatchState(109);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance delayedOut = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            CardInstance delayedIn = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);
            delayedOut.HasSummoningDelay = true;
            Assert.AreEqual(CommandResult.UnitCannotAct,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, delayedOut.InstanceId, delayedIn.InstanceId)),
                "the outgoing unit is fresh");

            delayedOut.HasSummoningDelay = false;
            delayedIn.HasSummoningDelay = true;
            Assert.AreEqual(CommandResult.UnitCannotAct,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, delayedOut.InstanceId, delayedIn.InstanceId)),
                "the incoming unit is fresh");
        }

        [Test]
        public void Retreat_RejectsATargetThatIsNotBenchedAndNotOurs()
        {
            var state = new MatchState(113);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            CardInstance otherActive = Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 0);
            CardInstance theirs = Ready(state, PlayerIndex.Opponent, 2, 2, SlotType.CoreBench);

            Assert.AreEqual(CommandResult.InvalidTarget,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, otherActive.InstanceId)),
                "an Active is not a place to retreat to - that would be a move across the board wearing a disguise");
            Assert.AreEqual(CommandResult.InvalidTarget,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, theirs.InstanceId)),
                "and nobody's Bench unit is a place to retreat to");
            Assert.AreEqual(CommandResult.InvalidTarget,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, theirs.InstanceId, active.InstanceId)),
                "nor is somebody else's Active the thing being rested");
        }

        [Test]
        public void Retreat_RefusesToBeFreeWhenThePlayerCannotPay()
        {
            var state = new MatchState(127);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;
            state.GetPlayer(PlayerIndex.Player).CurrentNP = MatchState.RetreatNP - 1;

            CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            CardInstance bench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);

            Assert.AreEqual(CommandResult.InsufficientNP,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId)));
            Assert.AreEqual(active, state.GetPlayer(PlayerIndex.Player).Board.CoreActive[0].Card,
                "and a refused retreat leaves the board exactly as it was");
        }

        [Test]
        public void Retreat_AcrossBattlefieldsRefillsTheNodeItVacatedBecauseRetreatIsASwap()
        {
            // Legal across battlefields, and under the contest rule it is also the answer to a question retreat
            // would otherwise raise badly: a Node you are being paid for is one you must keep a body on, so moving
            // its holder off would hand the Node - and its NP and Core bonus - straight to the opponent. Retreat is
            // a SWAP, so the incoming bench body takes up the vacated socket and the payment does not stop.
            // Occupation is what Board.HoldsNode answers and control is MatchState.ControlsNode reducing it
            // against the other board; both are asserted here because the swap has to survive both readings.
            var state = new MatchState(131);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance nodeHolder = Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 0);
            CardInstance coreBench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);
            Board board = state.GetPlayer(PlayerIndex.Player).Board;
            Assert.IsTrue(state.ControlsNode(PlayerIndex.Player, 0),
                "Node 1 starts ours - held, and the opponent fields nothing on it");

            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, nodeHolder.InstanceId, coreBench.InstanceId)));

            Assert.IsTrue(board.HoldsNode(0, PlayerIndex.Player),
                "the Node is still occupied, because the body that came in is standing on it");
            Assert.IsTrue(state.ControlsNode(PlayerIndex.Player, 0),
                "and still ours, so a retreat never pays the opponent a Node for free");
            Assert.AreEqual(coreBench, board.NodeActive[0][0].Card, "and it is the bench unit holding it now");
            Assert.AreEqual(nodeHolder, board.CoreBench[0].Card, "while the Node's former holder rests on the Core Bench");
            Assert.AreEqual(UnitState.Exhausted, nodeHolder.State, "both units gave up the action, as a retreat requires");
            Assert.AreEqual(UnitState.Exhausted, coreBench.State);
            Assert.IsTrue(board.CoreActive[0].IsFree(), "the Core Active was not quietly filled by the exchange");
        }

        [Test]
        public void Retreat_IntoTheCoreActiveCarriesTheNodeBonusesWithTheBody()
        {
            // The RefreshNodeMultipliers call at the end of ExecuteRetreat exists for exactly this case, and
            // without it the swap would be invisible to the stat bookkeeping: Node 1's +1 ATK and Node 2's +1 HP
            // are folded into whichever unit is standing in the Core Active, so a body that retreats into it has
            // to arrive buffed and the one that leaves has to arrive stripped. A player reads the card face, so
            // numbers that lag the exchange by one refresh are numbers that lie.
            var state = new MatchState(139);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance sitter = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            CardInstance incoming = Ready(state, PlayerIndex.Player, 3, 3, SlotType.CoreBench);
            Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 0);
            Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 1);
            state.RefreshNodeMultipliers();

            Assert.AreEqual(3, sitter.CurrentAtk, "Node 1 feeds the Core Active's ATK");
            Assert.AreEqual(3, sitter.CurrentHp, "Node 2 feeds its HP");
            Assert.AreEqual(3, incoming.CurrentAtk, "and a benched unit holds neither bonus");
            Assert.AreEqual(3, incoming.CurrentHp);

            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, sitter.InstanceId, incoming.InstanceId)));

            Assert.AreEqual(4, incoming.CurrentAtk, "the body that stepped in picked up both bonuses");
            Assert.AreEqual(4, incoming.CurrentHp);
            Assert.AreEqual(2, sitter.CurrentAtk, "and the one that stepped out put them down");
            Assert.AreEqual(2, sitter.CurrentHp);
            Assert.AreEqual(0, sitter.NodeAtkBonus + sitter.NodeHpBonus,
                "the leaving unit carries no phantom bonus, or losing the Node later would under-count");
        }

        [Test]
        public void Retreat_KillsNothingAndPromotesNothing()
        {
            var state = new MatchState(137);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance active = Ready(state, PlayerIndex.Player, 9, 9, SlotType.CoreActive);
            CardInstance bench = Ready(state, PlayerIndex.Player, 1, 1, SlotType.CoreBench);
            int boardBefore = state.GetPlayer(PlayerIndex.Player).Board.GetAllUnits(PlayerIndex.Player).Count;

            engine.ExecuteCommand(new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId));

            Assert.AreEqual(boardBefore, state.GetPlayer(PlayerIndex.Player).Board.GetAllUnits(PlayerIndex.Player).Count,
                "an exchange moves two cards and discards neither");
            Assert.AreEqual(MatchResult.None, state.Result);
        }

        // ------------------------------------------------------------------
        //  [Activate] in Combat, and the gate the click layer used to get wrong
        // ------------------------------------------------------------------

        [Test]
        public void ActivateAbility_FiresItsEffectExhaustsTheUnitAndIsCombatOnly()
        {
            var state = new MatchState(139);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));

            CardInstance card = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive,
                keywords: new[] { Keyword.Activate },
                effects: new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 2) });

            state.CurrentPhase = TurnPhase.Main;
            Assert.AreEqual(CommandResult.InvalidPhase,
                engine.ExecuteCommand(new ActivateAbilityCommand(PlayerIndex.Player, card.InstanceId)),
                "an ability is a Combat-step action, not a main-phase one");

            int npBefore = state.GetPlayer(PlayerIndex.Player).CurrentNP;
            state.CurrentPhase = TurnPhase.Combat;
            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new ActivateAbilityCommand(PlayerIndex.Player, card.InstanceId)));

            Assert.AreEqual(npBefore + 2, state.GetPlayer(PlayerIndex.Player).CurrentNP, "the effect resolved");
            Assert.AreEqual(UnitState.Exhausted, card.State, "and it cost the unit its action");
        }

        [Test]
        public void ActivateAbility_RejectsAUnitWithNoAbilityToActivate()
        {
            var state = new MatchState(149);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance plain = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            Assert.AreEqual(CommandResult.InvalidCommand,
                engine.ExecuteCommand(new ActivateAbilityCommand(PlayerIndex.Player, plain.InstanceId)),
                "no [Activate] keyword");

            CardInstance hollow = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive,
                keywords: new[] { Keyword.Activate });
            Assert.AreEqual(CommandResult.InvalidCommand,
                engine.ExecuteCommand(new ActivateAbilityCommand(PlayerIndex.Player, hollow.InstanceId)),
                "and [Activate] with no effects is a keyword with nothing behind it");
        }

        [Test]
        public void CanActivate_RefusesABenchedUnitThatTheEngineWouldAlsoRefuse()
        {
            // The bug this test exists for: the coordinator used to answer "can I activate" by reading the card -
            // Unit, [Activate], Ready - and never asked which socket it stood in. The engine resolves the id
            // through FindActiveCardInstance, so a benched [Activate] unit lights up as activatable and then
            // fails with UnitCannotAct. A button that lies about its own legality is worse than no button.
            var state = new MatchState(151);
            var engine = new MatchEngine(state);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance benched = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench,
                keywords: new[] { Keyword.Activate },
                effects: new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 1) });

            Assert.IsFalse(CombatLegality.CanActivate(state, PlayerIndex.Player, benched),
                "the legality layer says no");
            Assert.AreEqual(CommandResult.UnitCannotAct,
                engine.ExecuteCommand(new ActivateAbilityCommand(PlayerIndex.Player, benched.InstanceId)),
                "and the engine agrees, which is the whole point of asking the legality layer");

            CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive,
                keywords: new[] { Keyword.Activate },
                effects: new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 1) });
            Assert.IsTrue(CombatLegality.CanActivate(state, PlayerIndex.Player, active),
                "the same card in an Active socket is a real offer");
        }

        [Test]
        public void ActivatableUnits_ListsWhatCanBeActivatedAndNothingElse()
        {
            var state = new MatchState(157);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;

            CardInstance able = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive,
                keywords: new[] { Keyword.Activate },
                effects: new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 1) });
            CardInstance tired = Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 1,
                keywords: new[] { Keyword.Activate },
                effects: new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 1) });
            tired.Exhaust();
            CardInstance benchAbility = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench,
                keywords: new[] { Keyword.Activate },
                effects: new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 1) });
            Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeActive, node: 3);   // no [Activate] at all

            List<CardInstance> list = CombatLegality.ActivatableUnits(state, PlayerIndex.Player);
            CollectionAssert.AreEqual(new[] { able }, list,
                "one Ready Active with something to fire; the exhausted unit, the benched unit and the plain unit are not offers");

            Assert.AreEqual(0, CombatLegality.ActivatableUnits(state, PlayerIndex.Opponent).Count,
                "and it is the active player's question to ask");
        }

        [Test]
        public void CombatLegality_And_TheEngineAgreeOnEveryRetreatCase()
        {
            // The mirror test. Each row states which way the answer should go, so the two halves cannot pass by
            // agreeing to be wrong together.
            foreach (var testCase in RetreatCases())
            {
                var state = new MatchState(163);
                var engine = new MatchEngine(state);
                state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
                state.CurrentPhase = testCase.Phase;
                state.GetPlayer(PlayerIndex.Player).CurrentNP = testCase.NP;

                CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, testCase.ActiveSlot,
                    testCase.ActiveIndex, testCase.ActiveNode);
                CardInstance bench = Ready(state, PlayerIndex.Player, 2, 2, testCase.BenchSlot,
                    testCase.BenchIndex, testCase.BenchNode);
                active.State = testCase.ActiveState;
                bench.HasSummoningDelay = testCase.BenchDelayed;

                bool legal = CombatLegality.CanRetreat(state, PlayerIndex.Player, active, bench);
                CommandResult result = engine.ExecuteCommand(
                    new RetreatCommand(PlayerIndex.Player, active.InstanceId, bench.InstanceId));

                Assert.AreEqual(testCase.Expected, legal, $"{testCase.Name}: legality layer");
                Assert.AreEqual(testCase.Expected, result == CommandResult.Success,
                    $"{testCase.Name}: engine verdict was {result}, and the two must not disagree - " +
                    "a rail that lights a button the engine refuses teaches the player to ignore every light");
            }
        }

        [Test]
        public void RetreatChoices_ListsEveryBenchUnitTheActiveCanTradeFor()
        {
            var state = new MatchState(167);
            state.InitializeMatch(DeckOf(cheapUnits(12)), DeckOf(cheapUnits(12)));
            state.CurrentPhase = TurnPhase.Combat;
            state.GetPlayer(PlayerIndex.Player).CurrentNP = MatchState.RetreatNP;

            CardInstance active = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreActive);
            CardInstance coreBench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench);
            CardInstance nodeBench = Ready(state, PlayerIndex.Player, 2, 2, SlotType.NodeBench, node: 2);
            CardInstance fresh = Ready(state, PlayerIndex.Player, 2, 2, SlotType.CoreBench, index: 2);
            fresh.HasSummoningDelay = true;
            Ready(state, PlayerIndex.Opponent, 2, 2, SlotType.CoreBench);

            List<CardInstance> choices = CombatLegality.RetreatChoices(state, PlayerIndex.Player, active);
            CollectionAssert.AreEquivalent(new[] { coreBench, nodeBench }, choices,
                "both battlefields' benches are in play, the delayed one is not, and the opponent's never is");

            state.GetPlayer(PlayerIndex.Player).CurrentNP = 0;
            Assert.AreEqual(0, CombatLegality.RetreatChoices(state, PlayerIndex.Player, active).Count,
                "and with 0 NP there is no retreat to offer at all");
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>One row of the retreat mirror table.</summary>
        sealed class RetreatCase
        {
            public string Name;
            public TurnPhase Phase = TurnPhase.Combat;
            public int NP = MatchState.RetreatNP;
            public SlotType ActiveSlot = SlotType.CoreActive;
            public int ActiveNode = -1;
            public int ActiveIndex;
            public SlotType BenchSlot = SlotType.CoreBench;
            public int BenchNode = -1;

            /// <summary>
            /// Distinct from <see cref="ActiveIndex"/> wherever both halves land in the same socket *kind*.
            /// A case that put two cards in one slot would quietly overwrite the first, and the test would pass
            /// for a reason nothing to do with the rule it is probing.
            /// </summary>
            public int BenchIndex;

            public UnitState ActiveState = UnitState.Ready;
            public bool BenchDelayed;
            public bool Expected = true;
        }

        static List<RetreatCase> RetreatCases()
        {
            return new List<RetreatCase>
            {
                new RetreatCase { Name = "a plain Combat retreat" },
                new RetreatCase { Name = "not in Combat", Phase = TurnPhase.Main, Expected = false },
                new RetreatCase { Name = "no NP", NP = 0, Expected = false },
                new RetreatCase { Name = "one NP short of the price", NP = MatchState.RetreatNP - 1, Expected = false },
                new RetreatCase { Name = "the Active already swung", ActiveState = UnitState.Exhausted, Expected = false },
                new RetreatCase { Name = "the Active is destroyed", ActiveState = UnitState.Destroyed, Expected = false },
                new RetreatCase { Name = "the incoming unit is fresh", BenchDelayed = true, Expected = false },
                new RetreatCase { Name = "Node Active to its own Node's bench", BenchSlot = SlotType.NodeBench, BenchNode = 1 },
                new RetreatCase { Name = "Node Active to the Core bench", BenchSlot = SlotType.CoreBench, ActiveSlot = SlotType.NodeActive, ActiveNode = 2 },
                new RetreatCase { Name = "two Actives is a move, not a retreat", BenchSlot = SlotType.NodeActive, BenchNode = 0, Expected = false },
                new RetreatCase { Name = "two benches is not a retreat either", ActiveSlot = SlotType.CoreBench, BenchIndex = 1, Expected = false },
            };
        }

        static MatchState Replay(List<CardDefinition> deck)
        {
            var state = new MatchState(211);
            state.InitializeSetup(deck, deck.ToList());
            return state;
        }

        /// <summary>
        /// Fields whatever each player was offered, so a test can get to a live match. The command is the only
        /// door: CompleteSetup is internal precisely so that no caller - including a test - can stand up a match
        /// on an unconfirmed lineup.
        /// </summary>
        static void CompleteLineups(MatchState state)
        {
            var engine = new MatchEngine(state);
            foreach (PlayerIndex who in new[] { PlayerIndex.Player, PlayerIndex.Opponent })
            {
                List<CardInstance> offer = state.GetPlayer(who).LineupOffer.ToList();
                engine.ExecuteCommand(new ConfirmLineupCommand(who, Ids(offer)));
            }
        }

        static int[] Ids(List<CardInstance> cards)
        {
            return cards.Select(c => c.InstanceId).ToArray();
        }

        /// <summary>
        /// Puts a live, Ready unit of the requested shape into the requested socket. Setup and the board are
        /// hand-built rather than played through, because these tests are about one rule each and a helper that
        /// drags NP costs, summoning and the phase ladder into every one of them hides which check failed.
        /// </summary>
        static CardInstance Ready(MatchState state, PlayerIndex who, int atk, int hp, SlotType slotType,
            int index = 0, int node = -1, Keyword[] keywords = null, EffectDescriptor[] effects = null)
        {
            var definition = Unit("probe", 1, atk, hp, keywords ?? System.Array.Empty<Keyword>(), effects);
            var card = new CardInstance(definition, who);
            card.State = UnitState.Ready;
            card.HasSummoningDelay = false;

            Board board = state.GetPlayer(who).Board;
            BoardSlot slot = board.GetSlot(slotType, index, node);
            Assert.IsNotNull(slot, $"no such socket: {slotType}[{index}] node {node}");
            slot.Card = card;
            return card;
        }

        static List<CardDefinition> DeckOf(List<CardDefinition> cards) => cards;

        static List<CardDefinition> cheapUnits(int count)
        {
            var list = new List<CardDefinition>();
            for (int i = 0; i < count; i++)
            {
                list.Add(Unit($"unit_{i}", 1));
            }

            return list;
        }

        static CardDefinition Unit(string id, int cost = 1, int atk = 2, int hp = 2,
            Keyword[] keywords = null, EffectDescriptor[] effects = null)
        {
            return new CardDefinition
            {
                Id = id,
                Name = id,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = cost,
                Atk = atk,
                Hp = hp,
                Keywords = keywords ?? System.Array.Empty<Keyword>(),
                Effects = effects ?? System.Array.Empty<EffectDescriptor>()
            };
        }

        static CardDefinition Ability(string id, int cost)
        {
            return new CardDefinition
            {
                Id = id,
                Name = id,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Ability,
                Cost = cost,
                Atk = 0,
                Hp = 0
            };
        }
    }
}
