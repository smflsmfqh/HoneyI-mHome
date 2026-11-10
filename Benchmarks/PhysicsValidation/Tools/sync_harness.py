#!/usr/bin/env python3
"""하네스 원본(Benchmarks/PhysicsValidation/Harness)을 복원 작업 공간의 Assets/PhysicsBench로 복사하고
계측 파일 SHA 목록을 새 증거 파일로 남긴다. 사용자 원본 프로젝트의 Assets는 건드리지 않는다.

사용: python3 -I sync_harness.py <workspace_project> <evidence_json>
"""
import hashlib, json, os, shutil, sys, datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Harness")
PARTS = ("Runtime", "Editor")


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def main():
    ws, evidence = sys.argv[1], sys.argv[2]
    if os.path.exists(evidence):
        raise SystemExit("evidence exists, refusing to overwrite: " + evidence)
    project_root = os.path.dirname(os.path.dirname(ROOT))
    if os.path.realpath(ws) == os.path.realpath(project_root):
        raise SystemExit("refusing to install harness into the user's original project")
    dst_root = os.path.join(ws, "Assets", "PhysicsBench")
    files = []
    for part in PARTS:
        src_dir = os.path.join(SRC, part)
        dst_dir = os.path.join(dst_root, part)
        os.makedirs(dst_dir, exist_ok=True)
        keep = set()
        for name in sorted(os.listdir(src_dir)):
            if not name.endswith(".cs"):
                continue
            shutil.copyfile(os.path.join(src_dir, name), os.path.join(dst_dir, name))
            keep.add(name)
            files.append({"path": f"Assets/PhysicsBench/{part}/{name}", "sha256": sha(os.path.join(dst_dir, name))})
        for name in os.listdir(dst_dir):
            if name.endswith(".cs") and name not in keep:
                raise SystemExit("stale harness file in workspace, inspect manually: " + os.path.join(dst_dir, name))
    os.makedirs(os.path.dirname(evidence), exist_ok=True)
    json.dump({"syncedUtc": datetime.datetime.utcnow().isoformat() + "Z", "workspace": os.path.abspath(ws),
               "purpose": "PHYSICS_BENCH define 빌드 전용 계측. 게임 이동·충돌 코드 변경 없음",
               "files": files}, open(evidence, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(files, indent=1))


if __name__ == "__main__":
    main()
