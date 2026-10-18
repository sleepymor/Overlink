using System;
using System.Linq;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class CardValidatorTests
    {
        [Test]
        public void ValidUnit_Passes()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Infantry Squad",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 2,
                Atk = 2,
                Hp = 3,
                Shield = 0,
                ArtPath = "Art/infantry.png",
                AbilityText = "Standard unit."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Issues.Count, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateId_Error()
        {
            var card1 = new CardDefinition
            {
                Id = "DUP_ID",
                Name = "Unit One",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1
            };
            var card2 = new CardDefinition
            {
                Id = "DUP_ID",
                Name = "Unit Two",
                Faction = Faction.Frti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 2
            };

            var pool = new[] { card1, card2 };
            var result = CardValidator.ValidatePool(pool);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Id" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void MissingName_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Name" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Cost11_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Super Titan",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Cost = 11,
                Atk = 10,
                Hp = 10
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Cost" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void NegativeStat_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Cursed Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Cost = 1,
                Atk = -1,
                Hp = 1
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Atk" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void EmptyUnit_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Shell",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Cost = 1,
                Atk = 0,
                Hp = 0
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Type" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void DanglingReference_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Beacon",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Summon, TargetKind.AlliedActive, amount: 1, referenceId: "NON_EXISTENT_CARD")
                }
            };

            var pool = new[] { card };
            var result = CardValidator.Validate(card, pool);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "ReferenceId" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void ZeroAmount_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Dud Strike",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Cost = 1,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 0)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Amount" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void DealDamage_AgainstSelf_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Self Harm",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.Self, amount: 2)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void DealDamage_AgainstCommander_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Direct Commander Strike",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.Commander, amount: 3)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void DealAoEDamage_AgainstEnemyCommander_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Commander AoE Blast",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealAoEDamage, TargetKind.EnemyCommander, amount: 2)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Heal_AgainstSelf_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Self Heal Attempt",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Heal, TargetKind.Self, amount: 2)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Destroy_AgainstCommander_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Assassinate Commander",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Destroy, TargetKind.Commander)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Repair_AgainstSelf_Legal()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Emergency Repair",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Repair, TargetKind.Self, amount: 2)
                },
                AbilityText = "Repair self for 2."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.False);
            Assert.That(result.Issues.Any(i => i.Field == "Amount" && i.Severity == Severity.Error), Is.False);
        }

        [Test]
        public void Repair_AgainstCommander_Legal()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Commander Patch",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Repair, TargetKind.Commander, amount: CardValidator.MaxRepairAmount)
                },
                AbilityText = "Repair commander for 3."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.False);
            Assert.That(result.Issues.Any(i => i.Field == "Amount" && i.Severity == Severity.Error), Is.False);
        }

        [Test]
        public void Repair_AgainstEnemyActive_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Invalid Field Repair",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Repair, TargetKind.EnemyActive, amount: 2)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Repair_Amount4_Rejected()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Overclocked Repair",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Repair, TargetKind.Self, amount: 4)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Amount" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void GainNP_AgainstSelf_Legal()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Network Charge",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.GainNP, TargetKind.Self, amount: 2)
                },
                AbilityText = "Gain 2 NP."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.False);
        }

        [Test]
        public void Draw_AgainstCommander_Legal()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Commander Intel",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Draw, TargetKind.Commander, amount: 1)
                },
                AbilityText = "Draw 1 card."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Target" && i.Severity == Severity.Error), Is.False);
        }

        [Test]
        public void Keyword_WithoutSupport_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Unsupported Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 2,
                Keywords = new[] { Keyword.Draw }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Keyword_WrongType_Warning()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Piercing Protocol",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Keywords = new[] { Keyword.Piercing },
                Atk = 2,
                Hp = 1,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 2)
                },
                AbilityText = "Piercing blast."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Warning), Is.True);
        }

        [Test]
        public void NonCombatStats_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Overstated Ability",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Atk = 2,
                Hp = 0,
                Shield = 0
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Stats" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void EmptyAbilityText_Warning()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Secretive Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.GainNP, TargetKind.Self, amount: 1)
                },
                AbilityText = ""
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "AbilityText" && i.Severity == Severity.Warning), Is.True);
        }

        [Test]
        public void EmptyArt_Info()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "No Art Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                ArtPath = null
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "ArtPath" && i.Severity == Severity.Info), Is.True);
        }

        [Test]
        public void PoolRarityBudget_50_30_15_5_CheckedWhenTrue()
        {
            // Total 20 cards: 10 Common (50%), 6 Rare (30%), 3 Epic (15%), 1 Legendary (5%)
            var pool = Enumerable.Range(1, 20).Select(i =>
            {
                Rarity r = i switch
                {
                    <= 10 => Rarity.Common,
                    <= 16 => Rarity.Rare,
                    <= 19 => Rarity.Epic,
                    _ => Rarity.Legendary
                };
                return new CardDefinition
                {
                    Id = $"card_fsti_{i:D3}",
                    Name = $"Card {i}",
                    Faction = Faction.Fsti,
                    Type = CardType.Unit,
                    Atk = 1,
                    Hp = 1,
                    Rarity = r,
                    ArtPath = "art.png"
                };
            }).ToList();

            var balancedResult = CardValidator.ValidatePool(pool, checkRarityBudget: true);
            Assert.That(balancedResult.Issues.Any(x => x.Field == "Pool" && x.Severity == Severity.Warning), Is.False);

            // Change one card to violate budget
            pool[0].Rarity = Rarity.Legendary;
            var unbalancedResult = CardValidator.ValidatePool(pool, checkRarityBudget: true);
            Assert.That(unbalancedResult.Issues.Any(x => x.Field == "Pool" && x.Severity == Severity.Warning), Is.True);

            // When checkRarityBudget is false (default), no rarity warning
            var uncheckedResult = CardValidator.ValidatePool(pool, checkRarityBudget: false);
            Assert.That(uncheckedResult.Issues.Any(x => x.Field == "Pool" && x.Severity == Severity.Warning), Is.False);
        }

        // --- New validator tests ---

        [Test]
        public void NonCombatStats_Relay_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Armed Relay",
                Faction = Faction.Fsti,
                Type = CardType.Relay,
                Atk = 0,
                Hp = 3,
                Shield = 0
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Stats" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void NonCombatStats_Protocol_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Shielded Protocol",
                Faction = Faction.Fsti,
                Type = CardType.Protocol,
                Atk = 0,
                Hp = 0,
                Shield = 2
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Stats" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Duration_BelowMinusOne_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Bad Duration",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Buff, TargetKind.AlliedActive, amount: 1, duration: -2)
                },
                AbilityText = "Invalid buff."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Duration" && i.Severity == Severity.Error), Is.True);
        }

        [Test]
        public void Duration_MinusOne_Legal()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Permanent Buff",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Buff, TargetKind.AlliedActive, amount: 1, duration: -1)
                },
                AbilityText = "Permanent buff."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Duration" && i.Severity == Severity.Error), Is.False);
        }

        [Test]
        public void Duration_Zero_Legal()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Instant Buff",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Buff, TargetKind.AlliedActive, amount: 1, duration: 0)
                },
                AbilityText = "Instant buff."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Duration" && i.Severity == Severity.Error), Is.False);
        }

        [Test]
        public void Move_RequiresReferenceId()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Move",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Move, TargetKind.AlliedActive, amount: 1)
                },
                AbilityText = "Move."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "ReferenceId" && i.Severity == Severity.Error && i.Message.Contains("Move")), Is.True);
        }

        [Test]
        public void AddKeyword_RequiresReferenceId()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty AddKeyword",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.AddKeyword, TargetKind.AlliedActive, amount: 1)
                },
                AbilityText = "Grant keyword."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "ReferenceId" && i.Severity == Severity.Error && i.Message.Contains("AddKeyword")), Is.True);
        }

        [Test]
        public void Summon_RequiresReferenceId()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Summon",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Summon, TargetKind.AlliedActive, amount: 1)
                }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "ReferenceId" && i.Severity == Severity.Error && i.Message.Contains("Summon")), Is.True);
        }

        [Test]
        public void IdFormat_NoPrefix_Warning()
        {
            var card = new CardDefinition
            {
                Id = "U1",
                Name = "Simple Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                ArtPath = "art.png"
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Id" && i.Severity == Severity.Warning), Is.True);
        }

        [Test]
        public void IdFormat_Correct_NoWarning()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Good Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                ArtPath = "art.png"
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Id" && i.Severity == Severity.Warning), Is.False);
        }

        [Test]
        public void IdFormat_WrongFaction_Warning()
        {
            var card = new CardDefinition
            {
                Id = "card_frti_001",
                Name = "Mismatched Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                ArtPath = "art.png"
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Id" && i.Severity == Severity.Warning && i.Message.Contains("faction")), Is.True);
        }

        [Test]
        public void IdFormat_AllFactions_Pass()
        {
            foreach (Faction faction in Enum.GetValues(typeof(Faction)))
            {
                string token = CardJson.FactionToken(faction);
                var card = new CardDefinition
                {
                    Id = "card_" + token + "_001",
                    Name = "Valid " + faction,
                    Faction = faction,
                    Type = CardType.Unit,
                    Atk = 1,
                    Hp = 1,
                    ArtPath = "art.png"
                };

                var result = CardValidator.Validate(card);
                Assert.That(result.Issues.Any(i => i.Field == "Id" && i.Severity == Severity.Warning), Is.False,
                    "Unexpected Id warning for " + faction);
            }
        }

        [Test]
        public void IdFormat_TooFewDigits_Warning()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_01",
                Name = "Short Id",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                ArtPath = "art.png"
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Id" && i.Severity == Severity.Warning), Is.True);
        }

        [Test]
        public void DuplicateKeywords_Warning()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Double Piercing",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 2,
                Keywords = new[] { Keyword.Piercing, Keyword.Piercing },
                ArtPath = "art.png",
                AbilityText = "Piercing."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Warning && i.Message.Contains("Duplicate")), Is.True);
        }

        [Test]
        public void Fast_Keyword_WithoutFastEffect_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Fake Fast",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 2,
                Keywords = new[] { Keyword.Fast },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1, fast: false)
                },
                AbilityText = "Not actually fast."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Fast")), Is.True);
        }

        [Test]
        public void Fast_Keyword_WithFastEffect_NoError()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Legit Fast",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 2,
                Keywords = new[] { Keyword.Fast },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1, fast: true)
                },
                ArtPath = "art.png",
                AbilityText = "Fast strike."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Fast")), Is.False);
        }

        [Test]
        public void FastEffect_WithoutFastKeyword_Warning()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Hidden Fast",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 2,
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1, fast: true)
                },
                ArtPath = "art.png",
                AbilityText = "Hidden fast."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Effects" && i.Severity == Severity.Warning && i.Message.Contains("Fast")), Is.True);
        }

        [Test]
        public void Piercing_Keyword_SupportedByStat()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Piercing Fighter",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 3,
                Hp = 2,
                Keywords = new[] { Keyword.Piercing },
                ArtPath = "art.png",
                AbilityText = "Piercing."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Piercing")), Is.False);
        }

        [Test]
        public void Piercing_Keyword_NoStats_Error()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Piercing Nothing",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 0,
                Hp = 0,
                Keywords = new[] { Keyword.Piercing },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1)
                },
                AbilityText = "Piercing without stats."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Piercing")), Is.True);
        }

        [Test]
        public void FirstStrike_SupportedByHp()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "First Strike Tank",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 0,
                Hp = 5,
                Keywords = new[] { Keyword.FirstStrike },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1)
                },
                ArtPath = "art.png",
                AbilityText = "First strike."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("FirstStrike")), Is.False);
        }

        [Test]
        public void Lifesteal_SupportedByAtk()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Lifesteal Striker",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 2,
                Hp = 0,
                Keywords = new[] { Keyword.Lifesteal },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1)
                },
                ArtPath = "art.png",
                AbilityText = "Lifesteal."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Lifesteal")), Is.False);
        }

        [Test]
        public void CannotBlock_SupportedByAtk()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Cannot Block Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 3,
                Hp = 0,
                Keywords = new[] { Keyword.CannotBlock },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1)
                },
                ArtPath = "art.png",
                AbilityText = "Cannot block."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("CannotBlock")), Is.False);
        }

        [Test]
        public void SummoningDelay_SupportedByStat()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Delayed Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 5,
                Hp = 5,
                Keywords = new[] { Keyword.SummoningDelay },
                ArtPath = "art.png",
                AbilityText = "Summoning delay."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("SummoningDelay")), Is.False);
        }

        [Test]
        public void Activate_RequiresEffect()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Activate",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.Activate }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Activate")), Is.True);
        }

        [Test]
        public void Deploy_RequiresEffect()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Deploy",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.Deploy }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Deploy")), Is.True);
        }

        [Test]
        public void Persistent_RequiresEffect()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Persistent",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.Persistent }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Persistent")), Is.True);
        }

        [Test]
        public void Exhaust_Keyword_RequiresEffect()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Empty Exhaust",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.Exhaust }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Exhaust")), Is.True);
        }

        [Test]
        public void Shield_Keyword_RequiresPositiveShield()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Shield Label Only",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Shield = 0,
                Keywords = new[] { Keyword.Shield }
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Shield")), Is.True);
        }

        [Test]
        public void Deploy_WithEffect_NoError()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Deploy Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.Deploy },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1)
                },
                ArtPath = "art.png",
                AbilityText = "Deploy: deal 1 damage."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Deploy")), Is.False);
        }

        [Test]
        public void Heal_Keyword_WithHealEffect_NoError()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Healer",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.Heal },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Heal, TargetKind.AlliedActive, amount: 2)
                },
                ArtPath = "art.png",
                AbilityText = "Heal 2."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Heal")), Is.False);
        }

        [Test]
        public void AoE_Keyword_WithDealAoEDamageEffect_NoError()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "AoE Unit",
                Faction = Faction.Fsti,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                Keywords = new[] { Keyword.AoE },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealAoEDamage, TargetKind.AllEnemyActive, amount: 2)
                },
                ArtPath = "art.png",
                AbilityText = "AoE 2."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("AoE")), Is.False);
        }

        [Test]
        public void Repair_Keyword_WithRepairEffect_NoError()
        {
            var card = new CardDefinition
            {
                Id = "card_fsti_001",
                Name = "Repairer",
                Faction = Faction.Fsti,
                Type = CardType.Ability,
                Keywords = new[] { Keyword.Repair },
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.Repair, TargetKind.Self, amount: 1)
                },
                ArtPath = "art.png",
                AbilityText = "Repair 1."
            };

            var result = CardValidator.Validate(card);

            Assert.That(result.Issues.Any(i => i.Field == "Keywords" && i.Severity == Severity.Error && i.Message.Contains("Repair")), Is.False);
        }
    }
}
