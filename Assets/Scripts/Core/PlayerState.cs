using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    public sealed class PlayerState
    {
        public const int StartingNP = 3;

        /// <summary>GDD 2.1.1: the NP cap is 10 + the number of controlled Nodes; 10 is the base.</summary>
        public const int MaxNP = 10;

        /// <summary>GDD 2.1.7: a player's Core durability counter starts at 3.</summary>
        public const int StartingCoreDurability = 3;

        public PlayerIndex Index { get; }
        public int CurrentNP { get; set; }
        public int CoreDurability { get; set; }
        public Board Board { get; }
        public List<CardInstance> Hand { get; }
        public List<CardInstance> Deck { get; }
        public List<CardInstance> Discard { get; }

        public PlayerState(PlayerIndex index)
        {
            Index = index;
            CurrentNP = StartingNP;
            CoreDurability = StartingCoreDurability;
            Board = new Board();
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Discard = new List<CardInstance>();
        }

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

        public void ShuffleDeck()
        {
            Random rng = new Random();
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
