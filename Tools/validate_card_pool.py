#!/usr/bin/env python3
"""Validate card_pool.json against CardValidator rules (Python re-implementation)."""

import json
import sys
import re

ID_PATTERN = re.compile(r"^card_(fsti|frti|fpb)_\d{3}$")
MAX_COST = 10
MAX_REPAIR_AMOUNT = 3
MAX_FLAVOUR_LENGTH = 80

VALID_FACTIONS = {"fsti", "frti", "fpb"}
VALID_RARITIES = {"Common", "Rare", "Epic", "Legendary"}
VALID_TYPES = {"Unit", "Ability", "Relay", "Protocol"}
VALID_KEYWORDS = {
    "Piercing", "FirstStrike", "Lifesteal", "Draw", "AoE", "CannotBlock",
    "Activate", "Deploy", "Fast", "Shield", "Heal", "SummoningDelay",
    "Persistent", "Exhaust", "Repair"
}
VALID_EFFECT_TYPES = {
    "DealDamage", "DealAoEDamage", "Heal", "Draw", "GainNP", "Buff", "Debuff",
    "GrantShield", "Destroy", "Summon", "Move", "Exhaust", "AddKeyword",
    "SwitchPosition", "PreventBlock", "Repair"
}
VALID_TARGETS = {
    "Self", "Commander", "EnemyCommander", "AlliedActive", "EnemyActive",
    "AllAlliedActive", "AllEnemyActive", "Node", "EnemyNode", "Deck",
    "Discard", "Hand"
}
NEEDS_MAGNITUDE = {
    "DealDamage", "DealAoEDamage", "Heal", "Draw", "GainNP", "GrantShield",
    "Buff", "Debuff", "AddKeyword", "Repair", "Summon", "Move", "PreventBlock", "Exhaust"
}
REQUIRES_CARD = {"DealDamage", "DealAoEDamage", "Heal", "Destroy"}
NEEDS_REF = {"Summon", "Move", "AddKeyword"}
UNIT_ONLY_KEYWORDS = {"Piercing", "FirstStrike", "Lifesteal", "Shield", "SummoningDelay", "CannotBlock"}
PLAYER_TARGETS = {"Self", "Commander", "EnemyCommander"}

def validate_card(card, pool_ids):
    errors = []
    warnings = []
    infos = []
    cid = card.get("id", "???")

    # Identity
    if not card.get("id"):
        errors.append(f"{cid}: Id must be present.")
    else:
        m = ID_PATTERN.match(card["id"])
        if not m:
            warnings.append(f"{cid}: Id does not match expected format card_<faction>_<nnn>.")
        else:
            id_faction = m.group(1)
            card_faction = card.get("faction", "")
            if id_faction != card_faction:
                warnings.append(f"{cid}: Id faction '{id_faction}' != card faction '{card_faction}'.")

        # Uniqueness checked below

    if not card.get("name"):
        errors.append(f"{cid}: Name must be present.")

    if card.get("faction") not in VALID_FACTIONS:
        errors.append(f"{cid}: Invalid faction '{card.get('faction')}'.")

    if card.get("type") not in VALID_TYPES:
        errors.append(f"{cid}: Invalid type '{card.get('type')}'.")

    if card.get("rarity") not in VALID_RARITIES:
        errors.append(f"{cid}: Invalid rarity '{card.get('rarity')}'.")

    # Stats
    cost = card.get("cost", 0)
    atk = card.get("atk", 0)
    hp = card.get("hp", 0)
    shield = card.get("shield", 0)
    card_type = card.get("type", "")
    effects = card.get("effects", [])
    keywords = card.get("keywords", [])

    if cost < 0 or cost > MAX_COST:
        errors.append(f"{cid}: Cost {cost} out of range 0-{MAX_COST}.")
    if atk < 0:
        errors.append(f"{cid}: ATK cannot be negative.")
    if hp < 0:
        errors.append(f"{cid}: HP cannot be negative.")
    if shield < 0:
        errors.append(f"{cid}: Shield cannot be negative.")

    if card_type == "Unit":
        has_effects = len(effects) > 0
        if atk <= 0 and hp <= 0 and not has_effects:
            errors.append(f"{cid}: Unit must have Atk>0, Hp>0, or at least one effect.")
    elif card_type in ("Ability", "Relay", "Protocol"):
        if atk != 0 or hp != 0 or shield != 0:
            errors.append(f"{cid}: {card_type} must not have combat stats (atk={atk}, hp={hp}, shield={shield}).")

    # Effects
    has_any_fast_effect = False
    for i, eff in enumerate(effects):
        prefix = f"{cid} Effects[{i}]"
        etype = eff.get("type")
        etarget = eff.get("target")
        eamount = eff.get("amount", 0)
        eduration = eff.get("duration", 0)
        eref = eff.get("referenceId")
        efast = eff.get("fast", False)

        if etype not in VALID_EFFECT_TYPES:
            errors.append(f"{prefix}: Invalid EffectType '{etype}'.")
            continue
        if etarget not in VALID_TARGETS:
            errors.append(f"{prefix}: Invalid TargetKind '{etarget}'.")
            continue

        if eduration < -1:
            errors.append(f"{prefix}: Duration must be >= -1.")

        if etype in NEEDS_REF and not eref:
            errors.append(f"{prefix}: {etype} requires a non-empty referenceId.")

        if eref and pool_ids is not None and eref not in pool_ids:
            errors.append(f"{prefix}: referenceId '{eref}' does not exist in pool.")

        if etype in NEEDS_MAGNITUDE and eamount <= 0:
            errors.append(f"{prefix}: {etype} requires amount > 0, got {eamount}.")

        if etarget in PLAYER_TARGETS and etype in REQUIRES_CARD:
            errors.append(f"{prefix}: {etype} cannot target player target {etarget}.")

        if etype == "Repair":
            if etarget not in ("Self", "Commander"):
                errors.append(f"{prefix}: Repair only legal against Self or Commander, got {etarget}.")
            if eamount > MAX_REPAIR_AMOUNT:
                errors.append(f"{prefix}: Repair amount {eamount} exceeds max {MAX_REPAIR_AMOUNT}.")

        if efast:
            has_any_fast_effect = True

    has_fast_keyword = "Fast" in keywords
    if has_any_fast_effect and not has_fast_keyword:
        warnings.append(f"{cid}: Effect has Fast=true but card lacks Fast keyword.")

    # Keywords
    seen_kw = set()
    for kw in keywords:
        if kw in seen_kw:
            warnings.append(f"{cid}: Duplicate keyword {kw}.")
        seen_kw.add(kw)

        if kw not in VALID_KEYWORDS:
            errors.append(f"{cid}: Invalid keyword '{kw}'.")
            continue

        if kw == "Shield" and shield <= 0:
            errors.append(f"{cid}: Shield keyword requires shield > 0.")
        if kw == "Draw" and not any(e.get("type") == "Draw" for e in effects):
            errors.append(f"{cid}: Draw keyword requires Draw effect.")
        if kw == "Heal" and not any(e.get("type") == "Heal" for e in effects):
            errors.append(f"{cid}: Heal keyword requires Heal effect.")
        if kw == "Repair" and not any(e.get("type") == "Repair" for e in effects):
            errors.append(f"{cid}: Repair keyword requires Repair effect.")
        if kw == "AoE" and not any(e.get("type") == "DealAoEDamage" for e in effects):
            errors.append(f"{cid}: AoE keyword requires DealAoEDamage effect.")
        if kw == "Fast" and not has_any_fast_effect:
            errors.append(f"{cid}: Fast keyword requires at least one effect with fast=true.")

        if kw in ("Piercing", "FirstStrike", "Lifesteal", "CannotBlock", "SummoningDelay"):
            if atk <= 0 and hp <= 0:
                errors.append(f"{cid}: {kw} requires atk > 0 or hp > 0.")

        if kw in ("Activate", "Deploy", "Persistent", "Exhaust"):
            if len(effects) == 0:
                errors.append(f"{cid}: {kw} keyword requires at least one effect.")

        if card_type != "Unit" and kw in UNIT_ONLY_KEYWORDS:
            warnings.append(f"{cid}: {kw} does not apply to {card_type} (unit-only).")

    # Presentation
    has_mechanics = len(keywords) > 0 or len(effects) > 0
    if has_mechanics and not card.get("abilityText"):
        warnings.append(f"{cid}: Has mechanics but abilityText is empty.")
    if len(card.get("flavourText") or "") > MAX_FLAVOUR_LENGTH:
        warnings.append(f"{cid}: flavourText is {len(card['flavourText'])} characters; keep it to {MAX_FLAVOUR_LENGTH} or fewer.")
    if not card.get("artPath"):
        infos.append(f"{cid}: ArtPath is empty.")

    return errors, warnings, infos


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else r"E:\Games\Overlink\Archive\cards\card_pool.json"
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    cards = data.get("cards", [])
    pool_ids = {c["id"] for c in cards if c.get("id")}

    # Check uniqueness
    from collections import Counter
    id_counts = Counter(c["id"] for c in cards)
    dupes = {k: v for k, v in id_counts.items() if v > 1}
    if dupes:
        print(f"DUPLICATE IDS: {dupes}")

    all_errors = []
    all_warnings = []
    all_infos = []

    for card in cards:
        errs, warns, infos = validate_card(card, pool_ids)
        all_errors.extend(errs)
        all_warnings.extend(warns)
        all_infos.extend(infos)

    print(f"Validated {len(cards)} cards.")
    print(f"  Errors:   {len(all_errors)}")
    print(f"  Warnings: {len(all_warnings)}")
    print(f"  Infos:    {len(all_infos)}")

    if all_errors:
        print("\n=== ERRORS ===")
        for e in all_errors:
            print(f"  ERROR: {e}")

    if all_warnings:
        print("\n=== WARNINGS ===")
        for w in all_warnings:
            print(f"  WARN: {w}")

    if all_errors:
        sys.exit(1)
    else:
        print("\n✓ All cards pass validation (no errors).")
        sys.exit(0)


if __name__ == "__main__":
    main()
