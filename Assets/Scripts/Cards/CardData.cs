using System;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    /// <summary>
    /// Hand-view adapter for CardInstance. This is a reduced projection of CardDefinition used by HandController
    /// and CardView to render cards in the player's hand.
    /// <para>
    /// PR-04: This class is LOAD-BEARING. It serves as the instance-identity key in GameCoordinator.instanceByData,
    /// which pins physical card copies when the hand holds duplicates. Do not delete CardData without migrating
    /// its callers and the physical-copy identity handling. The current hand face (CardView) only renders
    /// Title, Cost, Description, and type-derived accent. Faction, Rarity, Type label, ATK, HP, Shield, Keywords,
    /// and ArtPath are not exposed by this adapter. The battlefield reader does show those fields from CardDefinition.
    /// </para>
    /// </summary>
    [Serializable]
    public class CardData
    {
        public string id;
        public string title;
        public int cost;
        [TextArea] public string description;
        public Color accent = Color.white;

        // These references are a presentation bridge only. CardData remains the reference-identity adapter.
        public CardDefinition Definition { get; private set; }
        public CardInstance Instance { get; private set; }

        public void AttachSource(CardDefinition definition, CardInstance instance)
        {
            Definition = definition;
            Instance = instance;
        }

        public CardData(string id, string title, int cost, string description, Color accent)
        {
            this.id = id;
            this.title = title;
            this.cost = cost;
            this.description = description;
            this.accent = accent;
        }

        // Note: identity is intentional. HandController.SetHand and GameCoordinator's lookup map reuse
        // views by *reference* (one cached CardData per card instance), so two physical copies of the
        // same definition must stay distinct objects. Do not add value-based Equals/GetHashCode here -
        // it would collapse duplicate cards in the hand's HashSet.

    }
}
