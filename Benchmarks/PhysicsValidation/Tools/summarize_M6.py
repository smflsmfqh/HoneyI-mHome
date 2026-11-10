#!/usr/bin/env python3
"""M6-r1 정식 결과 집계. 규칙은 Evidence/M6/M6-r1-freeze.json(+addendum-scene)을 그대로 옮긴 것이며, Windows 결과를 보기 전에 SHA를 동결한다.

입력
  - 추출한 결과 묶음: Evidence/M6/windows/<bundle>/W-M6-*  (묶음 A·B 여러 개 가능)
  - 판정: Evidence/M6/analysis/<run>-a1 (analyze_physics_bench.py c9950f0b… + thresholds-B1)
  - 기하: Evidence/M6/geom/geom-analysis-m6.json
run ID: W-M6-<V>-<grp>-<phys>-a<N>-s<seed>-<TS>
  V: S0a S0b S0p S1 S2 S3 BASE CURBFIX H0 / grp: f p4 p5c p5g h0 / phys: dt0.02-Discrete, dt0.01-ContinuousDynamic, ... 또는 def(인자 없음)

사용: python3 -I summarize_M6.py <PhysicsValidation 루트> <out.json>
"""
import json
import re
import statistics
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(sys.argv[1])
OUT = Path(sys.argv[2])
EV = ROOT / "Evidence/M6"
PEN_INVALID = 0.005
R_WORLD = 0.0094
INTENDED = {"CK08_flat_walk_noinput", "CK13_flat_hold_timescale0"}
FLOOR_RW = re.compile(r"path '[^']*/(road_\d+|Set_B_Tiles_\d+)[^']*' with Mesh '[^']*' in Scene '[^']*' but doesn't have Read/Write enabled")
RID = re.compile(r"^W-M6-(S0a|S0b|S0p|S1|S2|S3|BASE|CURBFIX|H0)-(f|p4|p5c|p5g|h0)-(def|dt[\d.]+-\w+?)-a(\d+)-s(\d+)-")
if OUT.exists():
    raise SystemExit("exists, refusing to overwrite: " + str(OUT))


def jl(p):
    return [json.loads(l) for l in Path(p).read_text().splitlines() if l.strip()]


def group(cid):
    if cid.startswith("CK"):
        return "CK"
    if cid.startswith("E2_C"):
        return "E2_C"
    if re.search(r"_a(30|60)$", cid):
        return "curb_angled"
    if re.search(r"_(Roll|Move)_c00$", cid):
        return "curb_frontRollMove"
    if cid.startswith("B_"):
        return "curb_straight"
    return "other"


runs = {}
problems = []
for bundle in sorted(p for p in (EV / "windows").glob("*") if p.is_dir()):
    env = (bundle / "windows-environment.txt").read_text(errors="replace") if (bundle / "windows-environment.txt").exists() else ""
    pc = " / ".join(x.strip() for x in re.findall(r"Name\s*:\s*(.+)", env)[:2])
    for raw in sorted(p for p in bundle.glob("W-M6-*") if p.is_dir()):
        m = RID.match(raw.name)
        if not m:
            problems.append("unparsed run id " + raw.name)
            continue
        ana = EV / "analysis" / (raw.name + "-a1")
        if not (ana / "classified.jsonl").exists():
            problems.append("no analysis " + raw.name)
            continue
        status = json.loads((raw / "status.json").read_text()) if (raw / "status.json").exists() else {}
        trials = {t["caseId"]: t for t in jl(raw / "trials.jsonl")}
        cls = {c["caseId"]: c for c in jl(ana / "classified.jsonl")}
        ck = {cid: c["outcome"] for cid, c in cls.items() if cid.startswith("CK")}
        ck_ok = len(ck) == 4 and all(cls[cid]["outcome"] == cls[cid]["expect"] for cid in ck)
        trace = None
        cases = {}
        for cid, c in cls.items():
            g = group(cid)
            if g == "CK":
                continue
            t = trials[cid]
            ro = c["rootOutcome"]
            if (str(ro).startswith("harness_error") and cid not in INTENDED) or t.get("initialPenDepth", 0) > PEN_INVALID \
                    or t.get("initialPenWallDepth", 0) > PEN_INVALID:
                cases[cid] = {"group": g, "tag": "invalid", "rootOutcome": ro}
                continue
            rec = {"group": g, "rootOutcome": ro, "tag": ro}
            if g.startswith("curb"):
                rec["maxPenWallDepthR"] = c.get("maxPenWallDepthR")
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
                                        trace[s["caseId"]][s["step"]] = s.get("bottomY")
                        b = trace.get(cid, {}).get(ps)
                    rec["passBottomY"] = b
                    if b is None:
                        rec["tag"] = "passed_unclassified"
                    else:
                        rec["tag"] = "passThrough" if b < t.get("wallTopY", 0.1) else "passedOver"
            cases[cid] = rec
        log = bundle / (raw.name + ".player.log")
        floor_rw = len(set(x for x in FLOOR_RW.findall(log.read_text(errors="replace")))) if log.exists() else None
        v, grp, phys, a, s = m.group(1), m.group(2), m.group(3), int(m.group(4)), int(m.group(5))
        runs[raw.name] = {"name": raw.name, "bundle": bundle.name, "pc": pc, "variant": v, "grp": grp, "phys": phys, "attempt": a, "seed": s,
                          "state": status.get("state"), "ckOk": ck_ok, "ck": ck, "floorRwObjects": floor_rw, "cases": cases}


def sel(**kw):
    return sorted((r for r in runs.values() if all(r[k] == v for k, v in kw.items())), key=lambda r: (r["attempt"], r["seed"]))


def tally(rs, groups):
    t = defaultdict(int)
    excluded = [r for r in rs if not r["ckOk"]]
    for r in rs:
        if not r["ckOk"]:
            continue
        for c in r["cases"].values():
            if c["group"] not in groups:
                continue
            if c["tag"] == "invalid":
                t["invalid"] += 1
                continue
            t["n"] += 1
            t[c["tag"]] += 1
    t = dict(t)
    t["fullStuck"] = t.get("rescue_failed", 0)
    t["directionalStuck"] = t.get("rescue_failed", 0) + t.get("escaped_by_rescue", 0)
    t["runsUsed"] = len(rs) - len(excluded)
    t["runsExcluded"] = [r["bundle"] + "/" + r["name"] for r in excluded]
    return t


CURB = ("curb_straight", "curb_frontRollMove", "curb_angled")


def curb_report(rs):
    rep = {"all": tally(rs, CURB)}
    for g in CURB:
        rep[g] = tally(rs, (g,))
    rep["E2_C"] = tally(rs, ("E2_C",))
    return rep


def pen_aux(rs):
    xs = sorted(c["maxPenWallDepthR"] for r in rs if r["ckOk"] for c in r["cases"].values()
                if c["group"] in CURB and c["tag"] == "recovered_after_penetration" and c.get("maxPenWallDepthR") is not None)
    if not xs:
        return {"n": 0}
    return {"n": len(xs), "medianR": round(statistics.median(xs), 3), "p90R": round(xs[min(len(xs) - 1, int(len(xs) * 0.9))], 3),
            "maxR": round(xs[-1], 3), "maxCm": round(xs[-1] * R_WORLD * 100, 2)}


def paired(ra, rb, la, lb):
    diff = []
    for a in ra:
        b = next((x for x in rb if x["attempt"] == a["attempt"] and x["seed"] == a["seed"]), None)
        if b is None or not (a["ckOk"] and b["ckOk"]):
            continue
        for cid, c in a["cases"].items():
            if c["group"] in CURB + ("E2_C",) and b["cases"].get(cid, {}).get("tag") != c["tag"]:
                diff.append({"attempt": a["attempt"], "case": cid, la: c["tag"], lb: b["cases"].get(cid, {}).get("tag")})
    return diff


out = {"generatedBy": "Tools/summarize_M6.py", "freeze": "Evidence/M6/M6-r1-freeze.json", "runCount": len(runs), "problems": problems,
       "pcs": sorted({r["pc"] for r in runs.values()}),
       "runs": {k: {kk: vv for kk, vv in r.items() if kk != "cases"} for k, r in runs.items()}}

# ---------- F: ①②③ 바닥 ----------
floor = {}
for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3"):
    rs = sel(variant=v, grp="f")
    t = tally(rs, ("E2_C",))
    rw = [r["floorRwObjects"] for r in rs]
    if v == "S0b":
        mc = {"rule": "바닥 Read/Write 오류 > 0", "ok": bool(rw) and all(x is not None and x > 0 for x in rw)}
    elif v in ("S0a", "S0p", "S2"):
        mc = {"rule": "바닥 Read/Write 오류 0", "ok": bool(rw) and all(x == 0 for x in rw)}
    else:
        mc = {"rule": "없음", "ok": True}
    mc["floorRwObjectsPerRun"] = rw
    floor[v] = {"runs": len(rs), "E2_C": t, "falls": t.get("fall", 0), "n": t.get("n", 0), "manipulationCheck": mc,
                "fallCases": sorted({cid for r in rs if r["ckOk"] for cid, c in r["cases"].items() if c["group"] == "E2_C" and c["tag"] == "fall"})}
out["F_floor"] = floor

geomf = EV / "geom/geom-analysis-m6.json"
geom = {}
if geomf.exists():
    for x in json.loads(geomf.read_text())["results"]:
        geom[x["variant"]] = {"holeRate": x["holeRate"], "absErrP95mm": x["absErrMm"].get("p95"), "curbAbsErrP95mm": x["curbAbsErrMm"].get("p95"),
                              "stepLost": x["stepLost"], "transitions": x["transitions"]}
out["geometry"] = geom

# ---------- H0 ----------
h0 = defaultdict(lambda: [0, 0])
for r in sel(variant="H0", grp="h0"):
    if not r["ckOk"]:
        continue
    for cid, c in r["cases"].items():
        if c["group"] != "E2_C" or c["tag"] == "invalid":
            continue
        h0[cid][1] += 1
        h0[cid][0] += c["tag"] == "fall"
out["H0"] = {"runs": len(sel(variant="H0", grp="h0")), "perCase_k_of_n": {k: f"{v[0]}/{v[1]}" for k, v in sorted(h0.items())},
             "totalFalls": sum(v[0] for v in h0.values()), "totalTrials": sum(v[1] for v in h0.values()),
             "casesWithFall": sum(1 for v in h0.values() if v[0] > 0)}

# ---------- P4: ④ ----------
p4 = {}
for phys in ("dt0.02-Discrete", "dt0.02-ContinuousDynamic", "dt0.01-Discrete", "dt0.01-ContinuousDynamic"):
    rs = sel(variant="BASE", grp="p4", phys=phys)
    p4[phys] = {"runs": len(rs), **curb_report(rs)}
p4["pairedDiff_vs_dt0.02-Discrete"] = {phys: paired(sel(variant="BASE", grp="p4", phys="dt0.02-Discrete"), sel(variant="BASE", grp="p4", phys=phys), "dt0.02-Discrete", phys)
                                       for phys in ("dt0.02-ContinuousDynamic", "dt0.01-Discrete", "dt0.01-ContinuousDynamic")}
out["P4"] = p4

# ---------- P5: ⑤ ----------
p5 = {}
for grp in ("p5c", "p5g"):
    p5[grp] = {v: {"runs": len(sel(variant=v, grp=grp)), **curb_report(sel(variant=v, grp=grp)), "penetrationAux": pen_aux(sel(variant=v, grp=grp))}
               for v in ("BASE", "CURBFIX")}
p5["p5c"]["pairedDiff"] = paired(sel(variant="BASE", grp="p5c"), sel(variant="CURBFIX", grp="p5c"), "BASE", "CURBFIX")
p5["p5c_BASE_vs_p4_dt0.01CCD_sameness"] = paired(sel(variant="BASE", grp="p5c"), sel(variant="BASE", grp="p4", phys="dt0.01-ContinuousDynamic"), "p5c-default", "p4-explicit")
out["P5"] = p5

# ---------- 판정(동결 규칙 그대로) ----------
v = {}
f = floor
mc_ok = {k: f[k]["manipulationCheck"]["ok"] for k in f}
s0 = [k for k in ("S0a", "S0b") if mc_ok[k] and f[k]["falls"] >= 1]
v["①"] = {"support": bool(s0) and f["S3"]["falls"] == 0, "S0withFalls": s0, "S3falls": f["S3"]["falls"],
          "S0p": "설정만으로는 해결되지 않았다 덧붙임" if (mc_ok["S0p"] and f["S0p"]["falls"] > 0) else ("alt_A(설정 의존)" if mc_ok["S0p"] else "S0p 조작 체크 실패(무효)"),
          "alt_B": not s0 and all(mc_ok[k] for k in ("S0a", "S0b")),
          "H0": "보조 문장 사용" if out["H0"]["totalFalls"] > 0 else "언급 안 함",
          "invalidVariants": [k for k, ok in mc_ok.items() if not ok]}
g = geom
if "S2" in g:
    lost2 = g["S2"]["stepLost"] / g["S2"]["transitions"]
    v["②"] = {"support": g["S2"]["curbAbsErrP95mm"] >= 50 or lost2 >= 0.5, "against": g["S2"]["curbAbsErrP95mm"] < 5 and lost2 < 0.1,
              "S2curbP95mm": g["S2"]["curbAbsErrP95mm"], "S2stepLostRate": round(lost2, 3), "S2runtimeFalls": f["S2"]["falls"]}
if "S3" in g:
    lost3 = g["S3"]["stepLost"] / g["S3"]["transitions"]
    s1 = all(k in g and g[k]["stepLost"] / g[k]["transitions"] >= 0.9 and g[k]["absErrP95mm"] >= 50 for k in ("S1", "S1z"))
    v["③"] = {"S3runtimeFalls0": f["S3"]["falls"] == 0, "S3geomMatch": g["S3"]["curbAbsErrP95mm"] <= 5 and lost3 <= 0.1 and g["S3"]["holeRate"] <= 0.01,
              "S3geom": g["S3"], "S1limit": s1, "S1runtimeFalls": f["S1"]["falls"],
              "supplementary_P5c_BASE_E2C_falls": p5["p5c"]["BASE"]["E2_C"].get("fall", 0), "supplementary_n": p5["p5c"]["BASE"]["E2_C"].get("n", 0)}
a, b = p4["dt0.02-Discrete"]["all"], p4["dt0.01-ContinuousDynamic"]["all"]
v["④"] = {"passThrough_0.02D": a.get("passThrough", 0), "passThrough_0.01CCD": b.get("passThrough", 0), "n_0.02D": a.get("n", 0), "n_0.01CCD": b.get("n", 0),
          "fullStuck_0.02D": a["fullStuck"], "fullStuck_0.01CCD": b["fullStuck"],
          "support_passThrough": b.get("passThrough", 0) < a.get("passThrough", 0),
          "sentence": ("passThrough 감소 없음 → ④ 제외" if b.get("passThrough", 0) >= a.get("passThrough", 0) else
                       ("감소했지만 0.01/CCD에서도 > 0" if b.get("passThrough", 0) > 0 else "0.01/CCD에서 0")),
          "stuckSentence": "끼임 남김/늘림 문장" if b["fullStuck"] > a["fullStuck"] else "수치 그대로(감소 또는 변화 없음)"}
c5, cg = p5["p5c"], p5["p5g"]


def zero(t):
    return t.get("passThrough", 0) == 0 and t["fullStuck"] == 0 and t.get("escaped_by_rescue", 0) == 0


def worse(tb, tc):
    return tc["E2_C"].get("fall", 0) > tb["E2_C"].get("fall", 0) or tc["all"].get("fall", 0) > tb["all"].get("fall", 0)


elim = zero(c5["CURBFIX"]["all"]) and zero(cg["CURBFIX"]["all"]) and not worse(c5["BASE"], c5["CURBFIX"]) and not worse(cg["BASE"], cg["CURBFIX"])


def bad(t):
    return t.get("passThrough", 0) + t["directionalStuck"]


reg = (cg["CURBFIX"]["all"]["directionalStuck"] + cg["CURBFIX"]["E2_C"].get("fall", 0)) - (cg["BASE"]["all"]["directionalStuck"] + cg["BASE"]["E2_C"].get("fall", 0)) >= 3 \
    or (cg["CURBFIX"]["all"]["fullStuck"] > 0 and cg["BASE"]["all"]["fullStuck"] == 0)
reduced = bad(c5["CURBFIX"]["all"]) < bad(c5["BASE"]["all"])
v["⑤"] = {"eliminate": elim, "partial": (not elim) and reduced and not reg, "against": (not reduced) or reg, "regressionSignal": reg,
          "ctl": {k: {"passThrough": c5[k]["all"].get("passThrough", 0), "fullStuck": c5[k]["all"]["fullStuck"], "escaped_by_rescue": c5[k]["all"].get("escaped_by_rescue", 0),
                      "n": c5[k]["all"].get("n", 0), "E2C_fall": c5[k]["E2_C"].get("fall", 0)} for k in ("BASE", "CURBFIX")},
          "game": {k: {"passThrough": cg[k]["all"].get("passThrough", 0), "fullStuck": cg[k]["all"]["fullStuck"], "escaped_by_rescue": cg[k]["all"].get("escaped_by_rescue", 0),
                       "n": cg[k]["all"].get("n", 0), "E2C_fall": cg[k]["E2_C"].get("fall", 0)} for k in ("BASE", "CURBFIX")},
          "aux": c5["CURBFIX"]["penetrationAux"], "auxSentence": (c5["CURBFIX"]["penetrationAux"].get("maxR", 0) >= 1.0),
          "passedUnclassified": {k: c5[k]["all"].get("passed_unclassified", 0) + cg[k]["all"].get("passed_unclassified", 0) for k in ("BASE", "CURBFIX")}}
out["verdicts"] = v
OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n")
print(json.dumps({"runs": len(runs), "problems": problems, "pcs": out["pcs"], "verdicts": v}, ensure_ascii=False, indent=1))
