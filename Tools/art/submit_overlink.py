"""Submit Overlink card renders to Hyperion ComfyUI from prompt_specs_v2.json.

Pipeline per card:
  CheckpointLoaderSimple(spec.checkpoint) -> LoraLoader(TorinoAqua, spec strengths)
  -> CLIPTextEncode(positive/negative) -> EmptyLatentImage(spec dims, batch 5)
  -> KSampler(spec steps/cfg/sampler, scheduler normal) -> VAEDecode -> SaveImage
Results are downloaded from the ComfyUI API to Tethys and horizontal ones are cropped
to 1024x640 by crop_overlink.process().

Usage:
  python submit_overlink.py --ids card_fsti_001,card_fsti_002      # dry run (no queue)
  python submit_overlink.py --ids ... --queue                       # queue + download
  python submit_overlink.py --faction fsti --queue                  # whole faction

Never modifies prompt_specs.json or the Hyperion generator. Skips cards whose
output folder already holds 5 files.
"""
import argparse
import json
import random
import time
import urllib.parse
import urllib.request
from pathlib import Path

API = "http://100.89.65.29:8188"
ART = Path(__file__).resolve().parent
SPECS = ART / "prompt_specs_v2.json"
OUT = ART / "renders_v2"
LORA = "TorinoAqua_Anime_Style_v2.safetensors"


def build_workflow(spec: dict, seed: int, prefix: str) -> dict:
    w, h = spec["dims"]
    wf = {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": spec["checkpoint"]}},
        "2": {"class_type": "LoraLoader",
              "inputs": {"model": ["1", 0], "clip": ["1", 1], "lora_name": LORA,
                         "strength_model": spec["lora_strength_model"],
                         "strength_clip": spec["lora_strength_clip"]}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["2", 1], "text": spec["positive"]}},
        "4": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["2", 1], "text": spec["negative"]}},
        "5": {"class_type": "EmptyLatentImage",
              "inputs": {"width": w, "height": h, "batch_size": spec["batch_size"]}},
        "6": {"class_type": "KSampler",
              "inputs": {"model": ["2", 0], "positive": ["3", 0], "negative": ["4", 0],
                         "latent_image": ["5", 0], "seed": seed,
                         "steps": spec["steps"], "cfg": spec["cfg"],
                         "sampler_name": spec["sampler"], "scheduler": "normal",
                         "denoise": 1.0}},
        "7": {"class_type": "VAEDecode",
              "inputs": {"samples": ["6", 0], "vae": ["1", 2]}},
    }
    if spec.get("crop"):
        # Horizontal: 1344x768 -> 1120x640 (uniform scale) -> centre-crop 1024x640.
        cw, ch = spec["crop"]
        mid_w = round(w * ch / h)
        wf["8"] = {"class_type": "ImageScale",
                  "inputs": {"image": ["7", 0], "upscale_method": "lanczos",
                             "width": mid_w, "height": ch, "crop": "disabled"}}
        wf["9"] = {"class_type": "ImageCrop",
                  "inputs": {"image": ["8", 0], "width": cw, "height": ch,
                             "x": (mid_w - cw) // 2, "y": 0}}
        wf["10"] = {"class_type": "SaveImage",
                   "inputs": {"images": ["9", 0], "filename_prefix": prefix}}
    else:
        wf["8"] = {"class_type": "SaveImage",
                  "inputs": {"images": ["7", 0], "filename_prefix": prefix}}
    return wf


def post(path: str, body: dict) -> dict:
    req = urllib.request.Request(API + path, data=json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def history(prompt_id: str) -> dict:
    with urllib.request.urlopen(f"{API}/history/{prompt_id}", timeout=60) as r:
        return json.load(r).get(prompt_id, {})


def wait_for(prompt_id: str, timeout_s: int = 1800) -> dict:
    t0 = time.time()
    while time.time() - t0 < timeout_s:
        h = history(prompt_id)
        if h.get("outputs"):
            return h
        time.sleep(5)
    raise TimeoutError(prompt_id)


def download(filename: str, subfolder: str, dest: Path) -> None:
    q = urllib.parse.urlencode({"filename": filename, "subfolder": subfolder, "type": "output"})
    with urllib.request.urlopen(f"{API}/view?{q}", timeout=120) as r:
        dest.write_bytes(r.read())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", default="")
    ap.add_argument("--faction", default="")
    ap.add_argument("--queue", action="store_true")
    args = ap.parse_args()

    specs = json.loads(SPECS.read_text(encoding="utf-8"))["cards"]
    if args.ids:
        want = set(args.ids.split(","))
        specs = [s for s in specs if s["id"] in want]
    if args.faction:
        specs = [s for s in specs if s["faction"] == args.faction]
    print(f"selected {len(specs)} cards")

    for s in specs:
        cid = s["id"]
        folder = OUT / s["faction"] / cid
        have = len(list(folder.glob("*.png"))) if folder.exists() else 0
        if have >= 5:
            print(f"skip {cid}: already {have} files")
            continue
        if not args.queue:
            wf = build_workflow(s, seed=0, prefix=f"overlink/{cid}")
            print(f"dry {cid} dims={s['dims']} crop={s.get('crop')} ckpt={s['checkpoint']} "
                  f"nodes={len(wf)} pos_len={len(s['positive'])}")
            continue
        folder.mkdir(parents=True, exist_ok=True)
        seed = random.randint(1, 2**31 - 1)
        prefix = f"overlink/{s['faction']}/{cid}"
        resp = post("/prompt", {"prompt": build_workflow(s, seed, prefix), "client_id": "themis"})
        pid = resp["prompt_id"]
        print(f"queued {cid} prompt_id={pid}")
        h = wait_for(pid)
        imgs = [img for node in h["outputs"].values() for img in node.get("images", [])]
        for i, img in enumerate(imgs):
            raw = folder / f"raw_{i:02d}.png"
            download(img["filename"], img.get("subfolder", ""), raw)
            raw.rename(folder / f"{cid}_{i:02d}.png")
        print(f"done {cid}: {len(imgs)} images")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
