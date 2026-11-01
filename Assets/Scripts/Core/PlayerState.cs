using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    public sealed class PlayerState
    {
        public const int StartingNP = 3;

        /// <summary>GDD 2.1.1: the NP cap is 10 + the number of controlled Nodes; 10 is the base.</summary>
        public const int MaxNP = 10;

        /// <summary>Starting and maximum Core durability for each player.</summary>
        public const int StartingCoreDurability = 20;

        public PlayerIndex Index { get; }
        public int CurrentNP { get; set; }
        public int CoreDurability { get; set; }
        public Board Board { get; }
        public List<CardInstance> Hand { get; }
        public List<CardInstance> Deck { get; }
        public List<CardInstance> Discard { get; }

        /// <summary>
        /// The opening lineup this player has been offered and may still field, reroll or abandon. Held apart
        /// from the hand on purpose: an offer card is not in play, is not a <see cref="Hand"/> card the
        /// opponent can see or affect, and going back to the deck is the default outcome for every card the
        /// player does not field. Empty once setup is closed.
        /// </summary>
        public List<CardInstance> LineupOffer { get; }

        /// <summary>
        /// How many rerolls this player has already bought during setup. The price of the next one is
        /// <see cref="MatchState.LineupRerollBaseCost"/> plus this many, so the escalation is per player and
        /// resets with the match rather than being a global counter.
        /// </summary>
        public int LineupRerolls { get; set; }

        /// <summary>True once this player has fielded a lineup and cannot reroll any more.</summary>
        public bool LineupConfirmed { get; set; }

        public PlayerState(PlayerIndex index, Random rng = null)
        {
            Index = index;
            CurrentNP = StartingNP;
            CoreDurability = StartingCoreDurability;
            Board = new Board();
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Discard = new List<CardInstance>();
            LineupOffer = new List<CardInstance>();
            shuffleRng = rng ?? new Random();
        }

        private readonly Random shuffleRng;

        /// <summary>GDD 2.1.1: NP can never exceed 10 + the number of Nodes you control.</summary>
        public static int NpCap(int controlledNodes)
        {
            return MaxNP + controlledNodes;
        }

        public bool CanAfford(int cost)
        {
            return CurrentNP >= cost;
        }

        public bool SpendNP(int amount)
        {
            if (amount < 0 || amount > CurrentNP)
            {
                return false;
            }

            CurrentNP -= amount;
            return true;
        }

        /// <summary>Adds NP without exceeding the given cap. Unspent NP carries over between turns.</summary>
        public void GainNP(int amount, int cap)
        {
            if (amount > 0)
            {
                CurrentNP = Math.Min(CurrentNP + amount, cap);
            }
        }

        /// <summary>Effect-driven NP gain, clamped to the base 10 cap when the caller has no Node context.</summary>
        public void GainNP(int amount)
        {
            GainNP(amount, MaxNP);
        }

        public void TakeDamage(int amount)
        {
            if (amount > 0)
            {
                CoreDurability -= amount;
                if (CoreDurability < 0)
                {
                    CoreDurability = 0;
                }
            }
        }

        public void RepairCore(int amount)
        {
            if (amount > 0)
            {
                CoreDurability = Math.Min(CoreDurability + amount, StartingCoreDurability);
            }
        }

        public bool IsDefeated()
        {
            return CoreDurability <= 0;
        }

        public CardInstance DrawCard()
        {
            if (Deck.Count == 0)
            {
                if (Discard.Count == 0)
                {
                    return null;
                }
                ReshuffleDiscard();
            }

            if (Deck.Count == 0)
            {
                return null;
            }

            CardInstance card = Deck[0];
            Deck.RemoveAt(0);
            Hand.Add(card);
            return card;
        }

        public void ReshuffleDiscard()
        {
            if (Discard.Count == 0)
            {
                return;
            }

            Deck.AddRange(Discard);
            Discard.Clear();
            ShuffleDeck();
        }

        /// <summary>
        /// Fisher-Yates over the deck, driven by the <see cref="Random"/> the owning
        /// <see cref="MatchState"/> handed to this player.
        /// <para>
        /// This used to construct its own unseeded <c>Random</c>, which made every shuffle — and therefore
        /// every opening hand, every Deck-Out race and the coin flip that shares the same stream —
        /// irreproducible. <see cref="MatchEngineTests"/> had to delete a correct assertion because of it:
        /// "the mulliganed card is not back in the hand" was a roughly one-in-twenty-six coin flip against
        /// the real shuffler rather than a wrong test. A seeded match replays identically, which is what
        /// lets that assertion come back and what netplay will need before two peers can disagree about a
        /// deck.
        /// </para>
        /// </summary>
        public void ShuffleDeck()
        {
            Random rng = shuffleRng;
            int n = Deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                CardInstance temp = Deck[k];
                Deck[k] = Deck[n];
                Deck[n] = temp;
            }
        }

        public bool RemoveFromHand(CardInstance card)
        {
            return Hand.Remove(card);
        }

        public void DiscardCard(CardInstance card)
        {
            Discard.Add(card);
        }
    }
}
