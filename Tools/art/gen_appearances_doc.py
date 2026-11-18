#!/usr/bin/env python3
"""Generate a detailed per-card art-direction document from real card data.

Sources:
  - Assets/Cards/card_pool.json : id/name/faction/rarity/type/cost/atk/hp/shield/
                                  keywords/abilityText/designNote
  - Tools/art/descriptions_*.py : props/focal/subject class

Output: Tools/art/card_pool_appearances.md (per-card sections, not a giant table)

Design rule: this is a DESIGN DOCUMENT ONLY. The SDXL positive prompt keeps at most
3 named props per card (descriptions_*.py untouched); the verified prompt pipeline
(compose_prompts.py / verify_prompts.py) is NOT changed by this document.
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
POOL = os.path.join(REPO, "Assets", "Cards", "card_pool.json")
OUT = os.path.join(HERE, "card_pool_appearances.md")

sys.path.insert(0, HERE)
from descriptions_fsti import DESCRIPTIONS as D_FSTI
from descriptions_frti import DESCRIPTIONS as D_FRTI
from descriptions_fpb import DESCRIPTIONS as D_FPB

DESC = {}
DESC.update(D_FSTI)
DESC.update(D_FRTI)
DESC.update(D_FPB)

FACTION_TITLE = {
    "fsti": "FSTI — Frontier Science & Technology Institute",
    "frti": "FRTI — Frontier Research & Technology Institute",
    "fpb": "FPB — Frontier Planning Bureau",
}
FACTION_SUBHEAD = {
    "fsti": "*(clean labs, holographic displays, sensors, data streams — cool blues, whites, glass, chrome)*",
    "frti": "*(heavy industry, forge and steel, machinery, combustion — hot oranges, iron greys, sparks, rivets)*",
    "fpb": "*(drafting studios, blueprints, conservation and restoration, galleries — warm paper tones, ink, gilt, protective coatings)*",
}
RARITY_ORDER = ["Common", "Rare", "Epic", "Legendary"]

TYPE_DEFAULT_CLASS = {"Unit": "human", "Ability": "effect", "Relay": "mecha", "Protocol": "effect"}

FACTION_WARDROBE = {
    "fsti": "white coat over workwear, safety goggles pushed up on the forehead",
    "frti": "heavy work apron, heat-resistant gloves, soot-streaked face",
    "fpb": "ink-stained hands, roll-neck sweater, drafter's apron pockets",
}

FACTION_LIGHTING = {
    "fsti": "cool overhead lab light, holographic blue rim, glass-and-chrome background",
    "frti": "forge glow, orange rim light on iron, sparks in the air",
    "fpb": "warm parchment light, gilt accents, drafting table or gallery wall behind",
}


def rarity_tone(rarity):
    return {
        "Common": "plain / workwear / utilitarian",
        "Rare": "refined / specialist / reinforced",
        "Epic": "authoritative / premium / commanding",
        "Legendary": "gilded / iconic / monumental",
    }[rarity]


def stat_line(c):
    parts = [f"cost {c['cost']}"]
    if c["type"] == "Unit":
        parts += [f"atk {c['atk']}", f"hp {c['hp']}", f"shield {c['shield']}"]
    kw = ", ".join(c["keywords"]) if c["keywords"] else "-"
    parts.append(f"keywords: {kw}")
    return " | ".join(parts)


def wardrobe_brief(c, desc):
    """Per-card wardrobe: grounded in faction, type, subject class, cost."""
    sc = desc.get("sc") or TYPE_DEFAULT_CLASS[c["type"]]
    if sc in ("mecha", "effect"):
        return "— no wardrobe (subject is a machine or effect; no humans)"
    base = FACTION_WARDROBE[c["faction"]]
    if c["cost"] >= 6:
        base = base.replace("work apron", "reinforced work apron").replace(
            "roll-neck sweater", "command roll-neck"
        )
    if c["type"] == "Unit" and c["hp"] >= 5:
        base += ", braced posture"
    if "Shield" in c["keywords"]:
        base += ", shield-ready stance"
    return base + "."


def pose_brief(c, desc):
    sc = desc.get("sc") or TYPE_DEFAULT_CLASS[c["type"]]
    if sc == "mecha":
        return "structure-centred, object fills the frame, no humans."
    if sc == "effect":
        return f"captured mid-effect — {desc.get('focal', 'the effect source')} in action."
    if c["type"] == "Unit":
        if c["atk"] >= 4:
            return "aggressive combat stance, weight forward, ready to strike."
        if c["hp"] >= 5:
            return "braced, defensive, immovable presence."
        return "steady working stance, alert."
    return "tool or protocol in operation."


def lighting_brief(c, desc):
    base = FACTION_LIGHTING[c["faction"]]
    if c["rarity"] == "Legendary":
        return base + ", dramatic rim light, gilded edges, monumental scale."
    if c["rarity"] == "Epic":
        return base + ", premium materials, commanding presence."
    if c["rarity"] == "Rare":
        return base + ", clean professional composition."
    return base + ", flat utilitarian composition."


def props_line(desc):
    props = desc.get("props", [])
    if not props:
        return "—"
    return "; ".join(props) + "."


def card_block(c, desc):
    sc = desc.get("sc") or TYPE_DEFAULT_CLASS[c["type"]]
    lines = []
    lines.append(f"#### {c['name']} (`{c['id']}`)")
    lines.append("")
    lines.append(f"{c['faction'].upper()} · {c['rarity']} · {c['type']} · {stat_line(c)}")
    lines.append("")
    ab = c["abilityText"] or "— (stats body only, no activated ability)"
    lines.append(f"**Ability:** {ab}")
    lines.append("")
    dn = c.get("designNote") or "—"
    lines.append(f"**Design intent:** {dn}")
    lines.append("")
    lines.append("**Art direction:**")
    lines.append("")
    focal = desc.get("focal", "")
    if sc == "mecha":
        subj = f"Mecha — {focal if focal else 'structure-centred machine subject, no humans'}"
    elif sc == "effect":
        subj = f"Effect — {focal}"
    else:
        subj = "Human — focus on the person, clear face, stance suited to the card's function."
    lines.append(f"- **Subject:** {subj}")
    lines.append(f"- **Wardrobe:** {wardrobe_brief(c, desc)}")
    lines.append(f"- **Pose:** {pose_brief(c, desc)}")
    lines.append(f"- **Lighting / background:** {lighting_brief(c, desc)}")
    lines.append(f"- **Prompt props (≤3 cap):** {props_line(desc)}")
    lines.append(f"- **Rarity cue:** {rarity_tone(c['rarity'])}")
    lines.append("")
    return lines


def main():
    with open(POOL, "r", encoding="utf-8") as f:
        pool = json.load(f)

    cards = pool["cards"]
    missing = [c["id"] for c in cards if c["id"] not in DESC]
    extra = sorted(set(DESC) - {c["id"] for c in cards})
    if missing:
        print(f"WARNING: {len(missing)} cards lack description entries: {missing[:5]}...")
    if extra:
        print(f"WARNING: {len(extra)} description entries not in pool: {extra[:5]}...")

    by_faction = {}
    for c in cards:
        by_faction.setdefault(c["faction"], []).append(c)

    out = ["# Overlink — Card Pool Appearance Descriptions",
           "",
           "Visual reference for the 150-card pool (`Assets/Cards/card_pool.json`), grouped by faction.",
           "One section per card with grounded art direction drawn from the card's real stats,",
           "ability text, design note, and the prompt subject/focal/props from the art pipeline",
           "descriptions. Rarity cues escalate with cost/power (Common = plain/workwear,",
           "Legendary = gilded/iconic).",
           "",
           "This is a DESIGN DOCUMENT ONLY. The SDXL positive prompt itself keeps **at most",
           "3 named props** per card (`Tools/art/descriptions_*.py`); this document is not",
           "fed to the generator and the verified prompt pipeline is unchanged.",
           ""]

    for fac in ("fsti", "frti", "fpb"):
        out.append(f"## {FACTION_TITLE[fac]}")
        out.append("")
        out.append(FACTION_SUBHEAD[fac])
        out.append("")
        for rarity in RARITY_ORDER:
            group = [c for c in by_faction[fac] if c["rarity"] == rarity]
            if not group:
                continue
            out.append(f"### {rarity}")
            out.append("")
            for c in group:
                out.extend(card_block(c, DESC.get(c["id"], {})))

    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(out))
    print(f"OK — wrote {OUT} ({os.path.getsize(OUT)} bytes), {len(cards)} cards")


if __name__ == "__main__":
    main()
