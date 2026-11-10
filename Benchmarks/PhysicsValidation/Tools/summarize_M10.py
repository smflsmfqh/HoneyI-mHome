#!/usr/bin/env python3
"""M10-r1 집계(설계에 쓰지 않은 연석 8지점, BASE vs CURBFIX). 규칙은 Evidence/M10/M10-r1-freeze.json. Windows 결과를 보기 전에 SHA 동결.

입력: Evidence/M10/windows/<bundle>/W-M10-*, 판정 Evidence/M10/analysis/<run>-a1 (analyze_physics_bench.py c9950f0b… + thresholds-B1)
run ID: W-M10-<BASE|CURBFIX>-ctl-def-a<N>-s<seed>-<TS>
범주: rootOutcome. 연석 passed_curb는 passStep의 bottomY로 passThrough(< wallTopY)/passedOver/passed_unclassified (summarize_M6.py와 같음)
실패: passThrough, rescue_failed, escaped_by_rescue, fall, passed_unclassified, 그 밖의 비정상 범주
조건(case) 단위: 3 attempt 중 하나라도 실패면 실패 조건
면 범위 확인: 분석기 통과 판정은 끝없는 평면 기준이다. passStep 위치를 면 방향 축에 투영해 연석 면 범위(mid ±L/2, joint ±L, 반지름 r 여유)를
  벗어났으면 passed_off_face로 따로 분류한다(관통·실패로 세지 않고 수와 ID를 보고). 면 기하는 Harness/Cases/M10-r1/m10-sites-selected.json

사용: python3 -I summarize_M10.py <PhysicsValidation 루트> <out.json>
"""
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(sys.argv[1])
OUT = Path(sys.argv[2])
EV = ROOT / "Evidence/M10"
PEN_INVALID = 0.005
R_WORLD = 0.0094
INTENDED = {"CK08_flat_walk_noinput", "CK13_flat_hold_timescale0"}
OK = {"pass", "blocked_escaped", "recovered_after_penetration", "passedOver"}
SEPARATE = {"passed_off_face"}  # 실패도 성공도 아님: 분모 제외, 따로 보고
RID = re.compile(r"^W-M10-(BASE|CURBFIX)-ctl-def-a(\d+)-s(\d+)-")
CID = re.compile(r"^B_(G\d\d)_(\w+?)_(c00|p02|m02|a30)$")
if OUT.exists():
    raise SystemExit("exists, refusing to overwrite: " + str(OUT))
sites = {s["site"]: s for s in json.loads((ROOT / "Harness/Cases/M10-r1/m10-sites-selected.json").read_text())["sites"]}


def jl(p):
    return [json.loads(l) for l in Path(p).read_text().splitlines() if l.strip()]


runs, problems = [], []
for bundle in sorted(p for p in (EV / "windows").glob("*") if p.is_dir()):
    for raw in sorted(p for p in bundle.glob("W-M10-*") if p.is_dir()):
        m = RID.match(raw.name)
        if not m:
            problems.append("unparsed " + raw.name)
            continue
        ana = EV / "analysis" / (raw.name + "-a1") / "classified.jsonl"
        if not ana.exists():
            problems.append("no analysis " + raw.name)
            continue
        man = json.loads((raw / "manifest.json").read_text())
        bi = json.loads(man["buildInfoJson"]) if man.get("buildInfoJson") else {}
        trials = {t["caseId"]: t for t in jl(raw / "trials.jsonl")}
        cls = {c["caseId"]: c for c in jl(ana)}
        ck = {cid: c["outcome"] for cid, c in cls.items() if cid.startswith("CK")}
        ck_ok = len(ck) == 4 and all(cls[cid]["outcome"] == cls[cid]["expect"] for cid in ck)
        trace = None
        cases = {}
        for cid, c in cls.items():
            if not CID.match(cid):
                continue
            t = trials[cid]
            ro = c["rootOutcome"]
            pen = max(t.get("initialPenDepth", 0), t.get("initialPenWallDepth", 0))
            if (str(ro).startswith("harness_error") and cid not in INTENDED) or pen > PEN_INVALID:
                cases[cid] = {"tag": "invalid", "rootOutcome": ro}
                continue
            tag = ro
            if ro == "passed_curb":
                ps = c.get("passStep")
                b = None
                if ps is not None:
                    if trace is None:
                        trace = defaultdict(dict)
                        with (raw / "trace.jsonl").open() as f:
                            for l in f:
                                if '"caseId":"B_' in l:
                                    s = json.loads(l)
                                    trace[s["caseId"]][s["step"]] = (s.get("bottomY"), s.get("p"))
                    b, pos = trace.get(cid, {}).get(ps, (None, None))
                else:
                    pos = None
                tag = "passed_unclassified" if b is None else ("passThrough" if b < t.get("wallTopY", 0.1) else "passedOver")
                site = sites[CID.match(cid).group(1)]
                if pos is not None:
                    ax, az = -site["normal"][1], site["normal"][0]
                    along = (pos[0] - site["point"][0]) * ax + (pos[2] - site["point"][1]) * az
                    half = site["faceLength"] / 2 if site["kind"] == "mid" else site["faceLength"]
                    if abs(along) > half - R_WORLD:
                        tag = "passed_off_face"
            cases[cid] = {"tag": tag, "fail": tag not in OK and tag not in SEPARATE, "maxPenR": c.get("maxPenWallDepthR")}
        runs.append({"run": raw.name, "bundle": bundle.name, "build": m.group(1), "attempt": int(m.group(2)), "seed": int(m.group(3)),
                     "buildId": bi.get("buildId"), "sceneSha256": bi.get("sceneSha256"), "experimentId": man.get("experimentId"),
                     "casesSha256": man.get("casesSha256"), "dt": man.get("dtOverride"), "ckOk": ck_ok, "ck": ck, "cases": cases})

EXPECT = {"BASE": ("M6-BASE-win-01", "9d0bb583cc4f4f0e5fe6185012204e9e2d0ac60623f3e4fec660dc390eacfddb"),
          "CURBFIX": ("M6-CURBFIX-win-01", "12ba8273bf267fd773693cab49d70ae6eaaaa6bfb1d55f0315998dbf0807d043")}
for r in runs:
    if (r["buildId"], r["sceneSha256"]) != EXPECT[r["build"]]:
        problems.append(f"build mismatch {r['run']} {r['buildId']} {r['sceneSha256']}")
    if r["experimentId"] != "M10-r1":
        problems.append("experimentId " + r["run"])
used = [r for r in runs if r["ckOk"]]
excluded = [r["run"] for r in runs if not r["ckOk"]]


def tally(build, pred=lambda cid: True):
    rs = [r for r in used if r["build"] == build]
    t = Counter()
    cond = defaultdict(list)
    cond_sep = defaultdict(list)
    for r in rs:
        for cid, c in r["cases"].items():
            if not pred(cid):
                continue
            if c["tag"] == "invalid":
                t["invalid"] += 1
                continue
            if c["tag"] in SEPARATE:
                t[c["tag"]] += 1
                cond_sep[cid].append(c["tag"])
                continue
            t["n"] += 1
            t[c["tag"]] += 1
            t["fail"] += c["fail"]
            cond[cid].append(c["tag"])
    failc = sorted(cid for cid, tags in cond.items() if any(x not in OK for x in tags))
    return {"runs": [r["run"] for r in rs], "trials": dict(t), "conditions": len(cond), "failConditions": len(failc),
            "offFaceTrials": sorted(f"{cid}x{len(v)}" for cid, v in cond_sep.items()),
            "failConditionIds": failc, "perCondition": {cid: tags for cid, tags in sorted(cond.items())}}


out = {"generatedBy": "Tools/summarize_M10.py", "freeze": "Evidence/M10/M10-r1-freeze.json", "runCount": len(runs), "runsExcluded": excluded,
       "problems": problems, "runs": [{k: v for k, v in r.items() if k != "cases"} for r in runs]}
T = {b: tally(b) for b in ("BASE", "CURBFIX")}
out["overall"] = T
out["bySite"] = {sid: {b: {k: v for k, v in tally(b, lambda cid, s=sid: cid.startswith(f"B_{s}_")).items() if k != "perCondition"} for b in ("BASE", "CURBFIX")}
                 | {"stratum": sites[sid]["stratum"], "kind": sites[sid]["kind"], "road": sites[sid]["road"]} for sid in sorted(sites)}
out["byStratum"] = {st: {b: {k: v for k, v in tally(b, lambda cid, st=st: sites[CID.match(cid).group(1)]["stratum"] == st).items() if k != "perCondition"}
                         for b in ("BASE", "CURBFIX")} for st in ("A", "B", "C")}
# 대응(같은 attempt·seed·case)
trans = Counter()
better, worse, changed = [], [], []
for a in [r for r in used if r["build"] == "BASE"]:
    b = next((x for x in used if x["build"] == "CURBFIX" and x["attempt"] == a["attempt"] and x["seed"] == a["seed"]), None)
    if b is None:
        continue
    for cid, c in a["cases"].items():
        d = b["cases"].get(cid)
        if d is None or "invalid" in (c["tag"], d["tag"]) or SEPARATE & {c["tag"], d["tag"]}:
            continue
        trans[f"{c['tag']} → {d['tag']}"] += 1
        k = f"{cid}@a{a['attempt']}"
        if c["fail"] and not d["fail"]:
            better.append(k)
        elif d["fail"] and not c["fail"]:
            worse.append(k + ":" + d["tag"])
        elif c["tag"] != d["tag"]:
            changed.append(k + f":{c['tag']}→{d['tag']}")
out["paired"] = {"transition": dict(sorted(trans.items())), "improved": len(better), "worsened": len(worse), "changedSameFailState": len(changed),
                 "improvedCases": better, "worsenedCases": worse, "changedCases": changed}
xs = sorted(c["maxPenR"] for r in used if r["build"] == "CURBFIX" for c in r["cases"].values()
            if c["tag"] == "recovered_after_penetration" and c.get("maxPenR") is not None)
out["auxPenetrationCURBFIX"] = {"n": len(xs)} if not xs else {"n": len(xs), "medianR": round(statistics.median(xs), 3),
                                                              "maxR": round(xs[-1], 3), "maxCm": round(xs[-1] * R_WORLD * 100, 2)}
# 판정(freeze 규칙 그대로)
bf, cf = T["BASE"]["failConditions"], T["CURBFIX"]["failConditions"]
cfail_trials = T["CURBFIX"]["trials"].get("fail", 0)
only_cf = sorted(set(T["CURBFIX"]["failConditionIds"]) - set(T["BASE"]["failConditionIds"]))
valid = not problems and len(used) == 6 and len(runs) == 6
if not valid:
    verdict = "invalid_or_incomplete"
elif cfail_trials == 0 and bf >= 1:
    verdict = "generalize"
elif cfail_trials == 0 and bf == 0:
    verdict = "no_regression_only"
elif only_cf or cf >= bf:
    verdict = "against"
else:
    verdict = "partial"
out["verdict"] = {"verdict": verdict, "BASE_failConditions": f"{bf}/{T['BASE']['conditions']}", "CURBFIX_failConditions": f"{cf}/{T['CURBFIX']['conditions']}",
                  "CURBFIX_failTrials": f"{cfail_trials}/{T['CURBFIX']['trials'].get('n', 0)}", "failOnlyInCURBFIX": only_cf,
                  "auxSentence": bool(xs) and xs[-1] >= 1.0,
                  "offFace": {b: T[b]["offFaceTrials"] for b in T},
                  "strataNamedInSentence": [st for st in ("A", "B", "C") if out["byStratum"][st]["BASE"]["failConditions"] >= 1],
                  "strataNoRegressionOnly": [st for st in ("A", "B", "C") if out["byStratum"][st]["BASE"]["failConditions"] == 0
                                             and out["byStratum"][st]["CURBFIX"]["failConditions"] == 0]}
OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n")
print(json.dumps(out["verdict"], ensure_ascii=False, indent=1))
