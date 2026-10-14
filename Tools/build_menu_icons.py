"""Build the coherent PixelGenesis menu icon family from editable SVG masters."""

from pathlib import Path

import cairosvg

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Art" / "Final" / "icons" / "menu"
RUNTIME = ROOT / "Assets" / "StreamingAssets" / "Art" / "Final" / "icons" / "menu"

# Each pictogram has a distinct silhouette at 24 px and uses the power-icon palette.
GLYPHS = {
    "continue": '<path d="M8 8v16l15-8Z" fill="#8fc9a0" stroke="#c4e8ae"/>',
    "new_world": '<circle cx="16" cy="16" r="10" fill="#356d81" stroke="#90d2df"/><path d="M8 18q5-6 9-2t7-1M16 6v20m-9-10h18" stroke="#b8d8a2"/>',
    "load": '<path d="M5 12V7h8l3 3h11v15H5Z" fill="#5a7190" stroke="#bfd0e4"/><path d="M16 12v9m-4-4 4 4 4-4" stroke="#f4d186"/>',
    "save": '<path d="M6 5h18l3 3v19H5V5Z" fill="#5a7190" stroke="#bfd0e4"/><path d="M10 5v8h12V5M10 27v-9h13v9" stroke="#f1d38d"/><path d="M17 19v6" stroke="#f1d38d"/>',
    "save_load": '<path d="M5 11V7h8l3 3h11v15H5Z" fill="#5a7190" stroke="#bfd0e4"/><path d="M13 15v7m-3-3 3 3 3-3m6-6v7m-3-4 3-3 3 3" stroke="#f2d388"/>',
    "delete": '<path d="M9 10h14l-1 17H10ZM7 8h18M13 8V5h6v3" fill="#a04e50" stroke="#f1b1a2"/><path d="M14 14v9m4-9v9" stroke="#ffe0c9"/>',
    "settings": '<circle cx="16" cy="16" r="8" fill="#657487" stroke="#d2d8df"/><circle cx="16" cy="16" r="3" fill="#e8ce87"/><path d="M16 4v4m0 16v4M4 16h4m16 0h4M7.5 7.5l3 3m11 11 3 3m0-17-3 3m-11 11-3 3" stroke="#d2d8df"/>',
    "achievements": '<path d="M10 5h12v10q0 7-6 7t-6-7ZM10 8H5v4q0 4 5 4m12-8h5v4q0 4-5 4M16 22v4m-5 1h10" fill="#aa8344" stroke="#f7d783"/><path d="m16 9 1.3 3h3l-2.4 2 1 3-2.9-1.8-2.9 1.8 1-3-2.4-2h3Z" fill="#ffe8a6" stroke="#ffe8a6"/>',
    "play_now": '<circle cx="16" cy="16" r="10" fill="#356d81" stroke="#98d4e0"/><path d="M10 18q6-8 12 0M16 6v20" stroke="#b8d8a2"/><circle cx="16" cy="16" r="2" fill="#f6db91"/>',
    "quit": '<path d="M17 6h8v20h-8M15 16H4m0 0 5-5m-5 5 5 5" stroke="#efa28d"/><path d="M20 11v10" stroke="#efa28d"/>',
    "inspect": '<circle cx="14" cy="14" r="7" fill="#457c8c" stroke="#b5dce3"/><path d="m19 19 8 8" stroke="#e9d49e"/><circle cx="14" cy="14" r="2" fill="#e9d49e"/>',
    "laws": '<path d="M16 5v21M8 10h16M6 25h20" stroke="#d5bc80"/><path d="m8 11-4 8h8Zm16 0-4 8h8Z" fill="#7793a0" stroke="#d1d9d6"/>',
    "statistics": '<path d="M5 27h22" stroke="#b7c9d4"/><rect x="7" y="17" width="4" height="10" fill="#79a7ba"/><rect x="14" y="11" width="4" height="16" fill="#8bc19b"/><rect x="21" y="6" width="4" height="21" fill="#dfb675"/>',
    "history": '<path d="M8 8q7-4 16 0v18q-9-4-16 0Z" fill="#756c5e" stroke="#e6cea1"/><path d="M12 12h8m-8 5h8m-8 5h5" stroke="#f2e0bc"/>',
    "era_clock": '<circle cx="16" cy="16" r="11" fill="#425f76" stroke="#bed5df"/><path d="M16 8v9l6 4" stroke="#f3d48f"/><path d="M16 4v3m0 18v3M4 16h3m18 0h3" stroke="#bed5df"/>',
    "graphs": '<path d="M5 26h22M6 25V6" stroke="#b6c9d2"/><path d="m8 21 5-5 4 2 5-9 4 3" stroke="#91d0a2"/><circle cx="22" cy="9" r="2" fill="#f1ce82"/>',
    "overlays": '<path d="m16 5 12 7-12 7L4 12Zm-12 13 12 7 12-7" fill="#54758b" stroke="#bad7e2"/><path d="m4 22 12 7 12-7" stroke="#e6c882"/>',
    "plates": '<path d="M5 9h22v13H5Z" fill="#5e7082" stroke="#d3dce2"/><path d="M10 14h12m-9 4h6" stroke="#f3d18a"/>',
    "menu": '<path d="M6 9h20M6 16h20M6 23h20" stroke="#f2d18a"/>',
    "tab_world": '<circle cx="16" cy="16" r="10" fill="#3d7b93" stroke="#a4dbe4"/><path d="M8 15q4-4 7 0t9 0m-8-9v20" stroke="#b2d39c"/>',
    "tab_civ": '<path d="M5 26h22M8 26V13l8-6 8 6v13M12 18h8m-6 8v-5h4v5" fill="#807468" stroke="#efd09b"/>',
    "tab_creatures": '<path d="M7 20q0-5 5-5h8q5 0 5 5t-5 5h-8q-5 0-5-5Z" fill="#9a7185" stroke="#edbed0"/><circle cx="9" cy="10" r="2" fill="#edbed0"/><circle cx="14" cy="8" r="2" fill="#edbed0"/><circle cx="19" cy="8" r="2" fill="#edbed0"/><circle cx="24" cy="10" r="2" fill="#edbed0"/>',
    "tab_nature": '<path d="M5 22q5-16 20-16 1 16-12 20-8 2-8-4Z" fill="#528c66" stroke="#b0d7a1"/><path d="M8 25 24 9" stroke="#e7d898"/>',
    "tab_destruction": '<path d="m16 4 4 9 7 3-6 4-3 8-5-7-8-2 7-5Z" fill="#a4574b" stroke="#f0af86"/><path d="m16 9 1 6-4 4" stroke="#ffe0ab"/>',
    "tab_other": '<circle cx="16" cy="16" r="9" fill="#725f88" stroke="#d3b9e1"/><circle cx="16" cy="16" r="2" fill="#f4d58e"/><path d="M16 5v4m0 14v4M5 16h4m14 0h4" stroke="#d3b9e1"/>',
    "favorite": '<path d="m16 5 3.5 7 8 1-5.8 5.5 1.5 8-7.2-3.8-7.2 3.8 1.5-8L4.5 13l8-1Z" fill="#b3914e" stroke="#f4da97"/>',
    "edit": '<path d="M7 24 20 8l4 4-13 15-6 1Z" fill="#648a9a" stroke="#c5dce3"/><path d="m18 10 4 4" stroke="#f3d28f"/>',
    "rename": '<path d="M7 24h18M8 19l11-12 5 5-11 12-6 2Z" fill="#6f8094" stroke="#e3d19e"/>',
    "follow": '<path d="M4 17q7-12 16-7l4-4v9l-5-3Q12 9 4 20Z" fill="#4d8993" stroke="#b9dce0"/><circle cx="24" cy="6" r="2" fill="#f1d58a"/>',
    "possess": '<circle cx="16" cy="11" r="5" fill="#9779a4" stroke="#e5c8ed"/><path d="M7 26q1-9 9-9t9 9M16 4v3m-4-1 2 2m6-2-2 2" stroke="#e5c8ed"/>',
    "bless": '<path d="M16 4v20M7 14h18" stroke="#f0d68e"/><path d="m8 8 2 2m14-2-2 2M8 23l2-2m14 2-2-2" stroke="#a6d5ad"/>',
    "curse": '<path d="m9 7 7 18 7-18M6 20l6-6m8 0 6 6" stroke="#d28aaf"/><circle cx="16" cy="25" r="2" fill="#d28aaf"/>',
    "kill": '<path d="M7 25 23 7m-1-2 5 5m-16 5 6 6M5 27l5-2" stroke="#eca28e"/>',
    "apply": '<path d="m5 17 7 7L27 8" stroke="#a9daa9"/>',
    "pin": '<path d="M10 6h12l-2 8 4 4H8l4-4ZM16 18v10" fill="#7c8997" stroke="#d4dce2"/>',
    "close": '<path d="M8 8 24 24M24 8 8 24" stroke="#edaa9c"/>',
    "prev": '<path d="m20 7-9 9 9 9" stroke="#e5ce91"/>',
    "next": '<path d="m12 7 9 9-9 9" stroke="#e5ce91"/>',
    "accept": '<circle cx="16" cy="16" r="11" fill="#4a8060" stroke="#acd8ab"/><path d="m9 16 5 5 9-10" stroke="#e9f3cb"/>',
    "reject": '<circle cx="16" cy="16" r="11" fill="#925654" stroke="#e9aba0"/><path d="m11 11 10 10m0-10L11 21" stroke="#ffe2d4"/>',
    "go_to": '<path d="m16 4 10 23-10-5-10 5Z" fill="#528c91" stroke="#c5e0da"/><path d="M16 8v14" stroke="#f1d48f"/>',
    "freeze": '<circle cx="16" cy="16" r="10" fill="#42758e" stroke="#b8dbe5"/><path d="M16 7v18M7 16h18m-15-6 12 12m0-12L10 22" stroke="#e5f2f0"/>',
    "overlay_off": '<path d="m16 5 12 7-12 7L4 12Zm-12 13 12 7 12-7" fill="#54758b" stroke="#bad7e2"/><path d="M5 27 27 5" stroke="#ec9f90"/>',
    "skip": '<path d="m5 7 10 9L5 25Zm12 0 10 9-10 9Z" fill="#7496a7" stroke="#c8dbe0"/>',
    "connection_save": '<path d="M6 7h19v18H6Z" fill="#526d83" stroke="#c0d7dd"/><path d="M10 12h12m-12 5h12m-12 5h7" stroke="#f0d48c"/><circle cx="25" cy="23" r="3" fill="#8ac4a1"/>',
    "connection_test": '<circle cx="10" cy="16" r="4" fill="#5f91a2" stroke="#c2dce2"/><circle cx="23" cy="9" r="3" fill="#5f91a2" stroke="#c2dce2"/><circle cx="23" cy="24" r="3" fill="#5f91a2" stroke="#c2dce2"/><path d="m13 14 7-4m-7 8 7 5" stroke="#ebd18f"/>',
}

BASE = '''<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
<circle cx="16" cy="16" r="15" fill="#1b2932" stroke="#60727c" stroke-width="1"/>
<g fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">{glyph}</g></svg>'''


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    RUNTIME.mkdir(parents=True, exist_ok=True)
    for name, glyph in GLYPHS.items():
        svg = BASE.format(glyph=glyph)
        (OUT / f"{name}.svg").write_text(svg, encoding="utf-8")
        png = cairosvg.svg2png(bytestring=svg.encode(), output_width=64, output_height=64)
        (OUT / f"{name}.png").write_bytes(png)
        (RUNTIME / f"{name}.png").write_bytes(png)
    print(f"Generated {len(GLYPHS)} menu icons and runtime copies")


if __name__ == "__main__":
    main()
