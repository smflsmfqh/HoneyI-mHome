#!/usr/bin/env python3
"""Extract a pinned historical Unity project into a NEW isolated workspace.

Never checks out either source repository and never replaces an existing folder.
Python standard library + Git only. H0 has no asset gitlink; its asset commit is an
estimate (see ESTIMATED_ASSETS) and its project lives under MiniGameProject/.
Unity import/compilation and physics tests are separate operations.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tarfile


VERSIONS = {
    "H1": ("9b83a248d281173446c0640ae1d6740506150c4d", "ffd2c2e0be38f63f7ff4ad6d88514f8ffb3d93aa"),
    "H2": ("442b99cebe6b0405ed0d762018d40a20ba40f30d", "8fd1b6c726fc85c3750fcbe0ca860cf0017501d5"),
    "H3": ("7cca6106f171560aafeafa88576e314c8213bab4", "5617eccdef11a084b36dbea14f4022b376b61c1c"),
    "H4": ("dd2d8801a9231d8a856390f50eb1354b17e4e2f1", "71b2dc714cf655d2755c547a9742b1ef94b333a8"),
    "H5": ("9c7114415fbd04353c33a10594a5490beb92df9b", "71b2dc714cf655d2755c547a9742b1ef94b333a8"),
    "H6": ("d746a7b51c1b642557613051c8c3908701d5461f", "a160f46591e2af970f47d05603d64ddad4781e59"),
    "H7": ("b8efce3526d3ef85eb0c85a580884369f3f5f48a", "a160f46591e2af970f47d05603d64ddad4781e59"),
}
# H0(1a44741, 05-18 18:05): 루트에 에셋 gitlink가 없다. H0 참조 GUID 대조로 23de6f3c(05-18 20:06,
# 다음 루트 커밋 31beed1이 처음 가리킨 에셋)를 추정 에셋으로 쓴다. Evidence/M5/M5-audit-01.json 참고.
VERSIONS["H0"] = ("1a4474108e7430e6ed854bb914d36e598c57ea66", "23de6f3c207d54acf193d8961e54da0ee48d68a2")
# Hm1(문서상 H-1, d3a9856, 05-18 16:40): H0 "수직 낙하 수정 중" 직전. MovePosition 이동.
# 에셋은 H0와 같은 23de6f3c로 고정해 H0와의 차이를 루트 변경으로 한정한다. 9c220857과 도시 맵은
# 이름 변경만 있고 내용이 같다(Evidence/M5/M5-audit-04-Hm1.json).
VERSIONS["Hm1"] = ("d3a98563b6dae62d68f5f08c689ac144b4885886", "23de6f3c207d54acf193d8961e54da0ee48d68a2")
ESTIMATED_ASSETS = {"H0": "no gitlink in root commit; asset estimated from GUID coverage (Evidence/M5/M5-audit-01.json)",
                    "Hm1": "no gitlink in root commit; same asset as H0 to isolate root changes (Evidence/M5/M5-audit-04-Hm1.json)"}
ROOT_PREFIX = {"H0": "MiniGameProject", "Hm1": "MiniGameProject"}
ASSET_PATH = Path("Assets/minigame-project-assets-smflsmfqh")


def root_rev(version, sha):
    """루트 저장소에서 프로젝트가 있는 tree-ish. H0는 하위 폴더."""
    prefix = ROOT_PREFIX.get(version)
    return f"{sha}:{prefix}" if prefix else sha


def git(repo, *args):
    return subprocess.check_output(["git", *args], cwd=repo)


def entries(repo, sha, paths=()):
    result = []
    for raw in git(repo, "ls-tree", "-r", "-l", "-z", sha, *paths).split(b"\0"):
        if not raw:
            continue
        meta, path = raw.split(b"\t", 1)
        mode, kind, blob, size = meta.decode().split()
        result.append({"path": path.decode(), "mode": mode, "kind": kind,
                       "git_object": blob, "bytes": int(size) if size.isdigit() else 0})
    return result


def extract(repo, sha, destination, paths=()):
    process = subprocess.Popen(["git", "archive", "--format=tar", sha, *paths],
                               cwd=repo, stdout=subprocess.PIPE)
    try:
        with tarfile.open(fileobj=process.stdout, mode="r|") as archive:
            for member in archive:
                if hasattr(tarfile, "data_filter"):
                    archive.extract(member, path=destination, filter="data")
                else:
                    target = (destination / member.name).resolve()
                    if not target.is_relative_to(destination.resolve()) or member.issym() or member.islnk():
                        raise ValueError("Unsafe archive member: " + member.name)
                    archive.extract(member, path=destination)
        if process.wait() != 0:
            raise RuntimeError("git archive failed")
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait()
        process.stdout.close()


def file_sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version", choices=VERSIONS)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--destination", type=Path,
                        help="New folder under Benchmarks/.physics-work; default: project")
    parser.add_argument("--inventory-only", action="store_true", help="Read Git objects without extracting files")
    args = parser.parse_args()
    root = args.root.resolve()
    assets = root / ASSET_PATH
    root_sha, asset_sha = VERSIONS[args.version]
    git(root, "cat-file", "-e", root_sha + "^{commit}")
    git(assets, "cat-file", "-e", asset_sha + "^{commit}")
    rrev = root_rev(args.version, root_sha)
    if args.version in ESTIMATED_ASSETS:
        if git(root, "ls-tree", rrev, str(ASSET_PATH)).strip():
            raise ValueError("H0 unexpectedly has an asset gitlink; re-check the estimate")
    else:
        linked = git(root, "ls-tree", root_sha, str(ASSET_PATH)).decode().split()[2]
        if linked != asset_sha:
            raise ValueError("Pinned asset SHA differs from historical root gitlink")
    root_files = entries(root, rrev, ("Assets", "Packages", "ProjectSettings"))
    asset_files = entries(assets, asset_sha)
    size = sum(row["bytes"] for row in root_files + asset_files)
    report = {"version": args.version, "root_sha": root_sha, "asset_sha": asset_sha,
              "original_source_bytes": size, "root_entries": len(root_files),
              "asset_entries": len(asset_files), "physics_tests_run": False,
              "root_prefix": ROOT_PREFIX.get(args.version), "asset_estimate": ESTIMATED_ASSETS.get(args.version)}
    if args.inventory_only:
        print(json.dumps(report, indent=2))
        return

    work = root / "Benchmarks/.physics-work"
    destination = (args.destination or work / "project").resolve()
    if not destination.is_relative_to(work.resolve()) or destination == work.resolve():
        raise ValueError("Destination must be inside Benchmarks/.physics-work")
    if destination.exists():
        raise FileExistsError("Existing workspace preserved: " + str(destination))
    # Reserve 5 GiB for working space and 5 GiB for Unity import, in addition to source.
    if shutil.disk_usage(root).free < size + 10 * 1024 ** 3:
        raise OSError("Not enough space for source, import budget, and reserve")

    work.mkdir(parents=True, exist_ok=True)
    ignore = work / ".gitignore"
    if not ignore.exists():
        ignore.write_text("*\n")
    destination.mkdir(parents=True)
    report.update({"created_at_utc": datetime.now(timezone.utc).isoformat(),
                   "workspace": str(destination), "state": "extracting"})
    marker = destination / ".physics-owned.json"
    marker.write_text(json.dumps(report, indent=2) + "\n")
    extract(root, rrev, destination, ("Assets", "Packages", "ProjectSettings"))
    target_assets = destination / ASSET_PATH
    target_assets.mkdir(parents=True, exist_ok=True)
    extract(assets, asset_sha, target_assets)
    # Verify bytes against Git blob identity before Unity is allowed to import.
    verification = []
    for base, rows, repo in [(destination, root_files, "root"), (target_assets, asset_files, "assets")]:
        for row in rows:
            if row["kind"] != "blob":
                continue
            path = base / row["path"]
            data = path.read_bytes() if not path.is_symlink() else str(path.readlink()).encode()
            blob = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
            if blob != row["git_object"]:
                raise ValueError("Extraction byte mismatch: " + str(path))
            verification.append({"repo": repo, **row, "sha256": hashlib.sha256(data).hexdigest()})
    evidence = root / "Benchmarks/PhysicsValidation/Evidence" / args.version
    evidence.mkdir(parents=True, exist_ok=True)
    index = evidence / "prepared-source-files.json"
    if index.exists():
        # Keep existing evidence immutable: a repeat preparation gets a distinct index.
        index = evidence / ("prepared-source-files-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + ".json")
    index.write_text(json.dumps(verification, indent=2) + "\n")
    report.update({"state": "prepared_source_verified", "source_file_index": str(index.relative_to(root)),
                   "source_file_index_sha256": file_sha256(index), "free_bytes": shutil.disk_usage(root).free})
    marker.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
