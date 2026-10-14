#!/usr/bin/env python3
"""Measure pixel differences between matching PNG screenshots.

Usage:
  python Tools/visual_compare.py reference.png candidate.png
  python Tools/visual_compare.py reference-folder candidate-folder --max-changed-pct 2

Without --max-changed-pct this is a report-only comparison, not an acceptance gate.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image


def png_files(path: Path) -> dict[str, Path]:
    if path.is_file():
        if path.suffix.lower() != ".png":
            raise ValueError(f"Expected PNG file: {path}")
        return {path.name: path}
    if not path.is_dir():
        raise FileNotFoundError(path)
    return {p.relative_to(path).as_posix(): p for p in sorted(path.rglob("*.png"))}


def compare_pair(name: str, baseline: Path, candidate: Path, channel_threshold: int) -> dict:
    with Image.open(baseline) as source, Image.open(candidate) as target:
        source = source.convert("RGBA")
        target = target.convert("RGBA")
        if source.size != target.size:
            return {
                "name": name,
                "baseline_size": list(source.size),
                "candidate_size": list(target.size),
                "dimension_match": False,
            }

        left, right = source.tobytes(), target.tobytes()
    pixels = len(left) // 4
    changed = 0
    total_delta = 0
    max_delta = 0
    for i in range(0, len(left), 4):
        pixel_delta = 0
        for channel in range(4):
            delta = abs(left[i + channel] - right[i + channel])
            total_delta += delta
            pixel_delta = max(pixel_delta, delta)
        max_delta = max(max_delta, pixel_delta)
        if pixel_delta > channel_threshold:
            changed += 1
    return {
        "name": name,
        "width": source.width,
        "height": source.height,
        "pixels": pixels,
        "changed_pixels": changed,
        "changed_pixel_pct": round(changed * 100.0 / max(1, pixels), 4),
        "mean_absolute_channel_delta": round(total_delta / max(1, pixels * 4), 4),
        "max_channel_delta": max_delta,
        "dimension_match": True,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", type=Path, help="Reference PNG or folder of reference PNGs")
    parser.add_argument("candidate", type=Path, help="Candidate PNG or folder of candidate PNGs")
    parser.add_argument("--channel-threshold", type=int, default=8,
                        help="Per-channel delta required to count a pixel as changed (default: 8)")
    parser.add_argument("--max-changed-pct", type=float,
                        help="Optional maximum changed-pixel percentage; omitted means report only")
    parser.add_argument("--report", type=Path, help="Optional JSON report output path")
    args = parser.parse_args(argv)
    if not 0 <= args.channel_threshold <= 255:
        parser.error("--channel-threshold must be from 0 through 255")
    if args.max_changed_pct is not None and not 0 <= args.max_changed_pct <= 100:
        parser.error("--max-changed-pct must be from 0 through 100")

    try:
        baselines = png_files(args.baseline)
        candidates = png_files(args.candidate)
        common = sorted(baselines.keys() & candidates.keys())
        missing_candidates = sorted(baselines.keys() - candidates.keys())
        missing_baselines = sorted(candidates.keys() - baselines.keys())
        if not common:
            raise ValueError("No PNG names match between baseline and candidate.")
        pairs = [compare_pair(name, baselines[name], candidates[name], args.channel_threshold) for name in common]
        report = {
            "baseline": str(args.baseline),
            "candidate": str(args.candidate),
            "channel_threshold": args.channel_threshold,
            "max_changed_pct": args.max_changed_pct,
            "mode": "threshold" if args.max_changed_pct is not None else "report-only",
            "missing_candidates": missing_candidates,
            "missing_baselines": missing_baselines,
            "images": pairs,
        }
        rendered = json.dumps(report, ensure_ascii=False, indent=2)
        print(rendered)
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(rendered + "\n", encoding="utf-8")

        failures = bool(missing_candidates or missing_baselines)
        for item in pairs:
            if not item["dimension_match"]:
                failures = True
            elif args.max_changed_pct is not None and item["changed_pixel_pct"] > args.max_changed_pct:
                failures = True
        return 1 if failures else 0
    except (OSError, ValueError) as error:
        print(f"visual_compare: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
