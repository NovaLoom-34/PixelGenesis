using PG.Content;
using PG.Powers;
using PG.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.4 unit inspector: General, Stats, Traits, Equipment, Family, Log, Brain.
    public sealed class UnitWindow : WindowBase
    {
        readonly UiContext _ui;
        readonly int _unit;
        readonly long _uid;
        string _deadNote;

        public UnitWindow(UiContext ui, int unit) : base(ui.Host, "")
        {
            _ui = ui;
            _unit = unit;
            _uid = ui.Sim.Units.Store.Uid[unit];
            SetTabs("ui.tab_general", "ui.tab_stats", "ui.tab_traits", "ui.tab_equipment", "ui.tab_family", "ui.tab_log", "ui.tab_brain");
        }

        bool Alive
        {
            get
            {
                var u = Host.Sim.Units.Store;
                return _unit < u.HighWater && u.State[_unit] == UnitStore.StateAlive && u.Uid[_unit] == _uid;
            }
        }

        protected override void Refresh()
        {
            if (!Alive)
            {
                _deadNote ??= Loc.F("ui.unit_dead_fmt", Host.Sim.Clock.Year + 1);
                SetTitle(_deadNote); Body.SetEnabled(false); return;
            }
            base.Refresh();
        }

        protected override void Build()
        {
            var sim = Host.Sim;
            if (!Alive)
            {
                _deadNote ??= Loc.F("ui.unit_dead_fmt", sim.Clock.Year + 1);
                SetTitle(_deadNote);
                Line(Loc.T("ui.unit_gone"));
                return;
            }
            var u = sim.Units.Store;
            int i = _unit;
            SetTitle(Names.Unit(sim, i));
            switch (Tab)
            {
                case 0: General(sim, u, i); break;
                case 1: Stats(u, i); break;
                case 2: Traits(sim, u, i); break;
                case 3: Equipment(sim, u, i); break;
                case 4: Family(sim, u, i); break;
                case 5: Log(sim, u, i); break;
                case 6: Brain(sim, u, i); break;
            }
        }

        void General(SimWorld sim, UnitStore u, int i)
        {
            var db = sim.Content;
            var sp = db.Species[u.Species[i]];
            Line(Loc.F("ui.unit_species_fmt", Loc.Name(sp), Names.Meta(sim, MetaKind.Subspecies, u.Subspecies[i])));
            Line(Loc.F("ui.unit_age_fmt", (int)u.AgeYears(i, sim.Clock.Tick), Loc.T("ui.age." + ((AgeStage)u.Age[i]).ToString().ToLowerInvariant()), u.Level[i], u.Xp[i]));
            Line(Loc.F("ui.unit_sex_fmt", Loc.T(u.Sex[i] == 0 ? "ui.sex_female" : u.Sex[i] == 1 ? "ui.sex_male" : "ui.sex_none")));
            Bar(Loc.T("ui.hp"), u.Hp[i] / Mathf.Max(1f, u.Stat(i, StatId.Hp)), UiTheme.BarHp);
            Bar(Loc.T("ui.mana"), u.Mana[i] / Mathf.Max(1f, u.Stat(i, StatId.Mana)), UiTheme.BarMana);
            Bar(Loc.T("ui.stamina"), u.Stamina[i] / Mathf.Max(1f, u.Stat(i, StatId.Stamina)), UiTheme.BarStamina);
            Bar(Loc.T("ui.saturation"), u.Saturation[i] / 100f, UiTheme.BarSaturation);
            Bar(Loc.T("ui.energy"), u.Energy[i] / 100f, UiTheme.BarEnergy);
            Line(Loc.F("ui.happiness_fmt", u.Happiness[i]));
            var civ = sim.Civ;
            int job = u.Job[i] - 1;
            if (job >= 0) Line(Loc.F("ui.job_fmt", Loc.Name(db.Jobs[job])));
            Line(Loc.F("ui.kills_fmt", u.Kills[i]));
            int d = u.Disease[i] - 1;
            if (d >= 0) Line(Loc.F("ui.disease_fmt", Loc.Name(db.Diseases[d])));
            int former = u.FormerSpecies[i] - 1;
            if (former >= 0) Line(Loc.F("ui.former_fmt", Loc.Name(db.Species[former])));
            var links = Row();
            int city = u.City[i];
            Link(links, "ui.city", MetaKind.City, city, sim);
            Link(links, "ui.kingdom", MetaKind.Kingdom, civ != null ? civ.KingdomOf(city) : -1, sim);
            Link(links, "ui.culture", MetaKind.Culture, u.Culture[i], sim);
            Link(links, "ui.language", MetaKind.Language, u.Language[i], sim);
            Link(links, "ui.religion", MetaKind.Religion, u.Religion[i], sim);
            Link(links, "ui.clan", MetaKind.Clan, u.Clan[i], sim);
            Link(links, "ui.subspecies", MetaKind.Subspecies, u.Subspecies[i], sim);

            var actions = Row();
            actions.Add(Small("ui.follow", () => _ui.Followed = _unit));
            actions.Add(Small("ui.possess", () => _ui.Possess?.Invoke(_unit)));
            actions.Add(Small("ui.edit_traits", () => _ui.OpenTraitEditor(MetaKind.Family, _unit, true)));
            actions.Add(Small("ui.bless", () => Edit(EditKind.Bless)));
            actions.Add(Small("ui.curse", () => Edit(EditKind.Curse)));
            var kill = Small("ui.kill", () => Edit(EditKind.Kill));
            kill.AddToClassList("pg-button--danger");
            actions.Add(kill);
            var rename = new TextField { value = Names.Unit(sim, i) };
            rename.AddToClassList("pg-field");
            var row = Row();
            row.Add(rename);
            row.Add(Small("ui.rename", () => Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.RenameUnit, Target = _unit, ExpectedUid = _uid, Text = rename.value })));
        }

        void Link(VisualElement row, string key, MetaKind kind, int index, SimWorld sim)
        {
            if (index < 0) return;
            row.Add(Raw(Loc.T(key) + ": " + Names.Meta(sim, kind, index), () => _ui.OpenMeta(kind, index)));
        }

        void Edit(EditKind kind) => Host.Edits.Edits.Add(new EditCommand { Kind = kind, Target = _unit, ExpectedUid = _uid });

        void Stats(UnitStore u, int i)
        {
            for (int s = 0; s < (int)StatId.Count; s++)
            {
                float v = u.Stat(i, (StatId)s);
                if (Mathf.Abs(v) < 0.001f) continue;
                Line(Loc.T("stat." + ((StatId)s).ToString().ToLowerInvariant()) + ": " + v.ToString("0.##"));
            }
        }

        void Traits(SimWorld sim, UnitStore u, int i)
        {
            var db = sim.Content;
            var traits = u.Traits[i];
            var row = Row();
            for (int t = 0; t < db.UnitTraits.Count; t++)
            {
                if (!traits.Has(t)) continue;
                var def = db.UnitTraits[t];
                bool known = _ui.Profile.IsDiscovered(def.Id) || sim.Units.Discovered.Has(t);
                var chip = new Label(known ? Loc.Name(def) : "?");
                chip.AddToClassList("pg-chip");
                chip.AddToClassList("pg-rarity-" + (def.Rarity ?? "normal"));
                chip.tooltip = known ? EffectFormatter.WithEffects(Loc.Name(def), def.Effects) : Loc.T("ui.undiscovered");
                row.Add(chip);
            }
            Heading("ui.subspecies_traits");
            var sub = u.Subspecies[i];
            var subs = sim.Units.Subspecies;
            if (!subs.IsAlive(sub)) return;
            var sst = subs.Traits(sub);
            var srow = Row();
            for (int t = 0; t < db.SubspeciesTraits.Count; t++)
                if (sst.Has(t))
                {
                    var def = db.SubspeciesTraits[t];
                    var c = new Label(Loc.Name(def)); c.AddToClassList("pg-chip");
                    c.tooltip = EffectFormatter.WithEffects(Loc.Name(def), def.Effects);
                    srow.Add(c);
                }
        }

        void Equipment(SimWorld sim, UnitStore u, int i)
        {
            Body.Add(Small("ui.edit_item", () => _ui.Windows.Show(("item", _uid), () => new ItemEditorWindow(_ui, _unit))));
            var civ = sim.Civ;
            var db = sim.Content;
            for (int s = 0; s < UnitStore.EquipSlots; s++)
            {
                int item = u.Equip[i * UnitStore.EquipSlots + s];
                string slot = Loc.T("ui.slot." + ((EquipSlot)s).ToString().ToLowerInvariant());
                if (civ == null || item < 0 || item >= civ.Items.Length) { Line(slot + ": -"); continue; }
                var d = civ.Items[item];
                string name = d.NameId >= 0 ? sim.Meta.Names.Get(d.NameId) + " — " : "";
                var l = Line(slot + ": " + name + Loc.Name(db.ItemQualities[d.Quality]) + " " + Loc.Name(db.Materials[d.Material]) + " " + Loc.Name(db.EquipmentTypes[d.Type]) +
                             "  " + Loc.F("ui.item_kills_fmt", d.Kills));
                l.AddToClassList("pg-quality-" + db.ItemQualities[d.Quality].Id.Substring(2));
            }
        }

        void Family(SimWorld sim, UnitStore u, int i)
        {
            bool pregnant = sim.Units.StPregnant >= 0 && u.HasStatus(i, sim.Units.StPregnant);
            string state = pregnant ? "ui.family_pregnant" : (UnitTask)u.Task[i] == UnitTask.Mate && u.Action[i] == 1
                ? "ui.family_courting" : UnitCourtship.HasLivingBond(u, i) ? "ui.family_paired" : "ui.family_single";
            Line(Loc.T(state));
            Person("ui.mother", u.Mother[i], sim);
            Person("ui.father", u.Father[i], sim);
            Person("ui.mate", u.Mate[i], sim);
            Heading("ui.children");
            var id = u.IdOf(i);
            var row = Row();
            int n = 0;
            for (int k = 0; k < u.Alive.Length && n < 40; k++)
            {
                int o = u.Alive[k];
                if (u.Mother[o] != id && u.Father[o] != id) continue;
                int child = o;
                row.Add(Raw(Names.Unit(sim, o), () => _ui.OpenUnit(child)));
                n++;
            }
            if (n == 0) Line("-");
        }

        void Person(string key, PG.Core.EntityId who, SimWorld sim)
        {
            var u = sim.Units.Store;
            if (!u.IsAlive(who)) { Line(Loc.T(key) + ": -"); return; }
            int o = who.Index;
            Row().Add(Raw(Loc.T(key) + ": " + Names.Unit(sim, o), () => _ui.OpenUnit(o)));
        }

        void Log(SimWorld sim, UnitStore u, int i)
        {
            var db = sim.Content;
            Heading("ui.mood_events");
            for (int s = 0; s < UnitStore.HapSlots; s++)
            {
                int e = u.HapEvent[i * UnitStore.HapSlots + s] - 1;
                if (e >= 0 && u.HapLeft[i * UnitStore.HapSlots + s] > 0) Line(Loc.Name(db.HappinessEvents[e]) + " (" + u.HapLeft[i * UnitStore.HapSlots + s] + ")");
            }
            Heading("ui.history");
            long uid = u.Uid[i];
            var h = sim.Meta.History;
            int shown = 0;
            for (int k = h.Length - 1; k >= 0 && shown < 30; k--)
            {
                var e = h[k];
                if (e.A != uid && e.B != uid && e.C != uid) continue;
                Line(HistoryWindow.Describe(sim, e, k));
                shown++;
            }
        }

        void Brain(SimWorld sim, UnitStore u, int i)
        {
            Line(Loc.T("ui.brain_note"));
            ulong mask = u.NeuronMask[i];
            int sub = u.Subspecies[i];
            ulong disabled = sim.Units.Subspecies.IsAlive(sub) ? sim.Units.Subspecies.Data(sub).DisabledNeurons : 0UL;
            for (int n = 0; n < NeuronKeys.Keys.Length; n++)
            {
                string key = NeuronKeys.Keys[n];
                if (key == null) continue;
                bool core = n < 13;
                bool has = (mask & (1UL << n)) != 0 || (disabled & (1UL << n)) != 0;
                if (!has && !core) continue;
                bool on = (disabled & (1UL << n)) == 0;
                var row = Row();
                var l = new Label(Loc.T("neuron." + key));
                l.AddToClassList("pg-bar-label");
                row.Add(l);
                int neuron = n;
                var toggle = Small(on ? "ui.on" : "ui.off", () => Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.SetNeuron, Target = sub, A = neuron, B = on ? 0 : 1 }));
                toggle.EnableInClassList("pg-button--selected", on);
                row.Add(toggle);
            }
        }
    }
}
