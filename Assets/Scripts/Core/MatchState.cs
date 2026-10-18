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

        public PlayerState[] Players { get; }
        public PlayerIndex ActivePlayer { get; private set; }

        public TurnPhase CurrentPhase { get; set; }
        public int TurnNumber { get; private set; }
        public MatchResult Result { get; private set; }

        /// <summary>
        /// Human-readable cause behind <see cref="Result"/> ("CORE DESTRUCTION", "DECK-OUT",
        /// "CONCESSION", "NODE DOMINATION", "BOTH CORES DESTROYED"), null while the match is live.
        /// Kept separate from <see cref="MatchResult"/> so the banner can say *why* the match ended.
        /// </summary>
        public string ResultReason { get; private set; }
        public List<string> EventLog { get; }

        public MatchState()
        {
            Players = new PlayerState[2];
            Players[0] = new PlayerState(PlayerIndex.Player);
            Players[1] = new PlayerState(PlayerIndex.Opponent);
            ActivePlayer = PlayerIndex.Player;
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
        /// </summary>
        private void CheckNodeDomination()
        {
            if (IsMatchOver())
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
        /// (+1 base, +1 per controlled Node) up to the 10 + Nodes cap, then check Node Domination.
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
            player.GainNP(1 + controlledNodes, cap);
            LogEvent($"{ActivePlayer} gains {1 + controlledNodes} NP (1 base + {controlledNodes} Nodes), now {player.CurrentNP}/{cap}");

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
            if (playerDeck == null || playerDeck.Count == 0)
            {
                throw new ArgumentException("Player deck cannot be null or empty", nameof(playerDeck));
            }

            if (opponentDeck == null || opponentDeck.Count == 0)
            {
                throw new ArgumentException("Opponent deck cannot be null or empty", nameof(opponentDeck));
            }

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
