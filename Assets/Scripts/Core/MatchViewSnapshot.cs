using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Overlink.Core
{
    /// <summary>
    /// Detached presentation data, not a rules state or a wire format. Deck identities,
    /// opponent hand and offer identities, randomness and event logs are never captured.
    /// Player indices retain their authoritative host-side meaning.
    /// </summary>
    public sealed class MatchViewSnapshot
    {
        public sealed class CardView
        {
            public int InstanceId { get; }
            public string DefinitionId { get; }
            public int Attack { get; }
            public int Health { get; }
            public int Shield { get; }
            public UnitState State { get; }
            public bool SummoningDelay { get; }
            public bool CannotBeBlocked { get; }
            public int NodeAtkBonus { get; }
            public int NodeHpBonus { get; }

            internal CardView(CardInstance card)
            {
                InstanceId = card.InstanceId; DefinitionId = card.DefinitionId;
                Attack = card.CurrentAtk; Health = card.CurrentHp; Shield = card.CurrentShield;
                State = card.State; SummoningDelay = card.HasSummoningDelay;
                CannotBeBlocked = card.CannotBeBlocked;
                NodeAtkBonus = card.NodeAtkBonus; NodeHpBonus = card.NodeHpBonus;
            }

            internal CardView(int id, string definition, int attack, int health, int shield, UnitState state,
                bool delay, bool cannotBlock, int nodeAttack, int nodeHealth)
            {
                InstanceId = id; DefinitionId = definition; Attack = attack; Health = health; Shield = shield;
                State = state; SummoningDelay = delay; CannotBeBlocked = cannotBlock;
                NodeAtkBonus = nodeAttack; NodeHpBonus = nodeHealth;
            }
        }

        public sealed class BoardCard
        {
            public SlotType Slot { get; }
            public int Index { get; }
            public int NodeIndex { get; }
            public CardView Card { get; }
            internal BoardCard(SlotType slot, int index, int nodeIndex, CardInstance card)
            { Slot = slot; Index = index; NodeIndex = nodeIndex; Card = new CardView(card); }
            internal BoardCard(SlotType slot, int index, int nodeIndex, CardView card)
            { Slot = slot; Index = index; NodeIndex = nodeIndex; Card = card; }
        }

        public sealed class PlayerView
        {
            public int NP { get; }
            public int CoreDurability { get; }
            public int DeckCount { get; }
            public int HandCount { get; }
            public int OfferCount { get; }
            public int DiscardCount { get; }
            public int LineupRerolls { get; }
            public bool LineupConfirmed { get; }
            public IReadOnlyList<CardView> Hand { get; }
            public IReadOnlyList<CardView> LineupOffer { get; }
            public IReadOnlyList<CardView> Discard { get; }
            public IReadOnlyList<BoardCard> Board { get; }

            internal PlayerView(PlayerState player, bool own)
            {
                NP = player.CurrentNP; CoreDurability = player.CoreDurability;
                DeckCount = player.Deck.Count; HandCount = player.Hand.Count;
                OfferCount = player.LineupOffer.Count; DiscardCount = player.Discard.Count;
                LineupRerolls = player.LineupRerolls; LineupConfirmed = player.LineupConfirmed;
                Hand = Cards(own ? player.Hand : null);
                LineupOffer = Cards(own ? player.LineupOffer : null);
                Discard = Cards(player.Discard);
                var board = new List<BoardCard>();
                AddSlots(board, player.Board.CoreActive); AddSlots(board, player.Board.CoreBench);
                for (int i = 0; i < global::Overlink.Core.Board.NodeCount; i++)
                {
                    AddSlots(board, player.Board.NodeActive[i]);
                    AddSlots(board, player.Board.NodeBench[i]);
                }
                if (player.Board.Protocol != null)
                    board.Add(new BoardCard(SlotType.Protocol, 0, -1, player.Board.Protocol));
                Board = board.AsReadOnly();
            }

            internal PlayerView(int np, int core, int deckCount, int handCount, int offerCount, int discardCount,
                int rerolls, bool confirmed, List<CardView> hand, List<CardView> offer, List<CardView> discard, List<BoardCard> board)
            {
                NP = np; CoreDurability = core; DeckCount = deckCount; HandCount = handCount;
                OfferCount = offerCount; DiscardCount = discardCount; LineupRerolls = rerolls;
                LineupConfirmed = confirmed; Hand = hand.AsReadOnly(); LineupOffer = offer.AsReadOnly();
                Discard = discard.AsReadOnly();
                Board = board.AsReadOnly();
            }

            static IReadOnlyList<CardView> Cards(List<CardInstance> source)
            {
                var cards = new List<CardView>();
                if (source != null) foreach (var card in source) cards.Add(new CardView(card));
                return cards.AsReadOnly();
            }

            static void AddSlots(List<BoardCard> result, BoardSlot[] slots)
            {
                foreach (var slot in slots)
                    if (slot.Card != null) result.Add(new BoardCard(slot.Type, slot.Index, slot.NodeIndex, slot.Card));
            }
        }

        public long Revision { get; }
        public PlayerIndex Viewer { get; }
        public PlayerIndex ActivePlayer { get; }
        public TurnPhase Phase { get; }
        public int TurnNumber { get; }
        public MatchResult Result { get; }
        public string ResultReason { get; }
        public bool SetupInProgress { get; }
        public bool CoinFlipped { get; }
        public PlayerIndex FirstPlayer { get; }
        public IReadOnlyList<PlayerView> Players { get; }

        public MatchViewSnapshot(MatchState state, PlayerIndex viewer, long revision)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (viewer != PlayerIndex.Player && viewer != PlayerIndex.Opponent) throw new ArgumentOutOfRangeException(nameof(viewer));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            // The toss is deliberately hidden until both lineups are confirmed. FlipCoin also
            // sets ActivePlayer, so both fields must use fixed placeholders during setup.
            bool hideToss = state.SetupInProgress;
            Revision = revision; Viewer = viewer; ActivePlayer = hideToss ? PlayerIndex.Player : state.ActivePlayer;
            Phase = state.CurrentPhase; TurnNumber = state.TurnNumber; Result = state.Result;
            ResultReason = state.ResultReason; SetupInProgress = state.SetupInProgress;
            CoinFlipped = !hideToss && state.CoinFlipped;
            FirstPlayer = hideToss ? PlayerIndex.Player : state.FirstPlayer;
            Players = new ReadOnlyCollection<PlayerView>(new[] {
                new PlayerView(state.Players[0], viewer == PlayerIndex.Player),
                new PlayerView(state.Players[1], viewer == PlayerIndex.Opponent) });
        }

        internal MatchViewSnapshot(long revision, PlayerIndex active, TurnPhase phase, int turn, MatchResult result,
            string reason, bool setup, bool flipped, PlayerIndex first, PlayerView[] players)
        {
            Revision = revision; Viewer = PlayerIndex.Opponent; ActivePlayer = active; Phase = phase;
            TurnNumber = turn; Result = result; ResultReason = reason; SetupInProgress = setup;
            CoinFlipped = flipped; FirstPlayer = first;
            Players = new ReadOnlyCollection<PlayerView>(players);
        }
    }
}
