"""Queue ALL remaining Overlink cards to Hyperion ComfyUI at once, then collect results.

- Source: prompt_specs_v2.json (150 cards, 5 images each, server-side resize/crop).
- Skips any card that already has 5 renders in renders_v2/<faction>/<card_id>/.
- Phase 1: POST every remaining prompt to /prompt without waiting.
- Phase 2: poll /history and download finished outputs straight to final names.
- Prompt IDs are saved to queue_all_ids.json before waiting, for recovery.
- Writes only new files under renders_v2/. Never deletes or overwrites.
"""
import json
import random
import sys
import time
from pathlib import Path

import submit_overlink as s

ART = s.ART
OUT = s.OUT
IDS_LOG = ART / "queue_all_ids.json"
FAILED_LOG = ART / "queue_all_failed.json"
TIMEOUT_S = 8 * 3600

specs = json.loads((ART / "prompt_specs_v2.json").read_text(encoding="utf-8"))["cards"]
by_id = {c["id"]: c for c in specs}

todo = []
for c in specs:
    folder = OUT / c["faction"] / c["id"]
    have = len(list(folder.glob(f"{c['id']}_*.png"))) if folder.exists() else 0
    if have < 5:
        todo.append(c)
print(f"cards total={len(specs)} to_queue={len(todo)} skipped={len(specs) - len(todo)}", flush=True)

queued = {}
for c in todo:
    seed = random.randint(1, 2**31 - 1)
    prefix = f"overlink/{c['faction']}/{c['id']}"
    resp = s.post("/prompt", {"prompt": s.build_workflow(c, seed, prefix), "client_id": "themis-full"})
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
        status = h.get("status", {}).get("status_str")
        if status == "error":
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
