using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Sim;
using Unity.Mathematics;

namespace PG.Powers
{
    // Bölüm 8.1: editors, laws, meta control and possession reach the simulation only through these commands.
    public enum EditKind : byte
    {
        AddUnitTrait, RemoveUnitTrait, AddSubspeciesTrait, RemoveSubspeciesTrait, AddMetaTrait, RemoveMetaTrait,
        SetGene, SetNeuron, CreateItem, RenameUnit, RenameMeta, Kill, Bless, Curse, Favorite, ReleaseControl, SetProtectedZone,
    }

    public struct EditCommand
    {
        public EditKind Kind;
        public MetaKind Meta;   // AddMetaTrait/RemoveMetaTrait/RenameMeta/Favorite
        public int Target;      // unit index, subspecies or meta index
        public long ExpectedUid; // nonzero rejects stale unit commands after slot reuse
        public int A, B, C;     // trait / gene slot + gene / neuron + on / item type, material, quality
        public string Text;     // new name
    }

    public struct LawCommand
    {
        public int Law;
        public bool On;
        public float Value;
    }

    // Bölüm 10 (DECISIONS #98): kingdom-wide city policy weights from the governor / player.
    public struct PolicyCommand
    {
        public int Kingdom;
        public float Food, Housing, Production, Defense, Culture, Tax;
    }

    // Bilateral diplomacy proposals need the other side's acceptance; Accept/RejectProposal act
    // on a pending proposal id (Other). DeclareWar stays unilateral.
    public enum MetaOrder : byte { Control, Release, DeclareWar, MakePeace, Ally, SetCapital, SetHeir, ArmyTarget,
        ProposePeace, ProposeAlliance, AcceptProposal, RejectProposal, LeaveAlliance, ProposeTradeAgreement, ProposeNonAggression, ProposeVassalage, ProposeTradeEmbargo }

    public struct MetaCommand
    {
        public MetaOrder Order;
        public int Kingdom;     // the controlled kingdom
        public int Other;       // target kingdom, war, city or unit index
        public bool HasPeaceTerms;
        public PeaceTerms PeaceTerms;
    }

    // Bölüm 8.11 era clock: change now, freeze, enable/disable, assign a clock slot.
    public enum EraOrder : byte { ChangeNow, Freeze, Enable, Slot }

    public struct EraCommand
    {
        public EraOrder Order;
        public int Era;         // era index (-1 = empty for Slot)
        public int Value;       // Freeze/Enable: 0/1, Slot: slot index
    }

    // Bölüm 8.12: one control frame for the possessed unit (the last one per tick wins).
    public struct ControlCommand
    {
        public int Unit;
        public long ExpectedUid;
        public float2 Move;     // -1..1
        public float2 AimAt;    // world position
        public bool Attack, Dash, Release;
        public sbyte Spell;     // -1 = none
    }

    public sealed class EditCommandQueue
    {
        public readonly List<EditCommand> Edits = new List<EditCommand>(16);
        public readonly List<LawCommand> Laws = new List<LawCommand>(4);
        public readonly List<MetaCommand> Metas = new List<MetaCommand>(4);
        public readonly List<PolicyCommand> Policies = new List<PolicyCommand>(2);
        public readonly List<EraCommand> Eras = new List<EraCommand>(2);
        public ControlCommand? Control;
        public int EditCount; // lifetime editor changes (ach.mad_scientist counts subspecies edits)
        public int SubspeciesEdits;
    }

    public static class EditSystems
    {
        public static void Install(SimWorld sim, EditCommandQueue queue) => sim.Pipeline.Add(new EditCommandSystem(queue, sim.Content));
    }

    public sealed class EditCommandSystem : ISimSystem
    {
        public const float DashStamina = 20f, DashSpeed = 1.5f, PossessedStep = 1f;

        readonly EditCommandQueue _q;
        readonly int _trDivine, _sstDivine;

        public EditCommandSystem(EditCommandQueue queue, ContentDB db)
        {
            _q = queue;
            _trDivine = db.UnitTraits.IdOrDefault("tr.divine_touch");
            _sstDivine = db.SubspeciesTraits.IdOrDefault("sst.divine_breeding");
        }

        public SimPhase Phase => SimPhase.InputCommands;
        public int Order => 1;

        public void Tick(in SimContext ctx)
        {
            foreach (var e in _q.Edits) Edit(ctx, e);
            _q.Edits.Clear();
            foreach (var l in _q.Laws) ctx.Nature.Laws.Set(l.Law, l.On, l.Value);
            _q.Laws.Clear();
            foreach (var m in _q.Metas) Meta(ctx, m);
            _q.Metas.Clear();
            foreach (var p in _q.Policies) Policy(ctx, p);
            _q.Policies.Clear();
            foreach (var e in _q.Eras) Era(ctx, e);
            _q.Eras.Clear();
            if (_q.Control.HasValue) Control(ctx, _q.Control.Value);
            _q.Control = null;
        }

        static void Policy(in SimContext ctx, in PolicyCommand p)
        {
            if (ctx.Meta == null || !ctx.Meta.KingdomAlive(p.Kingdom)) return;
            ctx.Meta.SetPolicy(p.Kingdom, KingdomPolicy.Normalized(p.Food, p.Housing, p.Production, p.Defense, p.Culture, p.Tax));
        }

        void Edit(in SimContext ctx, in EditCommand e)
        {
            var w = ctx.Units;
            var u = w.Store;
            var db = ctx.Content;
            var meta = ctx.Meta;
            bool unit = e.Target >= 0 && e.Target < u.HighWater && u.State[e.Target] == UnitStore.StateAlive;
            if (e.ExpectedUid != 0 && (!unit || u.Uid[e.Target] != e.ExpectedUid)) return;
            _q.EditCount++;
            switch (e.Kind)
            {
                case EditKind.AddUnitTrait when unit && e.A >= 0 && e.A < db.UnitTraits.Count:
                    w.GainTrait(e.Target, e.A);
                    w.GainTrait(e.Target, _trDivine); // Bölüm 8.10 divine mark
                    break;
                case EditKind.RemoveUnitTrait when unit && e.A >= 0 && e.A < db.UnitTraits.Count:
                    var traits = u.Traits[e.Target];
                    traits.Set(e.A, false);
                    u.Traits[e.Target] = traits;
                    u.StatsDirty[e.Target] = 1;
                    w.GainTrait(e.Target, _trDivine);
                    break;
                case EditKind.AddSubspeciesTrait:
                case EditKind.RemoveSubspeciesTrait:
                    var subs = w.Subspecies;
                    if (!subs.IsAlive(e.Target) || e.A < 0 || e.A >= db.SubspeciesTraits.Count) break;
                    if (e.Kind == EditKind.AddSubspeciesTrait) subs.AddTrait(e.Target, e.A);
                    else { subs.Header(e.Target).Traits.Set(e.A, false); subs.RefreshAllAbilities(); }
                    if (_sstDivine >= 0) subs.AddTrait(e.Target, _sstDivine);
                    _q.SubspeciesEdits++;
                    Dirty(u, e.Target);
                    break;
                case EditKind.AddMetaTrait:
                case EditKind.RemoveMetaTrait:
                    MetaTrait(ctx, e);
                    break;
                case EditKind.SetGene:
                    if (!w.Subspecies.IsAlive(e.Target)) break;
                    ref var genome = ref w.Subspecies.Data(e.Target).Genome;
                    if (e.A < 0 || e.A >= genome.Slots.Length || e.B < -1 || e.B >= db.Genes.Count) break;
                    genome.Slots[e.A] = (ushort)(e.B + 1);
                    if (_sstDivine >= 0) w.Subspecies.AddTrait(e.Target, _sstDivine);
                    _q.SubspeciesEdits++;
                    Dirty(u, e.Target);
                    break;
                case EditKind.SetNeuron:
                    if (!w.Subspecies.IsAlive(e.Target) || e.A < 0 || e.A >= 64) break;
                    w.Subspecies.SetNeuron(e.Target, (NeuronId)e.A, e.B != 0);
                    Dirty(u, e.Target);
                    break;
                case EditKind.CreateItem when unit && ctx.Civ != null && e.A >= 0 && e.A < db.EquipmentTypes.Count && e.B >= 0 && e.B < db.Materials.Count:
                    int item = ctx.Civ.CreateItem(e.A, e.B, math.clamp(e.C, 0, math.max(0, db.ItemQualities.Count - 1)), ctx.Clock.Tick, 0, -1);
                    int slot = e.Target * UnitStore.EquipSlots + (int)db.EquipmentTypes[e.A].SlotCode;
                    int old = u.Equip[slot];
                    if (old >= 0 && old < ctx.Civ.Items.Length) { var o = ctx.Civ.Items[old]; o.Owner = -1; o.DroppedTile = u.Tile(e.Target).y * ctx.World.Width + u.Tile(e.Target).x; ctx.Civ.Items[old] = o; }
                    var data = ctx.Civ.Items[item];
                    data.Owner = e.Target;
                    data.City = -1;
                    if (!string.IsNullOrEmpty(e.Text) && meta != null) data.NameId = meta.Names.Add(e.Text, false);
                    ctx.Civ.Items[item] = data;
                    u.Equip[slot] = item;
                    u.StatsDirty[e.Target] = 1;
                    break;
                case EditKind.RenameUnit when unit && meta != null && !string.IsNullOrEmpty(e.Text):
                    if (u.NameId[e.Target] >= 0) meta.Names.Rename(u.NameId[e.Target], e.Text);
                    else u.NameId[e.Target] = meta.Names.Add(e.Text, false);
                    break;
                case EditKind.RenameMeta when meta != null && !string.IsNullOrEmpty(e.Text):
                    ref var header = ref HeaderOf(ctx, e.Meta, e.Target, out bool ok);
                    if (!ok) break;
                    if (header.NameId >= 0) meta.Names.Rename(header.NameId, e.Text);
                    else header.NameId = meta.Names.Add(e.Text, false);
                    header.Flags |= MetaFlags.Edited;
                    break;
                case EditKind.Favorite:
                    ref var fav = ref HeaderOf(ctx, e.Meta, e.Target, out bool found);
                    if (found) fav.Flags ^= MetaFlags.Favorite;
                    break;
                case EditKind.Kill when unit: u.Kill(e.Target, DeathCause.Divine); break;
                case EditKind.ReleaseControl when unit:
                    u.Flags[e.Target] &= ~(uint)UnitFlags.PlayerControlled;
                    u.NextThink[e.Target] = ctx.Clock.Tick;
                    break;
                case EditKind.SetProtectedZone when (uint)e.Target < (uint)ctx.World.Zones.Length && e.A >= 0 && e.A <= db.Species.Count:
                    ctx.World.SetProtectedSpecies(e.Target, (ushort)e.A);
                    break;
                case EditKind.Bless when unit: w.AddStatus(e.Target, w.StBlessed); break;
                case EditKind.Curse when unit: w.AddStatus(e.Target, w.StCursed); break;
            }
        }

        static void Dirty(UnitStore u, int subspecies)
        {
            for (int k = 0; k < u.Alive.Length; k++) if (u.Subspecies[u.Alive[k]] == subspecies) u.StatsDirty[u.Alive[k]] = 1;
        }

        static MetaHeader _none;

        static ref MetaHeader HeaderOf(in SimContext ctx, MetaKind kind, int index, out bool ok)
        {
            var m = ctx.Meta;
            ok = true;
            switch (kind)
            {
                case MetaKind.Kingdom when index >= 0 && index < m.Kingdoms.Count: return ref m.Kingdoms.Header(index);
                case MetaKind.Culture when index >= 0 && index < m.Cultures.Count: return ref m.Cultures.Header(index);
                case MetaKind.Language when index >= 0 && index < m.Languages.Count: return ref m.Languages.Header(index);
                case MetaKind.Religion when index >= 0 && index < m.Religions.Count: return ref m.Religions.Header(index);
                case MetaKind.Clan when index >= 0 && index < m.Clans.Count: return ref m.Clans.Header(index);
                case MetaKind.City when ctx.Civ != null && index >= 0 && index < ctx.Civ.CityCount: return ref ctx.Civ.Cities.Header(index);
                case MetaKind.Subspecies when ctx.Units.Subspecies.IsAlive(index): return ref ctx.Units.Subspecies.Header(index);
            }
            ok = false;
            return ref _none;
        }

        static void MetaTrait(in SimContext ctx, in EditCommand e)
        {
            ref var h = ref HeaderOf(ctx, e.Meta, e.Target, out bool ok);
            if (!ok || e.A < 0) return;
            h.Traits.Set(e.A, e.Kind == EditKind.AddMetaTrait);
            h.Flags |= MetaFlags.Edited;
            if (e.Meta == MetaKind.Kingdom && e.Kind == EditKind.AddMetaTrait) ctx.Meta.Kingdom(e.Target).Trait = e.A;
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) u.StatsDirty[u.Alive[k]] = 1; // culture/religion/clan mods feed unit stats
        }

        // Bölüm 6.14 / 8.13: the player's orders for a controlled kingdom.
        static void Era(in SimContext ctx, in EraCommand c)
        {
            var nature = ctx.Nature;
            var era = nature.Era;
            int n = ctx.Content.Eras.Count;
            switch (c.Order)
            {
                case EraOrder.ChangeNow when c.Era >= 0 && c.Era < n:
                    EraSystem.SetEra(nature, c.Era, ctx.Clock.Year, ctx.Events, ref ctx.Rng.Get(RngStream.Meta));
                    break;
                case EraOrder.Freeze: era.Frozen = c.Value != 0; break;
                case EraOrder.Enable when c.Era >= 0 && c.Era < n: era.Enabled[c.Era] = c.Value != 0; break;
                case EraOrder.Slot when c.Value >= 0 && c.Value < EraState.SlotCount && c.Era >= -1 && c.Era < n:
                    era.ClockSlots[c.Value] = c.Era + 1;
                    break;
            }
        }

        static void Meta(in SimContext ctx, in MetaCommand m)
        {
            var meta = ctx.Meta;
            if (meta == null || !meta.KingdomAlive(m.Kingdom)) return;
            ref var header = ref meta.Kingdoms.Header(m.Kingdom);
            long tick = ctx.Clock.Tick;
            switch (m.Order)
            {
                case MetaOrder.Control: header.Flags |= MetaFlags.PlayerControlled; break;
                case MetaOrder.Release: header.Flags &= ~MetaFlags.PlayerControlled; break;
                case MetaOrder.DeclareWar:
                    if (meta.KingdomAlive(m.Other) && m.Other != m.Kingdom && !meta.AtWar(m.Kingdom, m.Other)
                        && !meta.HasVassalage(m.Kingdom, m.Other, tick))
                        WarSystem.Declare(ctx, meta, m.Kingdom, m.Other, meta.WarConquest, -1, tick);
                    break;
                case MetaOrder.MakePeace:
                    for (int w = 0; w < meta.Wars.Count; w++)
                    {
                        if (!meta.Wars.IsAlive(w)) continue;
                        var d = meta.Wars.Items[w];
                        bool ours = d.Attacker == m.Kingdom || d.Defender == m.Kingdom;
                        bool theirs = d.Attacker == m.Other || d.Defender == m.Other;
                        if (ours && theirs) WarSystem.EndWar(ctx, meta, w, WarResult.Peace, tick);
                    }
                    break;
                case MetaOrder.Ally:
                    if (meta.KingdomAlive(m.Other) && m.Other != m.Kingdom) KingdomSystem.Ally(ctx, meta, m.Kingdom, m.Other, tick);
                    break;
                case MetaOrder.SetCapital:
                    if (ctx.Civ != null && m.Other >= 0 && m.Other < ctx.Civ.CityCount && ctx.Civ.CityAlive(m.Other) && ctx.Civ.KingdomOf(m.Other) == m.Kingdom) meta.Kingdom(m.Kingdom).Capital = m.Other;
                    break;
                case MetaOrder.SetHeir:
                    var u = ctx.Units.Store;
                    if (u.IsAlive(m.Other) && ctx.Civ != null && ctx.Civ.KingdomOf(u.City[m.Other]) == m.Kingdom) meta.Kingdom(m.Kingdom).Heir = u.IdOf(m.Other);
                    break;
                case MetaOrder.ArmyTarget:
                    for (int a = 0; a < meta.Armies.Length; a++)
                    {
                        var army = meta.Armies[a];
                        if (army.Kingdom != m.Kingdom) continue;
                        army.TargetCity = m.Other;
                        meta.Armies[a] = army;
                    }
                    break;

                // --- Bölüm 10: bilateral diplomacy (DECISIONS #98) ---
                case MetaOrder.ProposePeace:
                    if (meta.KingdomAlive(m.Other) && m.Other != m.Kingdom && meta.AtWar(m.Kingdom, m.Other)
                        && !meta.HasProposal(ProposalKind.Peace, m.Kingdom, m.Other)
                        && !meta.HasProposal(ProposalKind.Peace, m.Other, m.Kingdom))
                    {
                        int id;
                        if (m.HasPeaceTerms) id = meta.AddPeaceProposal(m.Kingdom, m.Other, tick, m.PeaceTerms).Id;
                        else id = meta.AddProposal(ProposalKind.Peace, m.Kingdom, m.Other, tick);
                        ctx.Events.Publish(new DiplomacyProposalEvent(id, m.Kingdom, m.Other, false));
                    }
                    break;
                case MetaOrder.ProposeAlliance:
                    if (meta.KingdomAlive(m.Other) && m.Other != m.Kingdom && !meta.AtWar(m.Kingdom, m.Other)
                        && !meta.HasVassalage(m.Kingdom, m.Other, tick)
                        && !(meta.Kingdom(m.Kingdom).Alliance >= 0 && meta.Kingdom(m.Kingdom).Alliance == meta.Kingdom(m.Other).Alliance))
                    {
                        int id = meta.AddProposal(ProposalKind.Alliance, m.Kingdom, m.Other, tick);
                        ctx.Events.Publish(new DiplomacyProposalEvent(id, m.Kingdom, m.Other, true));
                    }
                    break;
                case MetaOrder.ProposeTradeAgreement:
                case MetaOrder.ProposeNonAggression:
                case MetaOrder.ProposeVassalage:
                case MetaOrder.ProposeTradeEmbargo:
                    if (meta.KingdomAlive(m.Other) && m.Other != m.Kingdom && !meta.AtWar(m.Kingdom, m.Other))
                    {
                        var kind = m.Order == MetaOrder.ProposeTradeAgreement ? ProposalKind.TradeAgreement
                            : m.Order == MetaOrder.ProposeNonAggression ? ProposalKind.NonAggression
                            : m.Order == MetaOrder.ProposeTradeEmbargo ? ProposalKind.TradeEmbargo : ProposalKind.Vassalage;
                        var treaty = kind == ProposalKind.TradeAgreement ? TreatyKind.TradeAgreement
                            : kind == ProposalKind.NonAggression ? TreatyKind.NonAggression
                            : kind == ProposalKind.TradeEmbargo ? TreatyKind.TradeEmbargo : TreatyKind.Vassalage;
                        if (!meta.HasTreaty(treaty, m.Kingdom, m.Other, tick) && !meta.HasProposal(kind, m.Kingdom, m.Other))
                        {
                            int id = meta.AddProposal(kind, m.Kingdom, m.Other, tick);
                            ctx.Events.Publish(new DiplomacyProposalEvent(id, m.Kingdom, m.Other, kind));
                        }
                    }
                    break;
                case MetaOrder.AcceptProposal:
                case MetaOrder.RejectProposal:
                    ResolveProposal(ctx, meta, m, tick);
                    break;
                case MetaOrder.LeaveAlliance:
                    KingdomSystem.LeaveAlliance(ctx, meta, m.Kingdom, tick);
                    break;
            }
        }

        // Only the receiving side may answer a proposal.
        static void ResolveProposal(in SimContext ctx, MetaWorld meta, in MetaCommand m, long tick)
        {
            if (!meta.FindProposal(m.Other, out var p)) return;
            if (p.To != m.Kingdom) return;
            bool accept = m.Order == MetaOrder.AcceptProposal;
            meta.RemoveProposal(p.Id, out _);
            if (accept) KingdomSystem.ApplyAcceptedProposal(ctx, meta, p, tick);
            ctx.Events.Publish(new DiplomacyProposalResolvedEvent(p.Id, p.From, p.To, p.Kind, accept));
        }

        // Bölüm 8.12 possession: the AI is off (PlayerControlled); the player walks, strikes, dashes and casts.
        static void Control(in SimContext ctx, in ControlCommand c)
        {
            var w = ctx.Units;
            var u = w.Store;
            if (c.Unit < 0 || c.Unit >= u.HighWater || u.State[c.Unit] != UnitStore.StateAlive) return;
            if (c.ExpectedUid != 0 && u.Uid[c.Unit] != c.ExpectedUid) return;
            int i = c.Unit;
            if (c.Release) { u.Flags[i] &= ~(uint)UnitFlags.PlayerControlled; u.NextThink[i] = ctx.Clock.Tick; return; }
            u.Flags[i] |= (uint)UnitFlags.PlayerControlled;
            u.Task[i] = 0;
            float dt = 1f / SimConst.TicksPerSecond;
            if (math.lengthsq(c.Move) > 0.01f)
            {
                float2 step = math.normalize(c.Move) * PossessedStep * (c.Dash && u.Stamina[i] >= DashStamina ? DashSpeed * 4f : 1f);
                if (c.Dash && u.Stamina[i] >= DashStamina) u.Stamina[i] -= DashStamina;
                w.StepToward(i, u.Pos[i] + step, dt);
            }
            if (c.Attack && u.AttackCooldown[i] <= 0)
            {
                float range = math.max(1f, u.Stat(i, StatId.Range));
                u.AttackCooldown[i] = (short)math.max(2, (int)(20f / u.Stat(i, StatId.AtkSpd)));
                if (range > 1.5f) w.LaunchProjectile(i, c.AimAt, u.Stat(i, StatId.Dmg), 0f, false);
                else
                {
                    int target = Nearest(w, i, c.AimAt, range + 0.5f);
                    ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
                    if (target >= 0) w.MeleeHit(i, target, ref rng, ctx.Clock.Tick);
                }
            }
            if (c.Spell >= 0) // the first ready spell (slot order as in the AI)
            {
                int enemy = Nearest(w, i, c.AimAt, 6f);
                if (UnitThinkSystem.ReadySpell(w, i, enemy, out int spell, out int slot))
                {
                    var def = ctx.Content.Spells[spell];
                    u.Mana[i] -= def.ManaCost;
                    u.SpellCooldown[i * UnitStore.SpellSlots + slot] = (ushort)math.max(1, def.CooldownTicks);
                    ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
                    SpellRegistry.Cast(w, ctx.Nature, i, def, enemy, ctx.Clock.Tick, ref rng);
                }
            }
        }

        static int Nearest(UnitWorld w, int self, float2 at, float radius)
        {
            var u = w.Store;
            int best = -1;
            float bestD = radius * radius;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int o = u.Alive[k];
                if (o == self || u.State[o] != UnitStore.StateAlive) continue;
                if (math.distancesq(u.Pos[o], u.Pos[self]) > radius * radius) continue;
                float d = math.distancesq(u.Pos[o], at);
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }
    }
}
