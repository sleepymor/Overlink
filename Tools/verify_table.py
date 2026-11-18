#!/usr/bin/env python3
"""Verify the rendered inspection table has the appearance section/column complete."""
import re

md = open(r"E:\Games\Overlink\Tools\card_pool_table.md", encoding="utf-8").read()
assert "# Appearance Descriptions" in md, "MD: appearance section header missing"
after = md.split("# Appearance Descriptions", 1)[1]
ids_md = set(re.findall(r"card_(?:fsti|frti|fpb)_\d{3}", after))
print("MD appearance section ids:", len(ids_md))

h = open(r"E:\Games\Overlink\Tools\card_pool_table.html", encoding="utf-8").read()
n_appear = h.count('class="appear"')
print("HTML appear cells:", n_appear)
n_dash = h.count('<td class="appear">—</td>')
print("HTML appear cells with em-dash fallback:", n_dash)
assert n_appear == 150, f"expected 150 appear cells, got {n_appear}"
assert n_dash == 0, "some cards missing appearance"

import json
d = json.load(open(r"E:\Games\Overlink\Assets\Cards\card_pool.json", encoding="utf-8"))
pool_ids = {c["id"] for c in d["cards"]}
assert ids_md == pool_ids, f"MD section mismatch: missing={pool_ids - ids_md} extra={ids_md - pool_ids}"
print("OK — all 150 ids covered in both MD section and HTML column.")
