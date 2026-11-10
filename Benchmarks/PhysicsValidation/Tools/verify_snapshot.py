#!/usr/bin/env python3
"""복원 작업 공간의 원본 파일이 등록된 Git blob과 여전히 같은지 읽기 전용으로 대조한다.
원본에 없는 추가 파일(계측 사본, Unity 생성 .meta 등)도 목록으로 남긴다. 아무것도 수정하지 않는다.

사용: python3 -I verify_snapshot.py <H1..H7> <workspace_project> <new_evidence_json>
"""
import hashlib, json, os, subprocess, sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare_snapshot import VERSIONS, ASSET_PATH, entries, root_rev  # noqa: E402


def blob_id(path):
    data = path.read_bytes() if not path.is_symlink() else str(path.readlink()).encode()
    return hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()


def main():
    version, ws, out = sys.argv[1], Path(sys.argv[2]).resolve(), Path(sys.argv[3])
    if out.exists():
        raise SystemExit("evidence exists, refusing to overwrite: " + str(out))
    root = Path(__file__).resolve().parents[3]
    root_sha, asset_sha = VERSIONS[version]
    rows = [(ws, r, "root") for r in entries(root, root_rev(version, root_sha), ("Assets", "Packages", "ProjectSettings"))]
    rows += [(ws / ASSET_PATH, r, "assets") for r in entries(root / ASSET_PATH, asset_sha)]
    expected, changed, missing = set(), [], []
    for base, r, repo in rows:
        if r["kind"] != "blob":
            continue
        p = base / r["path"]
        expected.add(p.resolve() if p.exists() else p)
        if not p.exists():
            missing.append(str(p.relative_to(ws)))
        elif blob_id(p) != r["git_object"]:
            changed.append(str(p.relative_to(ws)))
    extra = []
    for top in ("Assets", "Packages", "ProjectSettings"):
        for dirpath, _, files in os.walk(ws / top):
            for f in files:
                p = (Path(dirpath) / f).resolve()
                if p not in expected:
                    extra.append({"path": str(p.relative_to(ws)), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()})
    report = {"version": version, "root_sha": root_sha, "asset_sha": asset_sha, "workspace": str(ws),
              "checkedUtc": datetime.now(timezone.utc).isoformat(),
              "original_files_checked": len(expected), "changed": changed, "missing": missing,
              "extra_files": sorted(extra, key=lambda x: x["path"])}
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, ensure_ascii=False, indent=1) + "\n")
    print(json.dumps({k: report[k] for k in ("original_files_checked",)} | {"changed": len(changed), "missing": len(missing), "extra": len(extra)}))
    return 1 if changed or missing else 0


if __name__ == "__main__":
    sys.exit(main())
