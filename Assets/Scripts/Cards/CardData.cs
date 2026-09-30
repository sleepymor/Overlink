using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overlink.Cards
{
    [Serializable]
    public class CardData
    {
        public string id;
        public string title;
        public int cost;
        [TextArea] public string description;
        public Color accent = Color.white;

        public CardData(string id, string title, int cost, string description, Color accent)
        {
            this.id = id;
            this.title = title;
            this.cost = cost;
            this.description = description;
            this.accent = accent;
        }

        public static List<CardData> StarterDeck()
        {
            return new List<CardData>
            {
                new CardData("strike",  "Strike",  1, "Deal 3 damage.",      new Color(0.75f, 0.25f, 0.22f)),
                new CardData("guard",   "Guard",   1, "Gain 4 block.",       new Color(0.25f, 0.45f, 0.75f)),
                new CardData("focus",   "Focus",   0, "Draw a card.",        new Color(0.30f, 0.65f, 0.55f)),
                new CardData("slam",    "Slam",    2, "Deal 7 damage.",      new Color(0.80f, 0.45f, 0.15f)),
                new CardData("ward",    "Ward",    2, "Gain 9 block.",       new Color(0.35f, 0.55f, 0.85f)),
                new CardData("spark",   "Spark",   0, "Deal 1 damage.",      new Color(0.85f, 0.75f, 0.25f)),
                new CardData("surge",   "Surge",   1, "Gain 1 energy.",      new Color(0.55f, 0.35f, 0.75f)),
                new CardData("mend",    "Mend",    1, "Heal 4 HP.",          new Color(0.35f, 0.70f, 0.40f)),
                new CardData("volley",  "Volley",  3, "Deal 4 damage twice.",new Color(0.70f, 0.30f, 0.50f)),
                new CardData("bulwark", "Bulwark", 3, "Gain 14 block.",      new Color(0.45f, 0.50f, 0.60f)),
            };
        }
    }
}
