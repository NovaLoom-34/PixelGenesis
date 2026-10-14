using System.Collections.Generic;
using System.IO;
using System.Text;
using PG.Content;
using PG.Sim;
using Unity.Mathematics;

namespace PG.Persistence
{
    // Save section 17 (Bölüm 5.12, DECISIONS #103): Kingdom Inventions & Active Vehicles.
    // Preserves discovered technologies, research progress and vehicles across saves.
    public static class InventionSave
    {
        public static byte[] Write(SimWorld sim)
        {
            var civ = sim.Civ;
            var db = sim.Content;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write((byte)1); // format version

                // 1. Kingdom Inventions
                w.Write(civ.KingdomInventions.Count);
                foreach (var kv in civ.KingdomInventions)
                {
                    w.Write(kv.Key); // kingdom index
                    w.Write(kv.Value.Count);
                    foreach (int inv in kv.Value)
                    {
                        string id = inv >= 0 && inv < db.Inventions.Count ? db.Inventions[inv].Id : "";
                        w.Write(id);
                    }
                }

                // 2. Current Research & Progress
                w.Write(civ.CurrentResearch.Count);
                foreach (var kv in civ.CurrentResearch)
                {
                    w.Write(kv.Key); // kingdom
                    string invId = kv.Value >= 0 && kv.Value < db.Inventions.Count ? db.Inventions[kv.Value].Id : "";
                    w.Write(invId);
                    int progress = civ.ResearchProgress.TryGetValue(kv.Key, out int pr) ? pr : 0;
                    w.Write(progress);
                }

                // 3. Vehicles
                int vCount = civ.Vehicles.IsCreated ? civ.Vehicles.Length : 0;
                w.Write(vCount);
                for (int i = 0; i < vCount; i++)
                {
                    var v = civ.Vehicles[i];
                    w.Write((byte)v.Type);
                    w.Write(v.Pos.x); w.Write(v.Pos.y);
                    w.Write(v.Dir.x); w.Write(v.Dir.y);
                    w.Write(v.City);
                    w.Write(v.Kingdom);
                    w.Write(v.Speed);
                    w.Write(v.Hp);
                    w.Write(v.CargoRes);
                    w.Write(v.CargoAmount);
                    w.Write(v.Destination.x); w.Write(v.Destination.y);
                    w.Write(v.Origin.x); w.Write(v.Origin.y);
                    w.Write((byte)v.State);
                }

                return ms.ToArray();
            }
        }

        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            var civ = sim.Civ;
            var db = sim.Content;
            civ.KingdomInventions.Clear();
            civ.CurrentResearch.Clear();
            civ.ResearchProgress.Clear();
            if (civ.Vehicles.IsCreated) civ.Vehicles.Clear();
            if (raw == null || raw.Length == 0) return; // older save: clean defaults

            try
            {
                using (var ms = new MemoryStream(raw))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    if (r.ReadByte() != 1) return;

                    // 1. Kingdom Inventions
                    int kCount = r.ReadInt32();
                    for (int i = 0; i < kCount; i++)
                    {
                        int k = r.ReadInt32();
                        int invCount = r.ReadInt32();
                        var set = new HashSet<int>();
                        for (int j = 0; j < invCount; j++)
                        {
                            string id = r.ReadString();
                            int inv = db.Inventions.IdOrDefault(id);
                            if (inv >= 0) set.Add(inv);
                        }
                        if (sim.Meta.KingdomAlive(k)) civ.KingdomInventions[k] = set;
                    }

                    // 2. Current Research
                    int resCount = r.ReadInt32();
                    for (int i = 0; i < resCount; i++)
                    {
                        int k = r.ReadInt32();
                        string id = r.ReadString();
                        int progress = r.ReadInt32();
                        int inv = db.Inventions.IdOrDefault(id);
                        if (sim.Meta.KingdomAlive(k))
                        {
                            civ.CurrentResearch[k] = inv;
                            civ.ResearchProgress[k] = progress;
                        }
                    }

                    // 3. Vehicles
                    int vCount = r.ReadInt32();
                    for (int i = 0; i < vCount; i++)
                    {
                        var v = new VehicleData
                        {
                            Type = (VehicleType)r.ReadByte(),
                            Pos = new float2(r.ReadSingle(), r.ReadSingle()),
                            Dir = new float2(r.ReadSingle(), r.ReadSingle()),
                            City = r.ReadInt32(),
                            Kingdom = r.ReadInt32(),
                            Speed = r.ReadSingle(),
                            Hp = r.ReadSingle(),
                            CargoRes = r.ReadUInt16(),
                            CargoAmount = r.ReadInt32(),
                            Destination = new int2(r.ReadInt32(), r.ReadInt32()),
                            Origin = new int2(r.ReadInt32(), r.ReadInt32()),
                            State = (VehicleState)r.ReadByte(),
                            PathHandle = -1,
                        };
                        if (civ.Vehicles.IsCreated && sim.Meta.KingdomAlive(v.Kingdom))
                            civ.Vehicles.Add(v);
                    }
                }
            }
            catch (System.Exception e)
            {
                warnings?.Add($"inventions section unreadable ({e.Message}); defaults used");
            }
        }
    }
}
