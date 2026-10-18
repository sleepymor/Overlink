using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards.Tests
{
    [TestFixture]
    public class CardAssetTests
    {
        [Test]
        public void ToDefinition_MapsIdentityStatsAndEffects()
        {
            CardAsset asset = ScriptableObject.CreateInstance<CardAsset>();
            try
            {
                asset.id = "card_fsti_001";
                asset.cardName = "Signal Probe";
                asset.faction = Faction.Fsti;
                asset.rarity = Rarity.Rare;
                asset.type = CardType.Ability;
                asset.cost = 2;
                asset.abilityText = "Deal 2 damage. Draw 1.";
                asset.artPath = "Art/probe.png";
                asset.keywords = new List<Keyword> { Keyword.Draw };
                asset.effects = new List<CardEffectEntry>
                {
                    CardEffectEntry.FromDescriptor(new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, 2)),
                    CardEffectEntry.FromDescriptor(new EffectDescriptor(EffectType.Draw, TargetKind.Commander, 1))
                };

                CardDefinition definition = asset.ToDefinition();

                Assert.That(definition.Id, Is.EqualTo("card_fsti_001"));
                Assert.That(definition.Name, Is.EqualTo("Signal Probe"));
                Assert.That(definition.Faction, Is.EqualTo(Faction.Fsti));
                Assert.That(definition.Rarity, Is.EqualTo(Rarity.Rare));
                Assert.That(definition.Type, Is.EqualTo(CardType.Ability));
                Assert.That(definition.Cost, Is.EqualTo(2));
                Assert.That(definition.Atk, Is.EqualTo(0));
                Assert.That(definition.Keywords.Count, Is.EqualTo(1));
                Assert.That(definition.Keywords[0], Is.EqualTo(Keyword.Draw));
                Assert.That(definition.Effects.Count, Is.EqualTo(2));
                Assert.That(definition.Effects[0].Type, Is.EqualTo(EffectType.DealDamage));
                Assert.That(definition.Effects[0].Target, Is.EqualTo(TargetKind.EnemyActive));
                Assert.That(definition.Effects[0].Amount, Is.EqualTo(2));
                Assert.That(definition.Effects[1].Type, Is.EqualTo(EffectType.Draw));
                Assert.That(definition.Effects[1].Target, Is.EqualTo(TargetKind.Commander));
                Assert.That(definition.Effects[1].Amount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void FromDefinition_RoundTripsThroughToDefinition()
        {
            var original = new CardDefinition
            {
                Id = "card_fpb_003",
                Name = "Bulkhead",
                Faction = Faction.Fpb,
                Rarity = Rarity.Epic,
                Type = CardType.Unit,
                Cost = 4,
                Atk = 2,
                Hp = 6,
                Shield = 1,
                AbilityText = "Repair 2.",
                DesignNote = "Test.",
                ArtPath = "Art/bulkhead.png",
                Keywords = new[] { Keyword.Shield },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Repair, TargetKind.Self, 2)
                }
            };

            CardAsset asset = ScriptableObject.CreateInstance<CardAsset>();
            try
            {
                asset.FromDefinition(original);
                CardDefinition copy = asset.ToDefinition();

                Assert.That(copy.Id, Is.EqualTo(original.Id));
                Assert.That(copy.Name, Is.EqualTo(original.Name));
                Assert.That(copy.Faction, Is.EqualTo(original.Faction));
                Assert.That(copy.Rarity, Is.EqualTo(original.Rarity));
                Assert.That(copy.Type, Is.EqualTo(original.Type));
                Assert.That(copy.Cost, Is.EqualTo(original.Cost));
                Assert.That(copy.Atk, Is.EqualTo(original.Atk));
                Assert.That(copy.Hp, Is.EqualTo(original.Hp));
                Assert.That(copy.Shield, Is.EqualTo(original.Shield));
                Assert.That(copy.ArtPath, Is.EqualTo(original.ArtPath));
                Assert.That(copy.Keywords.Count, Is.EqualTo(1));
                Assert.That(copy.Effects.Count, Is.EqualTo(1));
                Assert.That(copy.Effects[0].Type, Is.EqualTo(EffectType.Repair));
                Assert.That(copy.Effects[0].Target, Is.EqualTo(TargetKind.Self));
                Assert.That(copy.Effects[0].Amount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
