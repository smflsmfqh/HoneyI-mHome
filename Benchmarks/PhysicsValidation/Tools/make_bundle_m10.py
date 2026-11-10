#!/usr/bin/env python3
"""M10 Windows 묶음 생성(M10-r1). 사용: python3 -I make_bundle_m10.py <PhysicsValidation 루트>
M6-r1 BASE·CURBFIX 빌드 폴더를 그대로 복사(BurstDebugInformation_DoNotShip·build-result.json 제외)하고 m10 case·profile-H7·RunM10.cmd·README를 넣는다.
"""
import datetime
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(sys.argv[1])
BUILDS = ROOT.parent / ".physics-work/builds"
NAME = "M10-r1"
STAGE = ROOT.parent / ".physics-work/bundles" / NAME
ZIP = ROOT / "Builds" / (NAME + ".zip")
FREEZE = ROOT / "Evidence/M10/M10-r1-freeze.json"
if STAGE.exists() or ZIP.exists():
    raise SystemExit("exists, refusing to overwrite")
builds = {"BASE": "M6-BASE-win-01", "CURBFIX": "M6-CURBFIX-win-01"}


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


STAGE.mkdir(parents=True)
man = {"bundle": NAME, "experimentId": "M10-r1", "freeze": "Evidence/M10/M10-r1-freeze.json", "freezeSha256": sha(FREEZE),
       "createdUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "runs": 6, "estimatedTime": "약 1시간(84건 run 6개)",
       "builds": {}, "cases": {}, "profiles": {}}
for d, bid in builds.items():
    shutil.copytree(BUILDS / bid, STAGE / d, ignore=lambda _d, names: [n for n in names if "DoNotShip" in n or n == "build-result.json"])
    info = json.loads(json.loads((BUILDS / bid / "build-result.json").read_text())["buildInfo"])
    man["builds"][d] = {"buildId": bid, "sceneSha256": info.get("sceneSha256"), "rootSha": info.get("rootSha"), "exeSha256": sha(BUILDS / bid / "PhysicsBench.exe")}
(STAGE / "cases").mkdir()
c = ROOT / "Harness/Cases/M10-r1/m10-curb.json"
shutil.copy2(c, STAGE / "cases" / c.name)
man["cases"][c.name] = sha(c)
(STAGE / "profiles").mkdir()
p = ROOT / "Harness/Cases/profile-H7.json"
shutil.copy2(p, STAGE / "profiles" / p.name)
man["profiles"][p.name] = sha(p)
shutil.copy2(ROOT / "Harness/Windows/RunM10.cmd", STAGE / "RunM10.cmd")
shutil.copy2(ROOT / "Harness/Windows/README-Windows-M10.txt", STAGE / "README-Windows-M10.txt")
man["runner"] = sha(STAGE / "RunM10.cmd")
(STAGE / "MANIFEST.json").write_text(json.dumps(man, ensure_ascii=False, indent=1) + "\n")
lines = [f"{sha(f)}  {f.relative_to(STAGE).as_posix()}" for f in sorted(p for p in STAGE.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt")]
(STAGE / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n")
subprocess.run(["zip", "-r", "-q", "-X", str(ZIP), NAME], cwd=STAGE.parent, check=True)
z = sha(ZIP)
(ZIP.parent / (ZIP.name + ".sha256")).write_text(f"{z}  {ZIP.name}\n")
print(json.dumps({"zip": str(ZIP.relative_to(ROOT)), "bytes": ZIP.stat().st_size, "sha256": z, "files": len(lines), "builds": man["builds"]}, ensure_ascii=False, indent=1))
