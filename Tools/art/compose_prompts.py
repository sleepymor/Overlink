#!/usr/bin/env python3
"""Compose per-card ComfyUI prompt specs for the 150-card Overlink pool.

Merges three sources into one spec per card:
  1. Tools/art/art_bible.py          - shared cohesion (axes, factions, rarity,
                                        type conventions, ComfyUI technical spec)
  2. Assets/Cards/card_pool.json     - id/name/faction/rarity/type (source of truth)
  3. Tools/art/descriptions_*.py     - per-card props (<=3), focal object, overrides
  4. Tools/card_pool_appearances.md  - one-line concept text (human-readable, carried
                                        into the spec as "concept" for inspection)

Output: Tools/art/prompt_specs.json

Subject class defaults (verified pipeline, MEMORY.md normal-card presets):
    Unit     -> human  (animagine-xl-3.1, ClearHand LoRA 1.5/1.0, (1girl:1.2) front-load)
    Relay    -> mecha  (illustrious-xl-v0.1, LoRA 0.0/0.0)
    Ability  -> effect (illustrious-xl-v0.1, LoRA 0.0/0.0, structure tokens + focal object)
    Protocol -> effect (same as Ability)
Override per card with sc="human"|"mecha"|"effect" in the description entry.

All human-subject cards default to the female front-load token; add
gender="male_human" to a description entry to flip a specific card.

Dims per rarity (from CardFaceView.cs geometry, verified in art bible):
    Common/Rare/Epic -> Standard art window 700x385 (1.818) -> generate 1400x768
    Legendary        -> FullArt 832x1216 portrait            -> generate 832x1216

Art style: TORINO AQUA on ALL card types, expressed as RENDERING language only
(painterly shading, rim light, lineart) - never composition language. The six
banned composition tokens are asserted absent by verify_prompts.py.
"""

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
POOL = os.path.join(REPO, "Assets", "Cards", "card_pool.json")
APPEARANCES = os.path.join(REPO, "Tools", "card_pool_appearances.md")
OUT = os.path.join(HERE, "prompt_specs.json")

sys.path.insert(0, HERE)
from art_bible import ART_BIBLE  # noqa: E402
from descriptions_fsti import DESCRIPTIONS as D_FSTI  # noqa: E402
from descriptions_frti import DESCRIPTIONS as D_FRTI  # noqa: E402
from descriptions_fpb import DESCRIPTIONS as D_FPB  # noqa: E402

DESC = {}
DESC.update(D_FSTI)
DESC.update(D_FRTI)
DESC.update(D_FPB)

C = ART_BIBLE["comfyui"]
TYPE_DEFAULT_CLASS = {"Unit": "human", "Ability": "effect", "Relay": "mecha", "Protocol": "effect"}

# The landscape-collapse guard is written to protect character cards. Four of
# its tokens ("no character, no humans, empty scene, landscape only") contradict
# an effect card's own positive, so effect cards get the guard minus those four.
EFFECT_GUARD_STRIP = {"no character", "no humans", "empty scene", "landscape only"}

QUALITY_HEADER = (
    "masterpiece, best quality, newest, absurdres, highres, ultra-detailed, "
    "gacha splash art, official art"
)


def dedupe_tokens(text):
    """Comma-split, strip, case-insensitive dedupe preserving first occurrence."""
    out, seen = [], set()
    for tok in text.split(","):
        t = tok.strip()
        if not t:
            continue
        key = t.lower()
        if key not in seen:
            seen.add(key)
            out.append(t)
    return ", ".join(out)


def strip_tokens(text, drop):
    keep = [t.strip() for t in text.split(",") if t.strip() and t.strip().lower() not in drop]
    return ", ".join(keep)


def load_appearances():
    by_id = {}
    with open(APPEARANCES, "r", encoding="utf-8") as f:
        for ln in f:
            if not ln.startswith("| `card_"):
                continue
            parts = [p.strip() for p in ln.strip().strip("|").split("|")]
            if len(parts) >= 3:
                by_id[parts[0].strip("`")] = parts[2]
    return by_id


def compose(card, desc, concept):
    cid = card["id"]
    rarity = card["rarity"]
    faction = ART_BIBLE["factions"][card["faction"]]
    sc = desc.get("sc") or TYPE_DEFAULT_CLASS[card["type"]]
    assert sc in ("human", "male_human", "mecha", "effect"), f"{cid}: bad subject class {sc}"
    gender = desc.get("gender", "human" if sc == "human" else None)
    front_key = sc if sc != "human" else gender  # human -> human|male_human

    props = desc.get("props", [])
    assert len(props) <= C["max_named_props"], f"{cid}: {len(props)} props exceeds cap {C['max_named_props']}"

    legendary = rarity == "Legendary"
    dims_key = "Legendary" if legendary else "Standard"
    dims = C["dims"][dims_key]["generate"]
    required_style = (
        C["required_style_tokens_portrait"] if legendary
        else C["required_style_tokens_landscape"]
    )

    rar = ART_BIBLE["rarities"][rarity]

    # ------------------------------------------------------------- positive
    parts = [QUALITY_HEADER]
    parts.append(C["subject_front_load"][front_key if front_key in C["subject_front_load"] else sc])
    if sc == "effect":
        parts.append(C["effect_structure_tokens"])
        if desc.get("focal"):
            parts.append(desc["focal"])
    else:
        if desc.get("focal"):
            parts.append(desc["focal"])
    if props:
        parts.append(", ".join(props))
    if desc.get("focus"):
        parts.append(desc["focus"])
    parts.append(faction["style_tokens"])
    parts.append(rar["style_tokens"])
    parts.append(", ".join(required_style))
    positive = dedupe_tokens(", ".join(parts))

    # ------------------------------------------------------------- negative
    neg = [C["negative_mandatory"]]
    if not legendary:
        if sc == "effect":
            neg.append(strip_tokens(C["negative_landscape_collapse"], EFFECT_GUARD_STRIP))
            neg.append(C["negative_effect_drift"])
        else:
            neg.append(C["negative_landscape_collapse"])
    if sc in ("human", "male_human"):
        neg.append(C["negative_human"])
    elif sc == "mecha":
        neg.append(C["negative_mecha"])
    elif sc == "effect":
        neg.append(C["negative_effect_drift"])
    negative = dedupe_tokens(", ".join(neg))

    cp_key = {"human": "human", "male_human": "human", "mecha": "mecha", "effect": "effect"}[sc]
    cp = C["checkpoints"][cp_key]

    return {
        "id": cid,
        "name": card["name"],
        "faction": card["faction"],
        "rarity": rarity,
        "type": card["type"],
        "subject_class": sc,
        "concept": concept,
        "dims": dims,
        "dims_mode": dims_key,
        "checkpoint": cp["model"],
        "lora_strength_model": cp["lora_strength_model"],
        "lora_strength_clip": cp["lora_strength_clip"],
        "sampler": C["sampler"]["name"],
        "steps": C["sampler"]["steps"],
        "cfg": C["sampler"]["cfg"],
        "prefix": f"overlink_{cid}",
        "props": props,
        "focal": desc.get("focal", ""),
        "positive": positive,
        "negative": negative,
    }


def main():
    with open(POOL, "r", encoding="utf-8") as f:
        pool = json.load(f)
    appearances = load_appearances()

    specs, problems = [], []
    for card in pool["cards"]:
        cid = card["id"]
        desc = DESC.get(cid)
        if desc is None:
            problems.append(f"{cid}: no description entry")
            continue
        if cid not in appearances:
            problems.append(f"{cid}: no appearance concept line")
        specs.append(compose(card, desc, appearances.get(cid, "")))

    extra = set(DESC) - {c["id"] for c in pool["cards"]}
    for x in sorted(extra):
        problems.append(f"{x}: description entry not in pool")

    if problems:
        print("PROBLEMS:")
        for p in problems:
            print(" -", p)
        sys.exit(1)

    assert len(specs) == 150, f"expected 150 specs, got {len(specs)}"
    prefixes = [s["prefix"] for s in specs]
    assert len(set(prefixes)) == 150, "prefix collision"

    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"source": os.path.relpath(POOL, REPO).replace("\\", "/"),
                   "art_bible": "Tools/art/art_bible.py",
                   "count": len(specs), "cards": specs}, f, indent=2, ensure_ascii=False)
    print(f"OK - wrote {len(specs)} specs to {OUT} ({os.path.getsize(OUT)} bytes)")


if __name__ == "__main__":
    main()
