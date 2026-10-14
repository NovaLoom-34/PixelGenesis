#!/usr/bin/env python3
"""Fail when Unity's EditMode reports omit source-declared test cases."""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


TEST_ATTRIBUTE = re.compile(r"\[\s*(?:TestCaseSource|TestCase|UnityTest|Test)\b")
GRAPHICS_CATEGORY = re.compile(r'\[\s*Category\s*\(\s*"Graphics"\s*\)\s*\]')
EXPLICIT_ATTRIBUTE = re.compile(r"\[\s*Explicit\b")
COMMENTS = re.compile(r"/\*.*?\*/|//[^\r\n]*", re.DOTALL)


def source_inventory(test_root: Path) -> tuple[int, int, list[str]]:
    total = 0
    graphics = 0
    invalid_explicit: list[str] = []
    for path in sorted(test_root.rglob("*.cs")):
        source = COMMENTS.sub("", path.read_text(encoding="utf-8-sig"))
        count = len(TEST_ATTRIBUTE.findall(source))
        if not count:
            continue
        total += count
        is_graphics = bool(GRAPHICS_CATEGORY.search(source))
        is_explicit = bool(EXPLICIT_ATTRIBUTE.search(source))
        if is_graphics:
            graphics += count
        if is_explicit and not is_graphics:
            invalid_explicit.append(str(path))
    return total, graphics, invalid_explicit


def result_total(path: Path) -> int:
    root = ET.parse(path).getroot()
    if root.tag != "test-run":
        raise ValueError(f"Expected NUnit <test-run> root in {path}")
    value = root.get("total")
    if value is None:
        raise ValueError(f"NUnit result has no total count: {path}")
    return int(value)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--editmode-xml", required=True, type=Path)
    parser.add_argument("--graphics-xml", required=True, type=Path)
    parser.add_argument("--editmode-floor", type=int, default=204)
    parser.add_argument("--graphics-floor", type=int, default=3)
    args = parser.parse_args()

    test_root = args.root / "Assets" / "_Project" / "Tests" / "EditMode"
    source_total, graphics_total, invalid_explicit = source_inventory(test_root)
    if invalid_explicit:
        raise ValueError("Explicit tests must be in the separately selected Graphics category: " + ", ".join(invalid_explicit))
    expected_editmode = source_total - graphics_total
    actual_editmode = result_total(args.editmode_xml)
    actual_graphics = result_total(args.graphics_xml)
    min_editmode = max(args.editmode_floor, expected_editmode)
    min_graphics = max(args.graphics_floor, graphics_total)

    print(f"Source inventory: {source_total} annotated cases ({expected_editmode} EditMode, {graphics_total} Graphics lower bounds).")
    print(f"NUnit results: {actual_editmode} EditMode and {actual_graphics} Graphics cases.")
    if actual_editmode < min_editmode or actual_graphics < min_graphics:
        print(f"FAIL: expected at least {min_editmode} EditMode and {min_graphics} Graphics cases.", file=sys.stderr)
        return 1
    print("PASS: NUnit discovery meets source inventory and historical floors.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ET.ParseError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
