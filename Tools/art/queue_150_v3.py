"""Queue ALL 150 Overlink cards to Hyperion at once, into a fresh folder.

- Output: renders_v3/<faction>/<card_id>/<card_id>_NN.png (never touches renders_v2).
- No skipping: every card gets 5 images from prompt_specs_v2.json.
- Phase 1: POST all 150 prompts. Prompt IDs saved to queue_150_v3_ids.json first.
- Phase 2: poll /history and download finished outputs. Failures -> queue_150_v3_failed.json.
- Refuses to run if renders_v3 already exists and is non-empty.
"""
import json
import random
import sys
import time
from pathlib import Path

import submit_overlink as s

ART = s.ART
OUT = ART / "renders_v3"
IDS_LOG = ART / "queue_150_v3_ids.json"
FAILED_LOG = ART / "queue_150_v3_failed.json"
TIMEOUT_S = 10 * 3600

if OUT.exists() and any(OUT.iterdir()):
    print("refusing: renders_v3 already has files", flush=True)
    sys.exit(1)

specs = json.loads((ART / "prompt_specs_v2.json").read_text(encoding="utf-8"))["cards"]
by_id = {c["id"]: c for c in specs}
print(f"cards to queue={len(specs)}", flush=True)

queued = {}
for c in specs:
    seed = random.randint(1, 2**31 - 1)
    prefix = f"overlink_v3/{c['faction']}/{c['id']}"
    resp = s.post("/prompt", {"prompt": s.build_workflow(c, seed, prefix), "client_id": "themis-v3"})
    if resp.get("node_errors") or "prompt_id" not in resp:
        print("REJECTED", c["id"], json.dumps(resp)[:500], flush=True)
        sys.exit(1)
    queued[c["id"]] = resp["prompt_id"]
IDS_LOG.write_text(json.dumps(queued, indent=2), encoding="utf-8")
print(f"queued {len(queued)} prompts; ids saved to {IDS_LOG.name}", flush=True)

pending = dict(queued)
failed = {}
done_count = 0
t0 = time.time()
while pending and time.time() - t0 < TIMEOUT_S:
    for cid, pid in list(pending.items()):
        h = s.history(pid)
        if h.get("status", {}).get("status_str") == "error":
            failed[cid] = pid
            del pending[cid]
            print("FAILED", cid, pid, flush=True)
            continue
        if not h.get("outputs"):
            continue
        c = by_id[cid]
        folder = OUT / c["faction"] / cid
        folder.mkdir(parents=True, exist_ok=True)
        imgs = [img for node in h["outputs"].values() for img in node.get("images", [])]
        for i, img in enumerate(imgs):
            s.download(img["filename"], img.get("subfolder", ""), folder / f"{cid}_{i:02d}.png")
        done_count += 1
        del pending[cid]
        print(f"done {cid} ({done_count}/{len(queued)}) images={len(imgs)}", flush=True)
    if failed:
        FAILED_LOG.write_text(json.dumps(failed, indent=2), encoding="utf-8")
    time.sleep(10)

print(f"FINISHED done={done_count} failed={len(failed)} still_pending={len(pending)}", flush=True)
