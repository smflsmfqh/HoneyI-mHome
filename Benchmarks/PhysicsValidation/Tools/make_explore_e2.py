#!/usr/bin/env python3
"""M5 탐색 case e2 생성기.

e1(H0 4m 격자 지점)의 시작 높이·minAllowedY가 H0 맵 기준이던 문제를 고친다. case ID와 행동은 모든 버전에서 같고,
버전별 파일에서는 시작 y·minAllowedY·notes만 그 버전의 시각 메시 probe(PhysicsBenchProbe)에서 다시 읽는다.
결과를 보기 전에 생성하며, 판정 임계값(thresholds-r4)은 건드리지 않는다.

사용:
  python3 -I make_explore_e2.py points <out.txt>
      probe에 넘길 x,z 목록(e1 지도 지점 + e2 경로 0.25m 간격 + 낙하 지점 후보)
  python3 -I make_explore_e2.py proppoints <probe-H7.json> <out.txt>
      H7 소품 돌진 case 출발점 x,z 목록(첫 probe의 MeshCollider 범위에서 계산, 두 번째 probe에 넘긴다)
  python3 -I make_explore_e2.py cases <version> <out_cases.json> <probe.json> [<probe2.json> ...] [--props] --seams-from <probe-H0.json>
      --props: 첫 probe의 MeshCollider로 H7 소품 돌진 case를 추가(H7 전용)
      --seams-from: 이음매 정지·낙하 지점을 고를 기준 probe(모든 버전에 같은 파일)
"""
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
CASES_DIR = HERE.parent / "Harness" / "Cases"
E1 = CASES_DIR / "explore-e1.json"

STEP = 0.25
# 경로 길이로 이동 시간을 정할 때 쓰는 속도. H0 e1 Mac 보행 trace의 실제 최고 수평 속도(3.0m/s).
# 느린 버전(H1 0.58m/s 등)은 같은 시간 동안 더 짧게 간다. 이동 거리는 trace로 따로 남는다.
H0_SPEED = 3.0
# 시각 지면으로 보는 높이 상한. 그 위의 면은 소품·차량 윗면으로 본다.
GROUND_MAX_Y = 0.5
DROP_HEIGHT = 1.0

# (id, 시작 x, 시작 z, 끝 x, 끝 z, 설명). 시작→끝 방향으로 걷는다. 좌표는 H0 4m 격자 도로 열·행(probe-H0-grid4m.json).
LONG_LINES = [
    ("L01", -52.0, -65.0, -52.0, 63.0, "서쪽 남북 도로 중앙"),
    ("L02", -6.0, -65.0, -6.0, 63.0, "가운데 남북 도로 중앙"),
    ("L03", 52.0, 63.0, 52.0, -65.0, "동쪽 남북 도로 중앙(남향)"),
    ("L04", -88.0, -69.0, 56.0, -69.0, "남쪽 동서 도로 중앙"),
    ("L05", 56.0, 67.0, -88.0, 67.0, "북쪽 동서 도로 중앙(서향)"),
    ("L06", -48.0, 21.0, 48.0, 21.0, "가운데 동서 도로 중앙"),
]
# 인도→연석→도로→연석→인도를 가로지르는 짧은 선. 같은 선을 양방향으로 걷는다.
CROSS_LINES = [
    ("C01", -62.0, -40.0, -42.0, -40.0, "서쪽 도로 횡단"),
    ("C03", -16.0, 0.5, 4.0, 0.5, "가운데 남북 도로 횡단"),
    ("C05", -29.5, 12.0, -29.5, 30.0, "가운데 동서 도로 횡단"),
    ("C07", 20.0, -60.0, 20.0, -73.0, "남쪽 도로 진입(맵 가장자리 방향)"),
    ("C09", 44.0, 0.0, 58.0, 0.0, "동쪽 도로 횡단"),
    ("C11", -81.5, 39.0, -60.0, 39.0, "H0 높이 0.1 타일 구역 횡단(출발점은 분수 AABB 동쪽 0.5m 밖)"),
]
# 15m 인도 타일의 이음매 바로 위를 따라 걷는 선. 이음매 위 광선은 두 삼각형 경계에 걸려 시각면을 못 찾으므로
# 높이는 진행 방향 오른쪽 SEAM_OFFSET 지점에서 읽는다(e2 probe 1차에서 z=0·x=-30 선이 '지면 없음'으로 나옴).
SEAM_LINES = [
    ("M01", -16.0, 0.0, -1.0, 0.0, "인도 타일 이음매(z=0) 위 동쪽 보행"),
    ("M02", -30.0, 15.5, -30.0, 29.5, "인도 타일 이음매(x=-30) 위 북쪽 보행"),
]
SEAM_OFFSET = 0.02
# H7 1차 probe(probe-H7-e2.json) 감사: 남쪽 도로(z -76.5~-58.5)의 x -46.5~-28.5, 13.5~46.5 구간은 시각 도로면(-0.1) 위를
# 윗면 0.0인 FloorColliders/road_009_floor Box가 덮는다. Box 끝은 도로 한가운데의 보이지 않는 0.1m 단차다.
# 단차 1m 앞(L04 선 위 점)에서 Sprint로 다가가 연석 시나리오를 수행한다. 다른 버전에서는 같은 위치의 대조군이다.
# (id, 출발 x, z, yaw, 기준 면 x, 법선 x)
BOX_EDGES = [
    ("B01", -47.5, -69.0, 90.0, -46.5, -1.0),
    ("B02", -27.5, -69.0, 270.0, -28.5, 1.0),
    ("B03", 12.5, -69.0, 90.0, 13.5, -1.0),
    ("B04", 47.5, -69.0, 270.0, 46.5, 1.0),
]
# 사후 표적(e2 2판): Mac M5-Hm1-unfreezeY-mac-e2-01(재구성 통제, Windows 증거 아님)에서 경로 안 낙하가 난 지점.
# (id, 선, 시작점에서 낙하 지점까지 거리(0.25m 격자로 반올림)). 같은 선 위 1.5m 앞에서 출발해 1.5m 넘어가게 걷고,
# 낙하 지점에서 정지한다. C05(경계가 선 끝과 겹침)는 C06이 같은 종류라 뺐다.
TARGETS = [
    ("K01", "C01", 13.75), ("K02", "C02", 14.75), ("K03", "C07", 11.75), ("K04", "C08", 9.75),
    ("K05", "C09", 12.75), ("K06", "C10", 9.75), ("K07", "C03", 16.5), ("K08", "C06", 15.5),
    ("K09", "C04", 19.5), ("K10", "L06", 48.5), ("K11", "L05", 116.5), ("K12", "C11", 7.0),
]
TARGET_LEAD = 1.5
# 버전별 실측 최고 수평 속도(m/s): Hm1 Mac e2(MovePosition 5), H0 e1 trace 3.0, H1 e1 trace 0.58, H7은 H1과 같은 값을 쓴다.
VERSION_SPEED = {"Hm1": 5.0, "H0": 3.0, "H1": 0.58, "H7": 0.58}
# 이음매 정지·낙하 지점은 H0 probe의 긴 경로에서 고른 뒤 모든 버전에 같은 xz로 쓴다(case ID 일치).
MAX_SEAMS_PER_LINE = 2


def line_points(x0, z0, x1, z1):
    n = int(round(math.hypot(x1 - x0, z1 - z0) / STEP))
    return [(round(x0 + (x1 - x0) * i / n, 3), round(z0 + (z1 - z0) * i / n, 3)) for i in range(n + 1)]


def yaw_of(x0, z0, x1, z1):
    # Unity yaw 0 = +z, 90 = +x
    return round(math.degrees(math.atan2(x1 - x0, z1 - z0)) % 360.0, 3)


def seam_probe_xy(x0, z0, x1, z1, q):
    """이음매 선 위 점 q의 높이를 읽을 지점(진행 방향 오른쪽으로 SEAM_OFFSET)."""
    L = math.hypot(x1 - x0, z1 - z0)
    dx, dz = (x1 - x0) / L, (z1 - z0) / L
    return round(q[0] + dz * SEAM_OFFSET, 3), round(q[1] - dx * SEAM_OFFSET, 3)


def all_lines():
    lines = [(i, a, b, c, d, n, "long") for i, a, b, c, d, n in LONG_LINES]
    lines += [(i, a, b, c, d, n, "seam") for i, a, b, c, d, n in SEAM_LINES]
    for i, a, b, c, d, n in CROSS_LINES:
        lines.append((i, a, b, c, d, n, "cross"))
        rid = "C%02d" % (int(i[1:]) + 1)
        lines.append((rid, c, d, a, b, n + " 역방향", "cross"))
    return lines


def e1_map_points():
    e1 = json.loads(E1.read_text())
    return sorted({(c["start"][0], c["start"][2]) for c in e1["cases"] if c["environment"] == "map"})


def cmd_points(out):
    pts = list(e1_map_points())
    for _, x0, z0, x1, z1, _, kind in all_lines():
        lp = line_points(x0, z0, x1, z1)
        pts += [seam_probe_xy(x0, z0, x1, z1, q) for q in lp] if kind == "seam" else lp
    seen, uniq = set(), []
    for p in pts:
        if p not in seen:
            seen.add(p)
            uniq.append(p)
    if Path(out).exists():
        raise SystemExit("exists, refusing to overwrite: " + out)
    Path(out).write_text("".join(f"{x},{z}\n" for x, z in uniq))
    print(len(uniq), "points")


def ground(point):
    """시각 지면(가장 높은 GROUND_MAX_Y 이하의 위쪽 면). 없으면 None."""
    for s in point["surfaces"]:
        if s["y"] <= GROUND_MAX_Y and s["normalY"] >= 0.9:
            return s
    return None


def r3(v):
    return round(v + 0.0, 3)


def reference_seams(ref_path):
    """기준 probe의 긴 경로에서 같은 높이로 지면 메시가 바뀌는 지점."""
    ref = json.loads(Path(ref_path).read_text())
    by = {(round(p["x"], 3), round(p["z"], 3)): p for p in ref["points"]}
    out = []
    for lid, x0, z0, x1, z1, _, kind in all_lines():
        if kind != "long":
            continue
        pts = line_points(x0, z0, x1, z1)
        gs = [ground(by[q]) if q in by else None for q in pts]
        for i in range(1, len(pts)):
            a, b = gs[i - 1], gs[i]
            if a is None or b is None:
                break
            if a["path"] != b["path"] and abs(a["y"] - b["y"]) <= 0.005:
                out.append((lid, pts[i], a["mesh"], b["mesh"]))
    return out


def cmd_cases(version, out, probe_paths, props=False, seam_ref=None):
    if Path(out).exists():
        raise SystemExit("exists, refusing to overwrite: " + out)
    probes = [json.loads(Path(pp).read_text()) for pp in probe_paths]
    by_xz = {}
    for probe in probes:
        for p in probe.get("points") or []:
            by_xz.setdefault((round(p["x"], 3), round(p["z"], 3)), p)
    e1 = json.loads(E1.read_text())
    src = "+".join(Path(pp).name for pp in probe_paths)
    cases, issues = [], []

    # 1) 판정기 검증 4건은 e1 그대로
    for c in e1["cases"]:
        if c["environment"] != "map":
            cases.append(dict(c))

    # 2) e1 지도 case: 같은 ID·행동, 시작 y와 minAllowedY만 이 버전 probe로
    for c in e1["cases"]:
        if c["environment"] != "map":
            continue
        n = dict(c)
        p = by_xz.get((round(c["start"][0], 3), round(c["start"][2], 3)))
        g = ground(p) if p else None
        if g is None:
            issues.append(n["id"] + ": no ground surface in " + src)
            continue
        y = r3(g["y"])
        n["start"] = [c["start"][0], y, c["start"][2]]
        if c["scenario"] == "hold":
            n["minAllowedY"] = y
        else:
            # 1초 보행·점프 경로의 합법 최저면은 맵 공통 도로면(-0.1)과 출발면 중 낮은 쪽
            n["minAllowedY"] = min(-0.1, y)
        n["notes"] = f"{g['mesh']} 시각 표면 y={y} ({version} {src}). e1과 같은 행동"
        cases.append(n)

    # 3) e2 경로
    ref_first_step = {}
    if seam_ref is not None:
        ref = json.loads(Path(seam_ref).read_text())
        rby = {(round(p["x"], 3), round(p["z"], 3)): p for p in ref["points"]}
        for lid, x0, z0, x1, z1, _, kind in all_lines():
            if kind != "cross":
                continue
            gs = [ground(rby[q]) if q in rby else None for q in line_points(x0, z0, x1, z1)]
            ref_first_step[lid] = next((i for i in range(1, len(gs)) if gs[i] and gs[i - 1] and abs(gs[i]["y"] - gs[i - 1]["y"]) > 0.005), None)
    template = next(c for c in e1["cases"] if c["scenario"] == "walk" and c["environment"] == "map")
    seams = []
    for lid, x0, z0, x1, z1, desc, kind in all_lines():
        pts = line_points(x0, z0, x1, z1)
        grounds = []
        for q in pts:
            p = by_xz.get(seam_probe_xy(x0, z0, x1, z1, q) if kind == "seam" else q)
            grounds.append(ground(p) if p else None)
        # 한 점(0.25m)짜리 빈 곳은 타일 경계선 위 광선이 두 삼각형 사이에 걸린 probe 한계로 보고 앞 점으로 채운다.
        # 두 점 이상 이어진 빈 곳만 실제 지면 없음으로 본다.
        filled = 0
        for i in range(1, len(grounds) - 1):
            if grounds[i] is None and grounds[i - 1] is not None and grounds[i + 1] is not None:
                grounds[i] = grounds[i - 1]
                filled += 1
        holes = [pts[i] for i, g in enumerate(grounds) if g is None]
        # 출발점부터 처음 지면이 없는 곳 직전까지만 쓴다(맵 바깥 낙하는 버그가 아니다).
        usable = len(grounds) if not holes else next(i for i, g in enumerate(grounds) if g is None)
        if usable < 8:
            issues.append(f"{lid}: start has no continuous ground ({len(holes)} holes)")
            continue
        g_used = grounds[:usable]
        start_y = r3(g_used[0]["y"])
        min_y = r3(min(g["y"] for g in g_used))
        dist = STEP * (usable - 1)
        changes = []
        for i in range(1, usable):
            a, b = g_used[i - 1], g_used[i]
            if a["path"] != b["path"] or abs(a["y"] - b["y"]) > 0.005:
                changes.append({"at": pts[i], "from": a["mesh"], "to": b["mesh"], "dy": r3(b["y"] - a["y"])})
                if abs(a["y"] - b["y"]) <= 0.005 and kind == "long":
                    seams.append((lid, pts[i], a["mesh"], b["mesh"]))
        yaw = yaw_of(x0, z0, x1, z1)
        move_sec = round(dist / H0_SPEED, 2)
        base = dict(template)
        base.update({"start": [x0, start_y, z0], "yaw": yaw, "moveMode": "Move", "moveSec": move_sec,
                     "observeSec": 1.5, "minAllowedY": min_y, "expect": ""})
        steps = sum(1 for c in changes if abs(c["dy"]) > 0.005)
        if kind == "seam":
            desc += f"(높이는 진행 방향 오른쪽 {SEAM_OFFSET}m 지점 probe)"
        note = (f"{desc}. {version} {src}: 사용 구간 {dist:.2f}m(H0 3m/s 기준 {move_sec}s), 지면 메시 전환 {len(changes)}회 "
                f"(높이 변화 {steps}회), 경로 최저 시각면 {min_y}" + (f", 지면 없음 {len(holes)}점으로 앞에서 자름" if holes else "")
                + (f", 경계선 한 점 빈 곳 {filled}개는 앞 점 높이로 채움" if filled else ""))
        w = dict(base, id=f"E2_{lid}_walk", scenario="walk", notes=note)
        cases.append(w)
        if kind == "cross":
            # 기준 probe(H0)의 첫 높이 변화(연석) 0.15m 앞(H0 속도 기준)에서 이동 중 점프. 모든 버전 같은 시각.
            first = ref_first_step.get(lid)
            dist_j = STEP * first if first is not None else dist / 2
            t = max(0.0, round((dist_j - 0.15) / H0_SPEED, 2))
            cases.append(dict(base, id=f"E2_{lid}_jump", scenario="jump", jumpAtSec=t, observeSec=2.0,
                              notes=note + f". 기준 probe 첫 높이 변화 {dist_j:.2f}m 지점 앞에서 이동 중 점프(t={t}s, H0 속도 기준)"))

    # 3b) H7 도로 위 Box 단차
    smoke = json.loads((CASES_DIR / "smoke-s2.json").read_text())
    curb_t = next(c for c in smoke["cases"] if c["scenario"] == "curb" and c["environment"] == "map")
    for bid, x, z, yaw, wx, nx in BOX_EDGES:
        p = by_xz.get((x, z))
        g = ground(p) if p else None
        if g is None:
            issues.append(f"{bid}: no ground at {x},{z} in {src}")
            continue
        gy = r3(g["y"])
        # H0·Hm1에는 Roll 액션이 없다(입력 미수신 하네스 오류가 됨). 이 두 버전만 구제 구르기를 뺀다.
        no_roll = version in ("H0", "Hm1")
        cases.append(dict(curb_t, id=f"E2_{bid}_edge", rescueRoll=not no_roll, start=[x, gy, z], yaw=yaw, moveMode="Sprint", minAllowedY=min(-0.1, gy),
                          wallNormal=[nx, 0.0, 0.0], wallPoint=[wx, gy, z], wallTopY=0.0, wallPlaneTolerance=0.0, wallTargets=[], expect="",
                          notes=f"H7 도로 위 road_009_floor Box 끝(x={wx}, 윗면 0.0) 1m 앞에서 Sprint 접근. 출발 시각면 y={gy} ({version} {src}). "
                                "H7 외 버전은 같은 위치 대조군(Box 없으면 no_wall_contact가 정상)"
                                + (". 이 버전에는 Roll 액션이 없어 구제 구르기 생략" if no_roll else "")))

    # 3c) 사후 표적: 경계 앞 짧은 보행 + 낙하 지점 정지
    line_def = {lid: (x0, z0, x1, z1) for lid, x0, z0, x1, z1, _, _ in all_lines()}
    speed = VERSION_SPEED[version]
    hold_tpl = next(c for c in e1["cases"] if c["scenario"] == "hold" and c["environment"] == "map")
    for kid, lid, along in TARGETS:
        x0, z0, x1, z1 = line_def[lid]
        L = math.hypot(x1 - x0, z1 - z0)
        ux, uz = (x1 - x0) / L, (z1 - z0) / L
        a0 = max(0.0, along - TARGET_LEAD)
        a1 = min(L, along + TARGET_LEAD)
        if a1 - along < 0.5:
            issues.append(f"{kid}: end of {lid} too close to target")
        sp = (round(x0 + ux * a0, 3), round(z0 + uz * a0, 3))
        tp = (round(x0 + ux * along, 3), round(z0 + uz * along, 3))
        gs, gt = ground(by_xz[sp]) if sp in by_xz else None, ground(by_xz[tp]) if tp in by_xz else None
        if gs is None or gt is None:
            issues.append(f"{kid}: start/target not probed {sp} {tp}")
            continue
        seg = [ground(by_xz[q]) for q in line_points(x0, z0, x1, z1) if q in by_xz
               and a0 - 1e-6 <= (q[0] - x0) * ux + (q[1] - z0) * uz <= a1 + 1e-6]
        min_y = r3(min(g["y"] for g in seg if g))
        move_sec = round((a1 - a0) / speed, 2)
        note = (f"사후 표적: Mac Hm1-unfreezeY 재구성 통제에서 {lid} 시작점 {along}m 지점 낙하. 그 1.5m 앞에서 같은 방향으로 "
                f"{a1 - a0:.2f}m 보행({version} 실측 {speed}m/s 기준 {move_sec}s). 구간 최저 시각면 {min_y} ({version} {src})")
        cases.append(dict(template, id=f"E2_{kid}_walk", scenario="walk", start=[sp[0], r3(gs["y"]), sp[1]], yaw=yaw_of(x0, z0, x1, z1),
                          moveMode="Move", moveSec=move_sec, observeSec=1.5, minAllowedY=min_y, expect="", notes=note))
        cases.append(dict(hold_tpl, id=f"E2_{kid}_hold", start=[tp[0], r3(gt["y"]), tp[1]], yaw=yaw_of(x0, z0, x1, z1),
                          minAllowedY=r3(gt["y"]), expect="",
                          notes=f"사후 표적 {lid} 낙하 지점 위 정지. 시각면 {gt['mesh']} y={r3(gt['y'])} ({version} {src})"))

    # 4) 같은 높이 메시 이음매: 정지·1m 낙하(스트레스, 행동 case와 구분). 지점은 기준 probe(H0)에서 고른다.
    if seam_ref is not None:
        seams = reference_seams(seam_ref)
    picked, used_lines = [], {}
    for s in seams:
        if used_lines.get(s[0], 0) < MAX_SEAMS_PER_LINE:
            used_lines[s[0]] = used_lines.get(s[0], 0) + 1
            picked.append(s)
    hold_t = next(c for c in e1["cases"] if c["scenario"] == "hold" and c["environment"] == "map")
    for k, (lid, (x, z), ma, mb) in enumerate(picked):
        p = by_xz.get((x, z))
        g = ground(p) if p else None
        if g is None:
            issues.append(f"E2_S{k:02d}: no ground at seam {x},{z} in {src}")
            continue
        y = r3(g["y"])
        base = dict(hold_t, start=[x, y, z], minAllowedY=y, expect="", yaw=0.0)
        cases.append(dict(base, id=f"E2_S{k:02d}_hold", notes=f"{lid} 경로의 같은 높이 메시 이음매 {ma}→{mb}, 시각면 y={y} ({version} {src}). 이음매 위 정지"))
        cases.append(dict(base, id=f"E2_S{k:02d}_drop", start=[x, r3(y + DROP_HEIGHT), z], observeSec=3.0,
                          notes=f"스트레스(실제 행동 아님): 이음매 {ma}→{mb} 위 {DROP_HEIGHT}m 높이에서 낙하, 착지면 y={y} ({version} {src})"))

    # 5) H7 소품 MeshCollider(Read/Write 없이 런타임 cooking 필요) 정면 돌진 - H7 파일에만 넣는다
    if props:
        cases += prop_cases(probes[0], by_xz, src, issues)

    out_doc = {"experimentId": "M5-explore-floor", "caseSetRevision": "e2", "version": version,
               "generatedBy": "Tools/make_explore_e2.py", "probe": src, "cases": cases}
    Path(out).write_text(json.dumps(out_doc, ensure_ascii=False, indent=1) + "\n")
    print(version, len(cases), "cases;", len(issues), "issues")
    for i in issues:
        print("  issue:", i)


PROP_APPROACH = 0.6
# H7 Sprint 수평 속도의 보수적 하한(m/s). 접근 시간 예산만 정하며 판정에는 쓰지 않는다.
PROP_SPEED_FLOOR = 0.5
# 결과를 보기 전에 종류별로 고른 대상(오브젝트 이름). 볼록/비볼록, Default/Obstacle 레이어를 섞고 집(Destination) 근처는 뺀다.
PROP_PICK = ["Bus_Stop_02", "Trash_Can_06", "Trash_Can_06 (1)", "Trash_02", "Fountain_03",
             "pizzeria house with environment", "plants 2", "table"]


def prop_targets(probe):
    """런타임 cooking이 필요한 활성·비트리거 소품 중 지면 높이에서 닿는 것. 경로 중복 제거."""
    seen, out = set(), []
    for mc in probe["meshColliders"]:
        if not mc.get("needsRuntimeCookingWithoutReadWrite") or mc["isTrigger"] or not mc["active"] or not mc["enabled"]:
            continue
        if mc["path"] in seen or not mc.get("boundsMin"):
            continue
        lo, hi = mc["boundsMin"], mc["boundsMax"]
        if lo[1] > 0.3 or hi[1] < 0.1:
            continue
        seen.add(mc["path"])
        out.append(mc)
    by_name = {mc["path"].split("/")[-1]: mc for mc in out}
    return [by_name[n] for n in PROP_PICK if n in by_name]


def prop_start(mc):
    lo, hi = mc["boundsMin"], mc["boundsMax"]
    return round(lo[0] - PROP_APPROACH, 3), round((lo[2] + hi[2]) / 2, 3)


def cmd_proppoints(probe_path, out):
    if Path(out).exists():
        raise SystemExit("exists, refusing to overwrite: " + out)
    pts = [prop_start(mc) for mc in prop_targets(json.loads(Path(probe_path).read_text()))]
    Path(out).write_text("".join(f"{x},{z}\n" for x, z in pts))
    print(len(pts), "prop start points")


def prop_cases(probe, by_xz, src, issues):
    """소품 AABB 서쪽 바깥에서 중심선을 따라 Sprint로 돌진한다. 기준 면 허용 오차를 소품 폭으로 넓혀
    그 소품 collider와의 접촉이 하나라도 있으면 대상 벽 접촉으로 센다. 중심선을 지나고도 접촉이 전혀 없으면
    (no_wall_contact 또는 passed_curb) 빌드에서 충돌면이 빈 후보다. 실제 메시가 AABB 중심선에 없을 수 있으므로
    궤적·캡처와 함께 해석한다."""
    smoke = json.loads((CASES_DIR / "smoke-s2.json").read_text())
    curb_t = next(c for c in smoke["cases"] if c["scenario"] == "curb" and c["environment"] == "map")
    out = []
    for k, mc in enumerate(prop_targets(probe)):
        lo, hi = mc["boundsMin"], mc["boundsMax"]
        x, cz = prop_start(mc)
        p = by_xz.get((x, cz))
        g = ground(p) if p else None
        if g is None:
            issues.append(f"prop {mc['path']}: no ground at start {x},{cz}")
            continue
        gy = r3(g["y"])
        width = hi[0] - lo[0]
        timeout = round((PROP_APPROACH + width) / PROP_SPEED_FLOOR + 1.0, 2)
        out.append(dict(curb_t, id=f"E2_P{k:02d}_prop", start=[x, gy, cz], yaw=90.0, moveMode="Sprint",
                        approachTimeoutSec=timeout, minAllowedY=min(-0.1, gy),
                        wallNormal=[-1.0, 0.0, 0.0], wallPoint=[r3(lo[0]), gy, cz], wallTopY=r3(hi[1]),
                        wallPlaneTolerance=r3(width), wallTargets=[mc["path"]], expect="",
                        notes=f"H7 소품 {mc['path']}(Read/Write 없이 런타임 cooking 필요, 폭 {width:.2f}m) AABB 서쪽 면 x={r3(lo[0])}로 "
                              f"중심선 Sprint 돌진. 출발 지면 y={gy} ({src}). 이 collider 접촉 유무가 관측 대상"))
    if not out:
        issues.append("no prop case generated")
    return out


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "points":
        cmd_points(sys.argv[2])
    elif len(sys.argv) >= 4 and sys.argv[1] == "proppoints":
        cmd_proppoints(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 5 and sys.argv[1] == "cases":
        argv = sys.argv[4:]
        ref = None
        if "--seams-from" in argv:
            i = argv.index("--seams-from")
            ref = argv[i + 1]
            argv = argv[:i] + argv[i + 2:]
        cmd_cases(sys.argv[2], sys.argv[3], [a for a in argv if a != "--props"], "--props" in argv, ref)
    else:
        raise SystemExit(__doc__)
