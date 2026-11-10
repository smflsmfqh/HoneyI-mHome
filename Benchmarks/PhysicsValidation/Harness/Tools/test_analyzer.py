#!/usr/bin/env python3
"""분석기 판정 경계 회귀 테스트.

실제 run의 trial/trace를 메모리에서만 복사·변형해 판정 경계를 확인한다. 원시 파일은 읽기만 한다.
Unity 물리 재현이 아니라 분석기 규칙 검증이다.

사용: python3 -I test_analyzer.py <run_dir> <thresholds.json>
"""
import copy
import importlib.util
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("analyzer", os.path.join(HERE, "analyze_physics_bench.py"))
A = importlib.util.module_from_spec(spec)
spec.loader.exec_module(A)


def load(run_dir, case_id):
    trial = next(json.loads(l) for l in open(os.path.join(run_dir, "trials.jsonl")) if json.loads(l)["caseId"] == case_id)
    rows = sorted((json.loads(l) for l in open(os.path.join(run_dir, "trace.jsonl")) if f'"{case_id}"' in l), key=lambda x: x["step"])
    return trial, rows


def sink_child(rows, trial, from_step, to_step):
    """from_step~to_step 동안 자식 body를 낙하 경계 아래로 옮긴다(주 캡슐은 그대로)."""
    r = trial["capsuleRadiusWorld"]
    limit = trial["minAllowedY"] - 10 * r
    for x in rows:
        if from_step <= x["step"] <= to_step and x.get("bodyRel"):
            x["bodyRel"][1] = limit - x["p"][1]


def main():
    run_dir, th_path = sys.argv[1], sys.argv[2]
    th = json.load(open(th_path))
    results = []

    def check(name, trial, rows, want_outcome, want_flag=None, want_root=None):
        res = A.classify_with_visual(trial, rows, th)
        ok = res["outcome"] == want_outcome and (want_flag is None or want_flag in res["flags"]) \
            and (want_root is None or res["rootOutcome"] == want_root)
        results.append({"name": name, "ok": ok, "outcome": res["outcome"], "rootOutcome": res["rootOutcome"],
                        "flags": res["flags"], "escapeStep": res.get("escapeStep"), "childFallStep": res.get("childFallStep")})

    base_t, base_r = load(run_dir, "CK05_curb_sprint")
    check("curb unmodified", copy.deepcopy(base_t), copy.deepcopy(base_r), "blocked_escaped")
    esc = A.classify(base_t, copy.deepcopy(base_r), th)["escapeStep"]
    rev0 = next(x["step"] for x in base_r if x["phase"] == "reverse")

    # 리뷰 반례: 이탈 단계는 시작됐지만 이탈 확정 전(2r 미만)에 자식 지속 낙하 → 종합 실패
    t, rows = copy.deepcopy(base_t), copy.deepcopy(base_r)
    sink_child(rows, t, rev0, rev0 + 10)
    check("child fall during escape phase before escape is confirmed", t, rows, "visual_fall", "child_fall", "blocked_escaped")

    # 이탈 확정 뒤 구제 단계에서만 자식 지속 낙하 → 이탈 성공 유지, 플래그만
    jump0 = next(x["step"] for x in base_r if x["phase"] == "rescue_jump")
    assert jump0 > esc
    t, rows = copy.deepcopy(base_t), copy.deepcopy(base_r)
    sink_child(rows, t, jump0, jump0 + 10)
    check("child fall only after escape (rescue phase)", t, rows, "blocked_escaped", "child_fall_after_escape")

    # 1스텝 순간 침투 → 실패 아님, 플래그만
    t, rows = copy.deepcopy(base_t), copy.deepcopy(base_r)
    sink_child(rows, t, rev0, rev0)
    check("child transient dip", t, rows, "blocked_escaped", "child_transient_dip")

    # 정지 case에서 자식 지속 낙하 → 종합 실패
    t, rows = load(run_dir, "CK01_flat_hold")
    sink_child(rows, t, 0, rows[-1]["step"])
    check("hold with sustained child fall", t, rows, "visual_fall", "child_fall", "pass")

    # 조합 입력 중 Sprint만 미수신 → 하네스 오류
    t, rows = load(run_dir, "CK05_curb_sprint")
    ev = next(e for e in t["inputs"] if "Sprint" in e.get("requiredActions", ""))
    ev["receivedActions"] = "Move"
    ev["receivedRealtime"] = -1
    check("composite input missing Sprint", t, rows, "harness_error:input_not_received")

    # 지원 동작·Y 고정 표시: outcome은 바뀌지 않고 플래그만 붙는다(Review r3 P2)
    caps = json.load(open(os.path.join(HERE, "..", "Cases", "capabilities-r1.json"), encoding="utf-8"))

    def check_caps(name, trial, rows, want_flags, absent_flags=()):
        before = A.classify_with_visual(copy.deepcopy(trial), copy.deepcopy(rows), th)
        after = A.annotate_capabilities(A.classify_with_visual(copy.deepcopy(trial), copy.deepcopy(rows), th), trial, caps)
        ok = before["outcome"] == after["outcome"] and all(f in after["flags"] for f in want_flags) \
            and not any(f in after["flags"] for f in absent_flags)
        results.append({"name": name, "ok": ok, "outcome": after["outcome"], "flags": after["flags"]})

    t, rows = load(run_dir, "CK05_curb_sprint")
    t["versionId"] = "Hm1"
    check_caps("Hm1 curb sprint: Sprint/Jump/Roll unsupported, Y frozen", t, rows,
               ["action_unsupported:Sprint", "action_unsupported:Jump", "action_unsupported:Roll", "y_position_frozen"])
    t, rows = load(run_dir, "CK05_curb_sprint")
    t["versionId"] = "Hm1"
    t["controls"] = list(t.get("controls") or []) + ["override:constraints=116->80"]
    check_caps("Hm1 with constraints override 80: not Y frozen", t, rows, ["action_unsupported:Sprint"], ["y_position_frozen"])
    t, rows = load(run_dir, "CK05_curb_sprint")
    check_caps("H1 curb sprint: all supported", t, rows, [], ["action_unsupported:Sprint", "action_unsupported:Jump", "action_unsupported:Roll", "y_position_frozen"])
    t, rows = load(run_dir, "CK01_flat_hold")
    t["versionId"] = "H0"
    check_caps("H0 hold: no unsupported action", t, rows, [], ["action_unsupported:Move"])
    rest = {"outcome": "pass", "flags": ["support_inferred_from_rest", "y_position_frozen"]}
    results.append({"name": "pass evidence breakdown", "ok": A.evidence_breakdown([rest, {"outcome": "pass", "flags": []}])
                    == {"pass_rest_inferred_y_frozen": 1, "pass_contact_or_probe": 1}})

    for x in results:
        print(("OK  " if x["ok"] else "BAD ") + json.dumps(x, ensure_ascii=False))
    return 0 if all(x["ok"] for x in results) else 1


if __name__ == "__main__":
    sys.exit(main())
