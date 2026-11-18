#!/usr/bin/env python3
"""Enforce Master's rule: card names must be <= 2 words.
Renames the 19 over-limit cards, checks uniqueness, revalidates in place.
Dry-run by default; pass --apply to write.
"""
import json
import re
import sys

POOL = r"E:\Games\Overlink\Assets\Cards\card_pool.json"

RENAME = {
    "card_fpb_041": "Exhibition Guard",
    "card_fpb_048": "Director Vasari",
    "card_frti_003": "Frame Hauler",
    "card_frti_014": "Boiler Guard",
    "card_frti_027": "Press Operator",
    "card_frti_028": "Furnace Tender",
    "card_frti_033": "Engine Guardian",
    "card_frti_034": "Railgun Prototype",
    "card_frti_041": "Siege Engine",
    "card_frti_042": "Lifter Mech",
    "card_frti_048": "Director Kaelin",
    "card_frti_049": "Grand Forge",
    "card_fsti_002": "Relay Operator",
    "card_fsti_003": "Data Clerk",
    "card_fsti_028": "Accelerator Tech",
    "card_fsti_041": "Entanglement Specialist",
    "card_fsti_046": "Learning Cycle",
    "card_fsti_049": "Dr. Lumen",
    "card_fsti_050": "Nexus Core",
}


def word_count(name):
    toks = [t for t in re.split(r"[\s]+", name.replace(".", " ").replace("-", " ")) if t.strip()]
    return len(toks)


with open(POOL, "r", encoding="utf-8") as f:
    data = json.load(f)

cards = data["cards"]
by_id = {c["id"]: c for c in cards}

# 1. All rename targets must exist
missing = [cid for cid in RENAME if cid not in by_id]
assert not missing, f"Rename targets missing from pool: {missing}"

# 2. Every new name must be <= 2 words
bad = [(cid, n, word_count(n)) for cid, n in RENAME.items() if word_count(n) > 2]
assert not bad, f"New names still over 2 words: {bad}"

# 3. Build the post-rename name set and check global uniqueness
new_names = {}
for c in cards:
    new_names[c["id"]] = RENAME.get(c["id"], c["name"])

seen = {}
dupes = []
for cid, nm in new_names.items():
    key = nm.lower()
    if key in seen:
        dupes.append((nm, seen[key], cid))
    else:
        seen[key] = cid
assert not dupes, f"Name collisions after rename: {dupes}"

# 4. Confirm no OTHER card in the pool is over 2 words (sanity)
still_over = [(cid, nm, word_count(nm)) for cid, nm in new_names.items() if word_count(nm) > 2]
assert not still_over, f"Cards still over 2 words: {still_over}"

print(f"Rename map OK: {len(RENAME)} renames, all targets exist, all <=2 words.")
print(f"Post-rename uniqueness OK: {len(set(n.lower() for n in new_names.values()))} unique names / {len(cards)} cards.")
print(f"No card over 2 words after rename.")
print()
print("Changes:")
for cid in sorted(RENAME):
    print(f"  {cid}: '{by_id[cid]['name']}'  ->  '{RENAME[cid]}'")

if "--apply" in sys.argv:
    for cid, nm in RENAME.items():
        by_id[cid]["name"] = nm
    with open(POOL, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("\nAPPLIED — card_pool.json rewritten.")
else:
    print("\n(dry run — pass --apply to write)")
