# Contributing to PixelGenesis

Thank you for your interest in contributing to **PixelGenesis**! 

PixelGenesis is an open-source sandbox simulation game designed for high performance, determinism, and deep emergent gameplay. To maintain stability, maintainability, and peak performance across large worlds, please follow these guidelines when contributing.

---

## 🏛️ Golden Architectural Rules

Before submitting any code, make sure your changes strictly adhere to these architectural laws:

### 1. Strict Layer Dependencies
The codebase is structured into isolated Assembly Definitions. Dependencies flow strictly one way:
```
Core ──► Content ──► World ──► (WorldGen / Sim) ──► Powers ──► UI
```
- **Rule**: Lower layers must NEVER reference upper layers.
- `PG.Render` and `PG.Persistence` are read-only observers of simulation state.
- `PG.Boot` orchestrates and wires dependencies together.

### 2. Zero Heap Allocations in Simulation Ticks (GC = 0)
- The simulation runs at a high tick rate (up to 20+ ticks/sec) and handles 5,000+ entities.
- **Rule**: Absolutely no heap allocations (`new`, `LINQ`, boxing, string formatting/concatenation, closure/lambda allocations) are permitted during the simulation tick.
- Simulation arrays use `NativeArray<T>`, `NativeList<T>`, or fixed-size unmanaged structs in Structure-of-Arrays (SoA) layout.

### 3. Determinism & Pseudo-Randomness
- Every run with the same seed must produce the exact same simulation outcome.
- **Rule**: Never use `UnityEngine.Random`, `System.Random`, or `Time.time` inside simulation code (`PG.Sim`, `PG.WorldGen`, `PG.Core`).
- Always pass and derive random numbers from isolated `SimRandom` generator streams.

### 4. Command Queue for UI Interactions
- **Rule**: UI components must NEVER directly alter simulation data.
- User input and UI buttons must enqueue commands (`PowerCommand`, `EditCommand`, `LawCommand`, `MetaCommand`) which are dispatched and executed deterministically at the start of the next simulation tick.

### 5. Content is Data-Driven
- **Rule**: Never hardcode gameplay constants (unit stats, costs, biome temperature limits, traits) directly into C# classes.
- All game definitions belong in `Data/*.json` backed by schemas in `Data/_schemas/`.
- If modifying or adding content definitions, edit the generator scripts in `Data/_generator/` and verify using:
  ```bash
  python Data/_generator/validate.py
  python Data/_generator/catalog.py
  ```

---

## 🧪 Testing Standards

- **EditMode Tests Required**: Any new system, mechanic, or bugfix must be accompanied by corresponding EditMode unit tests in `Assets/_Project/Tests/EditMode/`.
- **Determinism Check**: Ensure that determinism verification tests continue to pass with a green result.
- **Clean Compilation**: No new compiler warnings should be introduced.

---

## 💻 Development Workflow

1. **Fork & Branch**:
   - Fork the repository to your GitHub account.
   - Create a clean branch from `main`:
     - `feature/description` for new features
     - `fix/description` for bug fixes
2. **Setup**:
   - Clone with Git LFS:
     ```bash
     git clone https://github.com/<your-username>/PixelGenesis.git
     cd PixelGenesis
     git lfs pull
     ```
   - Open the project in **Unity 6.3 LTS (6000.3.25f1)**.
3. **Coding Standards**:
   - Follow the included `.editorconfig` rules:
     - 4-space indentation, LF line endings.
     - PascalCase for classes, structs, public methods, and public properties.
     - `_camelCase` for private fields.
     - English code comments and identifiers.
     - Player-facing text must use Localization keys.
4. **Validation**:
   - Run tests inside Unity Test Runner (EditMode).
   - Run `python Data/_generator/validate.py` to verify data schemas.
5. **Pull Request**:
   - Commit using clear, descriptive commit messages (Conventional Commits preferred: `feat:`, `fix:`, `perf:`, `refactor:`, `docs:`).
   - Submit your PR against the `main` branch.
   - Describe what problem is solved, which systems are affected, and include test verification results.

Thank you for helping make PixelGenesis better!
