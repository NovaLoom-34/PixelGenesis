using PG.Content;
using PG.Sim;

namespace PG.UI
{
    // Display names read from the simulation (never written).
    public static class Names
    {
        public static string Unit(SimWorld sim, int i)
        {
            var u = sim.Units.Store;
            if (i < 0 || i >= u.HighWater) return "?";
            int id = u.NameId[i];
            string species = Loc.Name(sim.Content.Species[u.Species[i]]);
            return id >= 0 ? sim.Meta.Names.Get(id) : species + " #" + (u.Uid[i] % 10000);
        }

        public static string Kingdom(SimWorld sim, int k) => k >= 0 && k < sim.Meta.Kingdoms.Count ? sim.Meta.KingdomName(k) : "-";
        public static string City(SimWorld sim, int c) => c >= 0 && sim.Civ != null && c < sim.Civ.CityCount ? sim.Meta.Names.Get(sim.Civ.Cities.Headers[c].NameId) : "-";

        public static string Meta(SimWorld sim, MetaKind kind, int i)
        {
            if (i < 0) return "-";
            var m = sim.Meta;
            switch (kind)
            {
                case MetaKind.Kingdom: return Kingdom(sim, i);
                case MetaKind.City: return City(sim, i);
                case MetaKind.Culture: return i < m.Cultures.Count ? m.Names.Get(m.Cultures.Headers[i].NameId) : "-";
                case MetaKind.Language: return i < m.Languages.Count ? m.Names.Get(m.Languages.Headers[i].NameId) : "-";
                case MetaKind.Religion: return i < m.Religions.Count ? m.Names.Get(m.Religions.Headers[i].NameId) : "-";
                case MetaKind.Clan: return i < m.Clans.Count ? m.Names.Get(m.Clans.Headers[i].NameId) : "-";
                case MetaKind.Subspecies:
                    var subs = sim.Units.Subspecies;
                    return i < subs.Count ? Loc.Name(sim.Content.Species[subs.Data(i).Species]) + " " + Loc.T("ui.line") + " " + (subs.Header(i).Uid % 1000) : "-";
                case MetaKind.War: return i < m.Wars.Count ? Kingdom(sim, m.Wars.Items[i].Attacker) + " / " + Kingdom(sim, m.Wars.Items[i].Defender) : "-";
                case MetaKind.Alliance: return i < m.Alliances.Count ? m.Names.Get(m.Alliances.Headers[i].NameId) : "-";
            }
            return "-";
        }

        public static string Trait(ContentDB db, int t) => t >= 0 && t < db.UnitTraits.Count ? Loc.Name(db.UnitTraits[t]) : "-";
    }
}
