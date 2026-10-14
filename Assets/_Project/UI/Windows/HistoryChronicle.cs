using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PG.Content;
using PG.Core;
using PG.Sim;

namespace PG.UI
{
    // Narrative text is built on demand by UI callers, never during a simulation tick.
    public static class HistoryChronicle
    {
        static readonly ConditionalWeakTable<MetaWorld, Dictionary<string, string>> Cache =
            new ConditionalWeakTable<MetaWorld, Dictionary<string, string>>();

        public static string Describe(SimWorld sim, HistoryEntry entry, int historyIndex = -1)
        {
            long eventId = historyIndex >= 0 && historyIndex < sim.Meta.HistoryDetails.Length
                ? sim.Meta.HistoryDetails[historyIndex].EventId
                : FallbackId(entry);
            string language = Loc.Language ?? "tr";
            string cacheKey = eventId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + language;
            var cache = Cache.GetValue(sim.Meta, _ => new Dictionary<string, string>());
            if (cache.TryGetValue(cacheKey, out string cached)) return cached;

            string text = Build(sim, entry, language, eventId, historyIndex);
            if (cache.Count >= 2048) cache.Clear();
            cache[cacheKey] = text;
            return text;
        }

        static string Build(SimWorld sim, HistoryEntry entry, string language, long eventId, int historyIndex)
        {
            if (entry.Template >= HistoryKeys.Keys.Length) return Loc.F("ui.year_fmt", Year(entry)) + " · " + Loc.T("ui.history");
            string key = HistoryKeys.Keys[entry.Template];
            string text;
            if (sim.Content.ChronicleTemplates.TryGet(key, out var definition))
            {
                var variants = language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? definition.En : definition.Tr;
                text = variants != null && variants.Length > 0
                    ? variants[(int)((ulong)eventId % (uint)variants.Length)]
                    : Loc.T(key);
            }
            else text = Loc.T(key);

            var kind = (HistoryKind)entry.Template;
            text = text.Replace("{A}", Resolve(sim, kind, entry, 0))
                       .Replace("{B}", Resolve(sim, kind, entry, 1))
                       .Replace("{C}", Resolve(sim, kind, entry, 2))
                       .Replace("{N}", entry.Extra.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (kind == HistoryKind.WarDeclared && (entry.Extra & 0x40000000) != 0)
            {
                var cause = (WarCause)((entry.Extra >> 8) & 0xff);
                var goal = (WarGoal)(entry.Extra & 0xff);
                text = text.Replace("{CAUSE}", Loc.T("ui.war_cause." + WarCauseId(cause)))
                           .Replace("{GOAL}", Loc.T("ui.war_goal." + WarGoalId(goal)));
            }
            else if (kind == HistoryKind.Famine) text = text.Replace("{CAUSE}", ResolveFamineCause(entry.Extra));
            else if (kind == HistoryKind.PopulationMigrated)
            {
                bool disaster = historyIndex >= 0 && historyIndex < sim.Meta.HistoryDetails.Length
                    && (sim.Meta.HistoryDetails[historyIndex].Flags & (ushort)HistoryMetadataFlags.DisasterMigration) != 0;
                text = text.Replace("{CAUSE}", Loc.T(disaster ? "ui.migration_cause_disaster" : "ui.migration_cause_famine"));
            }
            else text = text.Replace("{CAUSE}", "-").Replace("{GOAL}", "-");

            if (kind == HistoryKind.KingdomFell && TryGetCauseIndex(sim.Meta, historyIndex, out int causeIndex))
            {
                var cause = sim.Meta.History[causeIndex];
                long causeEventId = sim.Meta.HistoryDetails[causeIndex].EventId;
                string causeText = Build(sim, cause, language, causeEventId, causeIndex);
                text += " · " + Loc.F("ui.history_cause_fmt", causeText);
            }
            return Loc.F("ui.year_fmt", Year(entry)) + " · " + text;
        }

        public static bool TryGetCauseIndex(MetaWorld meta, int historyIndex, out int causeIndex)
        {
            causeIndex = -1;
            if (historyIndex < 0 || historyIndex >= meta.HistoryDetails.Length) return false;
            long causeEventId = meta.HistoryDetails[historyIndex].CauseEventId;
            if (causeEventId == 0) return false;
            for (int i = 0; i < meta.HistoryDetails.Length; i++)
            {
                if (meta.HistoryDetails[i].EventId != causeEventId) continue;
                causeIndex = i;
                return true;
            }
            return false;
        }

        static int Year(HistoryEntry entry) => (int)(entry.Tick / SimConst.TicksPerYear + 1);

        static long FallbackId(HistoryEntry entry)
        {
            unchecked
            {
                long h = entry.Tick * 486187739L + entry.Template;
                h = h * 31 + entry.A; h = h * 31 + entry.B; h = h * 31 + entry.C; h = h * 31 + entry.Extra;
                return h < 0 ? -h : h;
            }
        }

        static string Resolve(SimWorld sim, HistoryKind kind, HistoryEntry entry, int slot)
        {
            if (kind == HistoryKind.SpeciesEndangered || kind == HistoryKind.SpeciesExtinct)
                return slot == 0 && (uint)entry.Extra < (uint)sim.Content.Species.Count ? Loc.Name(sim.Content.Species[entry.Extra]) : "-";
            if (kind == HistoryKind.Disaster)
                return slot == 0 && (uint)entry.Extra < (uint)sim.Content.Disasters.Count ? Loc.Name(sim.Content.Disasters[entry.Extra]) : "-";
            if (kind == HistoryKind.InventionDiscovered)
                return slot == 0 ? Kingdom(sim, entry.A) : slot == 1 && (uint)entry.Extra < (uint)sim.Content.Inventions.Count ? Loc.Name(sim.Content.Inventions[entry.Extra]) : "-";
            if (kind == HistoryKind.Famine)
                return slot == 0 ? City(sim, entry.A) : slot == 1 ? Kingdom(sim, entry.B) : "-";
            if (kind == HistoryKind.PopulationMigrated)
                return slot == 0 ? City(sim, entry.A) : slot == 1 ? City(sim, entry.B) : slot == 2 ? Kingdom(sim, entry.C) : "-";
            if (kind == HistoryKind.PersonDied)
                return slot == 0 ? Unit(sim, entry.A) : "-";
            if (kind == HistoryKind.MetaDied)
            {
                int type = entry.Extra / 1000000, index = entry.Extra % 1000000;
                return slot == 0 && (MetaKind)type == MetaKind.Kingdom ? Kingdom(sim, entry.A) : slot == 0 ? MetaName(sim, (MetaKind)type, index) : "-";
            }
            long uid = slot == 0 ? entry.A : slot == 1 ? entry.B : entry.C;
            if (uid == 0) return "-";
            switch (kind)
            {
                case HistoryKind.KingdomFounded:
                case HistoryKind.KingdomFell:
                case HistoryKind.NewKing:
                    return slot == 0 ? Kingdom(sim, uid) : slot == 1 ? Unit(sim, uid) : "-";
                case HistoryKind.KingDied:
                    return slot == 0 ? Kingdom(sim, uid) : slot == 1 ? Unit(sim, uid) : "-";
                case HistoryKind.WarDeclared:
                    return slot < 2 ? Kingdom(sim, uid) : MetaName(sim, MetaKind.War, Find(sim.Meta.Wars.Headers, uid));
                case HistoryKind.Peace:
                    return slot == 0 ? MetaName(sim, MetaKind.War, Find(sim.Meta.Wars.Headers, uid)) : "-";
                case HistoryKind.CityFounded:
                case HistoryKind.CityRuined:
                    return slot == 0 ? City(sim, uid) : "-";
                case HistoryKind.CityCaptured:
                    return slot == 0 ? City(sim, uid) : Kingdom(sim, uid);
                case HistoryKind.Rebellion:
                    return slot == 0 ? City(sim, uid) : Kingdom(sim, uid);
                case HistoryKind.AllianceFormed:
                    return slot == 0 ? MetaName(sim, MetaKind.Alliance, Find(sim.Meta.Alliances.Headers, uid)) : "-";
                case HistoryKind.CultureFounded:
                    return slot == 0 ? MetaName(sim, MetaKind.Culture, Find(sim.Meta.Cultures.Headers, uid)) : Unit(sim, uid);
                case HistoryKind.LanguageFounded:
                    return slot == 0 ? MetaName(sim, MetaKind.Language, Find(sim.Meta.Languages.Headers, uid)) : Unit(sim, uid);
                case HistoryKind.ReligionFounded:
                    return slot == 0 ? MetaName(sim, MetaKind.Religion, Find(sim.Meta.Religions.Headers, uid)) : Unit(sim, uid);
                case HistoryKind.ClanFounded:
                    return slot == 0 ? MetaName(sim, MetaKind.Clan, Find(sim.Meta.Clans.Headers, uid)) : Unit(sim, uid);
                case HistoryKind.PlotSucceeded:
                case HistoryKind.PlotFailed:
                    return slot == 0 ? MetaName(sim, MetaKind.Plot, Find(sim.Meta.Plots.Headers, uid)) : Unit(sim, uid);
                case HistoryKind.BookWritten:
                    return slot == 0 ? Book(sim, uid) : Unit(sim, uid);
                case HistoryKind.ItemForged:
                    return slot == 0 ? Item(sim, uid) : Unit(sim, uid);
                case HistoryKind.Vassalage:
                case HistoryKind.TributePaid:
                    return slot < 2 ? Kingdom(sim, uid) : "-";
                case HistoryKind.CityPillaged:
                    return slot == 0 ? City(sim, uid) : slot == 1 ? Kingdom(sim, uid) : entry.C.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return "#" + uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        static int Find(Unity.Collections.NativeList<MetaHeader> headers, long uid)
        {
            for (int i = 0; i < headers.Length; i++) if (headers[i].Uid == uid) return i;
            return -1;
        }

        static string Kingdom(SimWorld sim, long uid)
        {
            int i = Find(sim.Meta.Kingdoms.Headers, uid);
            return i >= 0 ? sim.Meta.KingdomName(i) : "#" + uid;
        }

        static string City(SimWorld sim, long uid)
        {
            int i = Find(sim.Civ.Cities.Headers, uid);
            return i >= 0 ? Names.City(sim, i) : "#" + uid;
        }

        static string Unit(SimWorld sim, long uid)
        {
            if (sim.Meta.TryGetBiography(uid, out int biography))
            {
                int nameId = sim.Meta.BiographyTracks[biography].NameId;
                if (nameId >= 0) return sim.Meta.Names.Get(nameId);
            }
            var units = sim.Units.Store;
            for (int i = 0; i < units.HighWater; i++) if (units.Uid[i] == uid) return Names.Unit(sim, i);
            return "#" + uid;
        }

        static string MetaName(SimWorld sim, MetaKind kind, int index)
        {
            if (index < 0) return "?";
            if (kind == MetaKind.Plot && (uint)index < (uint)sim.Meta.Plots.Count)
                return sim.Meta.Names.Get(sim.Meta.Plots.Headers[index].NameId);
            return Names.Meta(sim, kind, index);
        }

        static string Book(SimWorld sim, long uid)
        {
            for (int i = 0; i < sim.Meta.Books.Length; i++)
            {
                var book = sim.Meta.Books[i];
                if (book.Uid != uid) continue;
                if (book.NameId >= 0) return sim.Meta.Names.Get(book.NameId);
                return book.Type < sim.Content.BookTypes.Count ? Loc.Name(sim.Content.BookTypes[book.Type]) : "#" + uid;
            }
            return "#" + uid;
        }

        static string Item(SimWorld sim, long uid)
        {
            for (int i = 0; i < sim.Civ.Items.Length; i++)
            {
                var item = sim.Civ.Items[i];
                if (item.Uid != uid) continue;
                if (item.NameId >= 0) return sim.Meta.Names.Get(item.NameId);
                return item.Type < sim.Content.EquipmentTypes.Count ? Loc.Name(sim.Content.EquipmentTypes[item.Type]) : "#" + uid;
            }
            return "#" + uid;
        }

        static string WarCauseId(WarCause cause) => cause switch
        {
            WarCause.ResourceShortage => "resource_shortage", WarCause.ReligiousDifference => "religious_difference",
            WarCause.CulturalDifference => "cultural_difference", WarCause.Revenge => "revenge", WarCause.AllianceCall => "alliance_call",
            WarCause.RebellionSuppression => "rebellion_suppression", WarCause.SuccessionDispute => "succession_dispute",
            WarCause.TreatyBreach => "treaty_breach", _ => "border_dispute",
        };

        static string ResolveFamineCause(int value)
        {
            var cause = (FamineCause)value;
            if (cause == (FamineCause.Drought | FamineCause.Cold)) return Loc.T("ui.famine_cause_both");
            if (cause == FamineCause.Drought) return Loc.T("ui.famine_cause_drought");
            if (cause == FamineCause.Cold) return Loc.T("ui.famine_cause_cold");
            return "-";
        }

        static string WarGoalId(WarGoal goal) => goal switch
        {
            WarGoal.Tribute => "tribute", WarGoal.Vassalize => "vassalize", WarGoal.Destruction => "destruction",
            WarGoal.Plunder => "plunder", _ => "city_conquest",
        };
    }
}
