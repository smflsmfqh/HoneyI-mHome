#!/usr/bin/env python3
"""fix1(b: 연석 수정 회귀 / c: cooking 30 복원) Windows 결과 집계. 동결 규칙을 그대로 옮긴 집계만 하며 판정은 바꾸지 않는다.

규칙 출처: Evidence/M5/fix/M5-fix1-freeze.json, M5-fix1-freeze-addendum-c.json(소품 예측), M5-fix1-freeze-addendum-c2.json(소품 지표)
입력: analyze_physics_bench.py(c9950f0b…) + thresholds-B1 출력(Evidence/M5/fix/analysis/<run>-a1), 원시 trials/trace, player log

소품 지표(addendum-c2) 계산 방식(이 스크립트에서 고정):
  - 구간: phase가 approach 또는 push인 스텝
  - 진행 방향 f = -wallNormal(수평), 가로 방향 = f에 수직인 수평축
  - 가로 이탈 = 구간 내 |(p - start)·가로| 최대값
  - 소품 면 너머 진행 = 구간 내 (p - wallPoint)·f 최대값
  - 느린 스텝 = 구간 내 v·f < 0.2m/s 인 스텝 수(출발 가속 스텝 포함. 별도로 첫 접근 0.2m/s 도달 이후만 센 값도 함께 보고)
  - straightPassThrough = targetWallContact 0 and 가로 이탈 < 0.05 and 너머 진행 > 1.0
  - obstructed = targetWallContact ≥ 1 or 느린 스텝 ≥ 10

사용: python3 -I summarize_M5_fix1.py <PhysicsValidation 루트> <bundle 이름> <out.json>
"""
import json
import math
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(sys.argv[1])
BUNDLE = ROOT / "Evidence/M5/windows" / sys.argv[2]
OUT = Path(sys.argv[3])
ANA = ROOT / "Evidence/M5/fix/analysis"
PEN_INVALID = 0.005
INTENDED = {"CK08_flat_walk_noinput", "CK13_flat_hold_timescale0"}
RW = re.compile(r"attached to GameObject at path '([^']*)' with Mesh '[^']*' in Scene '[^']*' but doesn't have Read/Write enabled")
STUCK = {"escaped_by_rescue", "rescue_failed"}
if OUT.exists():
    raise SystemExit("exists, refusing to overwrite: " + str(OUT))


def jl(p):
    return [json.loads(l) for l in Path(p).read_text().splitlines() if l.strip()]


def run_meta(name):
    m = re.match(r"W-F-(BASE|CURBFIX|COOK30)-(b|c)-(ctl|game)-a(\d+)-s(\d+)-", name)
    return {"variant": m.group(1), "caseset": m.group(2), "profile": m.group(3), "attempt": int(m.group(4)), "seed": int(m.group(5))}


def env_of(bundle):
    t = (bundle / "windows-environment.txt").read_text(errors="replace")
    return " / ".join(x.strip() for x in re.findall(r"Name\s*:\s*(.+)", t)[:2])


def trace_by_case(raw):
    d = defaultdict(list)
    with (raw / "trace.jsonl").open() as f:
        for l in f:
            if l.strip():
                s = json.loads(l)
                d[s["caseId"]].append(s)
    return d


def group(cid):
    if cid.startswith("CK"):
        return "CK"
    if cid.startswith("E2_C"):
        return "E2_C"
    if cid.startswith("E2_P"):
        return "E2_P"
    if re.search(r"_a(30|60)$", cid):
        return "curb_angled"
    if re.search(r"_(Roll|Move)_c00$", cid):
        return "curb_frontRollMove"
    return "curb_straight"


def prop_metrics(trial, steps):
    n = trial["wallNormal"]
    f = (-n[0], -n[2])
    fl = math.hypot(*f)
    f = (f[0] / fl, f[1] / fl)
    lat = (-f[1], f[0])
    st, wp = trial["start"], trial["wallPoint"]
    seg = [s for s in steps if s["phase"] in ("approach", "push")]
    contact = sum(1 for s in seg if s["targetWallContact"])
    latmax = max((abs((s["p"][0] - st[0]) * lat[0] + (s["p"][2] - st[2]) * lat[1]) for s in seg), default=0.0)
    beyond = max(((s["p"][0] - wp[0]) * f[0] + (s["p"][2] - wp[2]) * f[1] for s in seg), default=-99.0)
    vf = [s["v"][0] * f[0] + s["v"][2] * f[1] for s in seg]
    slow = sum(1 for x in vf if x < 0.2)
    first = next((i for i, x in enumerate(vf) if x >= 0.2), len(vf))
    slow_after = sum(1 for x in vf[first:] if x < 0.2)
    return {"targetContactSteps": contact, "lateralMax": round(latmax, 4), "beyondMax": round(beyond, 4),
            "slowSteps": slow, "slowStepsAfterFirstReach": slow_after, "segSteps": len(seg),
            "straightPassThrough": contact == 0 and latmax < 0.05 and beyond > 1.0,
            "obstructed": contact >= 1 or slow >= 10,
            "obstructedAfterAccel": contact >= 1 or slow_after >= 10}


runs = {}
for raw in sorted(p for p in BUNDLE.glob("W-F-*") if p.is_dir()):
    meta = run_meta(raw.name)
    ana = ANA / (raw.name + "-a1")
    trials = {t["caseId"]: t for t in jl(raw / "trials.jsonl")}
    cls = {c["caseId"]: c for c in jl(ana / "classified.jsonl")}
    ck = {cid: c["outcome"] for cid, c in cls.items() if cid.startswith("CK")}
    ck_ok = len(ck) == 4 and all(cls[cid]["outcome"] == cls[cid]["expect"] for cid in ck)
    tr = None
    cases = {}
    for cid, c in cls.items():
        g = group(cid)
        if g == "CK":
            continue
        t = trials[cid]
        if (str(c["rootOutcome"]).startswith("harness_error") and cid not in INTENDED) or t.get("initialPenDepth", 0) > PEN_INVALID \
                or t.get("initialPenWallDepth", 0) > PEN_INVALID:
            cases[cid] = {"group": g, "tag": "invalid", "rootOutcome": c["rootOutcome"]}
            continue
        ro = c["rootOutcome"]
        tag = ro
        rec = {"group": g, "rootOutcome": ro}
        if g.startswith("curb") and ro == "passed_curb":
            if tr is None:
                tr = trace_by_case(raw)
            ps = c.get("passStep")
            b = next((s["bottomY"] for s in tr[cid] if s["step"] == ps), None) if ps is not None else None
            rec["passBottomY"] = b
            tag = "passThrough" if (b is not None and b < t.get("wallTopY", 0.1)) else "passedOver"
        if g == "E2_P":
            if tr is None:
                tr = trace_by_case(raw)
            rec.update(prop_metrics(t, tr[cid]))
        rec["tag"] = tag
        cases[cid] = rec
    log = raw.parent / (raw.name + ".player.log")
    rw = sorted(set(RW.findall(log.read_text(errors="replace")))) if log.exists() else None
    runs[raw.name] = dict(meta, ckOk=ck_ok, ck=ck, cases=cases, rwPaths=rw)


def tally(rs):
    t = defaultdict(lambda: defaultdict(int))
    for r in rs:
        if not r["ckOk"]:
            continue
        for cid, c in r["cases"].items():
            if c["tag"] == "invalid":
                t[c["group"]]["invalid"] += 1
                continue
            g = t[c["group"]]
            g["n"] += 1
            g[c["tag"]] += 1
    res = {}
    for gname, g in t.items():
        g = dict(g)
        if gname.startswith("curb") or gname == "E2_C":
            g["stuck"] = g.get("escaped_by_rescue", 0) + g.get("rescue_failed", 0)
        res[gname] = g
    curb = defaultdict(int)
    for gname in ("curb_straight", "curb_frontRollMove", "curb_angled"):
        for k, v in res.get(gname, {}).items():
            curb[k] += v
    res["curb_all"] = dict(curb)
    return res


def sel(**kw):
    return [r for r in runs.values() if all(r[k] == v for k, v in kw.items())]


out = {"generatedBy": "Tools/summarize_M5_fix1.py", "analysisRevision": "a1", "bundle": BUNDLE.name, "pc": env_of(BUNDLE),
       "runsCkOk": {k: r["ckOk"] for k, r in runs.items()}}

# b controlled
bc = {v: tally(sel(variant=v, caseset="b", profile="ctl")) for v in ("BASE", "CURBFIX")}
paired = []
for rc in sel(variant="CURBFIX", caseset="b", profile="ctl"):
    rb = next((r for r in sel(variant="BASE", caseset="b", profile="ctl") if r["attempt"] == rc["attempt"] and r["seed"] == rc["seed"]), None)
    if rb is None or not (rb["ckOk"] and rc["ckOk"]):
        continue
    for cid, c in rc["cases"].items():
        if rb["cases"][cid]["tag"] != c["tag"]:
            paired.append({"attempt": rc["attempt"], "case": cid, "BASE": rb["cases"][cid]["tag"], "CURBFIX": c["tag"]})
out["b_controlled"] = {"tally": bc, "pairedDiff": paired}

# b game (+ c-game COOK30)
bg = {v: tally(sel(variant=v, caseset="b", profile="game")) for v in ("BASE", "CURBFIX", "COOK30")}
per_case_game = defaultdict(dict)
for r in sel(caseset="b", profile="game"):
    for cid, c in r["cases"].items():
        if c["group"] != "E2_C" or True:
            per_case_game[cid].setdefault(r["variant"], []).append(c["tag"])
changed = {cid: v for cid, v in per_case_game.items() if len({t for tags in v.values() for t in tags}) > 1}


def stuck_fall(t):
    return t.get("curb_all", {}).get("stuck", 0) + t.get("E2_C", {}).get("stuck", 0) + t.get("E2_C", {}).get("fall", 0)


out["b_game"] = {"tally": bg, "casesNotIdenticalAcrossRuns": changed,
                 "regressionSignal_CURBFIX": {"stuckPlusE2Cfall": {"BASE": stuck_fall(bg["BASE"]), "CURBFIX": stuck_fall(bg["CURBFIX"])},
                                              "rescueFailedOnlyInCURBFIX": bg["CURBFIX"].get("curb_all", {}).get("rescue_failed", 0) > 0
                                              and bg["BASE"].get("curb_all", {}).get("rescue_failed", 0) == 0},
                 "c_regression_COOK30_vs_BASE": {"stuckPlusE2Cfall": {"BASE": stuck_fall(bg["BASE"]), "COOK30": stuck_fall(bg["COOK30"])},
                                                 "curbPassThrough": {"BASE": bg["BASE"]["curb_all"].get("passThrough", 0),
                                                                     "COOK30": bg["COOK30"]["curb_all"].get("passThrough", 0)}}}

# c controlled
pred = json.loads((ROOT / "Evidence/M5/fix/M5-fix1-freeze-addendum-c.json").read_text())["predictions"]
props = defaultdict(lambda: defaultdict(list))
for r in sel(caseset="c", profile="ctl"):
    if not r["ckOk"]:
        continue
    for cid, c in r["cases"].items():
        if c["group"] != "E2_P":
            continue
        if c["tag"] == "invalid":
            props[cid][r["variant"] + "_invalid"].append(r["attempt"])
            continue
        props[cid][r["variant"]].append({k: c.get(k) for k in ("attempt", "tag", "targetContactSteps", "lateralMax", "beyondMax", "slowSteps",
                                                                  "slowStepsAfterFirstReach", "straightPassThrough", "obstructed", "obstructedAfterAccel")}
                                        | {"attempt": r["attempt"]})
cc = {}
for cid in sorted(props):
    row = {"prediction": next((v for k, v in pred.items() if k.startswith(cid)), None)}
    for v in ("BASE", "COOK30"):
        xs = props[cid][v]
        row[v] = {"passed_curb": sum(1 for x in xs if x["tag"] == "passed_curb"), "straightPassThrough": sum(x["straightPassThrough"] for x in xs),
                  "obstructed": sum(x["obstructed"] for x in xs), "obstructedAfterAccel": sum(x["obstructedAfterAccel"] for x in xs),
                  "n": len(xs), "invalidAttempts": props[cid][v + "_invalid"], "trials": xs}
    cc[cid] = row
rw = {}
for v in ("BASE", "COOK30"):
    paths = [set(r["rwPaths"] or []) for r in sel(variant=v, caseset="c", profile="ctl")]
    u = set().union(*paths) if paths else set()
    rw[v] = {"uniquePathsUnion": len(u), "perRun": [len(p) for p in paths], "paths": sorted(u)}
gpaths = {v: sorted(set().union(*[set(r["rwPaths"] or []) for r in sel(variant=v, caseset="b", profile="game")])) for v in ("BASE", "COOK30")}
rw["game_BASE_unique"] = len(gpaths["BASE"])
rw["game_COOK30_unique"] = len(gpaths["COOK30"])
rw["game_COOK30_paths"] = gpaths["COOK30"]
out["c_controlled"] = {"props": cc,
                       "totals": {v: {k: sum(cc[c][v][k] for c in cc) for k in ("passed_curb", "straightPassThrough", "obstructed", "obstructedAfterAccel", "n")}
                                  for v in ("BASE", "COOK30")},
                       "readWriteErrors": rw}
out["runs"] = runs
OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n")

print("PC:", out["pc"], "| CK fail:", [k for k, v in out["runsCkOk"].items() if not v])
for v, t in bc.items():
    print("b-ctl", v, {g: t.get(g) for g in ("curb_straight", "curb_frontRollMove", "curb_angled", "curb_all", "E2_C")})
print("b-ctl paired diffs:", len(paired))
for v, t in bg.items():
    print("b-game", v, {g: t.get(g) for g in ("curb_all", "E2_C")})
print("game not identical:", json.dumps(changed, ensure_ascii=False))
for cid, row in cc.items():
    print(cid, "| B", {k: row["BASE"][k] for k in ("passed_curb", "straightPassThrough", "obstructed", "obstructedAfterAccel")},
          "| C30", {k: row["COOK30"][k] for k in ("passed_curb", "straightPassThrough", "obstructed", "obstructedAfterAccel")})
print("RW:", {k: (v if not isinstance(v, dict) else {kk: vv for kk, vv in v.items() if kk != "paths"}) for k, v in rw.items() if k != "game_COOK30_paths"})
print("RW COOK30 paths:", rw["COOK30"]["paths"])
