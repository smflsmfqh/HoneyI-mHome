#!/usr/bin/env python3
"""e2 run의 낙하(fall) trial을 경로 기준으로 나눈다(읽기 전용, 판정은 analyze_physics_bench.py 결과를 그대로 씀).

walk/jump case는 시작점에서 진행 방향으로 잰 낙하 지점 거리를 case의 사용 구간 길이(probe로 지면을 확인한 거리)와
비교한다. 구간 밖(맵 가장자리 등 probe하지 않은 곳) 낙하는 'beyond_probed_path'로 따로 센다.
가장 가까운 지면 메시 전환(높이가 같은 타일 이음매 포함)과 높이 변화(연석)의 경로상 위치도 남긴다.
판정 결과를 바꾸지 않는 보조 집계다.

사용: python3 -I summarize_falls_e2.py <run_dir> <classified.jsonl> <probe.json> <out.json>
"""
import collections
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_explore_e2 as m  # noqa: E402


def main():
    run, cls_path, probe_path, out = sys.argv[1], sys.argv[2], sys.argv[3], Path(sys.argv[4])
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    rows = collections.defaultdict(list)
    for line in open(Path(run) / "trace.jsonl"):
        r = json.loads(line)
        rows[r["caseId"]].append(r)
    trials = {json.loads(l)["caseId"]: json.loads(l) for l in open(Path(run) / "trials.jsonl")}
    probe = json.loads(Path(probe_path).read_text())
    by = {(round(p["x"], 3), round(p["z"], 3)): p for p in probe["points"]}
    lines = {lid: (x0, z0, x1, z1, kind) for lid, x0, z0, x1, z1, _, kind in m.all_lines()}
    res = []
    for line in open(cls_path):
        c = json.loads(line)
        if c["outcome"] != "fall" or c["caseId"].startswith("CK"):
            continue
        t = trials[c["caseId"]]
        rs = rows[c["caseId"]]
        r = rs[0]["r"] if "r" in rs[0] else t["capsuleRadiusWorld"]
        lim = t["minAllowedY"] - 3 * r
        f = next(x for x in rs if x["bottomY"] < lim)
        item = {"caseId": c["caseId"], "fallPos": [round(v, 3) for v in f["p"]], "phase": f["phase"], "t": round(f["t"], 3),
                "contactsAtFall": f["contactIds"], "start": t["start"]}
        lid = c["caseId"].split("_")[1]
        if lid in lines:
            x0, z0, x1, z1, kind = lines[lid]
            L = math.hypot(x1 - x0, z1 - z0)
            ux, uz = (x1 - x0) / L, (z1 - z0) / L
            along = (f["p"][0] - x0) * ux + (f["p"][2] - z0) * uz
            pts = m.line_points(x0, z0, x1, z1)
            gs = []
            for q in pts:
                p = by.get(m.seam_probe_xy(x0, z0, x1, z1, q) if kind == "seam" else q)
                gs.append(m.ground(p) if p else None)
            steps = [round(m.STEP * i, 2) for i in range(1, len(gs)) if gs[i] and gs[i - 1] and abs(gs[i]["y"] - gs[i - 1]["y"]) > 0.005]
            switches = [(round(m.STEP * i, 2), gs[i - 1]["mesh"], gs[i]["mesh"], round(gs[i]["y"] - gs[i - 1]["y"], 3))
                        for i in range(1, len(gs)) if gs[i] and gs[i - 1] and (gs[i]["path"] != gs[i - 1]["path"] or abs(gs[i]["y"] - gs[i - 1]["y"]) > 0.005)]
            before = [w for w in switches if w[0] <= along + 1e-6]
            last = before[-1] if before else None
            item.update({"alongPath": round(along, 3), "probedPathLength": round(L, 2),
                         "category": "beyond_probed_path" if along > L else "within_probed_path",
                         "lastMeshSwitchBefore": {"along": last[0], "from": last[1], "to": last[2], "dy": last[3],
                                                  "distanceAfter": round(along - last[0], 3)} if last else None,
                         "nearestStepAlong": min(steps, key=lambda s: abs(s - along)) if steps else None,
                         "stepDyThere": None})
            if steps:
                i = int(round(item["nearestStepAlong"] / m.STEP))
                item["stepDyThere"] = round(gs[i]["y"] - gs[i - 1]["y"], 3)
        else:
            item["category"] = "point_case"
        res.append(item)
    cnt = collections.Counter(x["category"] for x in res)
    out.write_text(json.dumps({"run": run, "classified": cls_path, "probe": Path(probe_path).name, "counts": cnt, "falls": res},
                              ensure_ascii=False, indent=1) + "\n")
    print(dict(cnt))
    for x in res:
        print(x["caseId"], x["category"], x.get("alongPath"), "/", x.get("probedPathLength"), "lastSwitch", x.get("lastMeshSwitchBefore"), x["fallPos"])


if __name__ == "__main__":
    main()
