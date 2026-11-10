#!/usr/bin/env python3
"""M9 감사·대응 분석(M6-r1 결과). 규칙: Evidence/M6/audit/M9-audit-rules.json (계산 전에 기록).
판정(M8)을 바꾸지 않는 서술 감사. 범주 분류는 동결 summarize_M6.py와 같은 방식(rootOutcome, 연석 passed_curb는 passStep bottomY로 나눔).

사용: python3 -I audit_M9.py <PhysicsValidation 루트>
출력: Evidence/M6/audit/M9-audit.json
"""
import hashlib
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(sys.argv[1])
EV = ROOT / "Evidence/M6"
OUT = EV / "audit/M9-audit.json"
if OUT.exists():
    raise SystemExit("exists, refusing to overwrite: " + str(OUT))
PEN_INVALID = 0.005
INTENDED = {"CK08_flat_walk_noinput", "CK13_flat_hold_timescale0"}
RID = re.compile(r"^W-M6-(S0a|S0b|S0p|S1|S2|S3|BASE|CURBFIX|H0)-(f|p4|p5c|p5g|h0)-(def|dt[\d.]+-\w+?)-a(\d+)-s(\d+)-")
ZIPS = {"A": ("Evidence/PhysicsBench-M6A-Results-20261110T002814.zip", "1bdf17a0f9e98c568d31ed51bfd0bd412bdfd145bf0c260fc60cf0cbaa1c65e1"),
        "B": ("Evidence/PhysicsBench-M6B-Results-20261110T145343.zip", "3b5c84bb73b58dec7908a98bc570117f237c73ffc7a7f8a6941695b26b589e7b")}
CURB_FAIL = {"passThrough", "rescue_failed", "escaped_by_rescue", "fall", "passed_unclassified"}
CURB_OK = {"pass", "blocked_escaped", "recovered_after_penetration", "passedOver"}
ERR_PATH = re.compile(r"attached to GameObject at path '([^']*)'")


def jl(p):
    return [json.loads(l) for l in Path(p).read_text().splitlines() if l.strip()]


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def group(cid):
    if cid.startswith("CK"):
        return "CK"
    if cid.startswith("E2_C"):
        return "E2_C"
    if cid.startswith("B_"):
        return "curb"
    return "other"


def failed(g, tag):
    if tag == "invalid":
        return None
    if g == "E2_C":
        return tag != "pass"
    if tag in CURB_OK:
        return False
    return True  # CURB_FAIL 또는 그 밖의 비정상 범주는 실패로 셈


def err_class(path):
    n = path.split("/")[-1]
    if re.match(r"(road_\d+|Set_B_Tiles)", n):
        return "floor"
    if "Curb" in path:
        return "curb"
    if "Car" in path:
        return "car"
    return "other"


def coll_sig(cols):
    s = set()
    for c in cols:
        p = c["path"]
        if c["type"] == "MeshCollider" and re.search(r"/(road_\d+|Set_B_Tiles[^/]*)$", p):
            s.add("floorMeshConvex" if c["convex"] else "floorMesh")
        elif c["type"] == "BoxCollider" and "/FloorColliders/" in p:
            s.add("floorBox")
        elif "PhysicsBenchPlate" in p:
            s.add("plate")
        else:
            s.add("other:" + c["type"])
    return tuple(sorted(s))


EXPECT_SIG = {"S0a": lambda s: "floorMesh" in s and "floorMeshConvex" not in s,
              "S0p": lambda s: "floorMesh" in s and "floorMeshConvex" not in s,
              "H0": lambda s: "floorMesh" in s and "floorMeshConvex" not in s,
              "S0b": lambda s: "floorMesh" not in s and "floorMeshConvex" not in s,
              "S1": lambda s: "plate" in s,
              "S2": lambda s: "floorMeshConvex" in s and "floorMesh" not in s,
              "S3": lambda s: "floorBox" in s and "floorMesh" not in s and "floorMeshConvex" not in s}


def expect_phys(v, grp, phys):
    if grp in ("f", "h0"):
        return 0.02, "Discrete"
    if grp == "p4":
        dt, ccd = phys[2:].split("-", 1)
        return float(dt), ccd
    return 0.01, "ContinuousDynamic"


# ---------- 로드 ----------
runs = {}
audit = defaultdict(list)
for bundle in sorted(p for p in (EV / "windows").glob("*") if p.is_dir()):
    for raw in sorted(p for p in bundle.glob("W-M6-*") if p.is_dir()):
        m = RID.match(raw.name)
        v, grp, phys, att, seed = m.group(1), m.group(2), m.group(3), int(m.group(4)), int(m.group(5))
        man = json.loads((raw / "manifest.json").read_text())
        st = json.loads((raw / "status.json").read_text())
        order = json.loads((raw / "order.json").read_text())
        cf = json.loads((ROOT / "Harness/Cases/M6-r1" / (man["caseSetRevision"] + ".json")).read_text())
        cdef = {c["id"]: c for c in cf["cases"]}
        trials = jl(raw / "trials.jsonl")
        tmap = {t["caseId"]: t for t in trials}
        cls = {c["caseId"]: c for c in jl(EV / "analysis" / (raw.name + "-a1") / "classified.jsonl")}
        rid = raw.name
        # a 실행 완료
        ok_a = (st.get("state") == "completed" and st.get("planned") == st.get("finished") == len(cdef) and len(trials) == len(cdef)
                and set(order["caseIds"]) == set(cdef) and len(order["caseIds"]) == len(set(order["caseIds"])) and set(tmap) == set(cdef))
        audit["a_completion"].append({"run": rid, "ok": ok_a, "planned": st.get("planned"), "finished": st.get("finished"), "trials": len(trials), "cases": len(cdef)})
        edt, eccd = expect_phys(v, grp, phys)
        trace = None
        cases = {}
        errs = set()
        for cid, c in cls.items():
            t = tmap[cid]
            g = group(cid)
            # b 입력
            if c.get("missingInputs") or str(c["rootOutcome"]).startswith("harness_error"):
                audit["b_inputs"].append({"run": rid, "case": cid, "intended": cid in INTENDED, "outcome": c["rootOutcome"], "missing": c.get("missingInputs")})
            # c 초기 상태
            d = cdef[cid]
            st_ok = all(abs(a - b) <= 1e-4 for a, b in zip(t["start"], d["start"])) and abs(((t["startYaw"] - d.get("yaw", 0)) + 180) % 360 - 180) <= 1e-3
            ph_ok = abs(t["fixedDeltaTime"] - edt) < 1e-6 and t["collisionDetection"] == eccd
            pen = max(t.get("initialPenDepth", 0), t.get("initialPenWallDepth", 0))
            if not (st_ok and ph_ok) or pen > PEN_INVALID:
                audit["c_initialState"].append({"run": rid, "case": cid, "startOk": st_ok, "physOk": ph_ok, "dt": t["fixedDeltaTime"], "ccd": t["collisionDetection"], "initialPen": pen})
            if g == "CK":
                continue
            ro = c["rootOutcome"]
            if (str(ro).startswith("harness_error") and cid not in INTENDED) or pen > PEN_INVALID:
                tag = "invalid"
            else:
                tag = ro
                if g == "curb" and ro == "passed_curb":
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
                    tag = "passed_unclassified" if b is None else ("passThrough" if b < t.get("wallTopY", 0.1) else "passedOver")
            dt = t["fixedDeltaTime"]
            sec = lambda k: None if c.get(k) is None else round(c[k] * dt, 3)
            cases[cid] = {"group": g, "tag": tag, "fail": failed(g, tag), "sig": coll_sig(t.get("colliders", [])),
                          "fallSec": sec("fallStep"), "escapeSec": sec("escapeStep"), "passSec": sec("passStep"),
                          # firstWallContactTime은 게임 전체 fixedTime 기준 → trial 시작(phases[0]) 기준으로 바꾼다
                          "contactSec": None if c.get("firstWallContactTime", -1) < 0 or not t.get("phases") else
                          round(c["firstWallContactTime"] - (t["phases"][0]["fixedTime"] - t["phases"][0]["step"] * dt), 3),
                          "escapePhase": c.get("escapePhase"),
                          "maxPenR": c.get("maxPenWallDepthR")}
        # trial logErrors는 50개에서 잘리므로 player.log 전체를 쓴다
        log = bundle / (raw.name + ".player.log")
        errs = set(ERR_PATH.findall(log.read_text(errors="replace"))) if log.exists() else {"(no player.log)"}
        ec = Counter(err_class(p) for p in errs)
        runs[rid] = {"run": rid, "bundle": bundle.name, "v": v, "grp": grp, "phys": phys, "att": att, "seed": seed, "cases": cases,
                     "errObjects": dict(ec), "errCurbPaths": sorted(p for p in errs if err_class(p) == "curb"), "errPaths": errs}

# d 지형
for r in runs.values():
    if r["v"] in EXPECT_SIG:
        bad = [cid for cid, c in r["cases"].items() if c["group"] == "E2_C" and not EXPECT_SIG[r["v"]](c["sig"])]
        sigs = Counter(c["sig"] for c in r["cases"].values() if c["group"] == "E2_C")
        audit["d_terrain"].append({"run": r["run"], "variant": r["v"], "mismatch": bad, "signatures": {"+".join(k) or "(none)": n for k, n in sigs.items()}})
for grp in ("p5c", "p5g"):
    for att in (1, 2, 3):
        b = [r for r in runs.values() if r["v"] == "BASE" and r["grp"] == grp and r["att"] == att]
        f = [r for r in runs.values() if r["v"] == "CURBFIX" and r["grp"] == grp and r["att"] == att]
        if b and f:
            same = all(b[0]["cases"][cid]["sig"] == f[0]["cases"][cid]["sig"] for cid in b[0]["cases"])
            audit["d_terrain"].append({"pair": f"{grp} a{att} BASE↔CURBFIX", "startCollidersIdentical": same,
                                       "note": "연석 수정은 시작 콜라이더에 안 보임. 씬 SHA(12ba8273…)가 유일 증거"})
# e 오류 로그
for r in runs.values():
    audit["e_logErrors"].append({"run": r["run"], "objectsByClass": r["errObjects"], "curbPaths": r["errCurbPaths"]})
p5 = {(r["v"], r["grp"]): r["errPaths"] for r in runs.values() if r["grp"] in ("p5c", "p5g") and r["att"] == 1}
extra = {v: sorted(p5[(v, "p5g")] - p5[(v, "p5c")]) for v in ("BASE", "CURBFIX")}


# ---------- 표 ----------
def sel(**kw):
    return sorted((r for r in runs.values() if all(r[k] == x for k, x in kw.items())), key=lambda r: r["att"])


def cell(rs, pred):
    n = k = 0
    tags = Counter()
    fails = []
    inv = []
    for r in rs:
        for cid, c in sorted(r["cases"].items()):
            if not pred(cid, c):
                continue
            if c["tag"] == "invalid":
                inv.append(f"{cid}@a{r['att']}")
                continue
            n += 1
            tags[c["tag"]] += 1
            if c["fail"]:
                k += 1
                fails.append(f"{cid}@a{r['att']}:{c['tag']}")
    return {"k": k, "n": n, "pct": round(100 * k / n, 1) if n else None, "tags": dict(tags), "invalidExcluded": inv,
            "runs": [r["run"] for r in rs], "failures": fails}


is_curb = lambda cid, c: c["group"] == "curb"
is_e2c = lambda cid, c: c["group"] == "E2_C"


def kinds(rs):
    """낙하·끼임·통과·종합 실패(연석) 각각 k/n."""
    out = {}
    for name, tags in (("passThrough", {"passThrough"}), ("stuck", {"rescue_failed", "escaped_by_rescue"}), ("fall", {"fall"}),
                       ("combined", CURB_FAIL)):
        n = sum(1 for r in rs for c in r["cases"].values() if c["group"] == "curb" and c["tag"] != "invalid")
        f = [f"{cid}@a{r['att']}" for r in rs for cid, c in sorted(r["cases"].items()) if c["group"] == "curb" and c["tag"] in tags]
        out[name] = {"k": len(f), "n": n, "pct": round(100 * len(f) / n, 1) if n else None, "cases": f}
    e = cell(rs, is_e2c)
    out["E2_C_fall"] = {"k": e["k"], "n": e["n"], "cases": e["failures"]}
    out["runs"] = [r["run"] for r in rs]
    return out


def pairs(ra, rb, pred, la, lb):
    trans = Counter()
    better, worse, same_fail_changed = [], [], []
    n = 0
    for a in ra:
        b = next((x for x in rb if x["att"] == a["att"] and x["seed"] == a["seed"]), None)
        if b is None:
            continue
        for cid, c in sorted(a["cases"].items()):
            if not pred(cid, c) or cid not in b["cases"]:
                continue
            d = b["cases"][cid]
            if "invalid" in (c["tag"], d["tag"]):
                continue
            n += 1
            trans[f"{c['tag']} → {d['tag']}"] += 1
            key = f"{cid}@a{a['att']}"
            if c["fail"] and not d["fail"]:
                better.append(key)
            elif d["fail"] and not c["fail"]:
                worse.append(key + f":{d['tag']}")
            elif c["tag"] != d["tag"]:
                same_fail_changed.append(key + f":{c['tag']}→{d['tag']}")
    return {"from": la, "to": lb, "nPairs": n, "transition": dict(sorted(trans.items())), "improved": len(better), "worsened": len(worse),
            "categoryChangedSameFailState": len(same_fail_changed), "improvedCases": better, "worsenedCases": worse,
            "changedCases": same_fail_changed, "runsFrom": [r["run"] for r in ra], "runsTo": [r["run"] for r in rb]}


T = {}
# 바닥
T["floor_E2C"] = {v: cell(sel(v=v, grp="f"), is_e2c) for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3")}
T["floor_E2C"]["H0"] = cell(sel(v="H0"), is_e2c)
T["floor_E2C"]["P5c_BASE(보조)"] = cell(sel(v="BASE", grp="p5c"), is_e2c)
T["floor_pairs_vs_S3"] = {v: pairs(sel(v=v, grp="f"), sel(v="S3", grp="f"), is_e2c, v, "S3") for v in ("S0a", "S0b", "S0p", "S1", "S2")}
T["floor_perCase"] = {}
for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3", "H0"):
    rs = sel(v=v, grp="h0" if v == "H0" else "f")
    per = defaultdict(lambda: [0, 0])
    for r in rs:
        for cid, c in r["cases"].items():
            if c["group"] == "E2_C" and c["tag"] != "invalid":
                per[cid][1] += 1
                per[cid][0] += c["fail"]
    T["floor_perCase"][v] = {cid: f"{k}/{n}" for cid, (k, n) in sorted(per.items()) if k}
# 연석 ④ P4
P4 = ("dt0.02-Discrete", "dt0.01-Discrete", "dt0.02-ContinuousDynamic", "dt0.01-ContinuousDynamic")
T["P4_kinds"] = {p: kinds(sel(v="BASE", grp="p4", phys=p)) for p in P4}
T["P4_pairs_vs_0.02D"] = {p: pairs(sel(v="BASE", grp="p4", phys="dt0.02-Discrete"), sel(v="BASE", grp="p4", phys=p), is_curb, "dt0.02-Discrete", p) for p in P4[1:]}
# 연석 ⑤
T["P5_kinds"] = {f"{g}/{v}": kinds(sel(v=v, grp=g)) for g in ("p5c", "p5g") for v in ("BASE", "CURBFIX")}
T["P5c_pairs"] = pairs(sel(v="BASE", grp="p5c"), sel(v="CURBFIX", grp="p5c"), lambda cid, c: c["group"] in ("curb", "E2_C"), "BASE", "CURBFIX")
for g in ("p5c", "p5g"):
    b, f = T["P5_kinds"][f"{g}/BASE"], T["P5_kinds"][f"{g}/CURBFIX"]
    T["P5_kinds"][f"{g}/ppDiff"] = {k: round(f[k]["pct"] - b[k]["pct"], 1) for k in ("passThrough", "stuck", "fall", "combined")}
for p in P4[1:]:
    T["P4_kinds"][p]["ppDiff_vs_0.02D"] = {k: round(T["P4_kinds"][p][k]["pct"] - T["P4_kinds"]["dt0.02-Discrete"][k]["pct"], 1)
                                           for k in ("passThrough", "stuck", "fall", "combined")}

# 위치군(연석 지점 × 접근 × 각도)
CID = re.compile(r"^B_(S2|S3)_(\w+?)_(c00|p02|m02|a30|a60)$")


def loc_table(rs):
    t = defaultdict(lambda: defaultdict(lambda: [0, 0]))
    for r in rs:
        for cid, c in r["cases"].items():
            m = CID.match(cid)
            if not m or c["tag"] == "invalid":
                continue
            key = f"연석지점{m.group(1)}/{m.group(2)}/{m.group(3)}"
            t[key]["n"][1] += 1
            for tag in (c["tag"],):
                t[key][tag][0] += 1
    return {k: {"n": v["n"][1], **{tag: x[0] for tag, x in v.items() if tag != "n"}} for k, v in sorted(t.items())}


T["curb_location"] = {"P4/" + p: loc_table(sel(v="BASE", grp="p4", phys=p)) for p in P4}
T["curb_location"].update({f"{g}/{v}": loc_table(sel(v=v, grp=g)) for g in ("p5c", "p5g") for v in ("BASE", "CURBFIX")})


# 시간(초)
def times(rs):
    out = {}
    for tag in ("fall", "passThrough", "rescue_failed", "escaped_by_rescue", "blocked_escaped", "recovered_after_penetration"):
        xs = [(c, r) for r in rs for c in r["cases"].values() if c["tag"] == tag]
        if not xs:
            continue
        rec = {"n": len(xs)}
        for key, f in (("fallSec", lambda c: c["fallSec"]), ("passSec", lambda c: c["passSec"]),
                       ("escapeAfterContactSec", lambda c: None if c["escapeSec"] is None or c["contactSec"] is None else c["escapeSec"] - c["contactSec"])):
            v = sorted(x for x in (f(c) for c, _ in xs) if x is not None)
            if v:
                rec[key] = {"n": len(v), "median": round(statistics.median(v), 3), "min": round(v[0], 3), "max": round(v[-1], 3)}
        out[tag] = rec
    return out


T["time"] = {"floor/" + v: times(sel(v=v, grp="f")) for v in ("S0a", "S0b", "S0p", "S2")}
T["time"]["H0"] = times(sel(v="H0"))
T["time"].update({"P4/" + p: times(sel(v="BASE", grp="p4", phys=p)) for p in P4})
T["time"].update({f"{g}/{v}": times(sel(v=v, grp=g)) for g in ("p5c", "p5g") for v in ("BASE", "CURBFIX")})

# 잡음(같은 빌드·같은 seed a1↔a2, P4 0.01/CCD ↔ P5c BASE)
noise = {}
for key in sorted({(r["v"], r["grp"], r["phys"]) for r in runs.values()}):
    rs = sel(v=key[0], grp=key[1], phys=key[2])
    a1 = [r for r in rs if r["att"] == 1]
    a2 = [r for r in rs if r["att"] == 2 and r["seed"] == 1]
    if a1 and a2 and a1[0]["seed"] == 1:
        d = [cid for cid, c in a1[0]["cases"].items() if a2[0]["cases"].get(cid, {}).get("tag") != c["tag"]]
        noise["/".join(key) + " a1↔a2(seed1)"] = {"n": len(a1[0]["cases"]), "diff": len(d),
                                                 "cases": [f"{cid}:{a1[0]['cases'][cid]['tag']}→{a2[0]['cases'][cid]['tag']}" for cid in sorted(d)]}
cc = pairs(sel(v="BASE", grp="p4", phys="dt0.01-ContinuousDynamic"), sel(v="BASE", grp="p5c"), lambda cid, c: c["group"] in ("curb", "E2_C"),
           "P4 0.01/CCD(override)", "P5c BASE(기본값)")
noise["P4 0.01/CCD ↔ P5c BASE (같은 attempt)"] = {"nPairs": cc["nPairs"], "diff": cc["improved"] + cc["worsened"] + cc["categoryChangedSameFailState"],
                                                  "transitionOffDiagonal": {k: v for k, v in cc["transition"].items() if k.split(" → ")[0] != k.split(" → ")[1]},
                                                  "worsened": cc["worsenedCases"], "improved": cc["improvedCases"], "changed": cc["changedCases"]}
T["noise"] = noise

# ③ 기하 위치 확인
g3 = json.loads((EV / "geom/geom-S3.json").read_text())
over = Counter()
for l in g3["lines"]:
    for vy, cy, ci in zip(l["visualY"], l["colY"], l["colIdx"]):
        if vy is None or cy is None:
            continue
        if abs(cy - vy) > 0.005:
            over[(l["id"], g3["colliders"][ci].split("|")[0].split("/")[-1] if ci is not None and ci >= 0 else "none")] += 1
ga = [x for x in json.loads((EV / "geom/geom-analysis-m6.json").read_text())["results"] if x["variant"] == "S3"][0]
T["claim3_geomLocation"] = {"pointsOver5mm": sum(over.values()), "byLineCollider": {f"{a}|{b}": n for (a, b), n in over.most_common()},
                            "stepLostByLine": {k: v["stepLost"] for k, v in ga["perLine"].items() if v["stepLost"]},
                            "curbP95ByLineOver5mm": {k: round(v["curbAbsErrP95mm"], 1) for k, v in ga["perLine"].items() if v["curbAbsErrP95mm"] > 5}}

# ---------- 감사 요약 ----------
summary = {
    "rawZips": {k: {"path": p, "sha256": sha(ROOT / p), "matchesM8": sha(ROOT / p) == s} for k, (p, s) in ZIPS.items()},
    "runs": len(runs),
    "a_completion_fail": [x for x in audit["a_completion"] if not x["ok"]],
    "b_unintended": [x for x in audit["b_inputs"] if not x["intended"]],
    "b_intendedCount": Counter(x["case"] for x in audit["b_inputs"] if x["intended"]),
    "c_problems": audit["c_initialState"],
    "d_mismatch": [x for x in audit["d_terrain"] if x.get("mismatch") or x.get("startCollidersIdentical") is False],
    "e_curbErrorsInCURBFIX": [x for x in audit["e_logErrors"] if "CURBFIX" in x["run"] and x["curbPaths"]],
    "e_curbErrorsAny": sorted({p for x in audit["e_logErrors"] for p in x["curbPaths"]}),
    "e_objectsByClass": {x["run"]: x["objectsByClass"] for x in audit["e_logErrors"]},
    "e_p5gExtraVsP5c_a1": {k: {"n": len(v), "classes": dict(Counter(err_class(p) for p in v)), "paths": v} for k, v in extra.items()},
}
OUT.write_text(json.dumps({"generatedBy": "Tools/audit_M9.py", "rules": "Evidence/M6/audit/M9-audit-rules.json",
                           "rulesSha256": sha(EV / "audit/M9-audit-rules.json"), "summarizedFrom": "Evidence/M6/M6-r1-summary-final.json",
                           "auditSummary": summary, "tables": T,
                           "auditDetail": {k: v for k, v in audit.items() if k != "e_logErrors"}}, ensure_ascii=False, indent=1, default=list) + "\n")
print(json.dumps(summary, ensure_ascii=False, indent=1, default=list)[:6000])
