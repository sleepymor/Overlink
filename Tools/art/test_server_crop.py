"""Queue ONE horizontal card through the server-side ImageScale+ImageCrop path.

Writes to scratch_server_crop/ only (never renders_v2/). Verifies the size of what
ComfyUI itself produced, without any Tethys-side crop.
"""
import json
import random
import urllib.request
from pathlib import Path

from PIL import Image

import submit_overlink as s

ART = Path(__file__).resolve().parent
OUT = ART / "scratch_server_crop"
CID = "card_fsti_003"

spec = [c for c in json.loads((ART / "prompt_specs_v2.json").read_text(encoding="utf-8"))["cards"]
        if c["id"] == CID][0]
wf = s.build_workflow(spec, random.randint(1, 2**31 - 1), f"scratch/{CID}")
resp = s.post("/prompt", {"prompt": wf, "client_id": "themis-test"})
pid = resp["prompt_id"]
print("queued", CID, pid)
h = s.wait_for(pid)
imgs = [img for node in h["outputs"].values() for img in node.get("images", [])]
OUT.mkdir(exist_ok=True)
for i, img in enumerate(imgs):
    dest = OUT / f"{CID}_server_{i:02d}.png"
    s.download(img["filename"], img.get("subfolder", ""), dest)
    print("server output", dest.name, Image.open(dest).size)
print("count", len(imgs), "expected size (1024, 640)")
