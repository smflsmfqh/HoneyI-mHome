#!/usr/bin/env python3
"""트랙 B 기하 감사 분석기(결과 보기 전 고정, Docs/physics-collider-trackB-plan.md 5절).

정의
- 오차 e = 충돌면 윗면 y - 시각 바닥 윗면 y (같은 xz). 시각면이 없는 점은 제외
- 구멍: 충돌면이 없거나 e < -0.05m
- 전환점: 같은 선에서 이웃한 두 점의 시각 높이 차가 0.05m 이상인 곳(연석·단차)
- 연석 구간: 전환점 기준 ±0.6m 안의 점
- 단차 소실: 전환점 앞뒤 0.10m 지점의 충돌면 높이 차가 시각 높이 차의 50% 미만(부호 같음 기준)
- 지표: 전체·연석 구간의 |e| p50/p95/max(mm), 구멍 비율, 단차 소실 수/전환점 수

사용: python3 -I analyze_trackB_geom.py <out.json> <geom-V1.json> [<geom-V2.json> ...]
"""
import json
import sys
from pathlib import Path

STEP_MIN = 0.05
WINDOW = 0.6
SIDE = 0.10
HOLE = -0.05


def pct(xs, q):
    if not xs:
        return None
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(q * len(xs)))]


def analyze(path):
    d = json.loads(Path(path).read_text())
    allerr, curberr, holes, npts = [], [], 0, 0
    trans_total, trans_lost = 0, 0
    per_line = {}
    for L in d["lines"]:
        v, c, s = L["visualY"], L["colY"], L["along"]
        step = L["step"]
        n = len(v)
        trans = [i for i in range(1, n) if v[i] is not None and v[i - 1] is not None and abs(v[i] - v[i - 1]) >= STEP_MIN]
        near = [False] * n
        w = int(round(WINDOW / step))
        for t in trans:
            for j in range(max(0, t - w), min(n, t + w + 1)):
                near[j] = True
        le, lc, lh, lp = [], [], 0, 0
        for i in range(n):
            if v[i] is None:
                continue
            lp += 1
            if c[i] is None or c[i] - v[i] < HOLE:
                lh += 1
            if c[i] is None:
                continue
            e = abs(c[i] - v[i]) * 1000
            le.append(e)
            if near[i]:
                lc.append(e)
        k = int(round(SIDE / step))
        lost = 0
        for t in trans:
            a, b = t - 1 - k, t + k
            if a < 0 or b >= n or v[a] is None or v[b] is None or c[a] is None or c[b] is None:
                lost += 1
                continue
            dv, dc = v[b] - v[a], c[b] - c[a]
            if abs(dv) < 1e-6 or dc * dv <= 0 or abs(dc) < 0.5 * abs(dv):
                lost += 1
        key = "%s@%+.2f" % (L["id"], L["offset"])
        per_line[key] = {"points": lp, "holes": lh, "transitions": len(trans), "stepLost": lost,
                         "absErrP95mm": pct(le, 0.95), "curbAbsErrP95mm": pct(lc, 0.95), "absErrMaxmm": max(le) if le else None}
        allerr += le
        curberr += lc
        holes += lh
        npts += lp
        trans_total += len(trans)
        trans_lost += lost
    r = d["record"]
    return {"variant": d["variant"], "source": str(path),
            "record": {k: r.get(k) for k in ("floorObjects", "boxesDisabled", "zonesMadeTrigger", "runtimeTriggerZones", "meshCollidersAdded", "cooking", "convex", "plateAdded", "plateTopY", "floorMeshesReadable")},
            "logWarnings": len(r.get("logWarnings") or []),
            "points": npts, "holeRate": holes / npts if npts else None,
            "absErrMm": {"p50": pct(allerr, 0.5), "p95": pct(allerr, 0.95), "max": max(allerr) if allerr else None},
            "curbAbsErrMm": {"n": len(curberr), "p50": pct(curberr, 0.5), "p95": pct(curberr, 0.95), "max": max(curberr) if curberr else None},
            "transitions": trans_total, "stepLost": trans_lost,
            "perLine": per_line}


def main():
    out = Path(sys.argv[1])
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    res = [analyze(p) for p in sys.argv[2:]]
    out.write_text(json.dumps({"analyzer": "Tools/analyze_trackB_geom.py", "results": res}, ensure_ascii=False, indent=1) + "\n")
    for r in res:
        print("%-4s pts=%d holes=%.3f |e| p95=%s max=%s | curb n=%d p95=%s | steps lost %d/%d | warn %d" % (
            r["variant"], r["points"], r["holeRate"], fmt(r["absErrMm"]["p95"]), fmt(r["absErrMm"]["max"]),
            r["curbAbsErrMm"]["n"], fmt(r["curbAbsErrMm"]["p95"]), r["stepLost"], r["transitions"], r["logWarnings"]))


def fmt(x):
    return "-" if x is None else "%.1fmm" % x


if __name__ == "__main__":
    main()
