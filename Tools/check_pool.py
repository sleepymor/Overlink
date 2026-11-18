import json
d = json.load(open(r"E:\Games\Overlink\Assets\Cards\card_pool.json", "r", encoding="utf-8"))
cards = d["cards"]
print(f"Total: {len(cards)}")
ids = set()
dupes = []
for c in cards:
    if c["id"] in ids:
        dupes.append(c["id"])
    ids.add(c["id"])
print(f"Unique IDs: {len(ids)}, Dupes: {dupes}")
names = set(c["name"] for c in cards)
print(f"Unique names: {len(names)}")
if len(names) < len(cards):
    from collections import Counter
    nc = Counter(c["name"] for c in cards)
    print("Duplicate names:", {k:v for k,v in nc.items() if v > 1})
