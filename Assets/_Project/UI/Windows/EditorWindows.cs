using System;
using System.Collections.Generic;
using PG.Content;
using PG.Powers;
using PG.Sim;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Editors keep their controls stable while typing; Apply sends a command for the next simulation tick.
    public sealed class TraitEditorWindow : WindowBase
    {
        readonly UiContext _ui;
        readonly MetaKind _kind;
        readonly int _target;
        readonly bool _unit;
        readonly long _uid;
        string _query = "";
        public TraitEditorWindow(UiContext ui, MetaKind kind, int target, bool unit) : base(ui.Host, Loc.T("ui.edit_traits"))
        {
            _ui = ui; _kind = kind; _target = target; _unit = unit;
            _uid = unit ? ui.Sim.Units.Store.Uid[target] : 0;
            Rebuild();
        }
        protected override void Refresh() { }
        protected override void Build()
        {
            Line(Loc.T("ui.edit_next_tick"));
            var search = new TextField(Loc.T("ui.search")) { value = _query };
            Body.Add(search);
            var list = new VisualElement(); Body.Add(list);
            Action fill = () =>
            {
                list.Clear();
                foreach (var def in Definitions())
                {
                    if (Loc.Name(def).IndexOf(_query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int t = def.Index;
                    bool known = _ui.Profile.IsDiscovered(def.Id) || Host.Sim.Nature.Laws.IsOn("law.forbidden_codex");
                    var row = Row(list);
                    string tip = known ? EffectFormatter.WithEffects(Loc.Name(def), EffectsOf(def)) : Loc.T("ui.undiscovered");
                    var add = Raw(known ? "+ " + Loc.Name(def) : "?", () => Send(t, true)); add.tooltip = tip;
                    add.SetEnabled(known); row.Add(add);
                    var remove = Raw("− " + Loc.Name(def), () => Send(t, false)); remove.tooltip = tip;
                    row.Add(remove);
                }
            };
            search.RegisterValueChangedCallback(e => { _query = e.newValue; fill(); }); fill();
        }
        // Every trait-family def carries its own Effects JObject (no shared base); resolved here for the tooltip.
        static JObject EffectsOf(ContentDef def) => def switch
        {
            UnitTraitDef t => t.Effects,
            SubspeciesTraitDef t => t.Effects,
            MetaTraitDef t => t.Effects,
            _ => null,
        };

        IEnumerable<ContentDef> Definitions()
        {
            var d = Host.Content;
            if (_unit) return d.UnitTraits.All;
            switch (_kind)
            {
                case MetaKind.Subspecies: return d.SubspeciesTraits.All;
                case MetaKind.Culture: return d.CultureTraits.All;
                case MetaKind.Religion: return d.ReligionTraits.All;
                case MetaKind.Language: return d.LanguageTraits.All;
                case MetaKind.Clan: return d.ClanTraits.All;
                case MetaKind.Kingdom: return d.KingdomTraits.All;
                default: return Array.Empty<ContentDef>();
            }
        }
        void Send(int trait, bool add)
        {
            if (_unit && (!Host.Sim.Units.Store.IsAlive(_target) || Host.Sim.Units.Store.Uid[_target] != _uid)) return;
            var kind = _unit ? (add ? EditKind.AddUnitTrait : EditKind.RemoveUnitTrait) :
                _kind == MetaKind.Subspecies ? (add ? EditKind.AddSubspeciesTrait : EditKind.RemoveSubspeciesTrait) :
                (add ? EditKind.AddMetaTrait : EditKind.RemoveMetaTrait);
            Host.Edits.Edits.Add(new EditCommand { Kind = kind, Meta = _kind, Target = _target, A = trait, ExpectedUid = _uid });
        }
    }

    public sealed class GeneEditorWindow : WindowBase
    {
        readonly UiContext _ui;
        readonly int _sub;
        public GeneEditorWindow(UiContext ui, int sub) : base(ui.Host, Loc.T("ui.edit_genes")) { _ui = ui; _sub = sub; Rebuild(); }
        protected override void Refresh() { }
        protected override void Build()
        {
            if (!Host.Sim.Units.Subspecies.IsAlive(_sub)) return;
            Line(Loc.T("ui.subspecies_warning"));
            var names = new List<string> { "−" }; var ids = new List<int> { -1 };
            foreach (var g in Host.Content.Genes.All)
                if (_ui.Profile.IsDiscovered(g.Id) || Host.Sim.Nature.Laws.IsOn("law.forbidden_codex")) { names.Add(Loc.Name(g)); ids.Add(g.Index); }
            var genome = Host.Sim.Units.Subspecies.Data(_sub).Genome;
            for (int s = 0; s < genome.Slots.Length; s++)
            {
                int slot = s, current = genome.Slots[s] - 1;
                var row = Row();
                Line(Loc.F("ui.slot_fmt", s + 1), row);
                var geneLabel = Line(current >= 0 ? Loc.Name(Host.Content.Genes[current]) : "−", row);
                if (current >= 0) geneLabel.tooltip = EffectFormatter.WithEffects(Loc.Name(Host.Content.Genes[current]), Host.Content.Genes[current].Effects);
                var choice = new PopupField<string>(names, Math.Max(0, ids.IndexOf(current))); row.Add(choice);
                row.Add(Small("ui.apply", () => Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.SetGene, Target = _sub, A = slot, B = ids[choice.index] })));
            }
        }
    }

    public sealed class ItemEditorWindow : WindowBase
    {
        readonly int _unit;
        readonly long _uid;
        public ItemEditorWindow(UiContext ui, int unit) : base(ui.Host, Loc.T("ui.edit_item")) { _unit = unit; _uid = ui.Sim.Units.Store.Uid[unit]; Rebuild(); }
        protected override void Refresh() { }
        protected override void Build()
        {
            var db = Host.Content;
            var types = Choice(db.EquipmentTypes.All, "ui.item_type");
            var materials = Choice(db.Materials.All, "ui.material");
            var qualities = Choice(db.ItemQualities.All, "ui.quality");
            var name = new TextField(Loc.T("ui.name")); Body.Add(name);
            Body.Add(Small("ui.apply", () => Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.CreateItem, Target = _unit,
                ExpectedUid = _uid, A = types.index, B = materials.index, C = qualities.index, Text = name.value })));
        }
        PopupField<string> Choice<T>(IReadOnlyList<T> defs, string key) where T : ContentDef
        {
            var names = new List<string>(); foreach (var d in defs) names.Add(Loc.Name(d));
            var field = new PopupField<string>(Loc.T(key), names, 0); Body.Add(field); return field;
        }
    }

    public sealed class PeaceTermsWindow : WindowBase
    {
        readonly int _from, _to;

        public PeaceTermsWindow(UiContext ui, int from, int to) : base(ui.Host, Loc.T("ui.diplomacy.peace_terms"))
        {
            _from = from;
            _to = to;
            Rebuild();
        }

        protected override void Refresh() { }

        protected override void Build()
        {
            var sim = Host.Sim;
            if (sim?.Civ == null || !sim.Meta.KingdomAlive(_from) || !sim.Meta.KingdomAlive(_to))
            {
                Line(Loc.T("ui.diplomacy.peace_unavailable"));
                return;
            }

            Line(Loc.F("ui.diplomacy.peace_offer_to_fmt", Names.Kingdom(sim, _to)));
            var gold = new IntegerField(Loc.T("ui.diplomacy.tribute_gold")) { value = 0 };
            Body.Add(gold);
            long treasury = 0;
            int ownedCities = 0;
            for (int c = 0; c < sim.Civ.CityCount; c++)
            {
                if (!sim.Civ.CityAlive(c) || sim.Civ.KingdomOf(c) != _from) continue;
                ownedCities++;
                treasury += Math.Max(0, sim.Civ.City(c).Gold);
            }
            int availableGold = (int)Math.Min(int.MaxValue, treasury);
            Line(Loc.F("ui.diplomacy.tribute_available_fmt", availableGold));

            var cityIds = new List<int> { -1 };
            var cityNames = new List<string> { Loc.T("ui.diplomacy.no_city") };
            for (int c = 0; ownedCities > 1 && c < sim.Civ.CityCount; c++)
            {
                if (!sim.Civ.CityAlive(c) || sim.Civ.KingdomOf(c) != _from) continue;
                cityIds.Add(c);
                cityNames.Add(Names.City(sim, c));
            }
            var city = new PopupField<string>(Loc.T("ui.diplomacy.city_to_transfer"), cityNames, 0);
            Body.Add(city);

            var vassalize = new Toggle(Loc.T("ui.diplomacy.vassalize_proposer"));
            Body.Add(vassalize);
            var nonAggression = new IntegerField(Loc.T("ui.diplomacy.nonaggression_years")) { value = 10 };
            Body.Add(nonAggression);

            var actions = Row();
            actions.Add(Small("ui.apply", () =>
            {
                int cityIndex = city.index >= 0 && city.index < cityIds.Count ? cityIds[city.index] : -1;
                var terms = new PeaceTerms
                {
                    TributeGold = Math.Min(Math.Max(0, gold.value), availableGold),
                    CityToTransfer = cityIndex,
                    VassalizeProposer = vassalize.value,
                    NonAggressionYears = Math.Max(0, Math.Min(100, nonAggression.value)),
                };
                Host.Edits.Metas.Add(new MetaCommand
                {
                    Order = MetaOrder.ProposePeace,
                    Kingdom = _from,
                    Other = _to,
                    HasPeaceTerms = true,
                    PeaceTerms = terms,
                });
                Manager?.Close(this);
            }));
            actions.Add(Small("ui.cancel", () => Manager?.Close(this)));
        }
    }

    public sealed class MetaControlWindow : WindowBase
    {
        readonly int _kingdom;
        public MetaControlWindow(UiContext ui, int kingdom) : base(ui.Host, Loc.T("ui.meta_control")) { _kingdom = kingdom; Rebuild(); }
        protected override void Refresh() { }
        void Send(MetaOrder order, int other = -1) => Host.Edits.Metas.Add(new MetaCommand { Order = order, Kingdom = _kingdom, Other = other });
        protected override void Build()
        {
            var sim = Host.Sim;
            var controls = Row();
            controls.Add(Small("ui.take_control", () => Send(MetaOrder.Control)));
            controls.Add(Small("ui.release", () => Send(MetaOrder.Release)));
            if (sim.Meta.KingdomAlive(_kingdom) && sim.Meta.Kingdom(_kingdom).Alliance >= 0)
                controls.Add(Small("ui.leave_alliance", () => Send(MetaOrder.LeaveAlliance)));

            for (int k = 0; k < sim.Meta.Kingdoms.Count; k++)
            {
                if (k == _kingdom || !sim.Meta.KingdomAlive(k)) continue;
                int other = k; var row = Row(); Line(Names.Kingdom(sim, k), row);
                string warAction = sim.Meta.HasTreaty(TreatyKind.NonAggression, _kingdom, other, sim.Clock.Tick)
                    ? "ui.declare_war_breach" : "ui.declare_war";
                row.Add(Small(warAction, () => Send(MetaOrder.DeclareWar, other)));
                row.Add(Small("ui.peace", () => Send(MetaOrder.ProposePeace, other)));   // the other side must accept
                bool allied = sim.Meta.KingdomAlive(_kingdom) && sim.Meta.Kingdom(_kingdom).Alliance >= 0 &&
                              sim.Meta.Kingdom(_kingdom).Alliance == sim.Meta.Kingdom(other).Alliance;
                if (!allied)
                    row.Add(Small("ui.ally", () => Send(MetaOrder.ProposeAlliance, other))); // (Bölüm 10, DECISIONS #98)
            }
            for (int c = 0; c < sim.Civ.CityCount; c++)
            {
                if (!sim.Civ.CityAlive(c) || sim.Civ.KingdomOf(c) != _kingdom) continue;
                int city = c; Body.Add(Raw(Loc.T("ui.set_capital") + ": " + Names.City(sim, c), () => Send(MetaOrder.SetCapital, city)));
            }
        }
    }
}
