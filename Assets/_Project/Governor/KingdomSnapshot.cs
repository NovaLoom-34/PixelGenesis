using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using PG.Core;
using PG.Sim;
using Unity.Collections;

namespace PG.Governor
{
    // Compact read-only view of one kingdom for the LLM (~2-4 KB). Built on the main thread,
    // handed to the brain on a worker thread. Deterministic truncation everywhere.
    public sealed class KingdomSnapshot
    {
        public int KingdomIndex;
        public long KingdomUid;
        public string Name = "";
        public string Species = "";
        public int Year;
        public int Population;
        public int CityCount;
        public int Soldiers;
        public int Treasury;
        public float Power;
        public int Alliance; // -1 = none
        public int PeaceBanYearsLeft;
        public int MaxAdvisors;
        public float PolicyFood, PolicyHousing, PolicyProduction, PolicyDefense, PolicyCulture, PolicyTax; // current kingdom policy

        public readonly List<CityLine> Cities = new List<CityLine>();
        public readonly List<WarLine> Wars = new List<WarLine>();
        public readonly List<RelationLine> Relations = new List<RelationLine>();
        public readonly List<string> RecentEvents = new List<string>();
        public readonly List<ProposalLine> PendingProposals = new List<ProposalLine>();

        public struct CityLine
        {
            public string Name; public int Index, Population, Happiness, Food, Storage, FamineMonths, Loyalty, HallTier;
        }

        public struct WarLine
        {
            public string Enemy; public int EnemyIndex, Years, OurCasualties, TheirCasualties;
        }

        public struct RelationLine
        {
            public string Name, Status; public int Index, Opinion; public bool War, Allied;
        }

        public struct ProposalLine
        {
            public int Id, FromIndex, YearsAtWar; public string From, Kind; public bool Alliance;
        }

        public string ToJson()
        {
            var sb = new StringBuilder(2048);
            var sw = new JsonTextWriter(new StringWriter(sb)) { Formatting = Formatting.None };
            sw.WriteStartObject();
            sw.WritePropertyName("kingdom");
            sw.WriteStartObject();
            W(sw, "index", KingdomIndex); W(sw, "name", Name); W(sw, "species", Species);
            W(sw, "year", Year); W(sw, "population", Population); W(sw, "cities", CityCount);
            W(sw, "soldiers", Soldiers); W(sw, "treasury", Treasury); W(sw, "power", Power);
            W(sw, "alliance", Alliance); W(sw, "peaceBanYearsLeft", PeaceBanYearsLeft);
            sw.WritePropertyName("policy");
            sw.WriteStartObject();
            W(sw, "food", PolicyFood); W(sw, "housing", PolicyHousing); W(sw, "production", PolicyProduction);
            W(sw, "defense", PolicyDefense); W(sw, "culture", PolicyCulture); W(sw, "tax", PolicyTax * 100f);
            sw.WriteEndObject();
            sw.WriteEndObject();

            sw.WritePropertyName("cities"); sw.WriteStartArray();
            foreach (var c in Cities)
            {
                sw.WriteStartObject();
                W(sw, "name", c.Name); W(sw, "index", c.Index); W(sw, "pop", c.Population);
                W(sw, "happiness", c.Happiness); W(sw, "food", c.Food); W(sw, "storage", c.Storage);
                W(sw, "famineMonths", c.FamineMonths); W(sw, "loyalty", c.Loyalty); W(sw, "hallTier", c.HallTier);
                sw.WriteEndObject();
            }
            sw.WriteEndArray();

            sw.WritePropertyName("wars"); sw.WriteStartArray();
            foreach (var w in Wars)
            {
                sw.WriteStartObject();
                W(sw, "enemy", w.Enemy); W(sw, "enemyIndex", w.EnemyIndex); W(sw, "years", w.Years);
                W(sw, "ourCasualties", w.OurCasualties); W(sw, "theirCasualties", w.TheirCasualties);
                sw.WriteEndObject();
            }
            sw.WriteEndArray();

            sw.WritePropertyName("relations"); sw.WriteStartArray();
            foreach (var r in Relations)
            {
                sw.WriteStartObject();
                W(sw, "name", r.Name); W(sw, "index", r.Index); W(sw, "opinion", r.Opinion);
                sw.WritePropertyName("war"); sw.WriteValue(r.War);
                sw.WritePropertyName("allied"); sw.WriteValue(r.Allied);
                W(sw, "status", r.Status);
                sw.WriteEndObject();
            }
            sw.WriteEndArray();

            sw.WritePropertyName("recentEvents"); sw.WriteStartArray();
            foreach (var e in RecentEvents) sw.WriteValue(e);
            sw.WriteEndArray();

            sw.WritePropertyName("pendingProposals"); sw.WriteStartArray();
            foreach (var p in PendingProposals)
            {
                sw.WriteStartObject();
                W(sw, "id", p.Id); W(sw, "from", p.From); W(sw, "fromIndex", p.FromIndex);
                W(sw, "yearsAtWar", p.YearsAtWar);
                sw.WritePropertyName("alliance"); sw.WriteValue(p.Alliance);
                W(sw, "kind", p.Kind);
                sw.WriteEndObject();
            }
            sw.WriteEndArray();

            sw.WriteEndObject();
            sw.Flush();
            return sb.ToString();
        }

        static void W(JsonTextWriter sw, string name, bool v) { sw.WritePropertyName(name); sw.WriteValue(v); }

        static void W(JsonTextWriter sw, string name, int v) { sw.WritePropertyName(name); sw.WriteValue(v); }
        static void W(JsonTextWriter sw, string name, long v) { sw.WritePropertyName(name); sw.WriteValue(v); }
        static void W(JsonTextWriter sw, string name, float v) { sw.WritePropertyName(name); sw.WriteValue(v); }
        static void W(JsonTextWriter sw, string name, string v) { sw.WritePropertyName(name); sw.WriteValue(v ?? ""); }
    }

    // Reads the simulation (main thread only). Never mutates anything.
    public static class KingdomSnapshotBuilder
    {
        public const int MaxCities = 12, MaxWars = 6, MaxRelations = 10, MaxEvents = 8, MaxNameLength = 28, MaxProposals = 6;

        public static KingdomSnapshot Build(SimWorld sim, int kingdom)
        {
            var meta = sim.Meta;
            var snap = new KingdomSnapshot
            {
                KingdomIndex = kingdom,
                Year = sim.Clock.Year,
                MaxAdvisors = GovernorBrain.MaxAdvisors,
            };
            if (!meta.KingdomAlive(kingdom)) return snap;

            ref var kd = ref meta.Kingdom(kingdom);
            var header = meta.Kingdoms.Header(kingdom);
            snap.KingdomUid = header.Uid;
            snap.Name = Short(meta.KingdomName(kingdom));
            snap.Species = kd.Species < sim.Content.Species.Count ? sim.Content.Species[kd.Species].Id : "?";
            snap.Population = header.MemberCount;
            snap.CityCount = kd.Cities;
            snap.Soldiers = kd.Soldiers;
            snap.Treasury = kd.Treasury;
            snap.Power = kd.Power;
            snap.Alliance = kd.Alliance;
            long tick = sim.Clock.Tick;
            snap.PeaceBanYearsLeft = System.Math.Max(0,
                (int)System.Math.Ceiling((WarSystem.PeaceBanYears * SimConst.TicksPerYear - (tick - kd.LastWarEndTick)) / (float)SimConst.TicksPerYear));

            var civ = sim.Civ;
            if (civ != null)
            {
                for (int c = 0; c < civ.CityCount && snap.Cities.Count < MaxCities; c++)
                {
                    if (!civ.CityAlive(c) || civ.KingdomOf(c) != kingdom) continue;
                    ref var city = ref civ.City(c);
                    snap.Cities.Add(new KingdomSnapshot.CityLine
                    {
                        Index = c,
                        Name = Short(CityName(sim, c)),
                        Population = city.Population,
                        Happiness = city.Happiness,
                        Food = civ.FoodTotal(c),
                        Storage = city.StorageCap,
                        FamineMonths = city.FamineMonths,
                        Loyalty = city.Loyalty,
                        HallTier = city.HallTier,
                    });
                }
            }

            for (int w = 0; w < meta.Wars.Count && snap.Wars.Count < MaxWars; w++)
            {
                if (!meta.Wars.IsAlive(w)) continue;
                var war = meta.Wars.Items[w];
                if (war.Result != WarResult.Ongoing) continue;
                int enemy = war.Attacker == kingdom ? war.Defender : war.Defender == kingdom ? war.Attacker : -1;
                if (enemy < 0) continue;
                snap.Wars.Add(new KingdomSnapshot.WarLine
                {
                    EnemyIndex = enemy,
                    Enemy = Short(meta.KingdomAlive(enemy) ? meta.KingdomName(enemy) : "?"),
                    Years = (int)((tick - war.StartTick) / SimConst.TicksPerYear),
                    OurCasualties = war.Attacker == kingdom ? war.CasualtiesA : war.CasualtiesD,
                    TheirCasualties = war.Attacker == kingdom ? war.CasualtiesD : war.CasualtiesA,
                });
            }

            // Most extreme opinions first (deterministic: |opinion| desc, then index).
            var candidates = new List<(int index, int opinion)>();
            for (int b = 0; b < meta.Kingdoms.Count; b++)
            {
                if (b == kingdom || !meta.KingdomAlive(b)) continue;
                candidates.Add((b, meta.Opinion(kingdom, b)));
            }
            candidates.Sort((a, b) =>
            {
                int byAbs = System.Math.Abs(b.opinion).CompareTo(System.Math.Abs(a.opinion));
                return byAbs != 0 ? byAbs : a.index.CompareTo(b.index);
            });
            for (int i = 0; i < candidates.Count && snap.Relations.Count < MaxRelations; i++)
            {
                int b = candidates[i].index;
                ref var ob = ref meta.Kingdom(b);
                snap.Relations.Add(new KingdomSnapshot.RelationLine
                {
                    Index = b,
                    Name = Short(meta.KingdomName(b)),
                    Opinion = meta.Opinion(kingdom, b),
                    War = meta.AtWar(kingdom, b),
                    Allied = kd.Alliance >= 0 && kd.Alliance == ob.Alliance,
                    Status = meta.StatusBetween(kingdom, b, tick).ToString(),
                });
            }

            FillRecentEvents(sim, kingdom, snap);
            FillPendingProposals(sim, kingdom, snap);

            var policy = meta.PolicyOf(kingdom);
            snap.PolicyFood = policy.Food; snap.PolicyHousing = policy.Housing; snap.PolicyProduction = policy.Production;
            snap.PolicyDefense = policy.Defense; snap.PolicyCulture = policy.Culture; snap.PolicyTax = policy.Tax;
            return snap;
        }

        static void FillPendingProposals(SimWorld sim, int kingdom, KingdomSnapshot snap)
        {
            var meta = sim.Meta;
            long tick = sim.Clock.Tick;
            foreach (var p in meta.Proposals)
            {
                if (p.To != kingdom || snap.PendingProposals.Count >= MaxProposals) continue;
                int war = meta.WarBetween(p.From, p.To);
                snap.PendingProposals.Add(new KingdomSnapshot.ProposalLine
                {
                    Id = p.Id,
                    FromIndex = p.From,
                    From = Short(meta.KingdomAlive(p.From) ? meta.KingdomName(p.From) : "?"),
                    Kind = p.Kind.ToString(),
                    Alliance = p.Kind == ProposalKind.Alliance,
                    YearsAtWar = war >= 0 ? (int)((tick - meta.Wars.Items[war].StartTick) / SimConst.TicksPerYear) : 0,
                });
            }
        }

        // Newest first; keeps entries where the kingdom uid takes part in A/B.
        static void FillRecentEvents(SimWorld sim, int kingdom, KingdomSnapshot snap)
        {
            var meta = sim.Meta;
            long uid = meta.Kingdoms.Header(kingdom).Uid;
            var history = meta.History;
            for (int i = history.Length - 1; i >= 0 && snap.RecentEvents.Count < MaxEvents; i--)
            {
                var e = history[i];
                if (e.A != uid && e.B != uid) continue;
                int year = (int)(e.Tick / SimConst.TicksPerYear);
                string kind = ((HistoryKind)e.Template).ToString();
                string other = OtherName(sim, e.A == uid ? e.B : e.A);
                snap.RecentEvents.Add(other != null ? $"{kind} vs {other} (year {year})" : $"{kind} (year {year})");
            }
        }

        static string OtherName(SimWorld sim, long uid)
        {
            if (uid == 0) return null;
            var meta = sim.Meta;
            for (int k = 0; k < meta.Kingdoms.Count; k++)
                if (meta.Kingdoms.IsAlive(k) && meta.Kingdoms.Header(k).Uid == uid) return Short(meta.KingdomName(k));
            var civ = sim.Civ;
            if (civ != null)
                for (int c = 0; c < civ.CityCount; c++)
                    if (civ.CityAlive(c) && civ.Cities.Header(c).Uid == uid) return Short(CityName(sim, c));
            return null;
        }

        static string CityName(SimWorld sim, int city)
        {
            int nameId = sim.Civ.Cities.Header(city).NameId;
            return nameId >= 0 ? sim.Meta.Names.Get(nameId) : "city#" + city;
        }

        static string Short(string s) =>
            string.IsNullOrEmpty(s) || s.Length <= MaxNameLength ? s : s.Substring(0, MaxNameLength);
    }
}
