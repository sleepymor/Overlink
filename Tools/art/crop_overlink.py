"""Post-process Overlink card renders: 1344x768 -> 1024x640 (horizontal cards).

Method (keeps the full composition, no stretching):
  1. Resize 1344x768 to 1120x640 (uniform scale, aspect 1.75 preserved).
  2. Center-crop 1120x640 to 1024x640 (cuts 48 px from each side).

Vertical 832x1216 renders are copied through unchanged.

Usage:
  python crop_overlink.py <src_dir> <dst_dir>            # dry run (lists actions)
  python crop_overlink.py <src_dir> <dst_dir> --apply    # writes files

Never modifies <src_dir>. Refuses to write into a non-empty destination.
"""
import shutil
import sys
from pathlib import Path

from PIL import Image

HORIZ_IN = (1344, 768)
HORIZ_OUT = (1024, 640)
VERT_IN = (832, 1216)
EXTS = {".png", ".jpg", ".jpeg", ".webp"}


def process(src_path: Path, dst_path: Path) -> str:
    with Image.open(src_path) as im:
        im.load()
        size = im.size
        if size == HORIZ_IN:
            mid_w = round(HORIZ_IN[0] * HORIZ_OUT[1] / HORIZ_IN[1])  # 1120
            resized = im.resize((mid_w, HORIZ_OUT[1]), Image.LANCZOS)
            left = (mid_w - HORIZ_OUT[0]) // 2
            out = resized.crop((left, 0, left + HORIZ_OUT[0], HORIZ_OUT[1]))
            out.save(dst_path)
            return "cropped"
        if size == VERT_IN:
            shutil.copy2(src_path, dst_path)
            return "copied"
        shutil.copy2(src_path, dst_path)
        return f"copied-unknown-size {size}"


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    src = Path(sys.argv[1])
    dst = Path(sys.argv[2])
    apply = "--apply" in sys.argv
    files = sorted(p for p in src.rglob("*") if p.suffix.lower() in EXTS)
    print(f"source files: {len(files)}")
    if not apply:
        for p in files[:10]:
            with Image.open(p) as im:
                print(f"  {p.name} {im.size}")
        print("dry run only; pass --apply to write")
        return 0
    if dst.exists() and any(dst.iterdir()):
        print(f"refusing: destination not empty: {dst}")
        return 1
    dst.mkdir(parents=True, exist_ok=True)
    tally = {}
    for p in files:
        rel = p.relative_to(src)
        target = dst / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        result = process(p, target)
        tally[result] = tally.get(result, 0) + 1
    print("results:", tally)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
