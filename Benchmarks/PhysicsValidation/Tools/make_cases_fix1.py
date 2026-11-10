#!/usr/bin/env python3
"""b·c 단계 case 생성기(fix1). 결과 전 고정.

fix1-b.json (연석 수정 회귀, 62건)
  - CK 4(판정 검증)
  - E2_C 24: trackB-floor-S3.json의 연석 횡단 walk·jump(연석 오르기·내려오기·점프 착지 포함). 윗면이 같으므로 시작 높이 그대로
  - 연석 34: trackB-curb-S3.json 직진 18(Sprint·Boost·BoostSprint × 가로 -0.02/0/+0.02)
            + 정면 Roll·Move 각 1 × 2지점(4)
            + 비스듬한 진입 30°·60° × Sprint·BoostSprint·Roll × 2지점(12). 같은 연석 접점을 겨냥, 연석면까지 수직거리 0.16m+r
fix1-c.json (cooking 복원 검증, 12건): CK 4 + e2c H7 E2_P 소품 돌진 8(정의 그대로)

사용: python3 -I make_cases_fix1.py <PhysicsValidation 루트> <out_dir>
"""
import hashlib
import json
import math
import sys
from pathlib import Path

R_H7 = 0.0094
SPOTS = [("S2", -48.85, 52.5, 90.0, [-1.0, 0.0, 0.0]), ("S3", -56.14, 22.7, 270.0, [1.0, 0.0, 0.0])]
ROAD_Y = -0.1


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def main():
    root, out = Path(sys.argv[1]), Path(sys.argv[2])
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    floor_src = root / "Harness/Cases/trackB/trackB-floor-S3.json"
    curb_src = root / "Harness/Cases/trackB/trackB-curb-S3.json"
    e2c_src = root / "Harness/Cases/e2c/explore-e2c-H7.json"
    floor = json.loads(floor_src.read_text())
    curb = json.loads(curb_src.read_text())
    e2c = json.loads(e2c_src.read_text())
    ck = [c for c in floor["cases"] if c["id"].startswith("CK")]
    cross = [c for c in floor["cases"] if c["id"].startswith("E2_C")]
    straight = [c for c in curb["cases"] if c["id"].startswith("B_")]
    assert len(ck) == 4 and len(cross) == 24 and len(straight) == 18
    tmpl = straight[0]
    extra = []
    d0 = 0.16 + R_H7
    for sid, fx, fz, yaw0, nrm in SPOTS:
        for mode in ("Roll", "Move"):
            sx = fx + nrm[0] * d0
            extra.append(dict(tmpl, id=f"B_{sid}_{mode}_c00", start=[round(sx, 4), round(ROAD_Y + 0.05, 4), fz], yaw=yaw0, moveMode=mode,
                              wallNormal=nrm, wallPoint=[fx, ROAD_Y, fz],
                              notes=f"fix1 연석 정면 {sid} {mode}, 출발 0.16m+r"))
        for ang in (30, 60):
            for mode in ("Sprint", "BoostSprint", "Roll"):
                yaw = yaw0 + ang
                rad = math.radians(yaw)
                dx, dz = math.sin(rad), math.cos(rad)
                # 진행 방향의 연석 법선 성분(= -nrm 방향)이 cos(ang)이므로 연석면까지 수직거리 d0가 되도록 거리 d0/cos
                dist = d0 / math.cos(math.radians(ang))
                sx, sz = fx - dx * dist, fz - dz * dist
                extra.append(dict(tmpl, id=f"B_{sid}_{mode}_a{ang:02d}", start=[round(sx, 4), round(ROAD_Y + 0.05, 4), round(sz, 4)], yaw=yaw,
                                  moveMode=mode, wallNormal=nrm, wallPoint=[fx, ROAD_Y, fz],
                                  notes=f"fix1 연석 비스듬 진입 {sid} {ang}° {mode}, 접점 ({fx},{fz}) 겨냥, 연석면까지 수직 0.16m+r"))
    assert len(extra) == 16
    out.mkdir(parents=True)
    b = dict(floor, experimentId="M5-fix1", caseSetRevision="fix1-b", version="H7", cases=ck + cross + straight + extra,
             derivedFrom={"floor": floor_src.name, "floorSha256": sha(floor_src), "curb": curb_src.name, "curbSha256": sha(curb_src)})
    pb = out / "fix1-b.json"
    pb.write_text(json.dumps(b, ensure_ascii=False, indent=1) + "\n")
    props = [c for c in e2c["cases"] if c["id"].startswith("E2_P")]
    ck_c = [c for c in e2c["cases"] if c["id"] in {x["id"] for x in ck}]
    assert len(props) == 8 and len(ck_c) == 4, (len(props), len(ck_c))
    c = dict(e2c, experimentId="M5-fix1", caseSetRevision="fix1-c", cases=ck_c + props,
             derivedFrom={"e2c": e2c_src.name, "sha256": sha(e2c_src)})
    pc = out / "fix1-c.json"
    pc.write_text(json.dumps(c, ensure_ascii=False, indent=1) + "\n")
    rep = {"generatedBy": "Tools/make_cases_fix1.py", "files": {pb.name: {"sha256": sha(pb), "cases": len(b["cases"])}, pc.name: {"sha256": sha(pc), "cases": len(c["cases"])}}}
    (out / "fix1-derivation.json").write_text(json.dumps(rep, ensure_ascii=False, indent=1) + "\n")
    print(json.dumps(rep, ensure_ascii=False))


if __name__ == "__main__":
    main()
