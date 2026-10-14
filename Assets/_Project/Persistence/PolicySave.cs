using System.Collections.Generic;
using System.IO;
using PG.Sim;

namespace PG.Persistence
{
    // Save section 16 (Bölüm 10, DECISIONS #98): kingdom policies, pending proposals and treaties
    // proposals. Lives outside MetaSave so the raw MetaStore byte format never shifts;
    // missing in older files -> balanced policies, no proposals.
    public static class PolicySave
    {
        public static byte[] Write(SimWorld sim)
        {
            var meta = sim.Meta;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((byte)9); // format 9 adds tax policy; formats 1-8 remain readable

            int activePolicies = 0;
            foreach (var kv in meta.Policies) if (meta.KingdomAlive(kv.Key)) activePolicies++;
            w.Write(activePolicies);
            foreach (var kv in meta.Policies)
            {
                if (!meta.KingdomAlive(kv.Key)) continue;
                w.Write(kv.Key);
                w.Write(kv.Value.Food); w.Write(kv.Value.Housing); w.Write(kv.Value.Production);
                w.Write(kv.Value.Defense); w.Write(kv.Value.Culture); w.Write(kv.Value.Tax);
            }

            w.Write(meta.Proposals.Count);
            foreach (var p in meta.Proposals)
            {
                w.Write(p.Id); w.Write((byte)p.Kind); w.Write(p.From); w.Write(p.To); w.Write(p.Tick);
                w.Write(p.PeaceTerms.TributeGold); w.Write(p.PeaceTerms.CityToTransfer);
                w.Write(p.PeaceTerms.VassalizeProposer); w.Write(p.PeaceTerms.NonAggressionYears);
            }
            w.Write(meta.Treaties.Count);
            foreach (var treaty in meta.Treaties)
            {
                w.Write(treaty.A); w.Write(treaty.B); w.Write((byte)treaty.Kind);
                w.Write(treaty.StartTick); w.Write(treaty.EndTick);
                w.Write(treaty.TributeRate);
            }
            w.Write(meta.WarPlans.Count);
            foreach (var plan in meta.WarPlans)
            {
                w.Write(plan.War); w.Write((byte)plan.Cause); w.Write((byte)plan.Goal);
            }
            w.Write(meta.ArmyLogistics.Count);
            foreach (var logistics in meta.ArmyLogistics)
            {
                w.Write(logistics.Army); w.Write(logistics.Rations); w.Write(logistics.Morale);
                w.Write(logistics.SiegeProgress);
            }
            return ms.ToArray();
        }

        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            var meta = sim.Meta;
            meta.Policies.Clear();
            meta.Proposals.Clear();
            meta.Treaties.Clear();
            meta.WarPlans.Clear();
            meta.ArmyLogistics.Clear();
            meta.RebuildProposalIds();
            if (raw == null || raw.Length == 0) return; // older save: defaults
            try
            {
                using var ms = new MemoryStream(raw);
                using var r = new BinaryReader(ms);
                byte format = r.ReadByte();
                if (format < 1 || format > 9) return;
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int k = r.ReadInt32();
                    var p = new KingdomPolicy { Food = r.ReadSingle(), Housing = r.ReadSingle(), Production = r.ReadSingle(), Defense = r.ReadSingle(), Culture = r.ReadSingle(), Tax = 0.5f };
                    if (format >= 9)
                    {
                        float tax = r.ReadSingle();
                        p.Tax = float.IsNaN(tax) || float.IsInfinity(tax) ? 0.5f : System.Math.Max(0f, System.Math.Min(1f, tax));
                    }
                    if (meta.KingdomAlive(k)) meta.Policies[k] = p;
                }
                int proposals = r.ReadInt32();
                for (int i = 0; i < proposals; i++)
                {
                    var p = new DiplomacyProposal { Id = r.ReadInt32(), Kind = (ProposalKind)r.ReadByte(), From = r.ReadInt32(),
                        To = r.ReadInt32(), Tick = r.ReadInt64(), PeaceTerms = new PeaceTerms { CityToTransfer = -1 } };
                    if (format >= 5)
                    {
                        p.PeaceTerms.TributeGold = r.ReadInt32();
                        p.PeaceTerms.CityToTransfer = r.ReadInt32();
                        p.PeaceTerms.VassalizeProposer = r.ReadBoolean();
                        p.PeaceTerms.NonAggressionYears = r.ReadInt32();
                    }
                    bool validPeaceTerms = p.Kind != ProposalKind.Peace || p.PeaceTerms.TributeGold >= 0
                        && p.PeaceTerms.CityToTransfer >= -1 && p.PeaceTerms.NonAggressionYears >= 0
                        && p.PeaceTerms.NonAggressionYears <= 100;
                    if ((byte)p.Kind <= (byte)(format >= 8 ? ProposalKind.TradeEmbargo : format >= 3 ? ProposalKind.Vassalage : ProposalKind.NonAggression)
                        && validPeaceTerms && meta.KingdomAlive(p.From) && meta.KingdomAlive(p.To)) meta.Proposals.Add(p);
                }
                if (format >= 2)
                {
                    int treaties = r.ReadInt32();
                    for (int i = 0; i < treaties; i++)
                    {
                        var treaty = new DiplomacyTreaty
                        {
                            A = r.ReadInt32(), B = r.ReadInt32(), Kind = (TreatyKind)r.ReadByte(),
                            StartTick = r.ReadInt64(), EndTick = r.ReadInt64(),
                        };
                        if (format >= 3) treaty.TributeRate = r.ReadSingle();
                        bool validPair = treaty.Kind == TreatyKind.Vassalage ? treaty.A != treaty.B : treaty.A < treaty.B;
                        bool validKind = (byte)treaty.Kind <= (byte)(format >= 8 ? TreatyKind.TradeEmbargo : format >= 3 ? TreatyKind.Vassalage : TreatyKind.NonAggression);
                        bool validRate = treaty.Kind != TreatyKind.Vassalage || !float.IsNaN(treaty.TributeRate)
                            && !float.IsInfinity(treaty.TributeRate) && treaty.TributeRate >= 0.01f && treaty.TributeRate <= 0.2f;
                        if (validKind && validPair && validRate && treaty.EndTick > treaty.StartTick
                            && meta.KingdomAlive(treaty.A) && meta.KingdomAlive(treaty.B)) meta.Treaties.Add(treaty);
                    }
                }
                if (format >= 4)
                {
                    int warPlans = r.ReadInt32();
                    if (warPlans < 0 || warPlans > meta.Wars.Count) throw new InvalidDataException("invalid war plan count");
                    for (int i = 0; i < warPlans; i++)
                    {
                        int war = r.ReadInt32();
                        var cause = (WarCause)r.ReadByte();
                        var goal = (WarGoal)r.ReadByte();
                        if (war >= 0 && war < meta.Wars.Count && (byte)cause <= (byte)WarCause.SuccessionDispute
                            && (byte)goal <= (byte)WarGoal.Plunder)
                            meta.SetWarPlan(war, cause, goal);
                    }
                }
                if (format >= 6)
                {
                    int armies = r.ReadInt32();
                    if (armies < 0 || armies > meta.Armies.Length) throw new InvalidDataException("invalid army logistics count");
                    for (int i = 0; i < armies; i++)
                    {
                        int army = r.ReadInt32();
                        int rations = r.ReadInt32();
                        byte morale = r.ReadByte();
                        float siegeProgress = format >= 7 ? r.ReadSingle() : 0f;
                        if (army >= 0 && army < meta.Armies.Length && rations >= 0 && morale <= 100
                            && !float.IsNaN(siegeProgress) && !float.IsInfinity(siegeProgress) && siegeProgress >= 0f
                            && meta.Armies[army].State != ArmyState.Disbanded)
                            meta.SetArmyLogistics(army, rations, morale, siegeProgress);
                    }
                }
                meta.RebuildProposalIds();
            }
            catch (System.Exception e)
            {
                warnings?.Add($"policy section unreadable ({e.Message}); defaults used");
            }
        }
    }
}
