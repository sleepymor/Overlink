#!/usr/bin/env python3
"""Assert every composed prompt spec is safe to send to ComfyUI.

Checks per card (all must pass):
  1. coverage      - 150 specs, ids exactly match card_pool.json, unique prefixes
  2. banned tokens - none of the six verified-harmful landscape composition tokens
                     appear anywhere in the positive prompt
  3. cowboy shot   - absent from EVERY positive (removed globally from the shared
                     subject_front_load table; it is portrait framing and no separate
                     portrait front-load exists)
  4. dims          - Standard -> [1400, 768]; Legendary -> [832, 1216]; short side >= 768
  5. props         - at most max_named_props (3) named background props
  6. checkpoint    - human->animagine-xl-3.1 + LoRA 1.5/1.0; mecha/effect->illustrious-xl
                     + LoRA 0.0/0.0
  7. style tokens  - every required style token for the dims mode is present
  8. negatives     - mandatory guard present verbatim; landscape-collapse guard present
                     for non-Legendary character cards; effect drift guard for effect cards
  9. effect anchor - every effect-class card names a focal object in its positive
 10. sampler       - euler_ancestral / 35 steps / cfg 7 on every spec
 11. human words   - mecha/effect cards must not name humans in focal/props
                     (contradicts the (no humans:x) front-load; Mneme review 2026-11-13)

Run: python Tools/art/verify_prompts.py  (exit 0 = all green)
"""

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SPECS = os.path.join(HERE, "prompt_specs.json")
POOL = os.path.join(REPO, "Assets", "Cards", "card_pool.json")

sys.path.insert(0, HERE)
from art_bible import ART_BIBLE  # noqa: E402

C = ART_BIBLE["comfyui"]
BANNED = [t.lower() for t in C["banned_style_tokens"]]
MAX_PROPS = C["max_named_props"]
HUMAN_CKPT = C["checkpoints"]["human"]["model"]
MECHA_CKPT = C["checkpoints"]["mecha"]["model"]
HUMAN_WORDS_RE = re.compile(
    r"\b(artisans?|hands?|people|person|men|women|man|woman|girls?|boys?|workers?|crews?|figures?)\b"
)

fail = []
warn = []


def err(msg):
    fail.append(msg)


def toks(text):
    return [t.strip().lower() for t in text.split(",") if t.strip()]


def main():
    if not os.path.exists(SPECS):
        print(f"FAIL - {SPECS} not found; run compose_prompts.py first")
        return 1
    with open(SPECS, "r", encoding="utf-8") as f:
        data = json.load(f)
    with open(POOL, "r", encoding="utf-8") as f:
        pool = json.load(f)

    specs = data["cards"]
    pool_by_id = {c["id"]: c for c in pool["cards"]}

    # 1. coverage
    ids = [s["id"] for s in specs]
    if len(specs) != 150:
        err(f"coverage: expected 150 specs, got {len(specs)}")
    missing = set(pool_by_id) - set(ids)
    extra = set(ids) - set(pool_by_id)
    if missing:
        err(f"coverage: missing ids {sorted(missing)[:5]}...")
    if extra:
        err(f"coverage: ids not in pool {sorted(extra)[:5]}...")
    prefixes = [s["prefix"] for s in specs]
    if len(set(prefixes)) != len(prefixes):
        err("coverage: duplicate prefix")

    for s in specs:
        cid = s["id"]
        pos_l = s["positive"].lower()
        neg_l = s["negative"].lower()
        pos_toks = toks(pos_l)
        neg_toks = toks(neg_l)
        pc = pool_by_id.get(cid, {})
        legendary = s["rarity"] == "Legendary"

        if s["faction"] != pc.get("faction") or s["type"] != pc.get("type") \
                or s["rarity"] != pc.get("rarity"):
            err(f"{cid}: spec metadata disagrees with pool")

        # 2. banned tokens
        hit = [b for b in BANNED if b in pos_l]
        if hit:
            err(f"{cid}: banned style token(s) in positive: {hit}")

        # 3. cowboy shot removed globally from the shared front-load table
        if "cowboy shot" in pos_l:
            err(f"{cid}: 'cowboy shot' in positive (removed globally from front-loads)")

        # 4. dims
        want = [832, 1216] if legendary else [1400, 768]
        if list(s["dims"]) != want:
            err(f"{cid}: dims {s['dims']} != {want} (mode {s['dims_mode']})")
        if min(s["dims"]) < 768:
            err(f"{cid}: short side {min(s['dims'])} < 768 px SDXL heuristic")
        if s["dims_mode"] != ("Legendary" if legendary else "Standard"):
            err(f"{cid}: dims_mode mismatch")

        # 5. props
        if len(s["props"]) > MAX_PROPS:
            err(f"{cid}: {len(s['props'])} props > cap {MAX_PROPS}")

        # 6. checkpoint / LoRA
        human = s["subject_class"] in ("human", "male_human")
        if human:
            if s["checkpoint"] != HUMAN_CKPT:
                err(f"{cid}: human subject on {s['checkpoint']}, expected {HUMAN_CKPT}")
            if (s["lora_strength_model"], s["lora_strength_clip"]) != (1.5, 1.0):
                err(f"{cid}: human LoRA {s['lora_strength_model']}/{s['lora_strength_clip']} != 1.5/1.0")
            if "(1girl:" not in pos_l and "(1boy:" not in pos_l:
                err(f"{cid}: human front-load weight token missing")
        else:
            if s["checkpoint"] != MECHA_CKPT:
                err(f"{cid}: {s['subject_class']} on {s['checkpoint']}, expected {MECHA_CKPT}")
            if (s["lora_strength_model"], s["lora_strength_clip"]) != (0.0, 0.0):
                err(f"{cid}: {s['subject_class']} LoRA must be 0.0/0.0")
            if "(no humans:" not in pos_l:
                err(f"{cid}: non-human front-load missing 'no humans' weight token")

        # 7. required style tokens
        req = C["required_style_tokens_portrait"] if legendary \
            else C["required_style_tokens_landscape"]
        miss = [t for t in req if t.lower() not in pos_toks]
        if miss:
            err(f"{cid}: missing required style tokens {miss}")

        # 8. negatives
        mand = toks(C["negative_mandatory"])
        miss_mand = [t for t in mand if t not in neg_toks]
        if miss_mand:
            err(f"{cid}: negative missing mandatory tokens {miss_mand[:4]}...")
        if not legendary and s["subject_class"] != "effect":
            guard = toks(C["negative_landscape_collapse"])
            miss_g = [t for t in guard if t not in neg_toks]
            if miss_g:
                err(f"{cid}: negative missing landscape-collapse guard {miss_g}")
        if s["subject_class"] == "effect":
            drift = toks(C["negative_effect_drift"])
            miss_d = [t for t in drift if t not in neg_toks]
            if miss_d:
                err(f"{cid}: effect negative missing drift guard {miss_d}")
            # contradictory tokens must have been stripped from the collapse guard
            for bad in ("no humans", "no character"):
                if bad in neg_toks:
                    err(f"{cid}: effect negative contains '{bad}' contradicting its positive")
        if s["subject_class"] in ("human", "male_human"):
            if "bad hands" not in neg_toks:
                err(f"{cid}: human negative missing hand-error guard")
        elif s["subject_class"] == "mecha":
            if "organic body" not in neg_toks:
                err(f"{cid}: mecha negative missing organic-drift guard")

        # 9. effect anchor
        if s["subject_class"] == "effect":
            if not s["focal"].strip():
                err(f"{cid}: effect card has no focal object")
            struct = toks(C["effect_structure_tokens"])
            miss_s = [t for t in struct if t not in pos_toks]
            if miss_s:
                err(f"{cid}: effect positive missing structure tokens {miss_s}")

        # 10. sampler
        if (s["sampler"], s["steps"], s["cfg"]) != (C["sampler"]["name"], 35, 7):
            err(f"{cid}: sampler {s['sampler']}/{s['steps']}/{s['cfg']} != euler_ancestral/35/7")

        # 11. mecha/effect focal+props must not name humans (contradicts no-humans front-load)
        if s["subject_class"] in ("mecha", "effect"):
            blob = (s["focal"] + " , " + ", ".join(s["props"])).lower()
            m = HUMAN_WORDS_RE.search(blob)
            if m:
                err(f"{cid}: {s['subject_class']} focal/props name a human ('{m.group(0)}') "
                    f"contradicting the (no humans) front-load")

        if s["prefix"] != f"overlink_{cid}":
            err(f"{cid}: prefix {s['prefix']} != overlink_{cid}")

    # composition summary
    from collections import Counter
    by_class = Counter(s["subject_class"] for s in specs)
    by_mode = Counter(s["dims_mode"] for s in specs)
    by_ckpt = Counter(s["checkpoint"] for s in specs)
    print(f"specs: {len(specs)}  subject_class: {dict(by_class)}")
    print(f"dims_mode: {dict(by_mode)}  checkpoint: {dict(by_ckpt)}")
    print(f"positive length: min {min(len(s['positive']) for s in specs)}, "
          f"max {max(len(s['positive']) for s in specs)} chars")

    if warn:
        print(f"\nWARNINGS ({len(warn)}):")
        for w in warn[:20]:
            print(" -", w)
    if fail:
        print(f"\nFAILED ({len(fail)}):")
        for f_ in fail:
            print(" -", f_)
        return 1
    print("\nALL GREEN - every assertion passed for all 150 specs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
