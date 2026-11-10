#!/usr/bin/env python3
"""Windows 전달용 smoke 묶음 생성.

빌드 출력 폴더 전체(exe, _Data, UnityPlayer.dll 등)를 버전 이름 폴더로 복사하고 RunSmoke.cmd, 안내문,
MANIFEST.json, SHA256SUMS.txt를 더해 ZIP으로 만든다. 기존 묶음은 덮어쓰지 않는다.
Unity가 배포하지 말라고 표시한 *_DoNotShip 폴더는 제외한다.

사용: python3 -I make_smoke_bundle.py <bundle_id> <out_zip> H1=<build_dir> H7=<build_dir> ...
      [--script RunPilot.cmd] [--readme README-Windows-pilot.txt] [--extra profiles/x.json=<src>] ...
"""
import hashlib, json, os, shutil, sys, tempfile, zipfile
from datetime import datetime, timezone
from pathlib import Path

HARNESS = Path(__file__).resolve().parents[1] / "Harness"


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    bundle_id, out_zip = sys.argv[1], Path(sys.argv[2])
    script, readme, extras, builds = "RunSmoke.cmd", "README-Windows.txt", {}, {}
    rest = sys.argv[3:]
    i = 0
    while i < len(rest):
        a = rest[i]
        if a in ("--script", "--readme", "--extra"):
            val = rest[i + 1]
            i += 2
            if a == "--script":
                script = val
            elif a == "--readme":
                readme = val
            else:
                dst, src = val.split("=", 1)
                extras[dst] = src
            continue
        k, v = a.split("=", 1)
        builds[k] = v
        i += 1
    if out_zip.exists():
        raise SystemExit("bundle exists, refusing to overwrite: " + str(out_zip))
    with tempfile.TemporaryDirectory(dir=out_zip.parent) as tmp:
        root = Path(tmp) / bundle_id
        root.mkdir()
        manifest = {"bundleId": bundle_id, "createdUtc": datetime.now(timezone.utc).isoformat(), "builds": {}}
        for version, src in builds.items():
            src = Path(src)
            result = json.loads((src / "build-result.json").read_text())
            if result["result"] != "Succeeded":
                raise SystemExit(f"{version} build did not succeed")
            shutil.copytree(src, root / version, ignore=shutil.ignore_patterns("*_DoNotShip", "build-result.json"))
            manifest["builds"][version] = {"buildId": result["buildId"], "sourceDir": str(src),
                                           "buildInfo": json.loads(result["buildInfo"])}
        shutil.copyfile(HARNESS / "Windows" / script, root / script)
        shutil.copyfile(HARNESS / "Windows" / readme, root / readme)
        for dst, src in extras.items():
            (root / dst).parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(src, root / dst)
        manifest["script"] = script
        manifest["extras"] = {dst: sha(src) for dst, src in extras.items()}
        (root / "MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        files = sorted(p for p in root.rglob("*") if p.is_file())
        sums = "".join(f"{sha(p)}  {p.relative_to(root).as_posix()}\n" for p in files)
        (root / "SHA256SUMS.txt").write_text(sums)
        with zipfile.ZipFile(out_zip, "w", zipfile.ZIP_DEFLATED) as z:
            for p in sorted(q for q in root.rglob("*") if q.is_file()):
                z.write(p, Path(bundle_id) / p.relative_to(root))
    digest = sha(out_zip)
    Path(str(out_zip) + ".sha256").write_text(f"{digest}  {out_zip.name}\n")
    print(json.dumps({"zip": str(out_zip), "bytes": out_zip.stat().st_size, "sha256": digest, "files": len(files) + 1}))


if __name__ == "__main__":
    main()
