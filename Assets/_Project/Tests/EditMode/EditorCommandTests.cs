using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.Powers;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class EditorCommandTests
    {
        ContentDB _db;
        SimWorld _sim;
        EditCommandQueue _queue;
        int _unit;
        [OneTimeSetUp] public void Content() => _db = ContentDB.LoadAll(Path.Combine(Application.dataPath, "../Data"), null);
        [SetUp] public void Setup()
        {
            var settings = new WorldGenSettings { Seed = 91, Size = MapSizePreset.Tiny };
            _sim = new SimWorld(_db, WorldGenerator.Generate(settings, _db), new SimRandomProvider(91));
            _queue = new EditCommandQueue(); EditSystems.Install(_sim, _queue);
            var rng = SimRandom.Derive(91, 123, 0);
            _unit = _sim.Units.Store.Spawn(new UnitSpawnRequest { Species = _db.Species.IdOf("sp.human"), AgeYears = 20, Pos = new float2(32, 32), Subspecies = -1, Sex = -1 }, 0, ref rng).Index;
        }
        [TearDown] public void Cleanup() => _sim?.Dispose();

        [Test] public void TraitCommandAppliesOnlyWhenQueueRunsAndAddsDivineMark()
        {
            int trait = _db.UnitTraits.IdOf("tr.strong");
            var traits = _sim.Units.Store.Traits[_unit]; traits.Set(trait, false); _sim.Units.Store.Traits[_unit] = traits;
            _queue.Edits.Add(new EditCommand { Kind = EditKind.AddUnitTrait, Target = _unit, A = trait });
            Assert.That(_sim.Units.Store.Traits[_unit].Has(trait), Is.False);
            _sim.ApplyInputWhilePaused();
            Assert.That(_sim.Units.Store.Traits[_unit].Has(trait), Is.True);
            Assert.That(_sim.Units.Store.Traits[_unit].Has(_db.UnitTraits.IdOf("tr.divine_touch")), Is.True);
            Assert.That(_sim.Clock.Tick, Is.Zero);
        }

        [Test] public void StaleUnitCommandCannotKillReplacement()
        {
            _queue.Edits.Add(new EditCommand { Kind = EditKind.Kill, Target = _unit, ExpectedUid = _sim.Units.Store.Uid[_unit] + 100 });
            _sim.ApplyInputWhilePaused();
            Assert.That(_sim.Units.Store.IsAlive(_unit), Is.True);
        }

        [Test] public void InvalidEditorInputsAreIgnored()
        {
            _queue.Edits.Add(new EditCommand { Kind = EditKind.AddUnitTrait, Target = _unit, A = int.MaxValue });
            _queue.Edits.Add(new EditCommand { Kind = EditKind.CreateItem, Target = _unit, A = 0, B = int.MaxValue });
            _queue.Laws.Add(new LawCommand { Law = int.MaxValue, On = true });
            Assert.DoesNotThrow(() => _sim.ApplyInputWhilePaused());
            Assert.That(_sim.Civ.Items.Length, Is.Zero);
        }

        [Test] public void PossessionReleaseRestoresAiWithoutAdvancingPausedWorld()
        {
            _queue.Control = new ControlCommand { Unit = _unit, Spell = -1 };
            _sim.ApplyInputWhilePaused();
            Assert.That(_sim.Units.Store.Has(_unit, UnitFlags.PlayerControlled), Is.True);
            _queue.Edits.Add(new EditCommand { Kind = EditKind.ReleaseControl, Target = _unit });
            _sim.ApplyInputWhilePaused();
            Assert.That(_sim.Units.Store.Has(_unit, UnitFlags.PlayerControlled), Is.False);
            Assert.That(_sim.Clock.Tick, Is.Zero);
        }

        [Test] public void MeleeCannotHitRemoteUnitNearCursor()
        {
            var rng = SimRandom.Derive(91, 222, 0);
            int target = _sim.Units.Store.Spawn(new UnitSpawnRequest { Species = _db.Species.IdOf("sp.human"), AgeYears = 20, Pos = new float2(85, 85), Subspecies = -1, Sex = -1 }, 0, ref rng).Index;
            float hp = _sim.Units.Store.Hp[target];
            _queue.Control = new ControlCommand { Unit = _unit, Attack = true, AimAt = new float2(85, 85), Spell = -1 };
            _sim.ApplyInputWhilePaused();
            Assert.That(_sim.Units.Store.Hp[target], Is.EqualTo(hp));
        }
    }
}
