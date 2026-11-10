#!/usr/bin/env python3
"""M10 연석 일반화 case 생성(M10-r1). 결과를 보기 전에 실행하고, 선정 규칙·seed·출력 SHA를 freeze에 기록한다.

입력: Evidence/M10/m10-sites-raw.json (project-H7B 원본 씬 9d0bb583…에서 PhysicsBenchM10Sites로 만든 기하 후보, 물리 시뮬레이션 없음)
     Harness/Cases/M6-r1/m6-curb.json (CK 4개와 연석 case 필드 형식을 그대로 가져옴)
출력: Harness/Cases/M10-r1/m10-curb.json, m10-sites-selected.json

적격 후보(기하만):
  - 출발점 아래 바닥 -0.1±0.01, 면 안쪽 0.05m(연석 윗면) 0.1±0.01
  - c00·p02·m02·a30(+30°)·a30(-30°) 직선 경로의 첫 충돌이 대상 연석(joint면 이웃 연석 포함)
  - 출발 캡슐과 겹치는 비트리거 콜라이더 없음(개미와 충돌하는 레이어 기준, 런타임 트리거 구역은 트리거로 취급)
  - 면 길이 >= 0.3m, 설계에 쓴 도로 road_009(S2)·road_019(S3) 전체 제외(joint 이웃 Box가 그 도로에 속해도 제외)
층과 개수(seed 20261110, 층마다 정렬 후 random.Random(seed).sample):
  A 도로 직속 직선(축 정렬, mid): road_001·road_020·road_022 각 1
  B 곡선·사선 CurbColliders(mid): road_013 2, road_003 1 (같은 층·도로 안에서 서로 3m 이상)
  C 접합부(joint): road_020 2(적격 joint가 road_020에만 3개 있음. road_022 joint는 이웃이 road_019라 제외)
조건(지점마다 10): Sprint·Boost × c00·p02·m02·a30, BoostSprint a30, Move c00
"""
import hashlib
import json
import math
import random
import sys
from pathlib import Path

SEED = 20261110
R = 0.0094
GAP = 0.16 + R
EXP = "M10-r1"
STRATA = [("A", "mid", "road_001", 1), ("A", "mid", "road_020", 1), ("A", "mid", "road_022", 1),
          ("B", "mid", "road_013", 2), ("B", "mid", "road_003", 1),
          ("C", "joint", "road_020", 2)]
CONDS = [("Sprint", "c00"), ("Sprint", "p02"), ("Sprint", "m02"), ("Sprint", "a30"),
         ("Boost", "c00"), ("Boost", "p02"), ("Boost", "m02"), ("Boost", "a30"),
         ("BoostSprint", "a30"), ("Move", "c00")]


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def road(s):
    parts = s["box"].split("/")
    return parts[parts.index("CurbColliders") - 1] if "CurbColliders" in parts else parts[-1]


def eligible(s):
    g = lambda p: p["groundY"]
    return (abs(g(s["start"]) + 0.1) < 0.01 and abs(g(s["beyond"]) - 0.1) < 0.01 and all(s["pathHitTarget"])
            and not s["startOverlaps"] and s["faceLength"] >= 0.3 and road(s) not in ("road_009", "road_019")
            and not any(r in s["neighbor"].split("/") for r in ("road_009", "road_019")))


def main():
    root = Path(sys.argv[1])
    raw = root / "Evidence/M10/m10-sites-raw.json"
    out = root / "Harness/Cases" / EXP
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    sites = json.loads(raw.read_text())["sites"]
    el = [s for s in sites if eligible(s)]
    rng = random.Random(SEED)
    chosen = []
    for stratum, kind, rd, n in STRATA:
        pool = sorted((s for s in el if s["kind"] == kind and road(s) == rd), key=lambda s: (s["box"], s["point"]["x"], s["point"]["z"]))
        pick = []
        for s in rng.sample(pool, len(pool)):  # 무작위 순서로 훑으며 3m 간격 조건을 만족하는 것부터
            if all(math.dist((s["point"]["x"], s["point"]["z"]), (q["point"]["x"], q["point"]["z"])) >= 3.0 for q in pick):
                pick.append(s)
            if len(pick) == n:
                break
        assert len(pick) == n, (rd, kind, len(pool))
        for s in pick:
            chosen.append((stratum, s))
    base = json.loads((root / "Harness/Cases/M6-r1/m6-curb.json").read_text())
    tmpl = next(c for c in base["cases"] if c["id"] == "B_S2_Sprint_c00")
    cases = [c for c in base["cases"] if c["id"].startswith("CK")]
    assert len(cases) == 4
    sel = []
    for i, (stratum, s) in enumerate(chosen, 1):
        sid = f"G{i:02d}"
        p = (s["point"]["x"], s["point"]["z"])
        nx, nz = s["normal"]["x"], s["normal"]["z"]
        ax, az = -nz, nx  # 면 방향(법선을 +90° 돌린 것). 오프셋 부호는 이 축 기준
        sel.append({"site": sid, "stratum": stratum, "kind": s["kind"], "road": road(s), "box": s["box"], "neighbor": s["neighbor"],
                    "point": [round(p[0], 4), round(p[1], 4)], "normal": [round(nx, 6), round(nz, 6)], "faceLength": round(s["faceLength"], 3)})
        for mode, cond in CONDS:
            off = {"c00": 0.0, "p02": 0.02, "m02": -0.02, "a30": 0.0}[cond]
            ang = 30.0 if cond == "a30" else 0.0
            # 진행 방향 = -법선을 y축으로 ang만큼 회전(Unity yaw: 0=+z, 90=+x, 시계 방향 +)
            hx, hz = -nx, -nz
            a = math.radians(ang)
            dx, dz = hx * math.cos(a) + hz * math.sin(a), -hx * math.sin(a) + hz * math.cos(a)
            dist = GAP / math.cos(a)
            cx, cz = p[0] + ax * off, p[1] + az * off
            st = [round(cx - dx * dist, 4), -0.05, round(cz - dz * dist, 4)]
            yaw = round(math.degrees(math.atan2(dx, dz)) % 360.0, 3)
            c = dict(tmpl)
            c.update({"id": f"B_{sid}_{mode}_{cond}", "start": st, "yaw": yaw, "moveMode": mode,
                      "wallNormal": [round(nx, 6), 0.0, round(nz, 6)], "wallPoint": [round(cx, 4), -0.1, round(cz, 4)], "wallTopY": 0.1,
                      "notes": f"M10 {sid}({stratum}/{s['kind']}) {road(s)} 연석 콜라이더 면 접점 ({cx:.3f},{cz:.3f}), 출발 0.16m+r, {mode} {cond}"})
            cases.append(c)
    doc = {"experimentId": EXP, "caseSetRevision": "m10-curb", "version": "H7", "generatedBy": "Tools/make_cases_m10.py",
           "m10Source": {"sites": str(raw.relative_to(root)), "sitesSha256": sha(raw), "seed": SEED, "template": "M6-r1/m6-curb.json B_S2_Sprint_c00 + CK 4"},
           "cases": cases}
    out.mkdir(parents=True)
    (out / "m10-curb.json").write_text(json.dumps(doc, ensure_ascii=False, indent=1) + "\n")
    (out / "m10-sites-selected.json").write_text(json.dumps({"seed": SEED, "eligible": len(el), "sites": sel}, ensure_ascii=False, indent=1) + "\n")
    print(json.dumps({"eligible": len(el), "sites": sel, "cases": len(cases), "sha256": sha(out / "m10-curb.json")}, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
