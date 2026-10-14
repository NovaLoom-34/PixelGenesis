"""Inventory runtime sprite-source coverage for buildings and world features."""

import base64
import json
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Art"
STATES = ("construction", "complete", "ruin")
BUILDING_ATLAS_COLUMNS = {
    "house": ("architecture", 0), "watchtower": ("architecture", 1), "smithy": ("architecture", 2),
    "hall_1": ("settlement", 0), "granary": ("settlement", 1), "market": ("settlement", 2),
    "hall_2": ("civic", 0), "hall_3": ("civic", 1), "storage": ("civic", 2),
    "farm_shed": ("industry", 0), "mine": ("industry", 1), "lumber_camp": ("industry", 2),
    "bakery": ("food", 0), "pasture": ("food", 1), "beehive": ("food", 2),
    "bonfire": ("founding", 0), "tent": ("founding", 1), "hut": ("founding", 2),
    "well": ("publicworks", 0), "windmill": ("publicworks", 1), "inn": ("publicworks", 2),
    "manor": ("advanced", 0), "stable": ("advanced", 1), "barracks": ("advanced", 2),
    "fishing_hut": ("naval", 0), "docks": ("naval", 1), "shipyard": ("naval", 2),
    "library": ("culture", 0), "school": ("culture", 1), "temple": ("culture", 2),
    "grand_temple": ("religion", 0), "healers_house": ("religion", 1), "graveyard": ("religion", 2),
    "statue_god": ("monuments", 0), "monument": ("monuments", 1), "pyre": ("monuments", 2),
    "wall": ("fortifications", 0), "gate": ("fortifications", 1),
    "nest": ("habitats", 0), "hive": ("habitats", 1), "ruins": ("habitats", 2),
}


def valid_png(path):
    if not path.is_file():
        return False
    try:
        with Image.open(path) as image:
            image.verify()
        return True
    except Exception:
        return False


def item_name(item):
    return item.get("shortId") or item["id"].split(".", 1)[-1]


def building_atlas_valid(name):
    style, separator, remainder = name.partition("_")
    building, state_separator, state = remainder.rpartition("_")
    if not separator or not state_separator or building not in BUILDING_ATLAS_COLUMNS or state not in STATES:
        return False
    sheet, column = BUILDING_ATLAS_COLUMNS[building]
    image_path = ART / "Final" / "buildings" / f"{style}_{sheet}.png"
    layout_path = ART / "Final" / "buildings" / f"{style}_{sheet}.json"
    if not valid_png(image_path) or not layout_path.is_file():
        return False
    try:
        with Image.open(image_path) as image:
            if image.width < 192 or image.height < 192:
                return False
        layout = json.loads(layout_path.read_text(encoding="utf-8"))
        frames = layout.get("frames")
        columns = layout.get("columns", 3)
        if not isinstance(columns, int) or columns < 1 or column >= columns or not isinstance(frames, list) or len(frames) != columns * 3:
            return False
        frame = frames[STATES.index(state) * columns + column]
        required = ("x", "y", "width", "height", "outputWidth", "outputHeight", "sampleWidth", "sampleHeight")
        if any(not isinstance(frame.get(key), int) for key in required):
            return False
        if not (1 <= frame["outputWidth"] <= 64 and 1 <= frame["outputHeight"] <= 64):
            return False
        if not (1 <= frame["sampleWidth"] <= frame["outputWidth"] and 1 <= frame["sampleHeight"] <= frame["outputHeight"]):
            return False
        if min(frame["x"], frame["y"]) < 0 or min(frame["width"], frame["height"]) < 1:
            return False
        with Image.open(image_path) as image:
            if frame["x"] + frame["width"] > image.width or frame["y"] + frame["height"] > image.height:
                return False
        mask = base64.b64decode(frame.get("mask", ""), validate=True)
        return len(mask) == frame["sampleWidth"] * frame["sampleHeight"]
    except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError):
        return False


def feature_resource_names():
    image_path = ART / "Final" / "features" / "feature_resources_atlas.png"
    layout_path = ART / "Final" / "features" / "feature_resources_atlas.json"
    if not valid_png(image_path) or not layout_path.is_file():
        return set()
    try:
        layout = json.loads(layout_path.read_text(encoding="utf-8"))
        names = layout.get("sprites")
        columns, rows = layout.get("columns"), layout.get("rows")
        if (layout.get("version") != 1 or not isinstance(columns, int) or not isinstance(rows, int) or
                not 1 <= columns <= 8 or not 1 <= rows <= 8 or
                not isinstance(names, list) or len(names) != columns * rows):
            return set()
        with Image.open(image_path) as image:
            if image.width < columns * 16 or image.height < rows * 16:
                return set()
        return {name for name in names if isinstance(name, str)}
    except (OSError, ValueError, TypeError, json.JSONDecodeError):
        return set()


def resolve_building(style, building, state):
    name = f"{style}_{building}_{state}"
    candidates = (
        ("final-color", ART / "Final" / "buildings" / f"{name}_color.png"),
    )
    for source, path in candidates:
        if valid_png(path):
            return source
    if building_atlas_valid(name):
        return "building-atlas"
    for source, path in (
        ("final", ART / "Final" / "buildings" / f"{name}.png"),
        ("placeholder", ART / "Placeholder" / "buildings" / f"{name}.png"),
    ):
        if valid_png(path):
            return source
    return "fallback"


def resolve_feature(name, resource_names):
    for source, path in (
        ("final-color", ART / "Final" / "features" / f"{name}_color.png"),
    ):
        if valid_png(path):
            return source
    atlas_path = ART / "Final" / "features" / "feature_resources_atlas.png"
    if name in resource_names and valid_png(atlas_path):
        return "resource-atlas"
    for source, path in (
        ("final", ART / "Final" / "features" / f"{name}.png"),
        ("placeholder", ART / "Placeholder" / "features" / f"{name}.png"),
    ):
        if valid_png(path):
            return source
    return "fallback"


def report(label, entries):
    counts = Counter(source for _, source in entries)
    print(f"{label}: {len(entries)} runtime keys")
    for source in ("final-color", "building-atlas", "resource-atlas", "final", "placeholder", "fallback"):
        if counts[source]:
            print(f"  {source}: {counts[source]}")
    unresolved = [(name, source) for name, source in entries if source in ("placeholder", "fallback")]
    print(f"  unresolved: {len(unresolved)}")
    for name, source in unresolved[:24]:
        print(f"    {source}: {name}")
    if len(unresolved) > 24:
        print(f"    ... {len(unresolved) - 24} more")
    if label == "buildings":
        by_style = defaultdict(Counter)
        for name, source in entries:
            by_style[name.split("_", 1)[0]][source] += 1
        print("  per-style coverage:")
        for style, style_counts in sorted(by_style.items()):
            resolved = style_counts["final-color"] + style_counts["building-atlas"] + style_counts["final"]
            print(f"    {style}: {resolved}/123 final; {style_counts['placeholder']} placeholder; {style_counts['fallback']} fallback")


def main():
    buildings = json.loads((ROOT / "Data" / "buildings.json").read_text(encoding="utf-8"))["items"]
    styles = json.loads((ROOT / "Data" / "building_styles.json").read_text(encoding="utf-8"))["items"]
    features = json.loads((ROOT / "Data" / "features.json").read_text(encoding="utf-8"))["items"]

    building_entries = []
    for style in styles:
        style_name = item_name(style)
        for building in buildings:
            building_name = item_name(building)
            for state in STATES:
                key = f"{style_name}_{building_name}_{state}"
                building_entries.append((key, resolve_building(style_name, building_name, state)))

    resource_names = feature_resource_names()
    feature_entries = [(item_name(feature), resolve_feature(item_name(feature), resource_names)) for feature in features]
    report("buildings", building_entries)
    report("features", feature_entries)


if __name__ == "__main__":
    main()
