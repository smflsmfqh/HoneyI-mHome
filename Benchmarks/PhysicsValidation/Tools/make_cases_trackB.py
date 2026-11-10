#!/usr/bin/env python3
"""트랙 B 런타임 case 생성기.

바닥 세트(주장 ①·③): H7 e2c의 E2_C* 24건(연석 횡단 walk·jump) + CK 4건.
  - 시작 높이 = 해당 변형의 충돌면 윗면(기하 감사 geom-<V>.json, 시작점 최근접 1cm 점) + 0.05m.
    충돌면이 없으면 시각면 + 0.05m(빌드에서 비는지 보는 S0 계열은 에디터 형상 S0a 기준)
  - minAllowedY = 그 선의 시각 바닥 최저 높이. 낙하 판정은 thresholds-B1(0.5m 하강)
연석 세트(주장 ④): S2 road_009(도로→연석, +x 진행)·S3 road_019(도로→Curb_18, -x 진행) ×
  Sprint·Boost·BoostSprint × 가로 오프셋 -0.02/0/+0.02m = 18건 + CK 4건. 출발 = 시각 연석면에서 0.16m + 반지름.

사용: python3 -I make_cases_trackB.py <explore-e2c-H7.json> <geom_dir> <out_dir>
"""
import hashlib
import json
import sys
from pathlib import Path

FLOOR_VARIANTS = {"S0a": "S0a", "S0b": "S0a", "S0p": "S0a", "S1": "S1", "S2": "S2", "S3": "S3"}
LINES = {"C01": (-62.0, -40.0, -42.0, -40.0), "C03": (-16.0, 0.5, 4.0, 0.5), "C05": (-29.5, 12.0, -29.5, 30.0),
         "C07": (20.0, -60.0, 20.0, -73.0), "C09": (44.0, 0.0, 58.0, 0.0), "C11": (-81.5, 39.0, -60.0, 39.0)}
R_H7 = 0.0094
CURB_SPOTS = [
    # id, 연석면 x, z, 진행 yaw, 벽 법선(진행 반대), 출발 쪽 도로 시각 높이
    ("S2", -48.85, 52.5, 90.0, [-1.0, 0.0, 0.0], -0.1),
    ("S3", -56.14, 22.7, 270.0, [1.0, 0.0, 0.0], -0.1),
]
SPEEDS = ["Sprint", "Boost", "BoostSprint"]
OFFSETS = [-0.02, 0.0, 0.02]


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def line_of(case_id):
    n = int(case_id.split("_")[1][1:])
    base = "C%02d" % (n if n % 2 == 1 else n - 1)
    return base, n % 2 == 0


def geom_at(geom, line_id, reverse):
    L = next(l for l in geom["lines"] if l["id"] == line_id and abs(l["offset"]) < 1e-6)
    i = len(L["along"]) - 1 if reverse else 0
    vis = [v for v in L["visualY"] if v is not None]
    return L["colY"][i], L["visualY"][i], min(vis)


def main():
    src, geom_dir, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    doc = json.loads(src.read_text())
    ck = [c for c in doc["cases"] if c["id"] in ("CK01_flat_hold", "CK03_hole_walk_slow", "CK08_flat_walk_noinput", "CK13_flat_hold_timescale0")]
    cross = [c for c in doc["cases"] if c["id"].startswith("E2_C")]
    assert len(ck) == 4 and len(cross) == 24, (len(ck), len(cross))
    out.mkdir(parents=True)
    report = {"generatedBy": "Tools/make_cases_trackB.py", "source": src.name, "sourceSha256": sha(src), "files": {}}
    for v, gv in FLOOR_VARIANTS.items():
        gpath = geom_dir / f"geom-{gv}.json"
        geom = json.loads(gpath.read_text())
        cases, starts = [], {}
        for c in cross:
            lid, rev = line_of(c["id"])
            col, vis, vmin = geom_at(geom, lid, rev)
            x0, z0, x1, z1 = LINES[lid]
            sx, sz = (x1, z1) if rev else (x0, z0)
            assert abs(c["start"][0] - sx) < 1e-6 and abs(c["start"][2] - sz) < 1e-6, (c["id"], c["start"], sx, sz)
            base = col if col is not None else vis
            n = dict(c, start=[sx, round(base + 0.05, 4), sz], minAllowedY=round(vmin, 4),
                     notes=f"트랙 B {v}: 시작 = 충돌면({gv} 기하 {'있음' if col is not None else '없음→시각면'}) {base:.3f} + 0.05. minAllowedY = {lid} 시각 최저. 원 case {c['id']}")
            cases.append(n)
            starts[c["id"]] = n["start"][1]
        d = dict(doc, experimentId="M5-trackB", caseSetRevision="B1-floor", version="H7B-" + v, cases=ck + cases,
                 derivedFrom={"file": src.name, "sha256": sha(src), "geom": gpath.name, "geomSha256": sha(gpath)})
        p = out / f"trackB-floor-{v}.json"
        p.write_text(json.dumps(d, ensure_ascii=False, indent=1) + "\n")
        report["files"][p.name] = {"sha256": sha(p), "cases": len(d["cases"]), "geom": gpath.name, "startY": starts}
    tmpl = next(c for c in doc["cases"] if c["id"] == "E2_B01_edge")
    curb = []
    for sid, fx, fz, yaw, nrm, roadY in CURB_SPOTS:
        for sp in SPEEDS:
            for off in OFFSETS:
                d0 = 0.16 + R_H7
                sx = fx + nrm[0] * d0
                c = dict(tmpl, id=f"B_{sid}_{sp}_{'m' if off < 0 else ('p' if off > 0 else 'c')}{abs(int(round(off * 100))):02d}",
                         start=[round(sx, 4), round(roadY + 0.05, 4), round(fz + off, 4)], yaw=yaw, moveMode=sp,
                         minAllowedY=roadY, wallNormal=nrm, wallPoint=[fx, roadY, round(fz + off, 4)], wallTopY=0.1,
                         wallTargets=[],
                         notes=f"트랙 B 연석 돌진 {sid}: 시각 연석면 x={fx} 정면, 출발 0.16m+r, 가로 {off:+.2f}m, {sp}")
                curb.append(c)
    d = dict(doc, experimentId="M5-trackB", caseSetRevision="B1-curb", version="H7B-S3", cases=ck + curb,
             derivedFrom={"file": src.name, "sha256": sha(src), "template": "E2_B01_edge"})
    p = out / "trackB-curb-S3.json"
    p.write_text(json.dumps(d, ensure_ascii=False, indent=1) + "\n")
    report["files"][p.name] = {"sha256": sha(p), "cases": len(d["cases"])}
    (out / "trackB-cases-derivation.json").write_text(json.dumps(report, ensure_ascii=False, indent=1) + "\n")
    for k, v in report["files"].items():
        print(k, v["cases"], v["sha256"][:12])


if __name__ == "__main__":
    main()
