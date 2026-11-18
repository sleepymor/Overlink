"""Build prompt_specs_v2.json from prompt_specs.json (source is never modified).

Changes vs v1:
- Horizontal cards: generate at 1344x768, crop to 1024x640 after generation.
- Vertical cards: generate at 832x1216 (native), no crop.
- batch_size = 5 images per card.
- Prompts, checkpoints, LoRA strengths, sampler, steps, cfg, props are copied unchanged.
"""
import json
import shutil
from pathlib import Path

ART = Path(r"E:\Games\Overlink\Tools\art")
SRC = ART / "prompt_specs.json"
DST = ART / "prompt_specs_v2.json"

HORIZ_GEN = [1344, 768]
HORIZ_CROP = [1024, 640]
VERT_GEN = [832, 1216]
BATCH = 5

if DST.exists():
    raise SystemExit(f"Refusing to overwrite existing {DST}")

data = json.loads(SRC.read_text(encoding="utf-8"))
cards = data["cards"]
out = []
counts = {"horizontal": 0, "vertical": 0}
for c in cards:
    c2 = dict(c)
    w, h = c["dims"]
    if w > h:
        c2["orientation"] = "horizontal"
        c2["dims"] = list(HORIZ_GEN)
        c2["crop"] = list(HORIZ_CROP)
        counts["horizontal"] += 1
    else:
        c2["orientation"] = "vertical"
        c2["dims"] = list(VERT_GEN)
        c2["crop"] = None
        counts["vertical"] += 1
    c2["batch_size"] = BATCH
    c2["v1_dims"] = c["dims"]
    out.append(c2)

new = dict(data)
new["cards"] = out
new["count"] = len(out)
new["source"] = str(SRC.name)
new["version"] = "v2"
new["notes"] = "Horizontal: gen 1344x768 -> crop 1024x640. Vertical: 832x1216. batch_size 5 per card."

DST.write_text(json.dumps(new, ensure_ascii=False, indent=2), encoding="utf-8")
print("wrote", DST)
print("cards", len(out), counts)
print("total images", len(out) * BATCH)
