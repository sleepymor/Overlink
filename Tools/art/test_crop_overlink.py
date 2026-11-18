"""Self-test for crop_overlink.py on synthetic images in a scratch folder."""
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
SCRATCH = ROOT / "scratch_crop_test"
SRC = SCRATCH / "src"
DST = SCRATCH / "dst"

if SCRATCH.exists():
    shutil.rmtree(SCRATCH)
SRC.mkdir(parents=True)
Image.new("RGB", (1344, 768), (40, 90, 160)).save(SRC / "test_h.png")
Image.new("RGB", (832, 1216), (160, 90, 40)).save(SRC / "test_v.png")

py = sys.executable
subprocess.run([py, str(ROOT / "crop_overlink.py"), str(SRC), str(DST), "--apply"], check=True)

expect = {"test_h.png": (1024, 640), "test_v.png": (832, 1216)}
ok = True
for name, want in expect.items():
    got = Image.open(DST / name).size
    status = "OK" if got == want else "FAIL"
    ok = ok and got == want
    print(f"{status} {name} got={got} want={want}")
print("SELFTEST", "PASS" if ok else "FAIL")
