using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    /// <summary>
    /// Central match state. Phase order, NP economy, victory conditions, Node control and the Core
    /// multipliers follow GDD 2.1.1-2.1.8.
    /// </summary>
    public sealed class MatchState
    {
        public const int StartingHandSize = 5;

        /// <summary>
        /// The opening lineup offer is exactly this many cards, which is also the number of Active sockets on
        /// a board (the Core Active plus one per Node). Fielding all five is therefore the whole offer, and
        /// the reason a match that opens through setup never has an empty Active to begin with.
        /// </summary>
        public const int LineupOfferSize = 5;

        /// <summary>
        /// The most expensive Unit the opening offer may contain. A ceiling rather than a filter: it keeps a
        /// player's first turn from being decided by whether they happened to draw the four-cost bodies, and
        /// it is why the offer is searched for rather than merely taken off the top of the deck.
        /// </summary>
        public const int LineupMaxCost = 3;

        /// <summary>NP price of the first lineup reroll; each one already taken adds to the next.</summary>
        public const int LineupRerollBaseCost = 2;

        /// <summary>NP price of a Combat-phase retreat, paid by the unit's controller.</summary>
        public const int RetreatNP = 1;

        /// <summary>
        /// Extra NP for the player who loses the coin flip. The compensation for going second in a game where
        /// the first player sets the tempo, and the reason the flip is worth automating rather than ignoring.
        /// </summary>
        public const int SecondPlayerNPBonus = 1;

        public PlayerState[] Players { get; }
        public PlayerIndex ActivePlayer { get; private set; }

        public TurnPhase CurrentPhase { get; set; }
        public int TurnNumber { get; private set; }
        public MatchResult Result { get; private set; }

        /// <summary>
        /// True between <see cref="InitializeSetup"/> and <see cref="CompleteSetup"/>. While it holds, the
        /// only commands the engine honours are the two setup commands and a concession: no card may be
        /// played, no phase passed and no turn ended, because the board the rules would be applied to does
        /// not exist yet. A match started through <see cref="InitializeMatch"/> never enters it.
        /// </summary>
        public bool SetupInProgress { get; private set; }

        /// <summary>
        /// True once the coin has been tossed during setup. Flipping twice would not be a formality — it
        /// would silently re-decide who moves first after one player has already fielded a lineup.
        /// </summary>
        public bool CoinFlipped { get; private set; }

        /// <summary>
        /// The coin winner, i.e. the player whose Upkeep opens the match. Holds
        /// <see cref="PlayerIndex.Player"/> until a flip happens, which is also what a match started through
        /// <see cref="InitializeMatch"/> uses, so the legacy path is unchanged.
        /// </summary>
        public PlayerIndex FirstPlayer { get; private set; }

        /// <summary>
        /// The seed behind every shuffle and the coin flip, or null when the match draws its randomness from
        /// the environment. Surfaced so a console log can reproduce a match exactly; nothing in the rules
        /// reads it.
        /// </summary>
        public int? Seed { get; }

        /// <summary>
        /// The single random stream both players' shuffles and the coin flip draw from. One stream rather
        /// than one per player so that a replay depends only on the seed and the order operations happened
        /// in, not on which player object asked first.
        /// </summary>
        internal Random Rng { get; }

        /// <summary>
        /// Human-readable cause behind <see cref="Result"/> ("CORE DESTRUCTION", "DECK-OUT",
        /// "CONCESSION", "NODE DOMINATION", "BOTH CORES DESTROYED"), null while the match is live.
        /// Kept separate from <see cref="MatchResult"/> so the banner can say *why* the match ended.
        /// </summary>
        public string ResultReason { get; private set; }
        public List<string> EventLog { get; }

        public MatchState(int? seed = null)
        {
            Seed = seed;
            Rng = seed.HasValue ? new Random(seed.Value) : new Random();
            Players = new PlayerState[2];
            Players[0] = new PlayerState(PlayerIndex.Player, Rng);
            Players[1] = new PlayerState(PlayerIndex.Opponent, Rng);
            ActivePlayer = PlayerIndex.Player;
            FirstPlayer = PlayerIndex.Player;
            CurrentPhase = TurnPhase.Upkeep;
            TurnNumber = 1;
            Result = MatchResult.None;
            EventLog = new List<string>();
        }

        public PlayerState GetPlayer(PlayerIndex index)
        {
            return Players[(int)index];
        }

        public PlayerState GetOpponent(PlayerIndex index)
        {
            return Players[1 - (int)index];
        }

        public bool IsMatchOver()
        {
            return Result != MatchResult.None;
        }

        /// <summary>
        /// GDD 2.1.8: a player wins when the opponent's Core reaches 0. If both Cores are destroyed in
        /// the same atomic batch, the active player (whose turn it is) wins rather than a draw.
        /// </summary>
        public void CheckVictoryConditions()
        {
            if (IsMatchOver())
            {
                return;
            }

            bool playerDefeated = Players[0].IsDefeated();
            bool opponentDefeated = Players[1].IsDefeated();

            if (playerDefeated && opponentDefeated)
            {
                Result = ActivePlayer == PlayerIndex.Player ? MatchResult.PlayerWin : MatchResult.OpponentWin;
                ResultReason = "BOTH CORES DESTROYED";
                LogEvent($"Both Cores destroyed - active player ({ActivePlayer}) wins");
            }
            else if (opponentDefeated)
            {
                Result = MatchResult.PlayerWin;
                ResultReason = "CORE DESTRUCTION";
                LogEvent("Player wins by Core Destruction");
            }
            else if (playerDefeated)
            {
                Result = MatchResult.OpponentWin;
                ResultReason = "CORE DESTRUCTION";
                LogEvent("Opponent wins by Core Destruction");
            }
        }

        /// <summary>
        /// GDD 2.1.8 condition 4 (Concession): the conceding player loses immediately, whatever the turn
        /// or phase. This is the only victory path with no active-player tiebreak — the GDD's batch rule
        /// ("if both players satisfy a condition in that same batch, the active player wins") covers only
        /// conditions a board state can satisfy simultaneously, and a concession is a single player's
        /// decision, so it can never be satisfied by both sides at once.
        /// </summary>
        public void Concede(PlayerIndex player)
        {
            if (IsMatchOver())
            {
                return;
            }

            Result = player == PlayerIndex.Player ? MatchResult.OpponentWin : MatchResult.PlayerWin;
            ResultReason = player == PlayerIndex.Player ? "YOU CONCEDED" : "OPPONENT CONCEDED";
            LogEvent($"{player} concedes the match - opponent wins by Concession");
        }

        /// <summary>
        /// GDD 2.1.8 Node Domination: controlling 3 of the 4 Nodes at the start of your Upkeep wins the
        /// game immediately.
        /// <para>
        /// Except on the opening turn, and the reason is the opening lineup. A match that opens through setup
        /// fields all five Active sockets, which is <em>four Nodes held by both players</em> before a single
        /// card has been played. The condition as literally worded would therefore end the match at its first
        /// Upkeep and award it to whoever won a coin flip, which is not a victory condition, it is a coin flip
        /// with extra steps. Domination is a race about <em>keeping</em> Nodes after the opponent has been made
        /// to lose them; at 4-4 with nothing contested it has not started, so turn 1 is exempt - exactly as turn
        /// 1 already exempts itself from Draw, on the same logic that the opening is not a normal turn.
        /// </para>
        /// <para>
        /// Both players' opening Upkeeps are TurnNumber 1, because the counter advances only when control comes
        /// back to the first player, so this one guard covers the second player's opening as well. The first
        /// check that can fire is the first player's second Upkeep, by which point both have had one turn.
        /// </para>
        /// <para>
        /// The guard is total rather than scoped to setup-opened matches, because a match opened by
        /// <see cref="InitializeMatch"/> fields nothing at all and so could never dominate on turn 1 anyway -
        /// a rule with two shapes is one more way for the two paths to drift apart.
        /// </para>
        /// </summary>
        private void CheckNodeDomination()
        {
            if (IsMatchOver())
            {
                return;
            }

            if (TurnNumber <= 1)
            {
                return;
            }

            int controlled = GetPlayer(ActivePlayer).Board.CountControlledNodes(ActivePlayer);
            if (controlled >= Board.NodesForDomination)
            {
                Result = ActivePlayer == PlayerIndex.Player ? MatchResult.PlayerWin : MatchResult.OpponentWin;
                ResultReason = "NODE DOMINATION";
                LogEvent($"{ActivePlayer} wins by Node Domination ({controlled}/{Board.NodeCount} Nodes)");
            }
        }

        /// <summary>
        /// Removes every destroyed card into its owner's discard, promotes a Bench unit into each emptied
        /// Active slot, then recomputes the Node Core multipliers and re-checks victory.
        /// <para>
        /// Node control is never assigned: it stays derived from Active occupation, so removing a dead
        /// Active unit already loses the Node before promotion fills the hole.
        /// </para>
        /// </summary>
        public void ResolveDestructionAndPromotion()
        {
            for (int i = 0; i < Players.Length; i++)
            {
                RemoveDestroyedCards(Players[i]);
            }

            for (int i = 0; i < Players.Length; i++)
            {
                PromoteCoreBench(Players[i]);
            }

            for (int i = 0; i < Players.Length; i++)
            {
                PromoteNodeBenches(Players[i]);
            }

            // Node control can shift as Actives die or are promoted, so the Core multipliers (which key
            // off control) must be recomputed after the board settles.
            for (int i = 0; i < Players.Length; i++)
            {
                ApplyNodeMultipliers(Players[i]);
            }

            CheckVictoryConditions();
        }

        private void RemoveDestroyedCards(PlayerState player)
        {
            Board board = player.Board;

            for (int i = 0; i < board.CoreActive.Length; i++)
            {
                CollectDeadSlot(player, board.CoreActive[i]);
            }

            for (int i = 0; i < board.CoreBench.Length; i++)
            {
                CollectDeadSlot(player, board.CoreBench[i]);
            }

            for (int node = 0; node < Board.NodeCount; node++)
            {
                for (int i = 0; i < board.NodeActive[node].Length; i++)
                {
                    CollectDeadSlot(player, board.NodeActive[node][i]);
                }

                for (int i = 0; i < board.NodeBench[node].Length; i++)
                {
                    CollectDeadSlot(player, board.NodeBench[node][i]);
                }
            }

            if (board.Protocol != null && !board.Protocol.IsAlive())
            {
                player.DiscardCard(board.Protocol);
                LogEvent($"{player.Index} Protocol {board.Protocol.Definition.Name} is destroyed");
                board.Protocol = null;
            }
        }

        private void CollectDeadSlot(PlayerState player, BoardSlot slot)
        {
            if (slot.Card == null || slot.Card.IsAlive())
            {
                return;
            }

            CardInstance dead = slot.Card;
            LogEvent($"{dead.Definition.Name} is destroyed");

            // GDD 2.1.7: each Core Active destroyed (by combat or an effect) costs its owner exactly 1
            // Core durability, separately from any direct-Core or damage-transfer loss.
            if (slot.Type == SlotType.CoreActive && dead.Definition.Type == CardType.Unit)
            {
                player.TakeDamage(1);
                LogEvent($"{player.Index} loses 1 Core durability (Core Active destroyed)");
            }

            player.DiscardCard(dead);
            slot.Card = null;
        }

        private void PromoteCoreBench(PlayerState player)
        {
            Board board = player.Board;

            for (int i = 0; i < board.CoreActive.Length; i++)
            {
                if (board.CoreActive[i].IsOccupied())
                {
                    continue;
                }

                for (int b = 0; b < board.CoreBench.Length; b++)
                {
                    if (!board.CoreBench[b].IsOccupied())
                    {
                        continue;
                    }

                    CardInstance promoted = board.CoreBench[b].Card;
                    board.CoreBench[b].Card = null;
                    board.CoreActive[i].Card = promoted;
                    LogEvent($"{player.Index} promotes {promoted.Definition.Name} from Core Bench to Core Active {i}");
                    break;
                }
            }
        }

        private void PromoteNodeBenches(PlayerState player)
        {
            Board board = player.Board;

            for (int node = 0; node < Board.NodeCount; node++)
            {
                if (board.NodeActive[node][0].IsOccupied())
                {
                    continue;
                }

                for (int b = 0; b < board.NodeBench[node].Length; b++)
                {
                    if (!board.NodeBench[node][b].IsOccupied())
                    {
                        continue;
                    }

                    CardInstance promoted = board.NodeBench[node][b].Card;
                    board.NodeBench[node][b].Card = null;
                    board.NodeActive[node][0].Card = promoted;
                    LogEvent($"{player.Index} promotes {promoted.Definition.Name} to Node {node + 1} Active");
                    break;
                }
            }
        }

        /// <summary>
        /// GDD 2.1.7 Core Multipliers: a controlled Node 1 grants +1 ATK and a controlled Node 2 grants
        /// +1 HP to that player's Core Active Units. Both are folded into the unit's stats here and
        /// removed immediately if the Node is lost. Node 3 (First Strike) and Node 4 (draw on kill) are
        /// query-time bonuses resolved by the engine, so they need no stat bookkeeping.
        /// </summary>
        private void ApplyNodeMultipliers(PlayerState player)
        {
            Board board = player.Board;
            bool nodeAtk = board.IsNodeControlled(0, player.Index);
            bool nodeHp = board.IsNodeControlled(1, player.Index);

            foreach (CardInstance card in board.GetAllUnits(player.Index))
            {
                if (card.Definition.Type != CardType.Unit)
                {
                    continue;
                }

                BoardSlot slot = board.FindSlot(card);
                bool isCoreActive = slot != null && slot.Type == SlotType.CoreActive;

                int wantAtk = isCoreActive && nodeAtk ? 1 : 0;
                int atkDelta = wantAtk - card.NodeAtkBonus;
                if (atkDelta != 0)
                {
                    card.CurrentAtk = Math.Max(0, card.CurrentAtk + atkDelta);
                    card.NodeAtkBonus = wantAtk;
                }

                int wantHp = isCoreActive && nodeHp ? 1 : 0;
                int hpDelta = wantHp - card.NodeHpBonus;
                if (hpDelta != 0)
                {
                    card.CurrentHp = Math.Max(0, card.CurrentHp + hpDelta);
                    card.NodeHpBonus = wantHp;
                    if (card.CurrentHp == 0)
                    {
                        card.State = UnitState.Destroyed;
                    }
                }
            }
        }

        /// <summary>
        /// GDD 2.1.7: when a Core Active with no Shield points takes damage, 10% of it (rounded up per
        /// instance) transfers to its controller's Core as durability loss. Only Core Actives transfer;
        /// Actives at a Node do not.
        /// </summary>
        public void ApplyCoreDamageTransfer(CardInstance unit, int damage, int shieldBefore)
        {
            if (damage <= 0 || unit == null || unit.Definition.Type != CardType.Unit)
            {
                return;
            }

            PlayerState owner = GetPlayer(unit.Owner);
            BoardSlot slot = owner.Board.FindSlot(unit);
            if (slot == null || slot.Type != SlotType.CoreActive)
            {
                return;
            }

            // A Core Active that still had Shield standing when the damage landed does not transfer.
            if (shieldBefore > 0)
            {
                return;
            }

            int transfer = (damage + 9) / 10;
            if (transfer <= 0)
            {
                return;
            }

            owner.TakeDamage(transfer);
            LogEvent($"{unit.Definition.Name} transfers {transfer} Core durability ({damage} damage, no Shield)");
        }

        /// <summary>
        /// Recomputes the Node 1/2 Core multipliers for both players without resolving destruction or
        /// promotion. Used after a position switch, which changes which units are Core Actives but never
        /// destroys or promotes anything.
        /// </summary>
        public void RefreshNodeMultipliers()
        {
            for (int i = 0; i < Players.Length; i++)
            {
                ApplyNodeMultipliers(Players[i]);
            }
        }

        public void AdvancePhase()
        {
            if (IsMatchOver())
            {
                return;
            }

            switch (CurrentPhase)
            {
                case TurnPhase.Upkeep:
                    CurrentPhase = TurnPhase.Draw;
                    OnDrawPhase();
                    break;

                case TurnPhase.Draw:
                    CurrentPhase = TurnPhase.Main;
                    LogEvent($"{ActivePlayer} Main Phase");
                    break;

                case TurnPhase.Main:
                    CurrentPhase = TurnPhase.Combat;
                    LogEvent($"{ActivePlayer} Combat Phase");
                    break;

                case TurnPhase.Combat:
                    CurrentPhase = TurnPhase.End;
                    OnEndPhase();
                    break;

                case TurnPhase.End:
                    CurrentPhase = TurnPhase.Cleanup;
                    OnCleanupPhase();
                    break;

                case TurnPhase.Cleanup:
                    SwitchActivePlayer();
                    break;
            }

            CheckVictoryConditions();
        }

        private void SwitchActivePlayer()
        {
            if (ActivePlayer == PlayerIndex.Player)
            {
                ActivePlayer = PlayerIndex.Opponent;
            }
            else
            {
                ActivePlayer = PlayerIndex.Player;
                TurnNumber++;
            }

            CurrentPhase = TurnPhase.Upkeep;
            LogEvent($"Turn {TurnNumber} - {ActivePlayer} Upkeep Phase");
            OnUpkeepPhase();
        }

        /// <summary>
        /// GDD 2.1.2 Phase 1 Upkeep: ready the acting player's units (clearing Summoning Delay), gain NP
        /// (+3 base, +1 per controlled Node) up to the 10 + Nodes cap, then check Node Domination.
        /// </summary>
        private void OnUpkeepPhase()
        {
            PlayerState player = GetPlayer(ActivePlayer);

            foreach (CardInstance unit in player.Board.GetAllUnits(ActivePlayer))
            {
                unit.SetReady();
                unit.CannotBeBlocked = false;
            }

            int controlledNodes = player.Board.CountControlledNodes(ActivePlayer);
            int cap = PlayerState.NpCap(controlledNodes);
            player.GainNP(3 + controlledNodes, cap);
            LogEvent($"{ActivePlayer} gains {3 + controlledNodes} NP (3 base + {controlledNodes} Nodes), now {player.CurrentNP}/{cap}");

            CheckNodeDomination();
        }

        /// <summary>
        /// GDD 2.1.2 Phase 2 Draw: draw 1 card. Deck-Out (GDD 2.1.8) is a loss only when the player
        /// cannot draw during Draw <b>and</b> holds no cards in hand.
        /// </summary>
        private void OnDrawPhase()
        {
            PlayerState player = GetPlayer(ActivePlayer);
            LogEvent($"Turn {TurnNumber} - {ActivePlayer} Draw Phase");

            // The opening hand already covers the first round, so the very first turn skips its draw.
            if (TurnNumber > 1)
            {
                CardInstance drawn = player.DrawCard();
                if (drawn != null)
                {
                    LogEvent($"{ActivePlayer} draws a card");
                }
                else
                {
                    LogEvent($"{ActivePlayer} cannot draw (deck exhausted)");
                    if (player.Hand.Count == 0)
                    {
                        Result = ActivePlayer == PlayerIndex.Player ? MatchResult.OpponentWin : MatchResult.PlayerWin;
                        ResultReason = "DECK-OUT";
                        LogEvent($"{ActivePlayer} loses by Deck-Out");
                    }
                }
            }
        }

        private void OnEndPhase()
        {
            PlayerState player = GetPlayer(ActivePlayer);
            LogEvent($"{ActivePlayer} End Phase - {player.CurrentNP} NP remaining");
        }

        private void OnCleanupPhase()
        {
            LogEvent($"{ActivePlayer} Cleanup Phase");
        }

        public void InitializeMatch(List<CardDefinition> playerDeck, List<CardDefinition> opponentDeck)
        {
            ValidateDecks(playerDeck, opponentDeck);

            LoadDeck(Players[0], playerDeck);
            LoadDeck(Players[1], opponentDeck);

            Players[0].ShuffleDeck();
            Players[1].ShuffleDeck();

            for (int i = 0; i < StartingHandSize; i++)
            {
                Players[0].DrawCard();
                Players[1].DrawCard();
            }

            LogEvent($"Match initialized - Both players draw {StartingHandSize} cards");

            // The match opens on the first player's Upkeep so every turn - including the first - runs the
            // same NP gain and Node Domination check.
            CurrentPhase = TurnPhase.Upkeep;
            LogEvent($"Turn {TurnNumber} - {ActivePlayer} Upkeep Phase");
            OnUpkeepPhase();
        }

        /// <summary>
        /// Opens a match through the lineup setup instead of dealing straight into a hand: both decks are
        /// loaded and shuffled, and each player is then offered <see cref="LineupOfferSize"/> Units costing
        /// at most <see cref="LineupMaxCost"/> to field directly into their Active sockets.
        /// <para>
        /// The offer is <em>searched</em> for, not taken off the top: a ceiling on cost only means something
        /// if the deck is rummaged through to find cards under it. Everything the search skips stays in the
        /// deck in its original order, so the opening hand drawn at <see cref="CompleteSetup"/> is the true
        /// top of a deck that has been disturbed as little as possible.
        /// </para>
        /// <para>
        /// Nothing runs the first Upkeep here, and no card is playable yet: setup has to close first, which
        /// it does when both players have confirmed a lineup. The caller's job between this and then is one
        /// <see cref="FlipCoin"/> and two <c>ConfirmLineupCommand</c>s.
        /// </para>
        /// </summary>
        public void InitializeSetup(List<CardDefinition> playerDeck, List<CardDefinition> opponentDeck)
        {
            ValidateDecks(playerDeck, opponentDeck);

            LoadDeck(Players[0], playerDeck);
            LoadDeck(Players[1], opponentDeck);

            Players[0].ShuffleDeck();
            Players[1].ShuffleDeck();

            DealLineupOffer(Players[0]);
            DealLineupOffer(Players[1]);

            SetupInProgress = true;
            TurnNumber = 1;
            CurrentPhase = TurnPhase.Upkeep;

            LogEvent($"Setup - {Players[0].Index} offered {Players[0].LineupOffer.Count} card(s), " +
                     $"{Players[1].Index} offered {Players[1].LineupOffer.Count} card(s)");
        }

        /// <summary>
        /// Decides who takes the first turn. Idempotent: a second call returns the same answer instead of
        /// tossing again, because by then a player may already have fielded a lineup against the result.
        /// <para>
        /// Only meaningful during setup. Outside it there is no first turn left to decide, and returning
        /// <see cref="PlayerIndex.Player"/> silently in that case would be a lie the banner would repeat to
        /// the player, so the call is refused with an exception instead.
        /// </para>
        /// </summary>
        public PlayerIndex FlipCoin()
        {
            if (!SetupInProgress)
            {
                throw new InvalidOperationException(
                    "The coin is only flipped while the opening lineup setup is in progress.");
            }

            if (CoinFlipped)
            {
                return FirstPlayer;
            }

            bool heads = Rng.Next(2) == 0;
            FirstPlayer = heads ? PlayerIndex.Player : PlayerIndex.Opponent;
            ActivePlayer = FirstPlayer;
            CoinFlipped = true;

            LogEvent($"Coin flip: {(heads ? "heads" : "tails")} - {FirstPlayer} goes first, " +
                     $"{GetOpponent(FirstPlayer).Index} starts with +{SecondPlayerNPBonus} NP");

            return FirstPlayer;
        }

        /// <summary>
        /// What the next lineup reroll costs this player: the base price plus one for every reroll already
        /// taken. A player opens setup on <see cref="PlayerState.StartingNP"/> NP, so the escalation prices
        /// a second reroll at exactly the NP they start with and a third beyond it — the cap on rerolls is
        /// the economy, not a counter.
        /// </summary>
        public int LineupRerollCost(PlayerIndex who)
        {
            return LineupRerollBaseCost + GetPlayer(who).LineupRerolls;
        }

        /// <summary>
        /// Closes setup and starts the match: the cards nobody fielded go back into their decks, both players
        /// draw their opening hands, the coin loser is credited the going-second NP, and the coin winner's
        /// first Upkeep runs.
        /// <para>
        /// Internal on purpose. It is the one transition that makes a half-taken lineup impossible to undo,
        /// and the only caller is the engine's confirm-lineup path — so a front end cannot bypass a
        /// confirmation and open the match on an empty board. A test that wants setup closed drives the
        /// command, which is the path the game actually takes.
        /// </para>
        /// </summary>
        internal void CompleteSetup()
        {
            if (!SetupInProgress)
            {
                return;
            }

            for (int i = 0; i < Players.Length; i++)
            {
                PlayerState player = Players[i];

                // What was offered and not fielded is a deck card again. The shuffle that follows is what
                // makes a reroll or a refused offer unrecoverable-by-counting: the opponent cannot remember
                // which five you were shown and infer the rest of your deck from it.
                if (player.LineupOffer.Count > 0)
                {
                    player.Deck.AddRange(player.LineupOffer);
                    player.LineupOffer.Clear();
                    player.ShuffleDeck();
                }

                player.LineupConfirmed = true;
            }

            for (int i = 0; i < StartingHandSize; i++)
            {
                Players[0].DrawCard();
                Players[1].DrawCard();
            }

            // A match cannot start without a first player, so a caller that never asked for the toss gets it
            // here rather than silently defaulting to Player and paying the going-second NP to whoever
            // happens not to be. FlipCoin is idempotent, so the normal path — the flip happened during setup
            // — is untouched by this line.
            if (!CoinFlipped)
            {
                FlipCoin();
            }

            GetOpponent(FirstPlayer).CurrentNP += SecondPlayerNPBonus;

            SetupInProgress = false;
            ActivePlayer = FirstPlayer;
            TurnNumber = 1;
            CurrentPhase = TurnPhase.Upkeep;

            int fielded = Players[0].Board.GetAllUnits(PlayerIndex.Player).Count
                + Players[1].Board.GetAllUnits(PlayerIndex.Opponent).Count;
            LogEvent($"Lineups set - {fielded} unit(s) fielded, both players draw {StartingHandSize}");

            // Every opening unit is readied here, for <em>both</em> seats - not only the first player's, which is
            // what the Upkeep below would have done on its own. Two reasons, and the second is the important one.
            // The first is the rule: the lineup is not a deployment, so [Summoning Delay] has nothing to delay.
            // The second is that a unit which is not Ready cannot block, so if the second seat's lineup had waited
            // for its own first Upkeep, the first player would have swung five Ready bodies at a 3-durability Core
            // with nothing able to intercept - an unavoidable loss dressed up as a tempo disadvantage.
            for (int i = 0; i < Players.Length; i++)
            {
                foreach (CardInstance unit in Players[i].Board.GetAllUnits((PlayerIndex)i))
                {
                    unit.SetReady();
                }
            }

            // The lineup is the first thing that has ever put Node Actives on the board before a turn began, so
            // the Node 1/2 Core multipliers have to be computed here. Without this the Core Active sits one ATK
            // and one HP light until something dies or switches, which are the only two places the refresh used
            // to run, and a player who reads their own card face would see numbers that do not match.
            RefreshNodeMultipliers();

            LogEvent($"Turn {TurnNumber} - {ActivePlayer} Upkeep Phase");

            OnUpkeepPhase();
        }

        /// <summary>
        /// Moves the first <see cref="LineupOfferSize"/> Units at or under <see cref="LineupMaxCost"/> from
        /// the front of <paramref name="player"/>'s deck into their offer, leaving every skipped card in the
        /// deck in its original relative order.
        /// <para>
        /// Internal because the engine calls it on a reroll: the offer is dealt by the same search whether it
        /// is the first one or the fourth, so the two paths cannot drift apart.
        /// </para>
        /// </summary>
        internal static void DealLineupOffer(PlayerState player)
        {
            player.LineupOffer.Clear();

            for (int i = 0; i < player.Deck.Count && player.LineupOffer.Count < LineupOfferSize; i++)
            {
                CardInstance candidate = player.Deck[i];
                if (candidate.Definition.Type == CardType.Unit
                    && candidate.Definition.Cost <= LineupMaxCost)
                {
                    player.LineupOffer.Add(candidate);
                }
            }

            // Removed by reference after the scan rather than by splicing inside it: taking a card out mid
            // walk shifts every index behind it, which would silently skip the card after each hit.
            foreach (CardInstance candidate in player.LineupOffer)
            {
                player.Deck.Remove(candidate);
            }
        }

        static void ValidateDecks(List<CardDefinition> playerDeck, List<CardDefinition> opponentDeck)
        {
            if (playerDeck == null || playerDeck.Count == 0)
            {
                throw new ArgumentException("Player deck cannot be null or empty", nameof(playerDeck));
            }

            if (opponentDeck == null || opponentDeck.Count == 0)
            {
                throw new ArgumentException("Opponent deck cannot be null or empty", nameof(opponentDeck));
            }
        }

        private void LoadDeck(PlayerState player, List<CardDefinition> definitions)
        {
            foreach (CardDefinition def in definitions)
            {
                if (def != null)
                {
                    CardInstance instance = new CardInstance(def, player.Index);
                    player.Deck.Add(instance);
                }
            }
        }

        public void LogEvent(string message)
        {
            EventLog.Add(message);
        }
    }
}
