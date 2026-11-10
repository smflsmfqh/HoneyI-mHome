#!/usr/bin/env python3
"""M5 H0 반복 확인 case r1 생성기: 동결된 e2c H0 파일에서 부분집합만 뽑는다.

case 정의(각 case 객체)는 e2c와 같은 값을 그대로 쓴다. 바꾸는 것은 파일 머리의 experimentId·caseSetRevision과
파생 기록(derivedFrom)뿐이다. 각 case가 원본과 같은지 JSON 값으로 다시 대조해 기록한다.

선택: E2_C*(연석 횡단 24, 미리 정한 경로) + E2_K*(사후 표적 24) + CK*(판정 검증 4) + E1_P11R_hold
사용: python3 -I make_cases_r1.py <explore-e2c-H0.json> <out_dir>
"""
import hashlib
import json
import sys
from pathlib import Path


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def keep(cid):
    return cid.startswith(("E2_C", "E2_K", "CK")) or cid == "E1_P11R_hold"


def main():
    src, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
    if out_dir.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out_dir))
    doc = json.loads(src.read_text())
    if doc.get("version") != "H0":
        raise SystemExit("H0 case file expected")
    cases = [c for c in doc["cases"] if keep(c["id"])]
    by_src = {c["id"]: c for c in doc["cases"]}
    mismatched = [c["id"] for c in cases if json.dumps(c, sort_keys=True) != json.dumps(by_src[c["id"]], sort_keys=True)]
    if mismatched:
        raise SystemExit("case changed: " + ", ".join(mismatched))
    out = dict(doc, experimentId="M5-repeat-H0", caseSetRevision="r1", cases=cases,
               derivedFrom={"file": src.name, "sha256": sha(src),
                            "rule": "E2_C*·E2_K*·CK*·E1_P11R_hold 부분집합, case 정의 불변"})
    out_dir.mkdir(parents=True)
    dst = out_dir / "repeat-r1-H0.json"
    dst.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n")
    groups = {
        "E2_C": sorted(c["id"] for c in cases if c["id"].startswith("E2_C")),
        "E2_K": sorted(c["id"] for c in cases if c["id"].startswith("E2_K")),
        "CK": sorted(c["id"] for c in cases if c["id"].startswith("CK")),
        "E1": sorted(c["id"] for c in cases if c["id"].startswith("E1_")),
    }
    report = {"generatedBy": "Tools/make_cases_r1.py", "source": str(src), "sourceSha256": sha(src),
              "out": dst.name, "outSha256": sha(dst), "cases": len(cases),
              "groups": {k: {"count": len(v), "ids": v} for k, v in groups.items()},
              "caseDefinitionsIdenticalToSource": True}
    (out_dir / "r1-derivation.json").write_text(json.dumps(report, ensure_ascii=False, indent=1) + "\n")
    print(json.dumps({k: len(v) for k, v in groups.items()}), "total", len(cases), "sha", report["outSha256"])


if __name__ == "__main__":
    main()
