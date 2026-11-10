#!/usr/bin/env python3
"""물리 검증 원시 결과 판정·집계기.

런타임 하네스는 원시 사실(trials.jsonl / trace.jsonl)만 기록한다. 이 스크립트가 버전 관리되는
임계값 JSON으로 trial마다 결과를 판정하고, 판정기 검증 사례에서는 기대 결과와 비교한다.
원시 파일은 읽기만 하고, 출력은 새 디렉터리에만 쓴다(기존 출력이 있으면 거부).

사용:
  python3 -I analyze_physics_bench.py <run_dir> --thresholds thresholds.json --out <new_dir> [--capabilities capabilities.json]

--capabilities: 버전별 실제 지원 동작·Y 위치 고정 표. 주어지면 outcome은 그대로 두고
  action_unsupported:<동작>, y_position_frozen, support_inferred_from_rest의 근거 구분을 플래그·요약에 더한다.
"""
import argparse
import hashlib
import json
import math
import os
import sys
from collections import Counter, defaultdict

ESCAPE_PHASES = ("reverse", "side_left", "side_right")
RESCUE_PHASES = ("rescue_jump_press", "rescue_jump", "rescue_roll_press", "rescue_roll")
MOVE_ACTIONS = ("Move", "Move+Sprint", "Move+Roll", "Move+Jump", "Move+Sprint+Jump", "Move+Roll+Jump")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_jsonl(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError as e:
                raise SystemExit(f"{path}:{n}: 손상된 JSON 행: {e}")
    return rows


def horiz(a, b):
    return math.hypot(a[0] - b[0], a[2] - b[2])


def supported(row, r, th):
    """짧은 범위의 실제 지지: 위쪽 법선 접촉 또는 발 아래 짧은 검사."""
    for ny in row.get("contactNormalY", []):
        if ny >= th["supportNormalY"]:
            return True
    return row.get("probeId", -1) != -1 and row.get("probeNormalY", 0) >= th["supportNormalY"] \
        and row.get("probeDist", 1e9) <= th["supportProbeGapR"] * r


def input_audit(trial, th):
    """이동 요청이 실제 액션으로 수신됐는지. 미수신이면 하네스 오류.
    지연은 물리 시간 기준이다. 느린 PC의 프레임 지연(실시간)만으로 하네스 오류를 만들지 않는다."""
    missing = []
    for e in trial.get("inputs", []):
        if e["action"] in ("Reset", "Look"):
            continue
        if "requiredActions" in e:
            # 새 스키마: 바뀐 키에 묶인 액션이 모두 수신돼야 한다(Move+Sprint에서 Sprint 누락 검출).
            required = [a for a in e["requiredActions"].split(",") if a]
            if not required:
                continue
            received = [a for a in (e.get("receivedActions") or "").split(",") if a]
            lacking = [a for a in required if a not in received]
            late = e["receivedRealtime"] >= 0 and e["receivedFixedTime"] - e["requestFixedTime"] > th["inputReceiveMaxFixedSec"]
            if lacking or late:
                missing.append(e["action"] + "@" + format(e["requestFixedTime"], ".3f") + ("" if not lacking else " missing=" + "+".join(lacking)) + (" late" if late else ""))
            continue
        if e["action"] == "Release":
            continue
        if e["receivedRealtime"] < 0 or e["receivedFixedTime"] - e["requestFixedTime"] > th["inputReceiveMaxFixedSec"]:
            missing.append(e["action"] + "@" + format(e["requestFixedTime"], ".3f"))
    return missing


def first_fall(rows, trial, r, th):
    limit = trial["minAllowedY"] - th["fallDepthR"] * r
    for row in rows:
        if row["bottomY"] < limit:
            return row
    return None


def classify(trial, rows, th):
    res = {
        "caseId": trial["caseId"],
        "attempt": trial["attempt"],
        "scenario": trial["scenario"],
        "moveMode": trial["moveMode"],
        "expect": trial.get("expect") or "",
        "flags": [],
    }
    r = trial["capsuleRadiusWorld"]
    if trial["status"] != "completed":
        res["outcome"] = "harness_error:" + (trial.get("harnessError") or "unknown")
        return res
    if not rows or len(rows) != trial["steps"]:
        res["outcome"] = "harness_error:trace_incomplete"
        res["flags"].append(f"trace_rows={len(rows)} steps={trial['steps']}")
        return res
    missing = input_audit(trial, th)
    if missing:
        res["outcome"] = "harness_error:input_not_received"
        res["missingInputs"] = missing[:10]
        return res

    fall = first_fall(rows, trial, r, th)
    # 화면의 개미가 자식 body를 따라 움직일 수 있으므로, 자식 Rigidbody 낙하는 주 결과와 따로 보고한다.
    limit = trial["minAllowedY"] - th["fallDepthR"] * r
    # 한두 스텝 파고들었다가 복귀하는 순간 침투(child_transient_dip)와 지속 낙하(child_fall)를 구분한다.
    def child_low(x):
        rel = x.get("bodyRel") or []
        return any(x["p"][1] + rel[i + 1] < limit for i in range(0, len(rel), 3))
    need_low = max(1, int(round(th["stableSupportSec"] / trial["fixedDeltaTime"])))
    run_len, first_low, child_fall_step = 0, None, None
    for x in rows:
        if child_low(x):
            run_len += 1
            first_low = first_low if first_low is not None else x["step"]
            if run_len >= need_low and child_fall_step is None:
                child_fall_step = x["step"] - run_len + 1
        else:
            run_len = 0
    if child_fall_step is not None:
        res["flags"].append("child_fall")
        res["childFallStep"] = child_fall_step
    elif first_low is not None:
        res["flags"].append("child_transient_dip")
        res["childDipStep"] = first_low
    sat = sum(1 for x in rows if x.get("overlapSaturated"))
    if sat:
        res["flags"].append(f"overlap_saturated_steps={sat}")
    res["minBottomY"] = min(x["bottomY"] for x in rows)
    res["maxPenWallDepthR"] = max(x.get("penWallDepth", 0) for x in rows) / r
    if fall is not None:
        res["fallStep"] = fall["step"]
        res["fallPhase"] = fall["phase"]
        # 지지면이 있었는데 연속 스텝 사이에 아래로 넘어갔는지(터널링 정황) 참고 기록
        prev = next((x for x in rows if x["step"] == fall["step"] - 1), None)
        if prev is not None:
            res["fallPrevSupported"] = supported(prev, r, th)

    if trial["scenario"] in ("hold", "walk", "jump"):
        if fall is not None:
            res["outcome"] = "fall"
            return res
        tail = rows[-max(1, int(th["stableSupportSec"] / trial["fixedDeltaTime"])):]
        if all(supported(x, r, th) for x in tail):
            res["outcome"] = "pass"
        elif max(abs(x["v"][1]) for x in tail) < th.get("restVerticalSpeed", 0.02) \
                and max(x["bottomY"] for x in tail) - min(x["bottomY"] for x in tail) < 0.1 * r:
            # 접촉·짧은 검사로는 지지가 안 보이지만 수직으로 정지해 있다(예: H0의 CharacterController가 받치는 상태).
            res["outcome"] = "pass"
            res["flags"].append("support_inferred_from_rest")
            res["restGapAboveMinAllowedR"] = (tail[-1]["bottomY"] - trial["minAllowedY"]) / r
        else:
            res["outcome"] = "unsupported_end"
        return res

    if trial["scenario"] != "curb":
        res["outcome"] = "harness_error:unknown_scenario"
        return res

    fwd_yaw = math.radians(trial["startYaw"])
    fwd = (math.sin(fwd_yaw), math.cos(fwd_yaw))
    # 벽 쪽 방향: case의 기준 벽 법선(플레이어 쪽)을 뒤집은 수평 방향. 없으면 진행 방향.
    wn = trial.get("wallNormal") or []
    wall_in = fwd
    if len(wn) == 3 and math.hypot(wn[0], wn[2]) > 1e-6:
        k = math.hypot(wn[0], wn[2])
        wall_in = (-wn[0] / k, -wn[2] / k)
    wp = trial.get("wallPoint") or []
    contact_t = trial["firstWallContactTime"]
    res["firstWallContactTime"] = contact_t
    # 대상 벽이 아닌 수평 접점(인접 장애물 등)은 첫 접촉으로 쓰지 않고 따로 센다.
    res["otherWallContactSteps"] = sum(1 for x in rows if x.get("otherWallContacts", 0) > 0)
    res["preContactSpeed"] = trial["preContactSpeed"]
    # 런타임 값은 "콜백 직전 스텝 속도"라, CCD에서 접촉 콜백이 한 스텝 늦으면 0이 된다.
    # 궤적에서 첫 접촉 전 일정 구간의 최대 수평 속도를 따로 계산한다.
    if contact_t >= 0:
        window = [x for x in rows if contact_t - th.get("preContactWindowSec", 0.06) - 1e-6 <= x["t"] < contact_t - 1e-6]
        res["approachSpeedBeforeContact"] = max((math.hypot(x["v"][0], x["v"][2]) for x in window), default=None)
    if contact_t < 0:
        res["flags"].append("no_wall_contact")
    pen_sig = th["penetrationSignificantR"] * r
    pen_ok = th["penetrationResolvedR"] * r
    need = max(1, int(round(th["stableSupportSec"] / trial["fixedDeltaTime"])))

    # 끼임 기준 위치: 밀기 단계가 끝난 뒤(입력 해제 직후) 위치
    push_rows = [x for x in rows if x["phase"] == "push"]
    if not push_rows:
        res["outcome"] = "harness_error:no_push_phase"
        return res
    stuck_pos = push_rows[-1]["p"]
    contact_rows = [x for x in rows if contact_t >= 0 and x["t"] >= contact_t - 1e-6]
    if contact_rows:
        c0 = contact_rows[0]["p"]
        progress = max((x["p"][0] - c0[0]) * wall_in[0] + (x["p"][2] - c0[2]) * wall_in[1] for x in push_rows)
        res["pushProgressR"] = progress / r
        if progress > th["passThroughR"] * r:
            res["flags"].append("passed_curb")
    # 접촉 기록과 무관하게 기준 벽면을 넘어간 위치로 통과를 판정한다(접촉 전 터널링 포함).
    if len(wp) == 3:
        beyond = [((x["p"][0] - wp[0]) * wall_in[0] + (x["p"][2] - wp[2]) * wall_in[1], x) for x in rows
                  if x["phase"] in ("approach", "push") and (fall is None or x["step"] < fall["step"])]
        if beyond:
            depth, _ = max(beyond, key=lambda b: b[0])
            res["maxBeyondWallR"] = depth / r
            first = next((x for d, x in beyond if d > th["passThroughR"] * r), None)
            child_beyond = next((x for x in rows if x["phase"] in ("approach", "push") and x.get("bodyRel") and any(
                (x["p"][0] + x["bodyRel"][i] - wp[0]) * wall_in[0] + (x["p"][2] + x["bodyRel"][i + 2] - wp[2]) * wall_in[1] > th["passThroughR"] * r
                for i in range(0, len(x["bodyRel"]), 3))), None)
            if child_beyond is not None:
                res["flags"].append("child_beyond_wall")
            if first is not None:
                res["passStep"] = first["step"]
                if "passed_curb" not in res["flags"]:
                    res["flags"].append("passed_curb")
                if first["bottomY"] >= trial.get("wallTopY", 1e9) - 0.5 * r:
                    res["flags"].append("climbed")

    # 자식 body 관측 요약(판정 결과는 바꾸지 않음)
    res["maxChildPenWallDepthR"] = max(x.get("childPenWallDepth", 0) for x in rows) / r
    res["maxSelfPenDepthR"] = max(x.get("selfPenDepth", 0) for x in rows) / r
    rel0 = rows[0].get("bodyRel") or []
    if rel0:
        sep = max(max(abs(a - b) for a, b in zip(x.get("bodyRel") or rel0, rel0)) for x in rows)
        res["maxChildSeparationR"] = sep / r

    # 첫 물리 스텝 전에 해소되는 침투는 런타임이 배치 직후 값으로 따로 기록한다.
    res["initialPenWallDepthR"] = trial.get("initialPenWallDepth", 0) / r
    significant_pen_before = trial.get("initialPenWallDepth", 0) > pen_sig
    streak = 0
    escaped_phase = None
    for x in rows:
        if fall is not None and x["step"] >= fall["step"]:
            break
        if x.get("penWallDepth", 0) > pen_sig:
            if escaped_phase is None:
                significant_pen_before = True
        if x["phase"] not in ESCAPE_PHASES + RESCUE_PHASES:
            streak = 0
            continue
        d = (x["p"][0] - stuck_pos[0], x["p"][2] - stuck_pos[2])
        dist = math.hypot(*d)
        into_wall = d[0] * wall_in[0] + d[1] * wall_in[1]
        ok = dist >= th["escapeDistR"] * r and into_wall <= r and x.get("penWallDepth", 0) <= pen_ok and supported(x, r, th)
        streak = streak + 1 if ok else 0
        if streak >= need:
            escaped_phase = x["phase"]
            # 이탈 조건이 연속 충족돼 확정된 스텝. 단계 시작 스텝이 아니다.
            res["escapeStep"] = x["step"]
            break

    res["escapePhase"] = escaped_phase
    res["significantPenetration"] = significant_pen_before
    passed = "passed_curb" in res["flags"]
    pass_first = passed and (fall is None or res.get("passStep", -1) < fall["step"])
    if pass_first:
        # 통과가 먼저 일어났다. 이후 낙하가 있으면 플래그로만 남긴다.
        res["outcome"] = "passed_curb"
        if fall is not None:
            res["flags"].append("fall_after_pass")
    elif fall is not None and (escaped_phase is None):
        res["outcome"] = "fall"
    elif passed:
        res["outcome"] = "passed_curb"
    elif contact_t < 0:
        # 벽에 닿지도 넘지도 않았다. 성공으로 세지 않는다.
        res["outcome"] = "no_wall_contact"
    elif escaped_phase in ESCAPE_PHASES:
        res["outcome"] = "recovered_after_penetration" if significant_pen_before else "blocked_escaped"
    elif escaped_phase in RESCUE_PHASES:
        res["outcome"] = "escaped_by_rescue"
    else:
        res["outcome"] = "rescue_failed"
    if fall is not None and escaped_phase is not None:
        res["flags"].append("fall_after_escape:" + fall["phase"])
    return res


VISUAL_SUCCESS = ("pass", "blocked_escaped", "recovered_after_penetration")


def classify_with_visual(trial, rows, th):
    """주 캡슐 기준 결과(rootOutcome)와, 화면 개미(자식 body) 기준 실패를 합친 종합 결과(outcome)를 낸다.
    자식 body의 지속 낙하·벽 통과는 종합 실패다. 한두 스텝 순간 침투(child_transient_dip)는 플래그로만 남긴다."""
    res = classify(trial, rows, th)
    res["rootOutcome"] = res["outcome"]
    if res["outcome"] in VISUAL_SUCCESS:
        flags = res["flags"]
        esc = res.get("escapePhase")
        child_fall = "child_fall" in flags
        if child_fall and trial["scenario"] == "curb" and esc is not None:
            # 이탈 뒤 구제 단계의 자식 낙하는 이탈 판정 뒤 사건이다.
            # 이탈이 확정되기 전(이탈 단계 도중 포함)에 시작된 자식 지속 낙하는 종합 실패다.
            # 이탈 확정 뒤의 낙하(구제 단계 등)만 제외하고 플래그로 남긴다.
            step = res.get("childFallStep")
            esc_step = res.get("escapeStep")
            child_fall = step is not None and (esc_step is None or step <= esc_step)
            if not child_fall:
                res["flags"].append("child_fall_after_escape")
        if child_fall:
            res["outcome"] = "visual_fall"
        elif "child_beyond_wall" in flags:
            res["outcome"] = "visual_passed_curb"
    return res


def used_actions(trial):
    """trial이 실제로 요청한 플레이어 동작(입력 수신 여부와 별개). 요청 기록(inputs[].requiredActions)과
    모드에서 읽는다. trials.jsonl에는 구제 단계 설정이 없어 요청 기록이 근거다."""
    acts = set()
    for ev in trial.get("inputs") or []:
        acts.update(a for a in (ev.get("requiredActions") or "").split(",") if a)
    if trial["scenario"] == "jump":
        acts.add("Jump")
    if "Boost" in (trial.get("moveMode") or ""):
        acts.add("Boost")
    return acts - {"Look"}


def y_frozen(trial, cap):
    """버전 기본값에 통제(override:constraints=a->b)를 반영한 Y 위치 고정 여부."""
    frozen = bool(cap.get("positionYFrozen"))
    for c in trial.get("controls") or []:
        if c.startswith("override:constraints="):
            frozen = bool(int(c.split("->")[-1]) & 4)
    return frozen


def annotate_capabilities(res, trial, caps):
    """outcome을 바꾸지 않고 지원 동작·지지 근거 플래그만 더한다."""
    cap = caps["versions"].get(trial.get("versionId"))
    if cap is None:
        res["flags"].append("capability_unknown")
        return res
    missing = sorted(used_actions(trial) - set(cap["actions"]))
    for act in missing:
        res["flags"].append("action_unsupported:" + act)
    if y_frozen(trial, cap):
        res["flags"].append("y_position_frozen")
    return res


def evidence_breakdown(results):
    """pass를 지지 근거별로 나눈다. 정지 추론 pass는 정상 지지·착지와 같은 성공으로 합치지 않는다."""
    out = Counter()
    for x in results:
        if x["outcome"] != "pass":
            continue
        if "support_inferred_from_rest" not in x["flags"]:
            out["pass_contact_or_probe"] += 1
        elif "y_position_frozen" in x["flags"]:
            out["pass_rest_inferred_y_frozen"] += 1
        else:
            out["pass_rest_inferred"] += 1
    return dict(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run_dir")
    ap.add_argument("--thresholds", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--capabilities")
    a = ap.parse_args()

    if os.path.exists(a.out) and os.listdir(a.out):
        raise SystemExit(f"출력 폴더가 이미 있어 덮어쓰지 않음: {a.out}")
    os.makedirs(a.out, exist_ok=True)
    th = json.load(open(a.thresholds, encoding="utf-8"))
    trials_path = os.path.join(a.run_dir, "trials.jsonl")
    trace_path = os.path.join(a.run_dir, "trace.jsonl")
    status = json.load(open(os.path.join(a.run_dir, "status.json"), encoding="utf-8"))
    order = json.load(open(os.path.join(a.run_dir, "order.json"), encoding="utf-8"))
    trials = read_jsonl(trials_path)
    trace = defaultdict(list)
    for row in read_jsonl(trace_path):
        trace[(row["caseId"], row["attempt"])].append(row)

    seen = Counter((t["caseId"], t["attempt"]) for t in trials)
    dup = [k for k, n in seen.items() if n > 1]
    results = [classify_with_visual(t, sorted(trace.get((t["caseId"], t["attempt"]), []), key=lambda x: x["step"]), th) for t in trials]
    caps = json.load(open(a.capabilities, encoding="utf-8")) if a.capabilities else None
    if caps:
        for res, t in zip(results, trials):
            annotate_capabilities(res, t, caps)
    planned = order["caseIds"]
    done = {t["caseId"] for t in trials}
    checks = []
    for res in results:
        if res["expect"]:
            checks.append({"caseId": res["caseId"], "expect": res["expect"], "outcome": res["outcome"], "match": res["expect"] == res["outcome"]})

    summary = {
        "runDir": os.path.abspath(a.run_dir),
        "runState": status.get("state"),
        "inputs": {"trials.jsonl": sha256(trials_path), "trace.jsonl": sha256(trace_path), "thresholds": sha256(a.thresholds)},
        "thresholds": th,
        "planned": len(planned),
        "trials": len(trials),
        "missingCases": [c for c in planned if c not in done],
        "duplicateTrials": [list(k) for k in dup],
        "harnessErrors": sum(1 for x in results if x["outcome"].startswith("harness_error")),
        "outcomes": dict(Counter(x["outcome"] for x in results)),
        "passEvidence": evidence_breakdown(results),
        "capabilities": {"file": os.path.abspath(a.capabilities), "sha256": sha256(a.capabilities),
                         "unsupportedTrials": dict(Counter(f for x in results for f in x["flags"] if f.startswith("action_unsupported:")))} if caps else None,
        "classifierChecks": {"total": len(checks), "matched": sum(c["match"] for c in checks), "items": checks},
    }
    with open(os.path.join(a.out, "classified.jsonl"), "w", encoding="utf-8") as f:
        for res in results:
            f.write(json.dumps(res, ensure_ascii=False) + "\n")
    with open(os.path.join(a.out, "summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=1)
    print(json.dumps({k: summary[k] for k in ("runState", "planned", "trials", "missingCases", "harnessErrors", "outcomes")}, ensure_ascii=False))
    for c in checks:
        print(("OK  " if c["match"] else "BAD ") + f"{c['caseId']}: expect={c['expect']} outcome={c['outcome']}")
    complete = status.get("state") == "completed" and not summary["missingCases"] and not dup
    return 0 if complete and all(c["match"] for c in checks) else 3


if __name__ == "__main__":
    sys.exit(main())
