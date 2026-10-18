using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Overlink.Core
{
    public static class CardValidator
    {
        public const int MaxRepairAmount = 3;
        public const int MaxCost = 10;

        private static readonly Regex IdPattern = new Regex(@"^card_(fsti|frti|fpb)_\d{3}$", RegexOptions.Compiled);

        public static ValidationResult Validate(CardDefinition card, IEnumerable<CardDefinition> pool = null)
        {
            var issues = new List<ValidationIssue>();

            if (card == null)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Card", "CardDefinition cannot be null."));
                return new ValidationResult(issues);
            }

            ValidateIdentity(card, pool, issues);
            ValidateStats(card, issues);
            ValidateEffects(card, pool, issues);
            ValidateKeywords(card, issues);
            ValidatePresentation(card, issues);

            return new ValidationResult(issues);
        }

        private static void ValidateIdentity(CardDefinition card, IEnumerable<CardDefinition> pool, List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(card.Id))
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Id), "Card Id must be present."));
            }
            else
            {
                if (pool != null)
                {
                    int matches = 0;
                    foreach (CardDefinition other in pool)
                    {
                        if (other != null && other.Id == card.Id)
                        {
                            matches++;
                        }
                    }

                    if (matches > 1)
                    {
                        issues.Add(new ValidationIssue(Severity.Error, nameof(card.Id), "Card Id " + card.Id + " is not unique across the pool."));
                    }
                }

                var match = IdPattern.Match(card.Id);
                if (!match.Success)
                {
                    issues.Add(new ValidationIssue(Severity.Warning, nameof(card.Id), "Card Id '" + card.Id + "' does not match the expected format card_<faction>_<nnn>."));
                }
                else
                {
                    string idFactionToken = match.Groups[1].Value;
                    string cardFactionToken = CardJson.FactionToken(card.Faction);
                    if (idFactionToken != cardFactionToken)
                    {
                        issues.Add(new ValidationIssue(Severity.Warning, nameof(card.Id), "Card Id faction token '" + idFactionToken + "' does not match card Faction '" + cardFactionToken + "'."));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(card.Name))
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Name), "Card Name must be present."));
            }

            if (!Enum.IsDefined(typeof(Faction), card.Faction))
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Faction), "Card Faction must be present and valid."));
            }

            if (!Enum.IsDefined(typeof(CardType), card.Type))
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Type), "Card Type must be Unit, Ability, Relay or Protocol. Found: " + card.Type + "."));
            }

            if (!Enum.IsDefined(typeof(Rarity), card.Rarity))
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Rarity), "Card Rarity must be a defined rarity. Found: " + card.Rarity + "."));
            }
        }

        private static void ValidateStats(CardDefinition card, List<ValidationIssue> issues)
        {
            if (card.Cost < 0 || card.Cost > MaxCost)
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Cost), "Cost must be between 0 and " + MaxCost + " inclusive. Found: " + card.Cost + "."));
            }

            if (card.Atk < 0)
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Atk), "ATK cannot be negative. Found: " + card.Atk + "."));
            }

            if (card.Hp < 0)
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Hp), "HP cannot be negative. Found: " + card.Hp + "."));
            }

            if (card.Shield < 0)
            {
                issues.Add(new ValidationIssue(Severity.Error, nameof(card.Shield), "Shield cannot be negative. Found: " + card.Shield + "."));
            }

            if (card.Type == CardType.Unit)
            {
                bool hasEffects = card.Effects != null && card.Effects.Count > 0;
                if (card.Atk <= 0 && card.Hp <= 0 && !hasEffects)
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(card.Type), "A Unit must have Atk > 0, Hp > 0, or at least one effect."));
                }
            }
            else if (card.Atk != 0 || card.Hp != 0 || card.Shield != 0)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Stats", card.Type + " cards must not have combat stats. Found: Atk=" + card.Atk + ", Hp=" + card.Hp + ", Shield=" + card.Shield + "."));
            }
        }

        private static void ValidateEffects(CardDefinition card, IEnumerable<CardDefinition> pool, List<ValidationIssue> issues)
        {
            if (card.Effects == null)
            {
                return;
            }

            bool hasAnyFastEffect = false;

            for (int index = 0; index < card.Effects.Count; index++)
            {
                EffectDescriptor effect = card.Effects[index];
                string prefix = "Effects[" + index + "] ";

                if (!Enum.IsDefined(typeof(EffectType), effect.Type))
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(card.Effects), prefix + "has an undefined EffectType value."));
                    continue;
                }

                if (!Enum.IsDefined(typeof(TargetKind), effect.Target))
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(card.Effects), prefix + "has an undefined TargetKind value."));
                    continue;
                }

                if (effect.Duration < -1)
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(effect.Duration), prefix + "Duration must be >= -1. Found: " + effect.Duration + "."));
                }

                bool needsRef = effect.Type == EffectType.Summon ||
                                effect.Type == EffectType.Move ||
                                effect.Type == EffectType.AddKeyword;

                if (needsRef && string.IsNullOrWhiteSpace(effect.ReferenceId))
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(effect.ReferenceId), prefix + effect.Type + " requires a non-empty ReferenceId."));
                }

                if (!string.IsNullOrWhiteSpace(effect.ReferenceId) && pool != null)
                {
                    bool existsInPool = false;
                    foreach (CardDefinition other in pool)
                    {
                        if (other != null && other.Id == effect.ReferenceId)
                        {
                            existsInPool = true;
                            break;
                        }
                    }

                    if (!existsInPool)
                    {
                        issues.Add(new ValidationIssue(Severity.Error, nameof(effect.ReferenceId), prefix + "references card Id " + effect.ReferenceId + " which does not exist in the pool."));
                    }
                }

                if (NeedsMagnitude(effect.Type) && effect.Amount <= 0)
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(effect.Amount), prefix + "requires Amount > 0. Found: " + effect.Amount + "."));
                }

                bool isPlayerTarget = effect.Target == TargetKind.Self ||
                                        effect.Target == TargetKind.Commander ||
                                        effect.Target == TargetKind.EnemyCommander;

                if (isPlayerTarget && RequiresCard(effect.Type))
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(effect.Target), prefix + effect.Type + " cannot target player target " + effect.Target + "."));
                }

                if (effect.Type == EffectType.Repair)
                {
                    if (effect.Target != TargetKind.Self && effect.Target != TargetKind.Commander)
                    {
                        issues.Add(new ValidationIssue(Severity.Error, nameof(effect.Target), prefix + "Repair is only legal against Self or Commander. Found: " + effect.Target + "."));
                    }

                    if (effect.Amount > MaxRepairAmount)
                    {
                        issues.Add(new ValidationIssue(Severity.Error, nameof(effect.Amount), prefix + "Repair Amount must be at most " + MaxRepairAmount + ". Found: " + effect.Amount + "."));
                    }
                }

                if (effect.Fast)
                {
                    hasAnyFastEffect = true;
                }
            }

            bool hasFastKeyword = HasKeyword(card, Keyword.Fast);

            if (hasAnyFastEffect && !hasFastKeyword)
            {
                issues.Add(new ValidationIssue(Severity.Warning, nameof(card.Effects), "An effect has Fast=true but the card does not have Keyword.Fast."));
            }
        }

        private static bool NeedsMagnitude(EffectType type)
        {
            switch (type)
            {
                case EffectType.DealDamage:
                case EffectType.DealAoEDamage:
                case EffectType.Heal:
                case EffectType.Draw:
                case EffectType.GainNP:
                case EffectType.GrantShield:
                case EffectType.Buff:
                case EffectType.Debuff:
                case EffectType.AddKeyword:
                case EffectType.Repair:
                case EffectType.Summon:
                case EffectType.Move:
                case EffectType.PreventBlock:
                case EffectType.Exhaust:
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresCard(EffectType type)
        {
            return type == EffectType.DealDamage ||
                    type == EffectType.DealAoEDamage ||
                    type == EffectType.Heal ||
                    type == EffectType.Destroy;
        }

        private static void ValidateKeywords(CardDefinition card, List<ValidationIssue> issues)
        {
            if (card.Keywords == null)
            {
                return;
            }

            var seen = new HashSet<Keyword>();
            foreach (Keyword keyword in card.Keywords)
            {
                if (!seen.Add(keyword))
                {
                    issues.Add(new ValidationIssue(Severity.Warning, nameof(card.Keywords), "Duplicate keyword " + keyword + "."));
                }
            }

            foreach (Keyword keyword in card.Keywords)
            {
                if (!Enum.IsDefined(typeof(Keyword), keyword))
                {
                    issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Undefined Keyword value."));
                    continue;
                }

                switch (keyword)
                {
                    case Keyword.Shield:
                        if (card.Shield <= 0)
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword Shield requires Shield > 0."));
                        }
                        break;

                    case Keyword.Draw:
                        if (!HasEffect(card, EffectType.Draw))
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword Draw requires a matching effect."));
                        }
                        break;

                    case Keyword.Heal:
                        if (!HasEffect(card, EffectType.Heal))
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword Heal requires a matching effect."));
                        }
                        break;

                    case Keyword.Repair:
                        if (!HasEffect(card, EffectType.Repair))
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword Repair requires a matching effect."));
                        }
                        break;

                    case Keyword.AoE:
                        if (!HasEffect(card, EffectType.DealAoEDamage))
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword AoE requires a matching effect."));
                        }
                        break;

                    case Keyword.Piercing:
                    case Keyword.FirstStrike:
                    case Keyword.Lifesteal:
                    case Keyword.CannotBlock:
                    case Keyword.SummoningDelay:
                        if (card.Atk <= 0 && card.Hp <= 0)
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword " + keyword + " requires Atk > 0 or Hp > 0."));
                        }
                        break;

                    case Keyword.Fast:
                        if (!HasAnyFastEffect(card))
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword Fast requires at least one effect with Fast=true."));
                        }
                        break;

                    case Keyword.Activate:
                    case Keyword.Deploy:
                    case Keyword.Persistent:
                    case Keyword.Exhaust:
                        if (card.Effects == null || card.Effects.Count == 0)
                        {
                            issues.Add(new ValidationIssue(Severity.Error, nameof(card.Keywords), "Keyword " + keyword + " requires at least one effect."));
                        }
                        break;
                }

                if (card.Type != CardType.Unit && IsUnitOnlyKeyword(keyword))
                {
                    issues.Add(new ValidationIssue(Severity.Warning, nameof(card.Keywords), "Keyword " + keyword + " does not apply to card type " + card.Type + " (only applies to Unit)."));
                }
            }
        }

        private static bool IsUnitOnlyKeyword(Keyword keyword)
        {
            return keyword == Keyword.Piercing ||
                    keyword == Keyword.FirstStrike ||
                    keyword == Keyword.Lifesteal ||
                    keyword == Keyword.Shield ||
                    keyword == Keyword.SummoningDelay ||
                    keyword == Keyword.CannotBlock;
        }

        private static bool HasSupportingEffect(CardDefinition card, Keyword keyword)
        {
            if (card.Effects == null || card.Effects.Count == 0)
            {
                return false;
            }

            switch (keyword)
            {
                case Keyword.Draw:
                    return HasEffect(card, EffectType.Draw);
                case Keyword.Heal:
                    return HasEffect(card, EffectType.Heal);
                case Keyword.Repair:
                    return HasEffect(card, EffectType.Repair);
                case Keyword.AoE:
                    return HasEffect(card, EffectType.DealAoEDamage);
                case Keyword.Activate:
                case Keyword.Deploy:
                    return card.Effects.Count > 0;
                default:
                    return false;
            }
        }

        private static bool HasEffect(CardDefinition card, EffectType type)
        {
            if (card.Effects == null)
            {
                return false;
            }

            for (int i = 0; i < card.Effects.Count; i++)
            {
                if (card.Effects[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyFastEffect(CardDefinition card)
        {
            if (card.Effects == null)
            {
                return false;
            }

            for (int i = 0; i < card.Effects.Count; i++)
            {
                if (card.Effects[i].Fast)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasKeyword(CardDefinition card, Keyword keyword)
        {
            if (card.Keywords == null)
            {
                return false;
            }

            for (int i = 0; i < card.Keywords.Count; i++)
            {
                if (card.Keywords[i] == keyword)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidatePresentation(CardDefinition card, List<ValidationIssue> issues)
        {
            bool hasMechanics = (card.Keywords != null && card.Keywords.Count > 0) ||
                                (card.Effects != null && card.Effects.Count > 0);

            if (hasMechanics && string.IsNullOrWhiteSpace(card.AbilityText))
            {
                issues.Add(new ValidationIssue(Severity.Warning, nameof(card.AbilityText), "Card has mechanics but AbilityText is empty."));
            }

            if (string.IsNullOrWhiteSpace(card.ArtPath))
            {
                issues.Add(new ValidationIssue(Severity.Info, nameof(card.ArtPath), "ArtPath is empty."));
            }
        }

        public static ValidationResult ValidatePool(IEnumerable<CardDefinition> pool, bool checkRarityBudget = false)
        {
            var issues = new List<ValidationIssue>();

            if (pool == null)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Pool", "Card pool cannot be null."));
                return new ValidationResult(issues);
            }

            var list = pool as IList<CardDefinition> ?? pool.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    issues.AddRange(Validate(list[i], list).Issues);
                }
            }

            if (checkRarityBudget && list.Count > 0)
            {
                CardComposition composition = CardComposition.FromCards(list);
                int commonPct = (composition.Common * 100) / list.Count;
                int rarePct = (composition.Rare * 100) / list.Count;
                int epicPct = (composition.Epic * 100) / list.Count;
                int legendaryPct = (composition.Legendary * 100) / list.Count;

                if (commonPct != 50 || rarePct != 30 || epicPct != 15 || legendaryPct != 5)
                {
                    issues.Add(new ValidationIssue(Severity.Warning, "Pool", "Pool rarity budget mismatch. Expected 50/30/15/5, got " + commonPct + "/" + rarePct + "/" + epicPct + "/" + legendaryPct + " (counts: " + composition.Common + "/" + composition.Rare + "/" + composition.Epic + "/" + composition.Legendary + " of " + list.Count + ")."));
                }
            }

            return new ValidationResult(issues);
        }
    }
}
