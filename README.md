# PixelGenesis

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Unity 6.3 LTS](https://img.shields.io/badge/Unity-6000.3.25f1%20LTS-black?logo=unity)](https://unity.com/)
[![Render Pipeline: URP 2D](https://img.shields.io/badge/Render%20Pipeline-URP%202D-informational)](https://unity.com/)
[![Architecture: DOD / Burst](https://img.shields.io/badge/Architecture-DOD%20%2F%20Jobs%20%2F%20Burst-orange)](https://docs.unity3d.com/Packages/com.unity.burst@latest)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![Play Online](https://img.shields.io/badge/Play-pixelgenesis.web.tr-e07a8a)](https://pixelgenesis.web.tr/)

🎮 **Play / Official website: [pixelgenesis.web.tr](https://pixelgenesis.web.tr/)**

![PixelGenesis Gameplay](.github/assets/gameplay-preview.png)

**PixelGenesis** is an open-source, high-performance 2D pixel-art sandbox god simulator built with **Unity 6.3 LTS (C#)**.

Sculpt continents, manipulate climates, unleash divine miracles and cataclysms, and observe autonomous mortal civilizations as they build cities, cultivate crops, forge religions, draft laws, research technologies, form alliances, and wage wars in a deterministic, living simulated world.

---

## 🌟 Key Features

### 🌍 Dynamic Procedural World
- **Tile-Level Simulation**: Multi-layer cellular world simulation supporting terrain elevation, ground fertility, humidity, temperature, and dynamic water flow.
- **Natural Disasters & Meteorology**: Procedural clouds, rainfall, lightning strikes, tornadoes, earthquakes, forest fires with realistic propagation, and lava dynamics.
- **Biomes & Flora**: Diverse biomes (Temperate Grasslands, Deserts, Tundras, Rainforests, Volcanic Crags, Corrupted Lands) hosting unique vegetation and resources.

### 👑 Emergent Civilizations & Warfare
- **Autonomous Species**: Humans, Elves, Dwarves, Orcs, Beastfolk, and Insectoids—each featuring distinctive cultural archetypes and architectural designs.
- **City Growth & Economy**: Granaries, farms, timber yards, mines, barracks, watchtowers, temples, docks, and trade ships.
- **Politics & Meta-History**: Dynamic dynasties, succession crises, kingdoms, culture traits, languages, religious pantheons, plots, and procedural historical chronicles.

### 🧬 Ecology, Genetics & Metamorphosis
- **Deep Biology System**: Sub-species variation, genetic inheritance, phenotype palette swapping, mutation, and creature evolution.
- **Ecosystem Dynamics**: Predator-prey loops, grazing behaviors, pack mentalities, diseases, and necromantic zombie outbreaks.

### ✨ God Powers & Sandbox Tools
- **Terraforming & Elemental Brushes**: Sculpt mountains, hollow oceans, sow forests, seed minerals, and alter biomes in real time.
- **Divine Interventions & Cataclysms**: Meteor strikes, divine shields, blessings, curses, temporal acceleration, and entity possession.

### ⚡ Extreme Performance & Determinism
- **Data-Oriented Design (DOD)**: Core simulation data stored in Native Arrays and Structure-of-Arrays (SoA) layout.
- **Unity Job System & Burst Compiler**: Multi-threaded parallel processing capable of simulating 5,000+ individual units at 60 FPS.
- **Zero-Allocation Tick**: GC-free runtime tick design ensuring zero Garbage Collection stutter during gameplay.
- **Deterministic RNG**: Pseudo-random number generation driven strictly by isolated `SimRandom` seed streams.

---

## 🏗️ Architecture & Layering

PixelGenesis follows a strict one-way architectural dependency hierarchy:

```
Core ──► Content ──► World ──► (WorldGen / Sim) ──► Powers ──► UI
  ▲                    ▲              ▲               ▲        ▲
  └────────────────────┴──────────────┴───────────────┴────────┤
                   Boot (Composition & Orchestration)
```

- **`PG.Core`**: Foundational mathematics, pseudo-random generators (`SimRandom`), fast data structures, IDs, and shared utilities.
- **`PG.Content`**: Data models, JSON content definitions, and catalog lookups.
- **`PG.World`**: World grid, tilemaps, chunks, regions, and spatial indexing.
- **`PG.WorldGen`**: Procedural world generator algorithms and tectonic templates.
- **`PG.Sim`**: Pure simulation logic (ecosystems, entities, cities, economies, cultures, pathfinding).
- **`PG.Powers`**: Player interactions, god powers, cataclysms, and command queues.
- **`PG.Render`**: Read-only rendering, URP 2D sprite batching, and palette swap shaders.
- **`PG.Persistence`**: Fast binary world serialization and save/load pipelines.
- **`PG.UI`**: UI Toolkit (UIElements) HUDs, inspector panels, overlays, and modal dialogues.
- **`PG.Boot`**: Composition root, startup coordinator, and dependency injection.

---

## 📁 Repository Structure

```
PixelGenesis/
├── Art/                  # Source art, concepts, sprite sheets, building & unit data
├── Assets/               # Unity project root
│   ├── Plugins/          # Native plugins and external dependencies
│   ├── Resources/        # Dynamic Unity resources
│   ├── Scenes/           # Game scene (GameBootstrap)
│   ├── Settings/         # URP settings, input profiles, render assets
│   ├── StreamingAssets/  # Runtime data mirrors (synced via DataSync)
│   └── _Project/         # Main C# source code organized by assembly
│       ├── Boot/         # Composition root
│       ├── Content/      # Content loaders & models
│       ├── Core/         # Mathematics, PRNG, data structures
│       ├── Editor/       # Custom editor tools (DataSync, inspectors)
│       ├── Persistence/  # Save/load system
│       ├── Powers/       # God powers & commands
│       ├── Render/       # Shaders, camera, sprite renderers
│       ├── Sim/          # Systems (Units, Civ, World, Combat, AI)
│       ├── Tests/        # EditMode unit & integration tests
│       ├── UI/           # UI Toolkit documents, styles, controllers
│       ├── World/        # Tiles, chunks, regions, maps
│       └── WorldGen/     # Procedural map generation
├── Audio/                # Ambient tracks, SFX, UI sound effects
├── Data/                 # Single Source of Truth for game balance & definitions
│   ├── _generator/       # Python generator scripts (validate.py, catalog.py)
│   ├── _schemas/         # JSON Schemas for validation
│   └── *.json            # Game balance files (biomes, species, powers, etc.)
├── Packages/             # Unity Package Manager manifest & lockfile
├── ProjectSettings/      # Unity project configurations & version info
├── Tests/                # Test fixtures and save migration snapshots
├── Tools/                # Automation, soak testing, and art pipeline scripts
├── CHANGELOG.md          # Version changelog
├── CONTRIBUTING.md       # Contribution guidelines
├── CREDITS.md            # Third-party licenses & acknowledgments
├── DECISIONS.md          # Architectural decision records (ADRs)
└── LICENSE               # MIT License
```

---

## 🚀 Getting Started

### Prerequisites
1. **Unity Editor**: `Unity 6000.3.25f1` (Unity 6.3 LTS) or compatible Unity 6 release.
2. **Git & Git LFS**: Ensure [Git LFS](https://git-lfs.github.com/) is installed before cloning (`git lfs install`).
3. **IDE**: [JetBrains Rider](https://www.jetbrains.com/rider/) (recommended) or [Visual Studio Code](https://code.visualstudio.com/) with C# Dev Kit.
4. **Python** (Optional, for content tools): Python 3.10+ to run data validators and generation scripts.

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/<your-username>/PixelGenesis.git
   cd PixelGenesis
   git lfs pull
   ```

2. **Open with Unity Hub:**
   - Launch **Unity Hub**.
   - Click **Add → Add project from disk**.
   - Select the `PixelGenesis` folder.
   - Ensure the editor version is set to **6000.3.25f1** (Unity 6 LTS).

3. **Synchronize Game Data:**
   - On first open, Unity compiles the scripts and initializes the project.
   - Game content in `Data/` is mirrored into `Assets/StreamingAssets/Data/` automatically when entering Play Mode, or manually via:
     `Unity Menu Bar -> PixelGenesis -> Sync Data`.

4. **Run the Game:**
   - Open `Assets/Scenes/GameBootstrap.unity`.
   - Press **Play** in the Unity Editor!

---

## 🧪 Testing & Validation

### Running C# Unit Tests
EditMode tests verify determinism, simulation rules, economy loops, save migration, and data integrity:
- Open Unity: **Window → General → Test Runner**.
- Select **EditMode** and click **Run All**.
- Or via command line (batch mode):
  ```bash
  Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
  ```

### Validating Game Data (Python)
Validate all JSON content files against their JSON Schemas:
```bash
python Data/_generator/validate.py
```

Generate updated content catalog documentation:
```bash
python Data/_generator/catalog.py
```

---

## 🛠️ Modding & Data Extensions

PixelGenesis is 100% data-driven:
- Content definitions (biomes, species, buildings, status effects, traits, powers, etc.) reside in `Data/*.json`.
- Modders can create modular packs located in `Examples/Mods/` with custom JSON content and localization files without recompiling the C# engine.

---

## 🤝 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code standards, architecture requirements (zero GC in tick, layer constraints), and pull request workflow.

---

## 📜 License & Credits

- **Code & Engine**: Distributed under the [MIT License](LICENSE). Copyright (c) 2026 NovaLoom.
- **Third-Party Libraries & Dependencies**: See [CREDITS.md](CREDITS.md) for license details regarding third-party packages (Newtonsoft Json.NET, Firebase JS SDK, Open Font License typefaces, Unity Companion License).
