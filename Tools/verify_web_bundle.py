"""Reject a stale or incomplete Builds/WebSite before uploading it to hosting."""

import hashlib
import argparse
import json
import re
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "StreamingAssets"
BUNDLE = ROOT / "Builds" / "WebSite"


def same(a: Path, b: Path) -> bool:
    return b.is_file() and hashlib.sha256(a.read_bytes()).digest() == hashlib.sha256(b.read_bytes()).digest()


PLACEHOLDER = "__PG_BUILD_VERSION__"


def injected_page_matches(source: Path, built: Path) -> str | None:
    """deploy_web.ps1 swaps PLACEHOLDER for the run id in oyun.html; return a problem text or None."""
    if not built.is_file():
        return "missing"
    text = built.read_text(encoding="utf-8-sig")
    match = re.search(r'var buildVersion = "([A-Za-z0-9._-]+)"', text)
    if PLACEHOLDER in text or not match:
        return "build version placeholder not injected"
    expected = source.read_text(encoding="utf-8-sig").replace(PLACEHOLDER, match.group(1))
    return None if text == expected else "out of date"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-path", type=Path, default=BUNDLE, help="prepared hosting bundle to verify")
    parser.add_argument("--archive-path", type=Path, help="optional ZIP archive to compare with the prepared bundle")
    args = parser.parse_args()
    bundle = args.bundle_path if args.bundle_path.is_absolute() else ROOT / args.bundle_path
    bundle = bundle.resolve()
    problems = []
    binary = bundle / "Build" / "PixelGenesis.wasm"
    if not binary.is_file():
        problems.append("Unity WebGL binary missing")
    else:
        runtime_sources = [p for p in ROOT.glob("Assets/_Project/**/*.cs") if "Editor" not in p.parts]
        runtime_sources += [*ROOT.glob("Assets/_Project/**/*.shader"), *ROOT.glob("Assets/_Project/**/*.uss")]
        newest = max(runtime_sources, key=lambda p: p.stat().st_mtime)
        if binary.stat().st_mtime < newest.stat().st_mtime:
            problems.append(f"WebGL binary predates source: {newest.relative_to(ROOT)}")

    website = ROOT / "WebSite"
    for source in website.rglob("*"):
        if source.is_file():
            rel = source.relative_to(website)
            if "tests" in rel.parts:
                continue
            if rel.as_posix() == "oyun.html":
                why = injected_page_matches(source, bundle / rel)
                if why:
                    problems.append(f"Website file missing or out of date: oyun.html ({why})")
            elif not same(source, bundle / rel):
                problems.append(f"Website file missing or out of date: {rel}")

    for required in ("index.html", "oyun.html", ".htaccess", "firebase-config.js"):
        if not (bundle / required).is_file():
            problems.append(f"Required hosting file missing: {required}")
    for page in ("index.html", "oyun.html"):
        page_path = bundle / page
        if not page_path.is_file():
            continue
        html = page_path.read_text(encoding="utf-8", errors="replace")
        match = re.search(r'<meta\s+property=["\']og:image["\']\s+content=["\']([^"\']+)', html, re.IGNORECASE)
        if not match:
            problems.append(f"Open Graph image metadata missing: {page}")
            continue
        image = match.group(1).split("?", 1)[0]
        if not image.startswith(("https://", "http://", "data:")) and not (bundle / image).is_file():
            problems.append(f"Open Graph image missing for {page}: {image}")

    if args.archive_path:
        archive = args.archive_path if args.archive_path.is_absolute() else ROOT / args.archive_path
        if not archive.is_file():
            problems.append(f"Hosting archive missing: {archive}")
        else:
            try:
                with zipfile.ZipFile(archive) as zipped:
                    bad_member = zipped.testzip()
                    if bad_member:
                        problems.append(f"Hosting archive CRC failure: {bad_member}")
                    archive_files = [item for item in zipped.infolist() if not item.is_dir()]
                    archive_names = [item.filename.replace("\\", "/") for item in archive_files]
                    if len(archive_names) != len(set(archive_names)):
                        problems.append("Hosting archive contains duplicate paths")
                    if any(name.startswith("/") or ".." in Path(name).parts for name in archive_names):
                        problems.append("Hosting archive contains an unsafe path")
                    bundle_files = {
                        path.relative_to(bundle).as_posix(): path
                        for path in bundle.rglob("*")
                        if path.is_file()
                    }
                    if set(archive_names) != set(bundle_files):
                        missing = sorted(set(bundle_files) - set(archive_names))
                        extra = sorted(set(archive_names) - set(bundle_files))
                        problems.append(f"Hosting archive file list differs from bundle (missing={missing[:5]}, extra={extra[:5]})")
                    else:
                        for item, name in zip(archive_files, archive_names):
                            if hashlib.sha256(zipped.read(item)).digest() != hashlib.sha256(bundle_files[name].read_bytes()).digest():
                                problems.append(f"Hosting archive content differs from bundle: {name}")
                                break
            except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
                problems.append(f"Hosting archive could not be verified: {exc}")

    src_manifest = SOURCE / "manifest.json"
    dst_manifest = BUNDLE / "StreamingAssets" / "manifest.json"
    if not src_manifest.is_file() or not same(src_manifest, bundle / "StreamingAssets" / "manifest.json"):
        problems.append("StreamingAssets manifest missing or out of date")
    else:
        paths = json.loads(src_manifest.read_text(encoding="utf-8"))["files"]
        critical = [p for p in paths if p.startswith((
            "Art/Final/units/", "Art/Final/features/", "Art/Final/icons/powers/",
            "Art/Final/icons/menu/", "Data/",
        ))]
        for rel in critical:
            if not same(SOURCE / rel, bundle / "StreamingAssets" / rel):
                problems.append(f"Runtime asset missing or out of date: {rel}")
                if len(problems) >= 20:
                    break

    if problems:
        print("Web bundle NOT ready for upload:")
        for problem in problems:
            print(" - " + problem)
        return 1
    print("Web bundle ready: player, website, OG assets and critical StreamingAssets match source")
    if args.archive_path:
        print("Hosting archive ready: safe paths, CRC, file list and file contents match the bundle")
    return 0


if __name__ == "__main__":
    sys.exit(main())
