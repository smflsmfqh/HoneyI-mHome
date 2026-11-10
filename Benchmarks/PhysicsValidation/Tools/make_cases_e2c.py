#!/usr/bin/env python3
"""M5 탐색 case e2c 생성기: 동결된 e2b에서 버전별 미지원 동작을 걷어낸다(r3 리뷰 P2 반영).

e2b·make_explore_e2.py·분석기·capabilities-r1은 바꾸지 않는다. e2b 파일을 입력으로 읽어 다음만 바꾼다.
- Jump 미지원 버전: scenario=jump case 제외(같은 경로의 walk case가 남는다), curb 구제 점프 끔
- Sprint 미지원 버전: curb moveMode Sprint → Move
- Roll 미지원 버전: curb 구제 구르기 끔(e2b에서 이미 꺼져 있음)
case ID는 그대로 둔다. 바뀐 case의 notes 끝에 변경 내용을 붙인다. 바뀐 것이 없는 버전은 e2b 파일을 바이트 그대로 복사한다.

사용: python3 -I make_cases_e2c.py <e2b_dir> <capabilities.json> <out_dir>
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

VERSIONS = ["Hm1", "H0", "H1", "H7"]


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def derive(doc, actions):
    out, dropped, changed = [], [], []
    for c in doc["cases"]:
        if c["scenario"] == "jump" and "Jump" not in actions:
            dropped.append(c["id"])
            continue
        n = dict(c)
        notes = []
        if n["scenario"] == "curb":
            if n.get("moveMode") == "Sprint" and "Sprint" not in actions:
                n["moveMode"] = "Move"
                notes.append("Sprint 미지원으로 Move 접근")
            if n.get("rescueJump") and "Jump" not in actions:
                n["rescueJump"] = False
                notes.append("Jump 미지원으로 구제 점프 생략")
            if n.get("rescueRoll") and "Roll" not in actions:
                n["rescueRoll"] = False
                notes.append("Roll 미지원으로 구제 구르기 생략")
        if notes:
            n["notes"] = n.get("notes", "") + ". e2c: " + ", ".join(notes)
            changed.append(n["id"])
        out.append(n)
    return out, dropped, changed


def main():
    src_dir, caps_path, out_dir = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    if out_dir.exists():
        raise SystemExit("exists, refusing to overwrite: " + str(out_dir))
    caps = json.loads(caps_path.read_text())
    out_dir.mkdir(parents=True)
    report = {"generatedBy": "Tools/make_cases_e2c.py", "source": str(src_dir), "capabilities": str(caps_path),
              "capabilitiesSha256": sha(caps_path), "versions": {}}
    for v in VERSIONS:
        src = src_dir / f"explore-e2-{v}.json"
        dst = out_dir / f"explore-e2c-{v}.json"
        doc = json.loads(src.read_text())
        cases, dropped, changed = derive(doc, set(caps["versions"][v]["actions"]))
        if not dropped and not changed:
            shutil.copyfile(src, dst)
            mode = "byte_copy"
        else:
            doc = dict(doc, caseSetRevision="e2c", cases=cases,
                       derivedFrom={"file": src.name, "sha256": sha(src), "rule": "capabilities-r1 미지원 동작 제거"})
            dst.write_text(json.dumps(doc, ensure_ascii=False, indent=1) + "\n")
            mode = "derived"
        report["versions"][v] = {"mode": mode, "sourceSha256": sha(src), "outSha256": sha(dst),
                                 "cases": len(cases), "droppedJump": dropped, "changed": changed}
        print(v, mode, len(cases), "cases; dropped", len(dropped), "changed", len(changed))
    (out_dir / "e2c-derivation.json").write_text(json.dumps(report, ensure_ascii=False, indent=1) + "\n")


if __name__ == "__main__":
    main()
