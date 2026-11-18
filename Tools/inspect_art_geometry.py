#!/usr/bin/env python3
"""Inspect existing card frame/test-art PNG geometry to derive the art slot for prompts."""
import os
import struct
import glob

ROOTS = [
    r"E:\Games\Overlink\Assets\Cards\Frame",
    r"E:\Games\Overlink\Assets\Cards\Test_art",
    r"E:\Games\Overlink\Assets\Cards\Presentation",
    r"E:\Games\Overlink\Assets\Cards\PlaceholderFixtures",
    r"E:\Games\Overlink\Assets\Cards\Example",
]


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(26)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    w, h = struct.unpack(">II", head[16:24])
    return w, h


for root in ROOTS:
    if not os.path.isdir(root):
        continue
    print("=" * 70)
    print(root)
    files = sorted(glob.glob(os.path.join(root, "**", "*.png"), recursive=True))
    seen = {}
    for p in files:
        sz = png_size(p)
        rel = os.path.relpath(p, root)
        print(f"  {rel:60s} {sz}")
        if sz:
            seen.setdefault(sz, []).append(rel)
    print("  -- size buckets --")
    for sz, names in sorted(seen.items()):
        print(f"  {sz}: {len(names)} files  e.g. {names[0]}")
