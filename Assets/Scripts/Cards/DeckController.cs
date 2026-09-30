using System.Collections.Generic;
using UnityEngine;

namespace Overlink.Cards
{
    public class DeckController : MonoBehaviour
    {
        readonly Queue<CardData> drawPile = new Queue<CardData>();
        readonly List<CardData> discardPile = new List<CardData>();

        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;

        void Awake()
        {
            var cards = CardData.StarterDeck();
            Shuffle(cards);
            foreach (var c in cards) drawPile.Enqueue(c);
        }

        public CardData Draw()
        {
            if (drawPile.Count == 0) Reshuffle();
            if (drawPile.Count == 0) return null;
            return drawPile.Dequeue();
        }

        public void Discard(CardData card)
        {
            if (card != null) discardPile.Add(card);
        }

        void Reshuffle()
        {
            if (discardPile.Count == 0) return;
            Shuffle(discardPile);
            foreach (var c in discardPile) drawPile.Enqueue(c);
            discardPile.Clear();
            Debug.Log($"[Deck] Reshuffled discard into draw pile ({drawPile.Count} cards).");
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
