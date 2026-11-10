#!/usr/bin/env python3
"""e2 probe 경로를 따라 시각 지면 높이와 실제 지지 콜라이더 윗면을 비교한다(읽기 전용, 판정 아님).

지지 후보: 시각면+0.3 아래에 윗면이 있는 비트리거 콜라이더(MeshCollider 포함). NPCMovementZone은 런타임에
NPCSpawnZone이 트리거로 바꾸므로 뺀다. 차이가 0.02m를 넘는 점이 두 개 이상 이어지면 구간으로 보고한다.

사용: python3 -I audit_support_e2.py <probe.json> <out.json>
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_explore_e2 as m  # noqa: E402

SKIP = ("NPCMovementZone",)
TOL = 0.02


def support_top(p, gy):
    tops = [(c["maxY"], c["path"]) for c in p["colliders"]
            if not any(s in c["path"] for s in SKIP) and -0.7 < c["maxY"] < gy + 0.3]
    return max(tops) if tops else None


def main():
    probe_path, out = sys.argv[1], Path(sys.argv[2])
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    d = json.loads(Path(probe_path).read_text())
    by = {(round(p["x"], 3), round(p["z"], 3)): p for p in d["points"]}
    segs = []
    for lid, x0, z0, x1, z1, desc, kind in m.all_lines():
        seg = None
        for q in m.line_points(x0, z0, x1, z1):
            p = by.get(m.seam_probe_xy(x0, z0, x1, z1, q) if kind == "seam" else q)
            g = m.ground(p) if p else None
            if not g:
                seg = None
                continue
            st = support_top(p, g["y"])
            dy = None if st is None else round(st[0] - g["y"], 3)
            key = [dy, st[1] if st else None]
            if dy is None or abs(dy) > TOL:
                if seg and seg["supportDy"] == key[0] and seg["supportCollider"] == key[1]:
                    seg["end"], seg["points"] = list(q), seg["points"] + 1
                else:
                    seg = {"line": lid, "start": list(q), "end": list(q), "points": 1, "visualY": round(g["y"], 3),
                           "visualMesh": g["mesh"], "supportDy": key[0], "supportCollider": key[1]}
                    segs.append(seg)
            else:
                seg = None
    segs = [s for s in segs if s["points"] >= 2]
    out.write_text(json.dumps({"probe": Path(probe_path).name, "tolerance": TOL, "skip": SKIP, "segments": segs},
                              ensure_ascii=False, indent=1) + "\n")
    for s in segs:
        print(s["line"], s["start"], "->", s["end"], s["points"], "visualY", s["visualY"], "dy", s["supportDy"], s["supportCollider"].split("/")[-1] if s["supportCollider"] else None)


if __name__ == "__main__":
    main()
