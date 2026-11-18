#!/usr/bin/env python3
import json
d = json.load(open(r"E:\Games\Overlink\Assets\Cards\card_pool.json", encoding="utf-8"))
with open(r"E:\Games\Overlink\Tools\cards_dump.txt", "w", encoding="utf-8") as f:
    for c in d["cards"]:
        kw = ",".join(c.get("keywords", [])) or "-"
        ab = (c.get("abilityText") or "-").replace("\n", " ")
        st = f"{c['atk']}/{c['hp']}" + (f"+{c['shield']}" if c["shield"] else "")
        f.write(f"{c['id']}|{c['name']}|{c['faction']}|{c['rarity']}|{c['type']}|{c['cost']}|{st}|{kw}|{ab}\n")
print("wrote", len(d["cards"]), "lines")
