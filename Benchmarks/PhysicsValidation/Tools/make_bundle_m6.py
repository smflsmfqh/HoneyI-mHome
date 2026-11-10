#!/usr/bin/env python3
"""M7 Windows 묶음 생성(M6-r1). 사용: python3 -I make_bundle_m6.py <PhysicsValidation 루트> <A|B>
빌드 폴더에서 BurstDebugInformation_DoNotShip·build-result.json은 빼고 복사한다. SHA256SUMS.txt·MANIFEST.json을 넣고 ZIP과 .sha256을 만든다.
"""
import datetime
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(sys.argv[1])
WHICH = sys.argv[2]
BUILDS = ROOT.parent / ".physics-work/builds"
NAME = f"M6-r1-{WHICH}"
STAGE = ROOT.parent / ".physics-work/bundles" / NAME
ZIP = ROOT / "Builds" / (NAME + ".zip")
CASES = ROOT / "Harness/Cases/M6-r1"
if STAGE.exists() or ZIP.exists():
    raise SystemExit("exists, refusing to overwrite")

if WHICH == "A":
    builds = {"BASE": "M6-BASE-win-01", "CURBFIX": "M6-CURBFIX-win-01"}
    cases = ["m6-curb.json"]
    profiles = ["profile-H7.json", "profile-H7-game.json"]
    runs, est = 24, "약 2시간 50분(62건 run 24개, M5 실측 run당 약 7분)"
else:
    builds = {v: f"M6-{v}-win-01" for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3")}
    builds["H0"] = "M5-H0-win-01"
    cases = [f"m6-floor-{v}.json" for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3")] + ["m6-H0.json"]
    profiles = ["profile-H7.json"]
    runs, est = 24, "약 1시간 10분(28건 run 24개, M5 실측 run당 약 3분)"


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def ignore(d, names):
    return [n for n in names if "DoNotShip" in n or n == "build-result.json"]


STAGE.mkdir(parents=True)
man = {"bundle": NAME, "experimentId": "M6-r1", "freeze": "Evidence/M6/M6-r1-freeze.json", "freezeSha256": sha(ROOT / "Evidence/M6/M6-r1-freeze.json"),
       "createdUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "runs": runs, "estimatedTime": est, "builds": {}, "cases": {}, "profiles": {}}
for d, bid in builds.items():
    shutil.copytree(BUILDS / bid, STAGE / d, ignore=ignore)
    br = json.loads((BUILDS / bid / "build-result.json").read_text())
    info = json.loads(br["buildInfo"])
    man["builds"][d] = {"buildId": bid, "sceneSha256": info.get("sceneSha256"), "rootSha": info.get("rootSha"), "exeSha256": sha(BUILDS / bid / "PhysicsBench.exe")}
(STAGE / "cases").mkdir()
for c in cases:
    shutil.copy2(CASES / c, STAGE / "cases" / c)
    man["cases"][c] = sha(CASES / c)
(STAGE / "profiles").mkdir()
for p in profiles:
    shutil.copy2(ROOT / "Harness/Cases" / p, STAGE / "profiles" / p)
    man["profiles"][p] = sha(ROOT / "Harness/Cases" / p)
shutil.copy2(ROOT / f"Harness/Windows/RunM6{WHICH}.cmd", STAGE / f"RunM6{WHICH}.cmd")
shutil.copy2(ROOT / f"Harness/Windows/README-Windows-M6{WHICH}.txt", STAGE / f"README-Windows-M6{WHICH}.txt")
man["runner"] = sha(STAGE / f"RunM6{WHICH}.cmd")
(STAGE / "MANIFEST.json").write_text(json.dumps(man, ensure_ascii=False, indent=1) + "\n")
lines = []
for f in sorted(p for p in STAGE.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt"):
    lines.append(f"{sha(f)}  {f.relative_to(STAGE).as_posix()}")
(STAGE / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n")
subprocess.run(["zip", "-r", "-q", "-X", str(ZIP), NAME], cwd=STAGE.parent, check=True)
z = sha(ZIP)
(ZIP.parent / (ZIP.name + ".sha256")).write_text(f"{z}  {ZIP.name}\n")
print(json.dumps({"zip": str(ZIP.relative_to(ROOT)), "bytes": ZIP.stat().st_size, "sha256": z, "files": len(lines)}, ensure_ascii=False))
