"""Read-only coverage and transparency audit for final tree artwork."""

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
items = json.loads((ROOT / "Data" / "features.json").read_text(encoding="utf-8"))["items"]
trees = [item["id"].split(".", 1)[1] for item in items if item["kind"] == "tree"]
errors = []

for name in trees:
    path = ROOT / "Art" / "Final" / "features" / f"{name}_color.png"
    if not path.is_file():
        errors.append(f"missing: {path.name}")
        continue
    with Image.open(path) as source:
        image = source.convert("RGBA")
        minimum, maximum = image.getchannel("A").getextrema()
        if minimum != 0 or maximum < 128:
            errors.append(f"bad transparency: {path.name} ({minimum}, {maximum})")

print(f"tree kinds: {len(trees)}; checked art: {len(trees) - len(errors)}; errors: {len(errors)}")
for error in errors:
    print(error)
raise SystemExit(bool(errors))
