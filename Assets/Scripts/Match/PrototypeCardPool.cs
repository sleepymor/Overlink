using System.Collections.Generic;
using Overlink.Core;

namespace Overlink.Match
{
    /// <summary>
    /// Stand-in card pool for M4.
    /// <para>
    /// The pilot set is authored as <c>CardAsset</c> files by the Card Manager (item 1 of the plan), which
    /// is still open. Until those assets exist, the runtime needs a pool that exercises every part of the
    /// loop, so this defines 20 single-faction FSTI cards using conforming IDs
    /// (<c>card_fsti_001</c>..<c>card_fsti_020</c>) that pass <see cref="CardValidator"/> with zero errors:
    /// 11 Units, 4 Abilities, 3 Relays, 2 Protocols.
    /// </para>
    /// <para>
    /// Every <see cref="EffectType"/> used here is one the <see cref="EffectInterpreter"/> actually resolves,
    /// so the deck doubles as coverage for blocking, Bench promotion, Relay deployment and effect resolution.
    /// </para>
    /// </summary>
    public static class PrototypeCardPool
    {
        /// <summary>The 20 unique definitions. Safe to pass as the pool for Summon resolvers and validators.</summary>
        public static List<CardDefinition> All()
        {
            return new List<CardDefinition>
            {
                // ---- Units -------------------------------------------------------------
                Unit("card_fsti_001", "Relay Scout", 1, 2, 1, 0, new[] { Keyword.Piercing },
                    "Piercing. Gets into the Node grid early."),
                Unit("card_fsti_002", "Line Guardian", 2, 1, 4, 1, new[] { Keyword.Shield },
                    "Shield 1. Bulky body for holding a Core Active slot."),
                Unit("card_fsti_003", "Vanguard", 2, 3, 2, 0, new[] { Keyword.FirstStrike },
                    "First Strike. Wins the first exchange."),
                Unit("card_fsti_004", "Piercing Lancer", 3, 3, 3, 0, new[] { Keyword.Piercing },
                    "Piercing. Ignores Shield when dealing damage."),
                Unit("card_fsti_005", "Sanguine Drone", 3, 2, 3, 0, new[] { Keyword.Lifesteal },
                    "Lifesteal. Heals its controller for combat damage dealt."),
                DeployUnit("card_fsti_006", "Mesh Adept", 2, 1, 3, 0,
                    "Deploy: Draw 1 card.", EffectType.Draw, TargetKind.Self, 1),
                DeployUnit("card_fsti_007", "Field Medic", 3, 2, 3, 0,
                    "Deploy: Heal the active line for 3.", EffectType.Heal, TargetKind.AlliedActive, 3),
                DeployUnit("card_fsti_008", "Havoc Squad", 4, 4, 4, 0,
                    "Deploy: Exhaust an enemy unit.", EffectType.Exhaust, TargetKind.EnemyActive, 1),
                Unit("card_fsti_009", "Anchor Guard", 3, 1, 6, 0, new Keyword[0],
                    "A wall with no text. Just holds the line."),
                Unit("card_fsti_010", "Bulwark Sentry", 4, 2, 5, 2, new[] { Keyword.Shield },
                    "Shield 2. Expensive but hard to shift."),
                Unit("card_fsti_011", "Debilitator", 3, 2, 2, 0, new[] { Keyword.Activate },
                    "Activate: weaken an enemy active unit by 1 ATK.",
                    new EffectDescriptor(EffectType.Debuff, TargetKind.EnemyActive, 1)),

                // ---- Abilities --------------------------------------------------------
                Ability("card_fsti_012", "Volley", 2,
                    "Deal 2 damage to every enemy active unit.",
                    new EffectDescriptor(EffectType.DealAoEDamage, TargetKind.EnemyActive, 2)),
                Ability("card_fsti_013", "Reinforce", 3,
                    "Draw 2 cards.",
                    new EffectDescriptor(EffectType.Draw, TargetKind.Self, 2)),
                Ability("card_fsti_014", "Sabotage", 2,
                    "Destroy an enemy unit. Removes it from the field outright.",
                    new EffectDescriptor(EffectType.Destroy, TargetKind.EnemyActive, 1)),
                Ability("card_fsti_015", "Overclock", 1,
                    "Gain 2 NP this turn.",
                    new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 2)),

                // ---- Relays -----------------------------------------------------------
                Relay("card_fsti_016", "Signal Relay", 2,
                    "Occupy a Node slot. Its Active slot controls that Node for you."),
                Relay("card_fsti_017", "Node Beacon", 3,
                    "Occupy a Node slot. Second copy so the deck has enough Relay presence."),
                Relay("card_fsti_020", "Interdiction Net", 4,
                    "Occupy a Node slot. While present, enemy units deployed to that Node cannot be blocked."),

                // ---- Protocols --------------------------------------------------------
                Protocol("card_fsti_018", "Field Repairs", 3,
                    "Repair 3 Core durability when played.",
                    new[] { Keyword.Repair },
                    new EffectDescriptor(EffectType.Repair, TargetKind.Self, 3)),
                Protocol("card_fsti_019", "Bulwark Protocol", 4,
                    "Your active line gains +1/+1 when played.",
                    new Keyword[0],
                    new EffectDescriptor(EffectType.Buff, TargetKind.AllAlliedActive, 1, 1,
                        EffectDescriptor.PermanentDuration)),
            };
        }

        /// <summary>
        /// A 30-card list drawn from <see cref="All"/> (2 copies of the first 10, 1 of the rest), which
        /// satisfies the 30-card minimum and the 3-copy cap. <see cref="MatchState"/> shuffles it per player.
        /// </summary>
        public static List<CardDefinition> BuildDeck()
        {
            List<CardDefinition> pool = All();
            var deck = new List<CardDefinition>(30);

            // 20 cards, then the first 10 again: 30 total, and no card appears more than twice.
            for (int i = 0; i < pool.Count; i++)
            {
                deck.Add(pool[i]);
            }

            for (int i = 0; i < 10; i++)
            {
                deck.Add(pool[i]);
            }

            return deck;
        }

        private static CardDefinition Unit(
            string id, string name, int cost, int atk, int hp, int shield,
            Keyword[] keywords, string text, params EffectDescriptor[] effects)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = cost,
                Atk = atk,
                Hp = hp,
                Shield = shield,
                Keywords = keywords,
                AbilityText = text,
                DesignNote = "Prototype pool - replaces authored CardAsset content until M1 item 1 ships.",
                ArtPath = string.Empty,
                Effects = effects,
            };
        }

        private static CardDefinition DeployUnit(
            string id, string name, int cost, int atk, int hp, int shield,
            string text, EffectType type, TargetKind target, int amount)
        {
            var card = Unit(id, name, cost, atk, hp, shield,
                new[] { Keyword.Deploy }, text,
                new EffectDescriptor(type, target, amount));

            // Deploy requires at least one effect and Mesh Adept's text promises a Draw, so mirror the
            // keyword onto the Draw card the same way the validator expects keyword/effect pairs to line up.
            if (type == EffectType.Draw)
            {
                card.Keywords = new[] { Keyword.Deploy, Keyword.Draw };
            }

            return card;
        }

        private static CardDefinition Ability(string id, string name, int cost, string text, params EffectDescriptor[] effects)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Ability,
                Cost = cost,
                AbilityText = text,
                DesignNote = "Prototype pool - replaces authored CardAsset content until M1 item 1 ships.",
                ArtPath = string.Empty,
                Effects = effects,
            };
        }

        private static CardDefinition Relay(string id, string name, int cost, string text)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Relay,
                Cost = cost,
                AbilityText = text,
                DesignNote = "Prototype pool - replaces authored CardAsset content until M1 item 1 ships.",
                ArtPath = string.Empty,
                Effects = new EffectDescriptor[0],
            };
        }

        private static CardDefinition Protocol(string id, string name, int cost, string text,
            Keyword[] keywords, EffectDescriptor effect)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Protocol,
                Cost = cost,
                Keywords = keywords,
                AbilityText = text,
                DesignNote = "Prototype pool - replaces authored CardAsset content until M1 item 1 ships.",
                ArtPath = string.Empty,
                Effects = new[] { effect },
            };
        }
    }
}
