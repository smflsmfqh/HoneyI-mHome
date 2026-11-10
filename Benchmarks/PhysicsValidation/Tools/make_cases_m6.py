#!/usr/bin/env python3
"""M6 정식 case 생성기(M6-r1). case 정의는 M5 원본에서 그대로 복사하고 experimentId·caseSetRevision만 바꾼다.

출력(Harness/Cases/M6-r1/):
  m6-floor-<V>.json  V = S0a,S0b,S0p,S1,S2,S3  ← trackB/trackB-floor-<V>.json (28 = CK 4 + E2_C 24)
  m6-curb.json                                  ← fix1/fix1-b.json (62 = CK 4 + E2_C 24 + 연석 34)
  m6-H0.json                                    ← r1/repeat-r1-H0.json 중 CK 4 + E2_C 24 (E2_K 사후 표적·E1_P11R 인위 조건 제외)
  m6-derivation.json                            ← 출처 SHA, 출력 SHA, case 수

사용: python3 -I make_cases_m6.py <PhysicsValidation 루트>
"""
import hashlib
import json
import sys
from pathlib import Path

EXP = "M6-r1"


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def main():
    root = Path(sys.argv[1])
    cases = root / "Harness/Cases"
    out = cases / EXP
    if out.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out))
    plan = []
    for v in ("S0a", "S0b", "S0p", "S1", "S2", "S3"):
        plan.append((f"m6-floor-{v}.json", cases / f"trackB/trackB-floor-{v}.json", None, 28))
    plan.append(("m6-curb.json", cases / "fix1/fix1-b.json", None, 62))
    plan.append(("m6-H0.json", cases / "r1/repeat-r1-H0.json", lambda c: c["id"].startswith("CK") or c["id"].startswith("E2_C"), 28))
    out.mkdir(parents=True)
    rep = {"experimentId": EXP, "generatedBy": "Tools/make_cases_m6.py", "files": {}}
    for name, src, keep, n in plan:
        d = json.loads(src.read_text())
        cs = [c for c in d["cases"] if keep is None or keep(c)]
        assert len(cs) == n, (name, len(cs))
        ids = [c["id"] for c in cs]
        assert len(set(ids)) == len(ids)
        new = dict(d, experimentId=EXP, caseSetRevision=name[:-5], cases=cs,
                   m6Source={"file": str(src.relative_to(root)), "sha256": sha(src), "sourceExperimentId": d.get("experimentId"),
                             "rule": "case 정의 바이트 단위 불변(필터만)" if keep else "case 정의 불변"})
        p = out / name
        p.write_text(json.dumps(new, ensure_ascii=False, indent=1) + "\n")
        # 검증: 다시 읽어 원본 case와 같은지
        back = json.loads(p.read_text())["cases"]
        orig = {c["id"]: c for c in d["cases"]}
        assert all(c == orig[c["id"]] for c in back)
        rep["files"][name] = {"sha256": sha(p), "cases": n, "source": str(src.relative_to(root)), "sourceSha256": sha(src)}
    (out / "m6-derivation.json").write_text(json.dumps(rep, ensure_ascii=False, indent=1) + "\n")
    print(json.dumps(rep, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
