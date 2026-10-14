# Village graphics — 2026-09-27

Original handcrafted pixel-art models. The house features matching construction and ruin states; these three states are shared by human houses and huts so the models appear in early settlements. The human village hall (`hall_1`), well and windmill have distinctive completed models. Other styles and the remaining building/state combinations retain existing artwork.

| File prefix (under buildings) | States with final art |
|---|---|
| human_house, human_hut | construction, complete, ruin |
| human_hall_1, human_well, human_windmill | complete |

Files end in `_<state>_color.png`.

All 24 `kind: tree` entries now have distinct transparent sprites in `features/`, including acacia, palm and the special mushroom/crystal/void objects; `rock_color.png` remains a separate environment sprite. `SpriteLibrary` point-samples each to at most 15 pixels; other non-tree features retain their previous art. Run `python Tools/audit_tree_art.py` to check coverage and alpha.

Runtime files: `buildings/human_house_complete_color.png` and `buildings/human_hut_complete_color.png`. Source alpha is retained. `SpriteLibrary` samples these full-resolution source images to at most 64 pixels on their longest side with point sampling, before atlas packing. Their world width still comes from the building footprint. The `_color.png` suffix explicitly opts into original colors; ordinary gray-index art continues to use the kingdom/style palette. Era and burning tints apply to both.

Play in Unity after asset import, or use **PixelGenesis > Preview House Model** outside Play Mode. This editor command opens a fresh unsaved world, pauses it, and shows the house, hut, construction and ruin together. It writes `Logs/house-in-game.png` and `Logs/house-closeup.png`. Stop Play Mode to leave the preview. It never loads or writes a save slot.

**PixelGenesis > Preview Village Graphics** shows all of the above in a paused village fixture with a fixed seed, paths, fields and people for scale. It writes `Logs/village-in-game.png` and `Logs/village-closeup.png` from the actual URP world camera (without the HUD). The fixture layout is only an editor preview; normal worlds build the same structures through the existing simulation. Building ground shadows use one batched mesh, 25 vertices per live building, updated only when the building version changes, below vegetation and buildings.

**PixelGenesis > Preview All Building Styles** shows six styles and five building types together and writes `Logs/all-styles-in-game.png` and `Logs/all-styles-closeup.png`. Ordinary gray-index art now decodes correctly in the project's Linear color space. During rendering, a building atlas image is cut into the game's 3×3-tile construction cells and each new full-color world feature into at most one-tile-wide UV pieces. The source PNG remains editable as one image; the map receives the separate UV-mapped mesh pieces. This keeps all building states, owner-color palettes and gameplay footprints intact.

**PixelGenesis > Preview All Trees** shows the 24 tree-kind content entries in a fixed-seed, paused, unsaved world and writes `Logs/all-trees-in-game.png` and `Logs/all-trees-closeup.png` from the actual game camera.

**PixelGenesis > Preview Savanna Trees** compares acacias and palms against oak and pine in the same paused world and writes `Logs/savanna-trees-in-game.png` and `Logs/savanna-trees-closeup.png`. Stop Play Mode to leave either preview. An already running world needs a Play Mode restart to reload the cached feature atlas after artwork changes.
