## Description
Please include a summary of the changes and the related issue/feature. Mention any architectural decisions or relevant context.

Fixes #(issue)

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Content addition (changes to `Data/*.json` or art assets)
- [ ] Performance improvement (refactoring, job optimization)
- [ ] Documentation update
- [ ] Breaking change (fix or feature that would cause existing save games or mods to break)

## Architectural & Quality Checklist
- [ ] My code adheres to the layer dependencies (`Core` -> `Content` -> `World` -> `Sim` -> `Powers` -> `UI`).
- [ ] No heap allocations (GC = 0) are made during the simulation tick (`Update` / `Tick`).
- [ ] All pseudo-randomness is derived strictly through `SimRandom` (no `UnityEngine.Random` in sim).
- [ ] UI components do not mutate simulation state directly (commands are used).
- [ ] I have added EditMode tests covering the new code / bugfix in `Assets/_Project/Tests/EditMode/`.
- [ ] All EditMode tests pass.
- [ ] If changing content, `python Data/_generator/validate.py` passed with 0 errors.
- [ ] Code follows `.editorconfig` styling rules.
- [ ] No compiler warnings were introduced.
