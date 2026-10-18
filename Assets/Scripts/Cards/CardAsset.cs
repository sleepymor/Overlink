using System;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    [Serializable]
    public sealed class CardEffectEntry
    {
        public EffectType type;
        public TargetKind target;
        public int amount;
        public int amount2;
        public int duration;
        public string referenceId;
        public bool fast;

        public EffectDescriptor ToDescriptor()
        {
            return new EffectDescriptor(type, target, amount, amount2, duration, referenceId, fast);
        }

        public static CardEffectEntry FromDescriptor(EffectDescriptor descriptor)
        {
            var entry = new CardEffectEntry();
            entry.type = descriptor.Type;
            entry.target = descriptor.Target;
            entry.amount = descriptor.Amount;
            entry.amount2 = descriptor.Amount2;
            entry.duration = descriptor.Duration;
            entry.referenceId = descriptor.ReferenceId;
            entry.fast = descriptor.Fast;
            return entry;
        }
    }

    [CreateAssetMenu(menuName = "Overlink/Card", fileName = "card_new_000")]
    public sealed class CardAsset : ScriptableObject
    {
        public string id;
        public string cardName;
        public Faction faction;
        public Rarity rarity;
        public CardType type;
        [Range(0, 10)] public int cost;
        public int atk;
        public int hp;
        public int shield;
        public List<Keyword> keywords = new List<Keyword>();
        [TextArea(2, 6)] public string abilityText;
        [TextArea(2, 6)] public string designNote;
        public List<CardEffectEntry> effects = new List<CardEffectEntry>();
        public string artPath;

        public CardDefinition ToDefinition()
        {
            var definition = new CardDefinition();
            definition.Id = id;
            definition.Name = cardName;
            definition.Faction = faction;
            definition.Rarity = rarity;
            definition.Type = type;
            definition.Cost = cost;
            definition.Atk = atk;
            definition.Hp = hp;
            definition.Shield = shield;
            definition.AbilityText = abilityText;
            definition.DesignNote = designNote;
            definition.ArtPath = artPath;
            definition.Keywords = keywords != null ? keywords.ToArray() : Array.Empty<Keyword>();
            definition.Effects = BuildEffects();
            return definition;
        }

        private EffectDescriptor[] BuildEffects()
        {
            var result = new List<EffectDescriptor>();
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (effects[i] != null)
                    {
                        result.Add(effects[i].ToDescriptor());
                    }
                }
            }

            return result.ToArray();
        }


        public void FromDefinition(CardDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            id = definition.Id;
            cardName = definition.Name;
            faction = definition.Faction;
            rarity = definition.Rarity;
            type = definition.Type;
            cost = definition.Cost;
            atk = definition.Atk;
            hp = definition.Hp;
            shield = definition.Shield;
            abilityText = definition.AbilityText;
            designNote = definition.DesignNote;
            artPath = definition.ArtPath;

            keywords = new List<Keyword>();
            if (definition.Keywords != null)
            {
                keywords.AddRange(definition.Keywords);
            }

            effects = new List<CardEffectEntry>();
            if (definition.Effects != null)
            {
                for (int i = 0; i < definition.Effects.Count; i++)
                {
                    effects.Add(CardEffectEntry.FromDescriptor(definition.Effects[i]));
                }
            }
        }
    }
}