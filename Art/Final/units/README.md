# Four peoples — authored unit sheets

## Wild animals

Sheep, wolf, bear, and deer sheets were authored as original 4×6 pose sheets. The sources contain transparent gutters and 24 separate alpha-connected poses each. `Tools/prepare_unit_sheets.py sheep wolf bear deer` builds per-pose masks and targets standing heights of 18/19/23/23 pixels in the shared 40×40 frame. These sheets use the existing shader's full-color branch so fleece, fur and antlers retain the source colors. Normal zoom in Unity is still pending verification.

Dragon, slime, skeleton, and fairy form the first fantasy group. Slime has four small detached death droplets; its importer intentionally retains the 24 largest connected bodies. Those droplets are cosmetic and do not enter the game atlas. Normal zoom and event timing still need Unity verification.

The next eight fantasy threats are zombie, ember imp, sandworm, walking tree, ghost, fire elemental, water elemental, and earth golem. All eight have 24 isolated primary poses. Water elemental has one detached splash large enough to pass the component-area threshold; as with slime, only its 24 largest connected bodies enter the game atlas. Source colors are retained through the shader's full-color route. `Docs/previews/animals.html` shows the two threat subgroups. No third-party sprite sheets are used.

The other-monsters group starts with kraken, rot crawler, candy golem and gummy bear. All four have 24 connected primary poses and are shown in the preview under “Diğer canavarlar A”.

Human was the reference design for the other three peoples.

The other-monsters B/C groups add shroomling, crystal beetle, ash crawler, bone crawler, clock crab, void moth, flesh mound, and devourer. All eight have 24 isolated primary poses. Some sheets include small detached spore, crystal, spark or splash fragments; the importer retains the 24 large character bodies and excludes the tiny components from the runtime atlas. They appear in `Docs/previews/animals.html` at normalized game sprite scale.

Undead/magic variants begin with zombie runner, brute, bloater and crawler. These have distinct lean, muscular, round and ground-hugging silhouettes; the 24-frame sheets use the same color-preserving atlas route. `Docs/previews/animals.html` displays them as “Zombi varyantları A”.

The next undead/magic batch adds zombie beast, zombie dragon, zombie lord and necromancer. Their quadruped, winged, crowned and hooded-staff silhouettes remain distinct in the normalized 40×40 preview. As with prior sheets, detached spell fragments are omitted from the main character mask.

Bloodsucker, sky whale calf, dark mage and white mage form the next four. Their bat, flying whale, violet caster and golden healer profiles use full-color 24-frame sheets and are previewed under “Büyü ve gök canlıları”. Detached light particles are excluded from the primary body mask.

Druid, snowman and storm spirit complete the undead/magic/elemental group. All three appear in the “Doğa ve hava büyüsü” preview group.

The settlement/forest group begins with cat, dog, rabbit and cow. The four species are normalized to different standing heights and previewed together under “Evcil ve orman A”.

Chicken, rat, monkey and fox form the second settlement/forest batch. All four use isolated 24-frame masks and appear under “Evcil ve orman B” in the preview.

Boar, goat, wild horse and reindeer finish the settlement/forest group. The four final sources are masked and shown under “Evcil ve orman C”.

Penguin, crab, scorpion and lizard begin the wetland/desert/polar group. Each source has 24 primary body poses and is imported into the shared 40×40 frame. Two detached crab effect fragments are excluded from its atlas; the connected body remains in each pose. They appear under “Sulak, çöl, kutup A” in the browser preview.

Crocodile, snake, turtle and seal continue that group. All four retain 24 connected main-body poses and appear under “Sulak, çöl, kutup B”. Crocodile is width-limited in the 40×40 cell, with an effective standing height below its nominal target.

Hyena, lion, rhino and buffalo are the next wetland/desert/polar group. Their four distinct silhouettes are normalized together in “Sulak, çöl, kutup C”.

Camel, polar bear and salamander finish the 15-species wetland/desert/polar group. All three have 24 isolated poses and appear in “Sulak, çöl, kutup D”.

Frog, bee, ant and ordinary beetle start the final small/flying/water/special group. These distinct compact silhouettes have 24 poses each and appear under “Küçük canlılar A”.

Owl, crow, eagle and parrot follow as the bird subgroup. Their walking, attack and flying rows use the same 4×6 atlas convention and are compared in the “Kuşlar” preview group.

Snail, common fish, butterfly and caterpillar follow in “Küçük canlılar B”. The fish source is 1448×1086 rather than the other sheets' 1024×1536; connected-pose extraction still yields the same 24 valid frames.

Piranha, mosquito swarm, sky visitor and mechanical colossus form “Özel canlılar A”. The swarm intentionally has disconnected insects, so its importer crops the authored 4×6 grid cells as composite poses and retains all opaque insect pixels within each cell. The other three use connected-body extraction.

Bandit and cultist finish the 92-species inventory (four peoples plus 88 other species). Their authored attack rows show a hand-held knife and ritual staff respectively; the browser preview was checked at idle and attack poses under “Özel canlılar B”.

## Art direction and Animation Layout

Base specification: top-down three-quarter fantasy pixel-art character animation sheet, facing right. Transparent background, no text, labels, grid, scenery or ground shadow. Exactly four columns and six rows on a 1024×1536 canvas. Crisp pixel-art clusters, restrained highlights, strong readable silhouette. Monochrome grayscale indexed-art source; target values 28, 56, 84, 112, 140, 168, 196, 224; darkest outline, body shadows/midtones/highlights, clothing shadow/main in slots 5/6, accent/shine in 7/8. Consistent anatomy, scale and feet baseline. Every cell must contain one complete pose with space for weapons and no neighboring pose contact.

Rows: four subtle idle poses; four walking poses with alternating planted feet and arm swing; attack anticipation/contact/recovery/rest; two swimming poses repeated; sleeping pose repeated; three non-graphic falling/death poses and final pose repeated. Runtime currently uses frame counts 2,4,3,2,1,3. Death art is prepared, not yet drawn for dead units by UnitRenderer.

Human: compact balanced adult proportions, swept short dark hair, readable face, belted tunic with rolled sleeves, trousers and boots, small shoulder satchel. Short blade during attack. Keep the character unmistakably human and the clothing readable at game scale.

Orc variation, using human sheet as layout/style reference: heavy muscular broad-shouldered adult, strong arms, readable tusks, topknot, leather harness and waist cloth; heavy stone axe for attack. Maintain the reference perspective, palette semantics, layout and animation rows; original character design.

Dwarf variation, using human sheet as layout/style reference: visibly short and broad adult, large braided beard, small cap, work apron, thick boots and compact limbs; heavy hammer for attack. Preserve reference art style, monochrome palette and identical animation layout, while keeping dwarf proportions distinct.

Elf variation, using human sheet as layout/style reference: slender adult, long pointed ears, long swept hair, leaf-shaped split cloak/tunic and narrow boots; slender blade for attack. Preserve the reference perspective, palette and animation layout, with a tall graceful silhouette.

## Integration

`Tools/prepare_unit_sheets.py` identifies 24 alpha components and writes exact source rectangles to sibling JSON files. It never alters the PNGs. `DetailedUnitSheet.Build` normalizes each rectangle into a 40×40 runtime cell with nearest sampling, alpha threshold and the game's eight exact gray indices. It retains one common scale per species and a baseline of 36. Canvas world size is 2 tiles; visible standing height varies by race.

Each frame also carries a base64 component mask at its sampled width/height. This excludes neighboring limbs or weapons whose bounding rectangles overlap the current pose. Mask dimensions are validated by the runtime importer; the same mask is used in the browser preview and numeric audit.

`DataSync` mirrors PNGs and JSON into StreamingAssets and includes them in its manifest. Source originals are not browser-ready game binaries; the C# loader requires a new Unity build. The old placeholder sheets remain the fallback.

Preview: `Docs/previews/units.html`. Numeric audit: `python Tools/audit_unit_art.py`. Runtime test source: `DetailedUnitArtTests.cs`.

## Female counterparts

Four original female counterpart sheets are authored for the peoples, each using the corresponding male sheet as the style and layout reference. Art direction: fully clothed adult fantasy character, recognizable race silhouette, practical tunic/trousers/boots or apron, distinct hair, the same grayscale palette and 4-column/6-row animation layout. Empty hands allow the runtime equipment layer to provide the selected tool. Original PNG outputs are retained unchanged; metadata masks normalize the poses for runtime use.

The importer now selects the female atlas variant for Sex=0, with the original sheet as fallback. Female attack equipment uses the hand-anchored procedural motion over the unarmed movement pose; the authored female attack row is prepared but not selected as a separate equipped attack animation. Preview: `Docs/previews/families.html`; audit covers all eight sheets and 192 poses. See `Docs/Family_And_Grip_2026-10-05.md` for runtime validation limits.
