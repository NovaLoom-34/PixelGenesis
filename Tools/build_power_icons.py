"""Generate the first 20 world-tool icons from editable vector masters."""

import json
from pathlib import Path

import cairosvg

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Art" / "Final" / "icons" / "powers"
RUNTIME = ROOT / "Assets" / "StreamingAssets" / "Art" / "Final" / "icons" / "powers"

# The order is checked against powers.json so labels and pictograms cannot drift.
GLYPHS = {
    "soil": '<path d="M5 21h22M7 17h18M10 13h12" stroke="#bf8a56"/><path d="M8 22l3 3m8-3l3 3" stroke="#e1b475"/>',
    "sand": '<path d="M4 23q7-9 12-3t12 3" fill="#dfbb6e" stroke="#f3d899"/><circle cx="11" cy="23" r="1" fill="#fff0b4"/><circle cx="20" cy="22" r="1" fill="#fff0b4"/>',
    "hills": '<path d="M3 24q7-16 13 0M14 24q8-19 15 0" fill="#648e52" stroke="#a9c787"/><path d="M7 24h21" stroke="#cfb886"/>',
    "mountain": '<path d="M3 25 13 7l5 9 3-5 8 14Z" fill="#587286" stroke="#c7d7d9"/><path d="m9 14 4-7 4 8m2 0 2-4 3 5" stroke="#f2eee3"/>',
    "shallow": '<path d="M3 17q4-4 8 0t8 0t10 0M3 23q4-4 8 0t8 0t10 0" stroke="#7edaca"/><path d="M7 27h18" stroke="#d5c282"/>',
    "water": '<path d="M3 13q4-4 8 0t8 0t10 0M3 20q4-4 8 0t8 0t10 0M3 26q4-4 8 0t8 0t10 0" stroke="#53b9e5"/>',
    "deep_water": '<path d="M3 11q4-4 8 0t8 0t10 0M3 17q4-4 8 0t8 0t10 0" stroke="#5186d1"/><path d="M16 19v8m-4-4 4 4 4-4" stroke="#9cc8fb"/>',
    "raise": '<path d="M5 25h22M16 22V6m-6 6 6-6 6 6" stroke="#c9af78"/><path d="M8 21h16" stroke="#82bc78"/>',
    "lower": '<path d="M5 8h22M16 11v16m-6-6 6 6 6-6" stroke="#c9af78"/><path d="M8 12h16" stroke="#82bc78"/>',
    "sponge": '<rect x="7" y="7" width="18" height="18" rx="4" fill="#d2b66b" stroke="#f3db92"/><circle cx="12" cy="13" r="2" fill="#7d6b46"/><circle cx="20" cy="12" r="1.5" fill="#7d6b46"/><circle cx="18" cy="20" r="2.2" fill="#7d6b46"/>',
    "road": '<path d="M12 4q-5 9 1 13t-1 11h9q6-7 0-11t-1-13Z" fill="#8d8580" stroke="#d9c39b"/><path d="M16 7v4m0 5v4m0 5v2" stroke="#f4deaf"/>',
    "field": '<path d="M5 23h22M7 18h18M9 13h14" stroke="#af8755"/><path d="M10 24V10m6 14V8m6 16V10" stroke="#a8c76b"/><path d="m8 12 2-3 2 3m2-2 2-4 2 4m2 2 2-3 2 3" stroke="#d2cf76"/>',
    "eraser": '<path d="m7 20 12-13 8 8-11 12H9Z" fill="#d68f91" stroke="#ffd4c9"/><path d="m12 15 9 9M6 27h21" stroke="#f5d4b5"/>',
    "tree": '<path d="M16 22v6m-4 0h8" stroke="#a47850"/><path d="M7 22 16 5l9 17Z" fill="#4e9b67" stroke="#b2d393"/><path d="M10 17h12" stroke="#d1e4a8"/>',
    "bush": '<path d="M5 25h22" stroke="#a9885a"/><path d="M7 21q-1-7 6-7 3-8 8-2 7-1 6 8 0 5-8 5H12q-5 0-5-4Z" fill="#529765" stroke="#a4d184"/><circle cx="12" cy="19" r="1" fill="#e7c46f"/><circle cx="21" cy="20" r="1" fill="#e7c46f"/>',
    "flowers": '<path d="M16 17v10m-4 0h8" stroke="#70ad70"/><circle cx="16" cy="14" r="3" fill="#f3d579"/><circle cx="16" cy="8" r="3" fill="#e8a0be"/><circle cx="22" cy="14" r="3" fill="#e8a0be"/><circle cx="16" cy="20" r="3" fill="#e8a0be"/><circle cx="10" cy="14" r="3" fill="#e8a0be"/>',
    "mushrooms": '<path d="M11 18v8h4v-8m5-2v10h4V16" fill="#eadbb4" stroke="#f9e6c0"/><path d="M6 18q1-9 7-9t7 9Zm11-2q1-7 5-7t6 7Z" fill="#c87669" stroke="#f3b39a"/><circle cx="13" cy="13" r="1" fill="#ffe8c0"/><circle cx="23" cy="12" r="1" fill="#ffe8c0"/>',
    "wheat": '<path d="M16 28V7m-4 4 4 4 4-4m-8 5 4 4 4-4m-8 5 4 4 4-4" stroke="#d2ad57"/><path d="M16 4v5" stroke="#f8db84"/>',
    "rock": '<path d="M4 25 9 12l10-4 9 11-3 7Z" fill="#788b90" stroke="#d6d9c7"/><path d="m9 12 8 8 11-1m-11 1-2 6" stroke="#aebfc2"/>',
    "ore_copper": '<path d="M4 25 9 12l10-4 9 11-3 7Z" fill="#687c83" stroke="#c7d0c6"/><path d="m9 20 6-4 3 3 7-4m-12 9 5-5" stroke="#ec9b57"/><circle cx="22" cy="21" r="2" fill="#f6bb72"/>',
}

BASE = '''<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
<circle cx="16" cy="16" r="15" fill="#1b2932" stroke="#60727c" stroke-width="1"/>
<g fill="none" stroke-linecap="round" stroke-linejoin="round" stroke-width="2">{glyph}</g>
</svg>'''


def main():
    items = json.loads((ROOT / "Data" / "powers.json").read_text(encoding="utf-8"))["items"][:20]
    ids = [item["id"].split(".", 1)[1] for item in items]
    assert ids == list(GLYPHS), "First 20 powers changed: update icon meanings before regenerating"
    OUT.mkdir(parents=True, exist_ok=True)
    RUNTIME.mkdir(parents=True, exist_ok=True)
    for name, glyph in GLYPHS.items():
        svg = BASE.format(glyph=glyph)
        (OUT / f"{name}.svg").write_text(svg, encoding="utf-8")
        data = cairosvg.svg2png(bytestring=svg.encode(), output_width=64, output_height=64)
        (OUT / f"{name}.png").write_bytes(data)
        (RUNTIME / f"{name}.png").write_bytes(data)
    print(f"Generated {len(GLYPHS)} final power icons and runtime copies")


if __name__ == "__main__":
    main()
