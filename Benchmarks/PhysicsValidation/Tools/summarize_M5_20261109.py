#!/usr/bin/env python3
"""repeat-01·trackB-01·trackB-02 Windows 결과 집계(2026-11-09). 동결 규칙을 그대로 옮긴 집계만 하며 판정은 바꾸지 않는다.

입력: analyze_physics_bench.py 출력(classified.jsonl, a1)과 원시 trials/trace, player log.
규칙 출처: Evidence/M5/M5-repeat-r1-freeze.json, trackB/M5-trackB-freeze.json(+addendum), trackB/M5-trackB02-freeze.json

사용: python3 -I summarize_M5_20261109.py <PhysicsValidation 루트> <out.json>
"""
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(sys.argv[1])
OUT = Path(sys.argv[2])
EV = ROOT / "Evidence/M5"
W = EV / "windows"
PEN_INVALID = 0.005
FLOOR_RW = re.compile(r"path '[^']*/(road_\d+|Set_B_Tiles_\d+)[^']*' with Mesh '[^']*' in Scene '[^']*' but doesn't have Read/Write enabled")
INTENDED = {"CK08_flat_walk_noinput", "CK13_flat_hold_timescale0"}


def jl(p):
    return [json.loads(l) for l in Path(p).read_text().splitlines() if l.strip()]


def load_run(raw_dir, ana_dir):
    trials = {t["caseId"]: t for t in jl(raw_dir / "trials.jsonl")}
    cls = {c["caseId"]: c for c in jl(ana_dir / "classified.jsonl")}
    summ = json.loads((ana_dir / "summary.json").read_text())
    return trials, cls, summ


def ck_ok(cls):
    ck = [c for c in cls.values() if c["caseId"].startswith("CK")]
    return len(ck) == 4 and all(c["outcome"] == c["expect"] for c in ck), {c["caseId"]: c["outcome"] for c in ck}


def harness_err(c):
    return c["caseId"] not in INTENDED and str(c["rootOutcome"]).startswith("harness_error")


def env_of(bundle):
    t = (bundle / "windows-environment.txt").read_text(errors="replace")
    cpu = re.findall(r"Name\s*:\s*(.+)", t)
    return " / ".join(x.strip() for x in cpu[:2])


def pass_bottom(raw_dir, case, step):
    for l in (raw_dir / "trace.jsonl").open():
        if f'"caseId":"{case}"' in l and f'"step":{step},' in l:
            return json.loads(l)["bottomY"]
    return None


out = {"generatedBy": "Tools/summarize_M5_20261109.py", "analysisRevision": "a1"}

# ---------- repeat-01 ----------
rb = next(W.glob("PhysicsBench-Repeat01-Results-*"))
rep = {"bundle": rb.name, "pc": env_of(rb), "full": {}, "p11r": []}
fall_k = defaultdict(lambda: [0, 0, []])
excluded = []
same_seed = {}
for raw in sorted(rb.glob("W-H0-r1-full-*/")):
    ana = EV / "analysis" / (raw.name + "-a1")
    trials, cls, summ = load_run(raw, ana)
    ok, ck = ck_ok(cls)
    m = re.search(r"-a(\d+)-s(\d+)-", raw.name)
    a, s = int(m.group(1)), int(m.group(2))
    rep["full"][raw.name] = {"attempt": a, "seed": s, "ckOk": ok, "ck": ck, "outcomes": summ["outcomes"]}
    if not ok:
        excluded.append(raw.name)
        continue
    for cid, c in cls.items():
        if cid.startswith("CK") or cid == "E1_P11R_hold":
            continue
        if harness_err(c):
            continue
        if trials[cid].get("initialPenDepth", 0) > PEN_INVALID:
            continue
        rec = fall_k[cid]
        rec[1] += 1
        if c["rootOutcome"] == "fall":
            rec[0] += 1
            rec[2].append(a)
    if a in (1, 2):
        same_seed[a] = {cid: c["rootOutcome"] for cid, c in cls.items()}


def label(k, n):
    if n < 6:
        return f"{k}/{n}"
    return "안정 재현" if k >= 5 else ("간헐" if k >= 1 else "미재현")


pc02 = json.loads((EV / "M5-windows-pilot-02c-summary.json").read_text())
for grp in ("E2_C", "E2_K"):
    rows = {cid: {"k": v[0], "n": v[1], "label": label(v[0], v[1]), "fallAttempts": v[2]}
            for cid, v in sorted(fall_k.items()) if cid.startswith(grp)}
    rep[grp] = {"cases": rows,
                "anyFall": sum(1 for r in rows.values() if r["k"] > 0),
                "stable": sum(1 for r in rows.values() if r["label"] == "안정 재현"),
                "intermittent": sum(1 for r in rows.values() if r["label"] == "간헐"),
                "none": sum(1 for r in rows.values() if r["label"] == "미재현"),
                "totalFalls": sum(r["k"] for r in rows.values()), "totalTrials": sum(r["n"] for r in rows.values())}
rep["excludedRuns"] = excluded
if 1 in same_seed and 2 in same_seed:
    rep["sameSeedDiff(a1 vs a2)"] = {cid: [same_seed[1][cid], same_seed[2].get(cid)] for cid in same_seed[1]
                                     if same_seed[1][cid] != same_seed[2].get(cid)}
for raw in sorted(rb.glob("W-H0-r1-p11r-*/"), key=lambda p: int(re.search(r"-a(\d+)-", p.name).group(1))):
    ana = EV / "analysis" / (raw.name + "-a1")
    trials, cls, summ = load_run(raw, ana)
    t = trials["E1_P11R_hold"]
    c = cls["E1_P11R_hold"]
    steps = [json.loads(l) for l in (raw / "trace.jsonl").open()][:4]
    rep["p11r"].append({"run": raw.name, "fps": int(re.search(r"-f(\d+)-", raw.name).group(1)), "rootOutcome": c["rootOutcome"],
                        "initialPenDepth": t.get("initialPenDepth"),
                        "step0to3": [{"py": round(x["p"][1], 5), "penDepth": round(x["penDepth"], 5), "penDirY": round(x["penDirY"], 3)} for x in steps]})
p_by_fps = defaultdict(lambda: [0, 0])
for r in rep["p11r"]:
    p_by_fps[r["fps"]][1] += 1
    p_by_fps[r["fps"]][0] += r["rootOutcome"] == "fall"
rep["p11rByFps"] = {f"{k}fps": f"{v[0]}/{v[1]} fall" for k, v in sorted(p_by_fps.items())}
out["repeat01"] = rep

# ---------- trackB-01 floor ----------
tb = next(W.glob("PhysicsBench-TrackB01-Results-*"))
tb2 = next(W.glob("PhysicsBench-TrackB02-Results-*"))
TA = EV / "trackB/analysis"
floor = {"bundle": tb.name, "pc": env_of(tb), "variants": {}}
for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3"):
    vr = {"runs": {}, "E2C_fall": 0, "E2C_n": 0, "fallCases": defaultdict(int), "invalidInitialPen": 0, "harnessErr": 0,
          "childOnlyFall": 0}
    for raw in sorted(tb.glob(f"W-B-{v}-floor-*/")):
        trials, cls, summ = load_run(raw, TA / (raw.name + "-a1"))
        ok, ck = ck_ok(cls)
        log = (tb / (raw.name + ".player.log")).read_text(errors="replace")
        rw = len(set(m.group(1) for m in FLOOR_RW.finditer(log)))
        rwLines = len(FLOOR_RW.findall(log))
        vr["runs"][raw.name] = {"ckOk": ok, "ck": ck, "floorReadWriteErrorObjects": rw, "floorReadWriteErrorLines": rwLines,
                                "rootOutcomes": dict(sorted(defaultdict(int, {}).items()))}
        ro = defaultdict(int)
        for c in cls.values():
            ro[c["rootOutcome"]] += 1
        vr["runs"][raw.name]["rootOutcomes"] = dict(ro)
        if not ok:
            continue
        for cid, c in cls.items():
            if not cid.startswith("E2_C"):
                continue
            if harness_err(c):
                vr["harnessErr"] += 1
                continue
            if trials[cid].get("initialPenDepth", 0) > PEN_INVALID:
                vr["invalidInitialPen"] += 1
                continue
            vr["E2C_n"] += 1
            if c["rootOutcome"] == "fall":
                vr["E2C_fall"] += 1
                vr["fallCases"][cid] += 1
            elif c["outcome"] == "fall":
                vr["childOnlyFall"] += 1
    vr["fallCases"] = dict(sorted(vr["fallCases"].items()))
    floor["variants"][v] = vr

mc = {}
for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3"):
    rws = [r["floorReadWriteErrorObjects"] for r in floor["variants"][v]["runs"].values()]
    if v == "S0b":
        mc[v] = {"rule": "바닥 Read/Write 오류 > 0, E2_C 전부 root 낙하", "rwObjects": rws,
                 "ok": all(x > 0 for x in rws) and floor["variants"][v]["E2C_fall"] == floor["variants"][v]["E2C_n"]}
    elif v in ("S0a", "S0p", "S2"):
        mc[v] = {"rule": "바닥 Read/Write 오류 0", "rwObjects": rws, "ok": all(x == 0 for x in rws)}
    else:
        mc[v] = {"rule": "(조작 확인 대상 아님)", "rwObjects": rws}
floor["manipulationChecks"] = mc
out["trackB01_floor"] = floor


# ---------- 연석 ----------
def curb_runs(bundle, variant):
    res = {}
    for raw in sorted(bundle.glob(f"W-B-{variant}-curb-*/")):
        m = re.search(r"curb-dt([\d.]+)-(\w+?)-a(\d+)-s(\d+)-", raw.name)
        key = f"{m.group(1)}/{m.group(2)}"
        trials, cls, summ = load_run(raw, TA / (raw.name + "-a1"))
        ok, ck = ck_ok(cls)
        per = {}
        for cid, c in cls.items():
            if not cid.startswith("B_"):
                continue
            if harness_err(c) or trials[cid].get("initialPenWallDepth", 0) > PEN_INVALID:
                per[cid] = "invalid"
                continue
            ro = c["rootOutcome"]
            tag = ro
            if ro == "passed_curb":
                b = pass_bottom(raw, cid, c.get("passStep")) if c.get("passStep") is not None else None
                tag = "passThrough" if (b is not None and b < trials[cid].get("wallTopY", 0.1)) else "passedOver"
            per[cid] = tag
        res.setdefault(key, []).append({"run": raw.name, "attempt": int(m.group(3)), "seed": int(m.group(4)), "ckOk": ok, "ck": ck, "cases": per})
    return res


def tally(runs):
    t = defaultdict(int)
    n = 0
    for r in runs:
        if not r["ckOk"]:
            continue
        for cid, tag in r["cases"].items():
            if tag == "invalid":
                continue
            n += 1
            t[tag] += 1
    stuck = t["escaped_by_rescue"] + t["rescue_failed"]
    return {"n": n, "passThrough": t["passThrough"], "passedOver": t["passedOver"], "directionalStuck": stuck,
            "fullStuck": t["rescue_failed"], "byOutcome": dict(t)}


s3 = curb_runs(tb, "S3")
s3c = curb_runs(tb2, "S3c")
curb = {"S3": {k: {"tally": tally(v), "runs": v} for k, v in s3.items()},
        "S3c": {k: {"tally": tally(v), "runs": v} for k, v in s3c.items()}}
# 대응 비교(같은 설정·attempt·case)
pairs = {}
for key in s3c:
    diff = []
    for rc in s3c[key]:
        rs = next((r for r in s3.get(key, []) if r["attempt"] == rc["attempt"] and r["seed"] == rc["seed"]), None)
        if rs is None:
            continue
        for cid in rc["cases"]:
            if rs["cases"].get(cid) != rc["cases"][cid]:
                diff.append({"attempt": rc["attempt"], "case": cid, "S3": rs["cases"].get(cid), "S3c": rc["cases"][cid]})
    pairs[key] = diff
curb["pairedDiffS3vsS3c"] = pairs
out["curb"] = curb
OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n")

# ---------- 출력 ----------
print("repeat01 PC:", rep["pc"], "excluded:", excluded)
for grp in ("E2_C", "E2_K"):
    g = rep[grp]
    print(f" {grp}: 낙하 case {g['anyFall']}/{len(g['cases'])} (안정 {g['stable']}, 간헐 {g['intermittent']}, 미재현 {g['none']}), 전체 {g['totalFalls']}/{g['totalTrials']}")
    for cid, r in g["cases"].items():
        if r["k"]:
            print(f"   {cid}: {r['k']}/{r['n']} {r['label']} a{r['fallAttempts']}")
print(" sameSeed diff:", rep.get("sameSeedDiff(a1 vs a2)"))
print(" P11R:", rep["p11rByFps"], [(r["fps"], r["rootOutcome"], r["initialPenDepth"]) for r in rep["p11r"]])
print("trackB floor PC:", floor["pc"])
for v, vr in floor["variants"].items():
    print(f" {v}: E2_C root fall {vr['E2C_fall']}/{vr['E2C_n']} childOnly {vr['childOnlyFall']} invalidPen {vr['invalidInitialPen']} hErr {vr['harnessErr']} ck {[r['ckOk'] for r in vr['runs'].values()]} rw {[r['floorReadWriteErrorObjects'] for r in vr['runs'].values()]}")
print(" manipulation:", {k: v.get("ok") for k, v in mc.items()})
for var in ("S3", "S3c"):
    for k, v in curb[var].items():
        print(f" curb {var} {k}: {v['tally']} ck {[r['ckOk'] for r in v['runs']]}")
print(" paired diffs:", {k: len(v) for k, v in pairs.items()})
