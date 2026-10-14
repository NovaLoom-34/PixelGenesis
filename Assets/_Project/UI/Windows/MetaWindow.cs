using PG.Content;
using PG.Powers;
using PG.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.5 one template for every meta: General, Members, Cities, Relations, Wars, History, Books.
    public sealed class MetaWindow : WindowBase
    {
        public const int PageSize = 50;

        readonly UiContext _ui;
        readonly MetaKind _kind;
        readonly int _index;
        int _page;

        public MetaWindow(UiContext ui, MetaKind kind, int index) : base(ui.Host, "")
        {
            _ui = ui;
            _kind = kind;
            _index = index;
            if (_kind == MetaKind.Kingdom)
                SetTabs("ui.tab_general", "ui.tab_members", "ui.tab_cities", "ui.tab_relations", "ui.tab_wars", "ui.tab_history", "ui.tab_books", "ui.tab_inventions");
            else
                SetTabs("ui.tab_general", "ui.tab_members", "ui.tab_cities", "ui.tab_relations", "ui.tab_wars", "ui.tab_history", "ui.tab_books");
        }

        protected override void Build()
        {
            var sim = Host.Sim;
            SetTitle(Loc.T("ui.meta." + _kind.ToString().ToLowerInvariant()) + ": " + Names.Meta(sim, _kind, _index));
            switch (Tab)
            {
                case 0: General(sim); break;
                case 1: Members(sim); break;
                case 2: Cities(sim); break;
                case 3: Relations(sim); break;
                case 4: Wars(sim); break;
                case 5: History(sim); break;
                case 6: Books(sim); break;
                case 7: Inventions(sim); break;
            }
        }

        bool Header(SimWorld sim, out MetaHeader h)
        {
            var m = sim.Meta;
            h = default;
            switch (_kind)
            {
                case MetaKind.Kingdom: if (_index >= m.Kingdoms.Count) return false; h = m.Kingdoms.Headers[_index]; return true;
                case MetaKind.City: if (sim.Civ == null || _index >= sim.Civ.CityCount) return false; h = sim.Civ.Cities.Headers[_index]; return true;
                case MetaKind.Culture: if (_index >= m.Cultures.Count) return false; h = m.Cultures.Headers[_index]; return true;
                case MetaKind.Language: if (_index >= m.Languages.Count) return false; h = m.Languages.Headers[_index]; return true;
                case MetaKind.Religion: if (_index >= m.Religions.Count) return false; h = m.Religions.Headers[_index]; return true;
                case MetaKind.Clan: if (_index >= m.Clans.Count) return false; h = m.Clans.Headers[_index]; return true;
                case MetaKind.Subspecies: if (_index >= sim.Units.Subspecies.Count) return false; h = sim.Units.Subspecies.Header(_index); return true;
                case MetaKind.War: if (_index >= m.Wars.Count) return false; h = m.Wars.Headers[_index]; return true;
                case MetaKind.Alliance: if (_index >= m.Alliances.Count) return false; h = m.Alliances.Headers[_index]; return true;
            }
            return false;
        }

        // does unit i belong to this meta?
        bool Member(SimWorld sim, int i)
        {
            var u = sim.Units.Store;
            switch (_kind)
            {
                case MetaKind.Kingdom: return sim.Civ != null && sim.Civ.KingdomOf(u.City[i]) == _index;
                case MetaKind.City: return u.City[i] == _index;
                case MetaKind.Culture: return u.Culture[i] == _index;
                case MetaKind.Language: return u.Language[i] == _index;
                case MetaKind.Religion: return u.Religion[i] == _index;
                case MetaKind.Clan: return u.Clan[i] == _index;
                case MetaKind.Subspecies: return u.Subspecies[i] == _index;
            }
            return false;
        }

        void General(SimWorld sim)
        {
            if (!Header(sim, out var h)) { Line("-"); return; }
            var db = sim.Content;
            var color = new Color32((byte)(h.Color >> 24), (byte)(h.Color >> 16), (byte)(h.Color >> 8), 255);
            var swatch = new VisualElement();
            swatch.AddToClassList("pg-swatch");
            swatch.style.backgroundColor = (Color)color;
            Body.Add(swatch);
            Line(Loc.F("ui.founded_fmt", h.FoundedTick / PG.Core.SimConst.TicksPerYear + 1));
            if ((h.Flags & MetaFlags.Dead) != 0) Line(Loc.F("ui.died_fmt", h.DiedTick / PG.Core.SimConst.TicksPerYear + 1));
            if ((h.Flags & MetaFlags.Edited) != 0) Line(Loc.T("ui.edited_badge"));
            Line(Loc.F("ui.members_fmt", h.MemberCount));
            Specific(sim);
            // species mix bar (Bölüm 8.5)
            var u = sim.Units.Store;
            var counts = new int[db.Species.Count];
            int total = 0;
            for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; if (Member(sim, i)) { counts[u.Species[i]]++; total++; } }
            for (int s = 0; total > 0 && s < counts.Length; s++)
                if (counts[s] > 0) Bar(Loc.Name(db.Species[s]), counts[s] / (float)total, UiTheme.SpeciesBar);
            Heading("ui.traits");
            var row = Row();
            var defs = TraitDefs(db);
            for (int t = 0; defs != null && t < defs.Length; t++)
                if (h.Traits.Has(t)) { var c = new Label(defs[t]); c.AddToClassList("pg-chip"); row.Add(c); }
            var actions = Row();
            actions.Add(Small("ui.favorite", () => Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.Favorite, Meta = _kind, Target = _index })));
            if (_kind == MetaKind.Kingdom)
            {
                long kingdomUid = h.Uid;
                ulong seed = sim.Seed;
                int width = sim.World.Width, height = sim.World.Height;
                var profile = _ui.Profile;
                bool watched = profile.IsKingdomWatched(seed, width, height, kingdomUid);
                var watch = new Button { text = Loc.T(watched ? "ui.kingdom_unwatch" : "ui.kingdom_watch") };
                watch.AddToClassList("pg-button");
                watch.clicked += () =>
                {
                    bool isWatched = profile.ToggleKingdomWatch(seed, width, height, kingdomUid);
                    profile.Save();
                    watch.text = Loc.T(isWatched ? "ui.kingdom_unwatch" : "ui.kingdom_watch");
                };
                actions.Add(watch);
            }
            if (_kind != MetaKind.War && _kind != MetaKind.Alliance && _kind != MetaKind.City)
                actions.Add(Small("ui.edit_traits", () => _ui.OpenTraitEditor(_kind, _index, false)));
            if (_kind == MetaKind.Subspecies) actions.Add(Small("ui.edit_genes", () => _ui.Windows.Show(("genes", _index), () => new GeneEditorWindow(_ui, _index))));
            if (_kind == MetaKind.Kingdom) actions.Add(Small("ui.meta_control", () => _ui.Windows.Show(("control", _index), () => new MetaControlWindow(_ui, _index))));
            if (_kind == MetaKind.City && sim.Civ != null && _index < sim.Civ.CityCount) actions.Add(Small("ui.go_to", () => _ui.GoTo(sim.Civ.City(_index).Center)));
            var rename = new TextField { value = Names.Meta(sim, _kind, _index) };
            rename.AddToClassList("pg-field");
            var rrow = Row();
            rrow.Add(rename);
            rrow.Add(Small("ui.rename", () => Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.RenameMeta, Meta = _kind, Target = _index, Text = rename.value })));
        }

        string[] TraitDefs(ContentDB db)
        {
            System.Func<ContentDef, string> n = Loc.Name;
            switch (_kind)
            {
                case MetaKind.Culture: return Map(db.CultureTraits.Count, t => n(db.CultureTraits[t]));
                case MetaKind.Language: return Map(db.LanguageTraits.Count, t => n(db.LanguageTraits[t]));
                case MetaKind.Religion: return Map(db.ReligionTraits.Count, t => n(db.ReligionTraits[t]));
                case MetaKind.Clan: return Map(db.ClanTraits.Count, t => n(db.ClanTraits[t]));
                case MetaKind.Kingdom: return Map(db.KingdomTraits.Count, t => n(db.KingdomTraits[t]));
                case MetaKind.Subspecies: return Map(db.SubspeciesTraits.Count, t => n(db.SubspeciesTraits[t]));
            }
            return null;
        }

        static string[] Map(int count, System.Func<int, string> f)
        {
            var a = new string[count];
            for (int i = 0; i < count; i++) a[i] = f(i);
            return a;
        }

        void Specific(SimWorld sim)
        {
            var m = sim.Meta;
            var u = sim.Units.Store;
            switch (_kind)
            {
                case MetaKind.Kingdom when _index < m.Kingdoms.Count:
                    var k = m.Kingdom(_index);
                    if (u.IsAlive(k.King)) { int king = k.King.Index; Row().Add(Raw(Loc.T("ui.king") + ": " + Names.Unit(sim, king), () => _ui.OpenUnit(king))); }
                    if (u.IsAlive(k.Heir)) { int heir = k.Heir.Index; Row().Add(Raw(Loc.T("ui.heir") + ": " + Names.Unit(sim, heir), () => _ui.OpenUnit(heir))); }
                    Line(Loc.F("ui.kingdom_stats_fmt", k.Cities, k.Soldiers, (int)k.Power, k.Treasury));
                    if (k.Capital >= 0) { int cap = k.Capital; Row().Add(Raw(Loc.T("ui.capital") + ": " + Names.City(sim, cap), () => _ui.OpenMeta(MetaKind.City, cap))); }
                    BrainLines();
                    break;
                case MetaKind.City when sim.Civ != null && _index < sim.Civ.CityCount:
                    var c = sim.Civ.City(_index);
                    Line(Loc.F("ui.city_stats_fmt", c.Population, c.Housing, c.HallTier, c.Loyalty, c.Gold));
                    int kingdom = c.Kingdom;
                    if (kingdom >= 0) Row().Add(Raw(Loc.T("ui.kingdom") + ": " + Names.Kingdom(sim, kingdom), () => _ui.OpenMeta(MetaKind.Kingdom, kingdom)));
                    if (u.IsAlive(c.Leader)) { int leader = c.Leader.Index; Row().Add(Raw(Loc.T("ui.leader") + ": " + Names.Unit(sim, leader), () => _ui.OpenUnit(leader))); }
                    break;
                case MetaKind.War when _index < m.Wars.Count:
                    var w = m.Wars.Items[_index];
                    long end = w.EndTick > 0 ? w.EndTick : sim.Clock.Tick;
                    Line(Loc.F("ui.war_fmt", Names.Kingdom(sim, w.Attacker), Names.Kingdom(sim, w.Defender)));
                    Line(Loc.F("ui.war_time_fmt", (end - w.StartTick) / PG.Core.SimConst.TicksPerYear, (sim.Clock.Tick - w.StartTick) / PG.Core.SimConst.TicksPerYear));
                    Line(Loc.F("ui.war_losses_fmt", w.CasualtiesA, w.CasualtiesD, w.CapturedCities));
                    Line(Loc.T("ui.war_result." + w.Result.ToString().ToLowerInvariant()));
                    for (int armyIndex = 0; armyIndex < m.Armies.Length; armyIndex++)
                    {
                        var army = m.Armies[armyIndex];
                        if (army.War != _index || army.State == ArmyState.Disbanded) continue;
                        if (!m.TryGetArmyLogistics(armyIndex, out var supply)) supply = new ArmyLogistics { Army = armyIndex, Morale = 100 };
                        Line(Loc.F("ui.war_army_logistics_fmt", Names.Kingdom(sim, army.Kingdom), army.Soldiers,
                            supply.Morale, supply.Rations));
                        if (army.State == ArmyState.Sieging && supply.SiegeProgress > 0f)
                            Line(Loc.F("ui.war_siege_progress_fmt", (int)supply.SiegeProgress));
                    }
                    break;
                case MetaKind.Alliance when _index < m.Alliances.Count:
                    var a = m.Alliances.Items[_index];
                    var row = Row();
                    for (int q = 0; q < a.Kingdoms.Length; q++) { int kk = a.Kingdoms[q]; row.Add(Raw(Names.Kingdom(sim, kk), () => _ui.OpenMeta(MetaKind.Kingdom, kk))); }
                    break;
            }
        }

        // Governor panel (Bölüm 10, DECISIONS #97/#98): AI toggle, connection status, pending
        // proposals for this kingdom, and the decision log with advisor opinions and rejections.
        void BrainLines()
        {
            var gov = PG.Governor.GovernorRunner.Active;
            if (gov == null) return;
            bool aiOn = !gov.Log.PlayerTaken.Contains(_index);
            Row().Add(Small(aiOn ? "ui.gov.takeover" : "ui.gov.hands_over", () =>
            {
                bool turnAiOn = aiOn;
                Host.Edits.Metas.Add(new MetaCommand { Order = turnAiOn ? MetaOrder.Release : MetaOrder.Control, Kingdom = _index });
                gov.SetKingdomAI(_index, turnAiOn);
                Rebuild();
            }));
            Line(Loc.F("ui.gov.status_fmt", Loc.T("ui.gov.status." + gov.Status.ToString().ToLowerInvariant()) +
                (string.IsNullOrEmpty(gov.StatusMessage) ? "" : " — " + gov.StatusMessage)));

            var sim = Host.Sim;
            var entry = gov.Log.LastForKingdom(_index);
            if (entry == null) { Line(Loc.T("ui.gov.no_decision")); return; }
            Line(Loc.F("ui.gov.last_fmt", entry.Year, entry.Thought));
            for (int i = 0; i < entry.AdvisorNames.Count && i < entry.AdvisorThoughts.Count; i++)
                Line(Loc.F("ui.gov.advisor_fmt", entry.AdvisorNames[i], entry.AdvisorThoughts[i]));
            foreach (var a in entry.Actions)
                if (!string.IsNullOrEmpty(a)) Line("· " + a);
            if (entry.Rejected.Count > 0) Line(Loc.T("ui.gov.rejected"));
            foreach (var r in entry.Rejected)
                if (!string.IsNullOrEmpty(r)) Line("  ✕ " + r);
        }

        void SendProposal(MetaOrder order, int proposalId) =>
            Host.Edits.Metas.Add(new MetaCommand { Order = order, Kingdom = _index, Other = proposalId });

        void Members(SimWorld sim)
        {
            var u = sim.Units.Store;
            var nav = Row();
            nav.Add(Small("ui.prev", () => { _page = Mathf.Max(0, _page - 1); Rebuild(); }));
            nav.Add(new Label(Loc.F("ui.page_fmt", _page + 1)));
            nav.Add(Small("ui.next", () => { _page++; Rebuild(); }));
            int seen = 0, shown = 0;
            for (int k = 0; k < u.Alive.Length && shown < PageSize; k++)
            {
                int i = u.Alive[k];
                if (!Member(sim, i)) continue;
                if (seen++ < _page * PageSize) continue;
                int unit = i;
                int job = u.Job[i] - 1;
                string jobName = job >= 0 ? Loc.Name(sim.Content.Jobs[job]) : "-";
                Row().Add(Raw(Names.Unit(sim, i) + "  " + (int)u.AgeYears(i, sim.Clock.Tick) + "  L" + u.Level[i] + "  " + jobName, () => _ui.OpenUnit(unit)));
                shown++;
            }
            if (shown == 0) Line("-");
        }

        void Cities(SimWorld sim)
        {
            var civ = sim.Civ;
            if (civ == null) return;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                var d = civ.City(c);
                bool match = _kind == MetaKind.Kingdom ? d.Kingdom == _index : _kind == MetaKind.Culture ? d.Culture == _index
                           : _kind == MetaKind.Religion ? d.Religion == _index : _kind == MetaKind.Language ? d.Language == _index : false;
                if (!match) continue;
                int city = c;
                Row().Add(Raw(Names.City(sim, c) + "  " + Loc.F("ui.pop_fmt", d.Population), () => _ui.OpenMeta(MetaKind.City, city)));
                Bar(Loc.T("ui.loyalty"), Mathf.InverseLerp(-100f, 100f, d.Loyalty), UiTheme.BarLoyalty);
            }
        }

        void Relations(SimWorld sim)
        {
            if (_kind != MetaKind.Kingdom) { Line("-"); return; }
            var m = sim.Meta;
            bool playerControlled = (m.Kingdoms.Header(_index).Flags & MetaFlags.PlayerControlled) != 0;
            if (playerControlled)
                for (int i = 0; i < m.Proposals.Count; i++)
                {
                    var proposal = m.Proposals[i];
                    if (proposal.To != _index || !m.KingdomAlive(proposal.From)) continue;
                    int proposalId = proposal.Id;
                    string key = proposal.Kind == ProposalKind.Alliance ? "ui.gov.proposal_alliance_fmt"
                        : proposal.Kind == ProposalKind.TradeAgreement ? "ui.gov.proposal_trade_fmt"
                        : proposal.Kind == ProposalKind.NonAggression ? "ui.gov.proposal_nonaggression_fmt"
                        : proposal.Kind == ProposalKind.TradeEmbargo ? "ui.gov.proposal_embargo_fmt"
                        : proposal.Kind == ProposalKind.Vassalage ? "ui.gov.proposal_vassalage_fmt" : "ui.gov.proposal_peace_fmt";
                    var proposalRow = Row();
                    Line(Loc.F(key, Names.Kingdom(sim, proposal.From)), proposalRow);
                    if (proposal.Kind == ProposalKind.Peace)
                    {
                        var terms = proposal.PeaceTerms;
                        if (terms.TributeGold > 0) Line(Loc.F("ui.gov.peace_tribute_fmt", terms.TributeGold), proposalRow);
                        if (terms.CityToTransfer >= 0 && sim.Civ != null && sim.Civ.CityAlive(terms.CityToTransfer))
                            Line(Loc.F("ui.gov.peace_city_fmt", Names.City(sim, terms.CityToTransfer)), proposalRow);
                        if (terms.VassalizeProposer) Line(Loc.T("ui.gov.peace_vassalize"), proposalRow);
                        if (terms.NonAggressionYears > 0) Line(Loc.F("ui.gov.peace_nap_fmt", terms.NonAggressionYears), proposalRow);
                    }
                    proposalRow.Add(Small("ui.gov.accept", () => SendProposal(MetaOrder.AcceptProposal, proposalId)));
                    proposalRow.Add(Small("ui.gov.reject", () => SendProposal(MetaOrder.RejectProposal, proposalId)));
                }

            for (int b = 0; b < m.Kingdoms.Count; b++)
            {
                if (b == _index || !m.KingdomAlive(b)) continue;
                int op = m.Opinion(_index, b);
                var status = m.StatusBetween(_index, b, sim.Clock.Tick);
                string state = status switch
                {
                    DiplomaticStatus.War => Loc.T("ui.at_war"),
                    DiplomaticStatus.Alliance => Loc.T("ui.allied"),
                    DiplomaticStatus.Vassalage => Loc.T(m.VassalOf(_index, sim.Clock.Tick) == b ? "ui.diplomacy.vassal" : "ui.diplomacy.overlord"),
                    DiplomaticStatus.TradeAgreement => Loc.T("ui.diplomacy.trade"),
                    DiplomaticStatus.NonAggression => Loc.T("ui.diplomacy.nonaggression"),
                    DiplomaticStatus.TradeEmbargo => Loc.T("ui.diplomacy.embargo"),
                    _ => "",
                };
                Bar(Names.Kingdom(sim, b) + " " + op + " " + state, Mathf.InverseLerp(-100f, 100f, op), op >= 0 ? UiTheme.OpinionPos : UiTheme.OpinionNeg);

                if (!playerControlled) continue;
                var envoyRow = new VisualElement();
                int offerCount = 0;
                int targetKingdom = b;
                void AddOffer(ProposalKind kind, MetaOrder order, string label)
                {
                    foreach (var pending in m.Proposals)
                        if (pending.Kind == kind && (pending.From == _index && pending.To == targetKingdom || pending.From == targetKingdom && pending.To == _index)) return;
                    envoyRow.Add(Small(label, () => Host.Edits.Metas.Add(new MetaCommand { Order = order, Kingdom = _index, Other = targetKingdom })));
                    offerCount++;
                }
                void AddPeaceOffer()
                {
                    foreach (var pending in m.Proposals)
                        if (pending.Kind == ProposalKind.Peace &&
                            (pending.From == _index && pending.To == targetKingdom || pending.From == targetKingdom && pending.To == _index)) return;
                    envoyRow.Add(Small("ui.diplomacy.offer_peace", () =>
                        _ui.Windows.Show(("peace-offer", _index, targetKingdom), () => new PeaceTermsWindow(_ui, _index, targetKingdom))));
                    offerCount++;
                }

                if (status == DiplomaticStatus.War)
                {
                    AddPeaceOffer();
                }
                else
                {
                    var current = m.Kingdom(_index);
                    bool alreadyAllied = current.Alliance >= 0 && current.Alliance == m.Kingdom(b).Alliance;
                    if (!alreadyAllied) AddOffer(ProposalKind.Alliance, MetaOrder.ProposeAlliance, "ui.diplomacy.offer_alliance");
                    if (!m.HasTreaty(TreatyKind.TradeAgreement, _index, b, sim.Clock.Tick) &&
                        !m.HasTreaty(TreatyKind.TradeEmbargo, _index, b, sim.Clock.Tick))
                        AddOffer(ProposalKind.TradeAgreement, MetaOrder.ProposeTradeAgreement, "ui.diplomacy.offer_trade");
                    if (!m.HasTreaty(TreatyKind.NonAggression, _index, b, sim.Clock.Tick))
                        AddOffer(ProposalKind.NonAggression, MetaOrder.ProposeNonAggression, "ui.diplomacy.offer_nonaggression");
                    if (!m.HasTreaty(TreatyKind.TradeEmbargo, _index, b, sim.Clock.Tick) &&
                        !m.HasTreaty(TreatyKind.TradeAgreement, _index, b, sim.Clock.Tick))
                        AddOffer(ProposalKind.TradeEmbargo, MetaOrder.ProposeTradeEmbargo, "ui.diplomacy.offer_embargo");
                    if (m.VassalOf(_index, sim.Clock.Tick) < 0 && !m.HasVassalage(_index, b, sim.Clock.Tick))
                        AddOffer(ProposalKind.Vassalage, MetaOrder.ProposeVassalage, "ui.diplomacy.offer_vassalage");
                }

                if (offerCount > 0) Line(Loc.F("ui.diplomacy.envoy_fmt", Names.Kingdom(sim, b)), envoyRow);
            }
        }

        void Wars(SimWorld sim)
        {
            var m = sim.Meta;
            for (int w = 0; w < m.Wars.Count; w++)
            {
                var d = m.Wars.Items[w];
                bool ours = _kind == MetaKind.War ? w == _index : d.Attacker == _index || d.Defender == _index;
                if (!ours || _kind != MetaKind.Kingdom && _kind != MetaKind.War) continue;
                long end = d.EndTick > 0 ? d.EndTick : sim.Clock.Tick;
                int war = w;
                Row().Add(Raw(Names.Meta(sim, MetaKind.War, w) + "  " + Loc.F("ui.war_time_fmt", (end - d.StartTick) / PG.Core.SimConst.TicksPerYear, (sim.Clock.Tick - d.StartTick) / PG.Core.SimConst.TicksPerYear),
                              () => _ui.OpenMeta(MetaKind.War, war)));
            }
        }

        void History(SimWorld sim)
        {
            if (!Header(sim, out var h)) return;
            var hist = sim.Meta.History;
            int shown = 0;
            for (int k = hist.Length - 1; k >= 0 && shown < 60; k--)
            {
                var e = hist[k];
                if (e.A != h.Uid && e.B != h.Uid && e.C != h.Uid) continue;
                Line(HistoryWindow.Describe(sim, e, k));
                shown++;
            }
            if (shown == 0) Line("-");
        }

        void Books(SimWorld sim)
        {
            var books = sim.Meta.Books;
            int shown = 0;
            for (int b = 0; b < books.Length && shown < 100; b++)
            {
                var d = books[b];
                bool match = _kind == MetaKind.Language ? d.Language == _index : _kind == MetaKind.Culture ? d.Culture == _index
                           : _kind == MetaKind.Religion ? d.Religion == _index : _kind == MetaKind.City && d.City == _index;
                if (!match) continue;
                Line(sim.Meta.Names.Get(d.NameId) + " — " + Loc.Name(sim.Content.BookTypes[d.Type]));
                shown++;
            }
            if (shown == 0) Line("-");
        }

        void Inventions(SimWorld sim)
        {
            if (_kind != MetaKind.Kingdom) { Line("-"); return; }
            var civ = sim.Civ;
            var db = sim.Content;
            if (civ == null || db.Inventions.Count == 0) return;

            Heading("ui.inventions_researching");
            if (civ.CurrentResearch.TryGetValue(_index, out int current) && current >= 0 && current < db.Inventions.Count)
            {
                var inv = db.Inventions[current];
                civ.ResearchProgress.TryGetValue(_index, out int prog);
                Line(Loc.Name(inv) + " (" + Loc.T("ui.inv_category." + inv.Category) + ")");
                Bar(Loc.F("ui.research_progress_fmt", prog, inv.Cost), prog / (float)inv.Cost, new Color(0.95f, 0.8f, 0.25f));
                Line(inv.Description);
            }
            else
            {
                Line(Loc.T("ui.no_current_research"));
            }

            Heading("ui.inventions_unlocked");
            var row = Row();
            int unlockedCount = 0;
            for (int i = 0; i < db.Inventions.Count; i++)
            {
                if (!civ.HasInvention(_index, i)) continue;
                unlockedCount++;
                var inv = db.Inventions[i];
                var badge = new Label("💡 " + Loc.Name(inv));
                badge.AddToClassList("pg-chip");
                badge.tooltip = inv.Description;
                row.Add(badge);
            }
            if (unlockedCount == 0) Line(Loc.T("ui.no_inventions_yet"));
        }
    }
}
