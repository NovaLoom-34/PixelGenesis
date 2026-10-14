using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PG.Content;
using PG.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.3: shape/icon/colors/pattern indices; the render side draws them (BannerAtlas, Bölüm 8).
    public struct Banner
    {
        public byte Shape, Icon, BgColor, FgColor, Pattern;
        public const int Shapes = 12, Icons = 64, Colors = 16, Patterns = 6;

        public static Banner Random(ref SimRandom rng)
        {
            var b = new Banner
            {
                Shape = (byte)rng.Range(0, Shapes), Icon = (byte)rng.Range(0, Icons), BgColor = (byte)rng.Range(0, Colors),
                FgColor = (byte)rng.Range(0, Colors), Pattern = (byte)rng.Range(0, Patterns),
            };
            if (b.FgColor == b.BgColor) b.FgColor = (byte)((b.BgColor + 8) % Colors);
            return b;
        }
    }

    // Bölüm 6.4 (culture, religion, language: Bölüm 6.7-6.9; -1 until then).
    public struct KingdomData
    {
        public EntityId King, Heir;
        public int Capital;
        public int Culture, Religion, Language;
        public int Alliance;
        public ushort RulerTitle;
        public int Treasury;
        public long LastWarEndTick;
        public long SuccessionTick;     // loyalty -10 for a year after a new king
        public byte Phonology;          // name set until languages exist (DECISIONS #57)
        public ushort Species;
        public int Root;                // name root (NamePool id)
        public int Trait;               // kingdom_traits.json index, -1 = none
        public int Cities, Soldiers;    // monthly caches
        public float Power;
    }

    public enum WarResult : byte { Ongoing, Peace, AttackerWon, DefenderWon, Vanished }
    public enum WarCause : byte { BorderDispute, ResourceShortage, ReligiousDifference, CulturalDifference, Revenge, AllianceCall, RebellionSuppression, SuccessionDispute, TreatyBreach }
    public enum WarGoal : byte { CityConquest, Tribute, Vassalize, Destruction, Plunder }

    // Kept outside WarData so the unmanaged MetaStore save layout stays stable.
    public struct WarPlan { public int War; public WarCause Cause; public WarGoal Goal; }

    public struct WarData
    {
        public ushort Type;
        public int Attacker, Defender;
        public FixedList64Bytes<int> AttackerSide, DefenderSide;
        public long StartTick, EndTick;
        public int CasualtiesA, CasualtiesD;
        public int CapturedCities;
        public int TargetCity;
        public WarResult Result;
    }

    public enum ArmyState : byte { Gathering, Marching, Sieging, Returning, Disbanded }

    public struct ArmyData
    {
        public int Kingdom, War, HomeCity, TargetCity;
        public EntityId Captain;
        public int2 RallyTile;
        public ArmyState State;
        public int Soldiers, StartSoldiers; // monthly census
        public long StateTick;
    }
    // Kept outside ArmyData so the unmanaged MetaStore save layout remains compatible.
    public struct ArmyLogistics { public int Army, Rations; public byte Morale; public float SiegeProgress; }

    public struct AllianceData
    {
        public FixedList64Bytes<int> Kingdoms;
        public int Leader;
    }

    // Bölüm 6.7-6.13
    public struct CultureData { public int OriginKingdom, Generation, ParentCulture, OriginCity; }
    public struct LanguageData { public byte Phonology, Script; public int ParentLanguage, WrittenWorks, OriginCity; }
    public struct ReligionData { public long FounderUid; public int HolyCity, DeityTrait, Temples; }
    public struct ClanData { public long FounderUid; public EntityId Chief; public int Kings; }
    public enum PlotState : byte { Active, Succeeded, Failed }
    public struct PlotData
    {
        public ushort Def;
        public EntityId Initiator;
        public FixedList64Bytes<EntityId> Members; // 7 at most
        public float Progress;                     // 0-1
        public int TargetMeta;                     // kingdom/city/etc. depending on the plot, -1 = none
        public long StartTick;
        public PlotState State;
    }
    public struct BookData
    {
        public long Uid;
        public ushort Type;
        public int Language, Culture, Religion;
        public long AuthorUid;
        public int NameId, Library, City;
        public long WrittenTick;
        public byte Condition, Dead;
    }
    public struct HistoryEntry { public long Tick; public ushort Template; public long A, B, C; public int Extra; }
    // Stored in a versioned sidecar so HistoryEntry's raw save layout remains compatible.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HistoryMetadata { public long EventId, CauseEventId; public ushort Importance, Flags; }

    [Flags]
    public enum HistoryMetadataFlags : ushort { None = 0, DisasterMigration = 1 }

    public enum BiographyEventKind : byte { Birth, Partnership, Child, Battle, Office, Death, Milestone, LegendaryItem }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BiographyEvent { public long Tick, RelatedUid; public int Value; public BiographyEventKind Kind; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BiographyTrack
    {
        public long Uid, BirthTick, DeathTick, KillerUid, MateUid, MotherUid, FatherUid;
        public int NameId;
        public ushort Species, KillsAtDeath;
        public byte LevelAtDeath, EventCount, NextEvent, DeathCause, IsDead;
    }

    public enum WorldSummaryKind : byte { Century, EraTransition }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WorldSummary
    {
        public int StartYear, EndYear, Population, Cities, Kingdoms, Wars, Sick, Undead;
        public int LargestKingdomPopulation, DeadliestWarCasualties, FamousScore, CulturesFounded, CulturesEnded;
        public long LargestKingdomUid, DeadliestWarUid, FamousPersonUid;
        public ushort EraFrom, EraTo;
        public WorldSummaryKind Kind;
    }

    // Kept in a versioned list beside WorldSummary so existing v3 summary bytes stay readable.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WorldSummaryPopulationSplit
    {
        public int Civilization, Wildlife;
        public static WorldSummaryPopulationSplit Unknown => new WorldSummaryPopulationSplit { Civilization = -1, Wildlife = -1 };
    }

    public static class HistoryImportanceScore
    {
        public static ushort Calculate(int baseImportance, int participantScale)
        {
            int baseline = Math.Max(0, Math.Min(100, baseImportance));
            int scalePercent = Math.Max(0, Math.Min(20, participantScale));
            int score = baseline * (100 + scalePercent) / 100;
            return (ushort)Math.Min(100, score);
        }

        public static int ArmyScale(int soldiers)
            => soldiers <= 0 ? 0 : Math.Min(20, (int)Math.Floor(Math.Log(soldiers + 1d, 2d) * 3d));

        public static ushort CalculateWithScales(int baseImportance, int participantScale, int armyScale)
        {
            int combinedScale = Math.Min(20, Math.Max(0, participantScale) + Math.Max(0, armyScale));
            return Calculate(baseImportance, combinedScale);
        }
    }

    public readonly struct MetaFoundedEvent : ISimEvent { public readonly MetaKind Kind; public readonly int Index; public MetaFoundedEvent(MetaKind k, int i) { Kind = k; Index = i; } }
    public readonly struct MetaDiedEvent : ISimEvent { public readonly MetaKind Kind; public readonly int Index; public MetaDiedEvent(MetaKind k, int i) { Kind = k; Index = i; } }
    public readonly struct PlotFinishedEvent : ISimEvent { public readonly int Plot; public readonly bool Success; public PlotFinishedEvent(int p, bool s) { Plot = p; Success = s; } }

    public readonly struct KingdomFoundedEvent : ISimEvent { public readonly int Kingdom; public KingdomFoundedEvent(int k) { Kingdom = k; } }
    public readonly struct NewKingEvent : ISimEvent { public readonly int Kingdom; public readonly long KingUid; public NewKingEvent(int k, long uid) { Kingdom = k; KingUid = uid; } }
    public readonly struct WarDeclaredEvent : ISimEvent
    {
        public readonly int War, Attacker, Defender;
        public readonly WarCause Cause;
        public readonly WarGoal Goal;
        public WarDeclaredEvent(int w, int a, int d, WarCause cause = WarCause.BorderDispute, WarGoal goal = WarGoal.CityConquest)
        { War = w; Attacker = a; Defender = d; Cause = cause; Goal = goal; }
    }
    public readonly struct PeaceEvent : ISimEvent { public readonly int War; public PeaceEvent(int w) { War = w; } }
    public readonly struct CityCapturedEvent : ISimEvent { public readonly int City, From, To; public CityCapturedEvent(int c, int from, int to) { City = c; From = from; To = to; } }
    public readonly struct CityPillagedEvent : ISimEvent
    {
        public readonly int City, Kingdom, Gold, Rations;
        public CityPillagedEvent(int city, int kingdom, int gold, int rations) { City = city; Kingdom = kingdom; Gold = gold; Rations = rations; }
    }
    public readonly struct RebellionEvent : ISimEvent { public readonly int City, From, To; public RebellionEvent(int c, int from, int to) { City = c; From = from; To = to; } }
    public readonly struct AllianceFormedEvent : ISimEvent { public readonly int Alliance; public AllianceFormedEvent(int a) { Alliance = a; } }
    public readonly struct VassalageFormedEvent : ISimEvent { public readonly int Subject, Overlord; public VassalageFormedEvent(int subject, int overlord) { Subject = subject; Overlord = overlord; } }
    public readonly struct TributePaidEvent : ISimEvent { public readonly int Subject, Overlord, Amount; public TributePaidEvent(int subject, int overlord, int amount) { Subject = subject; Overlord = overlord; Amount = amount; } }

    // Bilateral proposals need both sides' acceptance; the war
    // declaration stays unilateral. Proposals live outside the meta store so the raw
    // KingdomData/MetaHeader save format never shifts.
    // Keep the first two values stable: they are stored in section 16 of existing saves.
    public enum ProposalKind : byte { Peace = 0, Alliance = 1, TradeAgreement = 2, NonAggression = 3, Vassalage = 4, TradeEmbargo = 5 }
    public enum TreatyKind : byte { TradeAgreement = 0, NonAggression = 1, Vassalage = 2, TradeEmbargo = 3 }
    public enum DiplomaticStatus : byte { Neutral, TradeAgreement, NonAggression, Vassalage, Alliance, War, TradeEmbargo }
    // Vassalage uses A=subject and B=overlord; other treaty pairs are normalized A<B.
    public struct DiplomacyTreaty { public int A, B; public TreatyKind Kind; public long StartTick, EndTick; public float TributeRate; }
    public struct PeaceTerms { public int TributeGold, CityToTransfer, NonAggressionYears; public bool VassalizeProposer; }
    public struct DiplomacyProposal { public int Id; public ProposalKind Kind; public int From, To; public long Tick; public PeaceTerms PeaceTerms; }
    public readonly struct DiplomacyProposalEvent : ISimEvent
    {
        public readonly int Id, From, To; public readonly ProposalKind Kind; public bool Alliance => Kind == ProposalKind.Alliance;
        public DiplomacyProposalEvent(int id, int from, int to, bool alliance) : this(id, from, to, alliance ? ProposalKind.Alliance : ProposalKind.Peace) { }
        public DiplomacyProposalEvent(int id, int from, int to, ProposalKind kind) { Id = id; From = from; To = to; Kind = kind; }
    }
    public readonly struct DiplomacyProposalResolvedEvent : ISimEvent
    {
        public readonly int Id, From, To; public readonly ProposalKind Kind; public readonly bool Accepted;
        public bool Alliance => Kind == ProposalKind.Alliance;
        public DiplomacyProposalResolvedEvent(int id, int from, int to, bool alliance, bool accepted) : this(id, from, to, alliance ? ProposalKind.Alliance : ProposalKind.Peace, accepted) { }
        public DiplomacyProposalResolvedEvent(int id, int from, int to, ProposalKind kind, bool accepted) { Id = id; From = from; To = to; Kind = kind; Accepted = accepted; }
    }

    // Kingdom-wide city policy weights (food / housing / production / defense / culture);
    // shares sum to 1. The city planner multiplies its needs scores with 0.5 + share.
    public struct KingdomPolicy
    {
        public float Food, Housing, Production, Defense, Culture, Tax;

        public static KingdomPolicy Balanced => new KingdomPolicy { Food = 0.2f, Housing = 0.2f, Production = 0.2f, Defense = 0.2f, Culture = 0.2f, Tax = 0.5f };

        public static KingdomPolicy Normalized(float food, float housing, float production, float defense, float culture, float tax = 50f)
        {
            if (float.IsNaN(tax) || float.IsInfinity(tax)) tax = 50f;
            var p = new KingdomPolicy
            {
                Food = Math.Max(0f, food), Housing = Math.Max(0f, housing), Production = Math.Max(0f, production),
                Defense = Math.Max(0f, defense), Culture = Math.Max(0f, culture), Tax = Math.Max(0f, Math.Min(100f, tax)) / 100f,
            };
            float sum = p.Food + p.Housing + p.Production + p.Defense + p.Culture;
            if (sum <= 0f) return Balanced;
            p.Food /= sum; p.Housing /= sum; p.Production /= sum; p.Defense /= sum; p.Culture /= sum;
            return p;
        }
    }

    // Politics and names: kingdoms, wars, armies, alliances, opinions, name pool (Bölüm 6.1-6.6).
    public sealed class MetaWorld : IDisposable
    {
        public readonly MetaStore<KingdomData> Kingdoms = new MetaStore<KingdomData>(MetaKind.Kingdom);
        public readonly MetaStore<WarData> Wars = new MetaStore<WarData>(MetaKind.War);
        public readonly MetaStore<AllianceData> Alliances = new MetaStore<AllianceData>(MetaKind.Alliance);
        public NativeList<ArmyData> Armies;
        public readonly MetaStore<CultureData> Cultures = new MetaStore<CultureData>(MetaKind.Culture);
        public readonly MetaStore<LanguageData> Languages = new MetaStore<LanguageData>(MetaKind.Language);
        public readonly MetaStore<ReligionData> Religions = new MetaStore<ReligionData>(MetaKind.Religion);
        public readonly MetaStore<ClanData> Clans = new MetaStore<ClanData>(MetaKind.Clan);
        public readonly MetaStore<PlotData> Plots = new MetaStore<PlotData>(MetaKind.Plot);
        public NativeList<BookData> Books;
        public NativeList<HistoryEntry> History;
        public NativeList<HistoryMetadata> HistoryDetails;
        public NativeList<BiographyTrack> BiographyTracks;
        public NativeList<BiographyEvent> BiographyEvents;
        public NativeList<WorldSummary> WorldSummaries;
        public NativeList<WorldSummaryPopulationSplit> WorldSummaryPopulationSplits;
        NativeList<long> _recentDisasterCities;
        long _recentDisasterCacheTick = long.MinValue, _recentDisasterCacheLookback;
        readonly Dictionary<long, int> _biographyIndex = new Dictionary<long, int>(MaxBiographyTracks);
        public long NextHistoryEventId = 1;
        const int HistorySidecarMagic = 0x48535431; // HST1
        const int HistorySidecarVersion = 4;
        public long NextBookUid = 1;
        public const int MaxHistory = 200000;
        public const int MaxBiographyTracks = 16000, BiographyEventsPerUnit = 6;
        public const int MaxWorldSummaries = 4096;
        public readonly NamePool Names = new NamePool();
        readonly Dictionary<long, short> _opinion = new Dictionary<long, short>();
        readonly HashSet<long> _atWar = new HashSet<long>();   // derived from Wars
        public readonly List<DiplomacyProposal> Proposals = new List<DiplomacyProposal>(8); // section 16
        public readonly List<DiplomacyTreaty> Treaties = new List<DiplomacyTreaty>(8); // section 16; alliance/war remain derived
        public readonly List<WarPlan> WarPlans = new List<WarPlan>(8); // section 16; stable WarData layout remains untouched
        public readonly List<ArmyLogistics> ArmyLogistics = new List<ArmyLogistics>(8); // section 16; stable ArmyData layout remains untouched
        int _nextProposalId = 1;
        public readonly Dictionary<int, KingdomPolicy> Policies = new Dictionary<int, KingdomPolicy>(); // section 16
        public readonly ContentDB Content;
        public readonly int WarConquest, WarRebellion, WarReligious, WarSuccession;
        public readonly int[] SpeciesPhonology;                // default name set per species
        public readonly float[] TraitOpinion, TraitRebelResist, TraitWarChance, TraitArmySize, TraitGold; // per kingdom trait (no string lookups in ticks)
        public readonly int TrPeaceful, TrBloodthirsty, TrHonest, TrDeceitful;                 // unit traits that move opinions
        public readonly int CulTrueRoots, CulLibraryKeepers, CulScholars, CulBookBurners, CulPacifist, CulPureBlood; // Bölüm 6.7-6.12 trait ids
        public readonly int RelMissionaries, RelHolyWar, RelInquisition, LangEternal, LangManyDialects;
        public readonly int[] LangScripts;

        public MetaWorld(ContentDB content)
        {
            Content = content;
            Armies = new NativeList<ArmyData>(16, Allocator.Persistent);
            Books = new NativeList<BookData>(16, Allocator.Persistent);
            History = new NativeList<HistoryEntry>(256, Allocator.Persistent);
            HistoryDetails = new NativeList<HistoryMetadata>(256, Allocator.Persistent);
            BiographyTracks = new NativeList<BiographyTrack>(256, Allocator.Persistent);
            BiographyEvents = new NativeList<BiographyEvent>(256 * BiographyEventsPerUnit, Allocator.Persistent);
            WorldSummaries = new NativeList<WorldSummary>(32, Allocator.Persistent);
            WorldSummaryPopulationSplits = new NativeList<WorldSummaryPopulationSplit>(32, Allocator.Persistent);
            _recentDisasterCities = new NativeList<long>(32, Allocator.Persistent);
            WarConquest = Math.Max(0, content.WarTypes.IdOrDefault("war.conquest"));
            WarRebellion = Math.Max(0, content.WarTypes.IdOrDefault("war.rebellion"));
            WarReligious = Math.Max(0, content.WarTypes.IdOrDefault("war.religious"));
            WarSuccession = content.WarTypes.IdOrDefault("war.succession");
            SpeciesPhonology = new int[content.Species.Count];
            var sets = content.NameSets;
            for (int s = 0; s < SpeciesPhonology.Length; s++) SpeciesPhonology[s] = Math.Max(0, sets.IndexOf("lang.plain_speech"));
            int kt = content.KingdomTraits.Count;
            TraitOpinion = new float[kt]; TraitRebelResist = new float[kt]; TraitWarChance = new float[kt]; TraitArmySize = new float[kt]; TraitGold = new float[kt];
            for (int t = 0; t < kt; t++)
            {
                var d = content.KingdomTraits[t];
                TraitOpinion[t] = d.Add("opinion");
                TraitRebelResist[t] = d.Pct("rebellionResist");
                TraitWarChance[t] = d.Pct("warChance");
                TraitArmySize[t] = d.Pct("armySize");
                TraitGold[t] = d.Pct("gold");
            }
            TrPeaceful = content.UnitTraits.IdOrDefault("tr.peaceful");
            TrBloodthirsty = content.UnitTraits.IdOrDefault("tr.bloodthirsty");
            TrHonest = content.UnitTraits.IdOrDefault("tr.honest");
            TrDeceitful = content.UnitTraits.IdOrDefault("tr.deceitful");
            CulTrueRoots = content.CultureTraits.IdOrDefault("cul.true_roots");
            CulLibraryKeepers = content.CultureTraits.IdOrDefault("cul.library_keepers");
            CulScholars = content.CultureTraits.IdOrDefault("cul.scholars");
            CulBookBurners = content.CultureTraits.IdOrDefault("cul.book_burners");
            CulPacifist = content.CultureTraits.IdOrDefault("cul.pacifist_creed");
            CulPureBlood = content.CultureTraits.IdOrDefault("cul.pure_blood");
            RelMissionaries = content.ReligionTraits.IdOrDefault("rel.missionaries");
            RelHolyWar = content.ReligionTraits.IdOrDefault("rel.holy_war");
            RelInquisition = content.ReligionTraits.IdOrDefault("rel.inquisition");
            LangEternal = content.LanguageTraits.IdOrDefault("lang.eternal_text");
            LangManyDialects = content.LanguageTraits.IdOrDefault("lang.many_dialects");
            LangScripts = new[] { content.LanguageTraits.IdOrDefault("lang.written_script"), content.LanguageTraits.IdOrDefault("lang.runic"), content.LanguageTraits.IdOrDefault("lang.pictographic") };
        }

        public ref KingdomData Kingdom(int k) => ref Kingdoms.Data(k);
        public bool KingdomAlive(int k) => Kingdoms.IsAlive(k);

        // --- opinions (-100..100), stored per ordered pair ---

        static long Key(int a, int b) => ((long)a << 32) | (uint)b;
        public short Opinion(int a, int b) => _opinion.TryGetValue(Key(a, b), out short v) ? v : (short)0;
        public void SetOpinion(int a, int b, int v) => _opinion[Key(a, b)] = (short)math.clamp(v, -100, 100);

        // --- war lookups ---

        public bool AtWar(int a, int b) => a >= 0 && b >= 0 && a != b && _atWar.Contains(Key(math.min(a, b), math.max(a, b)));

        public void RebuildWarPairs()
        {
            _atWar.Clear();
            for (int w = 0; w < Wars.Count; w++)
            {
                if (!Wars.IsAlive(w)) continue;
                var d = Wars.Items[w];
                foreach (int x in d.AttackerSide)
                    foreach (int y in d.DefenderSide)
                        if (x != y) _atWar.Add(Key(math.min(x, y), math.max(x, y)));
            }
        }

        public int WarBetween(int a, int b)
        {
            for (int w = 0; w < Wars.Count; w++)
            {
                if (!Wars.IsAlive(w)) continue;
                var d = Wars.Items[w];
                if (d.AttackerSide.Contains(a) && d.DefenderSide.Contains(b) || d.AttackerSide.Contains(b) && d.DefenderSide.Contains(a)) return w;
            }
            return -1;
        }

        public int ActiveWars(int k)
        {
            int n = 0;
            for (int w = 0; w < Wars.Count; w++)
            {
                if (!Wars.IsAlive(w)) continue;
                var d = Wars.Items[w];
                if (d.AttackerSide.Contains(k) || d.DefenderSide.Contains(k)) n++;
            }
            return n;
        }

        public void SetWarPlan(int war, WarCause cause, WarGoal goal)
        {
            if (war < 0 || war >= Wars.Count) return;
            for (int i = 0; i < WarPlans.Count; i++)
            {
                if (WarPlans[i].War != war) continue;
                WarPlans[i] = new WarPlan { War = war, Cause = cause, Goal = goal };
                return;
            }
            WarPlans.Add(new WarPlan { War = war, Cause = cause, Goal = goal });
        }

        public bool TryGetWarPlan(int war, out WarPlan plan)
        {
            for (int i = 0; i < WarPlans.Count; i++)
                if (WarPlans[i].War == war) { plan = WarPlans[i]; return true; }
            plan = default;
            return false;
        }

        public void SetArmyLogistics(int army, int rations, int morale, float siegeProgress = 0f)
        {
            if (army < 0 || army >= Armies.Length) return;
            if (float.IsNaN(siegeProgress) || float.IsInfinity(siegeProgress)) siegeProgress = 0f;
            var value = new ArmyLogistics { Army = army, Rations = Math.Max(0, rations), Morale = (byte)math.clamp(morale, 0, 100),
                SiegeProgress = math.max(0f, siegeProgress) };
            for (int i = 0; i < ArmyLogistics.Count; i++)
                if (ArmyLogistics[i].Army == army) { ArmyLogistics[i] = value; return; }
            ArmyLogistics.Add(value);
        }

        public bool TryGetArmyLogistics(int army, out ArmyLogistics logistics)
        {
            for (int i = 0; i < ArmyLogistics.Count; i++)
                if (ArmyLogistics[i].Army == army) { logistics = ArmyLogistics[i]; return true; }
            logistics = new ArmyLogistics { Army = army, Morale = 100 };
            return false;
        }

        public void RemoveArmyLogistics(int army)
        {
            for (int i = ArmyLogistics.Count - 1; i >= 0; i--)
                if (ArmyLogistics[i].Army == army) ArmyLogistics.RemoveAt(i);
        }

        // --- diplomacy proposals + kingdom policy (Bölüm 10, DECISIONS #98) ---

        // One pending proposal per kind and pair; returns the (existing or new) id.
        public int AddProposal(ProposalKind kind, int from, int to, long tick)
        {
            foreach (var p in Proposals)
                if (p.Kind == kind && p.From == from && p.To == to) return p.Id;
            int id = _nextProposalId++;
            Proposals.Add(new DiplomacyProposal { Id = id, Kind = kind, From = from, To = to, Tick = tick,
                PeaceTerms = kind == ProposalKind.Peace ? new PeaceTerms { CityToTransfer = -1 } : default });
            return id;
        }

        public DiplomacyProposal AddPeaceProposal(int from, int to, long tick, PeaceTerms terms)
        {
            terms.TributeGold = Math.Max(0, terms.TributeGold);
            terms.CityToTransfer = Math.Max(-1, terms.CityToTransfer);
            if (terms.NonAggressionYears < 0) terms.NonAggressionYears = 0;
            if (terms.NonAggressionYears > 100) terms.NonAggressionYears = 100;
            foreach (var proposal in Proposals)
                if (proposal.Kind == ProposalKind.Peace && proposal.From == from && proposal.To == to) return proposal;
            int id = _nextProposalId++;
            var created = new DiplomacyProposal { Id = id, Kind = ProposalKind.Peace, From = from, To = to,
                Tick = tick, PeaceTerms = terms };
            Proposals.Add(created);
            return created;
        }

        public bool HasProposal(ProposalKind kind, int from, int to)
        {
            foreach (var proposal in Proposals)
                if (proposal.Kind == kind && proposal.From == from && proposal.To == to) return true;
            return false;
        }

        public void RebuildProposalIds()
        {
            _nextProposalId = 1;
            foreach (var proposal in Proposals)
                if (proposal.Id >= _nextProposalId && proposal.Id < int.MaxValue) _nextProposalId = proposal.Id + 1;
        }

        public bool HasTreaty(TreatyKind kind, int a, int b, long tick)
        {
            if (kind == TreatyKind.Vassalage) return HasVassalage(a, b, tick);
            int lo = math.min(a, b), hi = math.max(a, b);
            for (int i = Treaties.Count - 1; i >= 0; i--)
            {
                var treaty = Treaties[i];
                if (treaty.EndTick <= tick) { Treaties.RemoveAt(i); continue; }
                if (treaty.Kind == kind && treaty.A == lo && treaty.B == hi) return true;
            }
            return false;
        }

        public DiplomaticStatus StatusBetween(int a, int b, long tick)
        {
            if (AtWar(a, b)) return DiplomaticStatus.War;
            if (VassalOf(a, tick) == b || VassalOf(b, tick) == a) return DiplomaticStatus.Vassalage;
            if (HasTreaty(TreatyKind.TradeEmbargo, a, b, tick)) return DiplomaticStatus.TradeEmbargo;
            if (KingdomAlive(a) && KingdomAlive(b) && Kingdom(a).Alliance >= 0 && Kingdom(a).Alliance == Kingdom(b).Alliance)
                return DiplomaticStatus.Alliance;
            if (HasTreaty(TreatyKind.NonAggression, a, b, tick)) return DiplomaticStatus.NonAggression;
            if (HasTreaty(TreatyKind.TradeAgreement, a, b, tick)) return DiplomaticStatus.TradeAgreement;
            return DiplomaticStatus.Neutral;
        }

        public void SetTreaty(TreatyKind kind, int a, int b, long tick, int durationTicks)
        {
            if (kind == TreatyKind.Vassalage) { SetVassal(a, b, tick, 0.05f); return; }
            int lo = math.min(a, b), hi = math.max(a, b);
            for (int i = Treaties.Count - 1; i >= 0; i--)
            {
                var treaty = Treaties[i];
                bool conflictingTradeStatus = (kind == TreatyKind.TradeAgreement || kind == TreatyKind.TradeEmbargo)
                    && (treaty.Kind == TreatyKind.TradeAgreement || treaty.Kind == TreatyKind.TradeEmbargo);
                if ((treaty.Kind == kind || conflictingTradeStatus) && treaty.A == lo && treaty.B == hi) Treaties.RemoveAt(i);
            }
            Treaties.Add(new DiplomacyTreaty { A = lo, B = hi, Kind = kind, StartTick = tick, EndTick = tick + Math.Max(1, durationTicks) });
        }

        public bool BreakTreaty(TreatyKind kind, int a, int b, long tick)
        {
            if (kind == TreatyKind.Vassalage) return false;
            int lo = math.min(a, b), hi = math.max(a, b);
            bool broken = false;
            for (int i = Treaties.Count - 1; i >= 0; i--)
            {
                var treaty = Treaties[i];
                if (treaty.EndTick <= tick) { Treaties.RemoveAt(i); continue; }
                if (treaty.Kind == kind && treaty.A == lo && treaty.B == hi)
                {
                    Treaties.RemoveAt(i);
                    broken = true;
                }
            }
            return broken;
        }

        public bool SetVassal(int subject, int overlord, long tick, float tributeRate)
        {
            if (!KingdomAlive(subject) || !KingdomAlive(overlord) || subject == overlord || AtWar(subject, overlord)) return false;
            int ancestor = overlord;
            for (int depth = 0; depth < Kingdoms.Count && ancestor >= 0; depth++)
            {
                if (ancestor == subject) return false; // prevent a vassal cycle
                ancestor = VassalOf(ancestor, tick);
            }
            for (int i = Treaties.Count - 1; i >= 0; i--)
                if (Treaties[i].Kind == TreatyKind.Vassalage && Treaties[i].A == subject)
                    Treaties.RemoveAt(i);
            Treaties.Add(new DiplomacyTreaty
            {
                A = subject, B = overlord, Kind = TreatyKind.Vassalage, StartTick = tick,
                EndTick = long.MaxValue, TributeRate = math.clamp(tributeRate, 0.01f, 0.2f),
            });
            return true;
        }

        public int VassalOf(int subject, long tick)
        {
            for (int i = Treaties.Count - 1; i >= 0; i--)
            {
                var treaty = Treaties[i];
                if (treaty.EndTick <= tick) { Treaties.RemoveAt(i); continue; }
                if (treaty.Kind == TreatyKind.Vassalage && treaty.A == subject) return treaty.B;
            }
            return -1;
        }

        public bool HasVassalage(int a, int b, long tick) => VassalOf(a, tick) == b || VassalOf(b, tick) == a;

        public void ExpireTreaties(long tick)
        {
            for (int i = Treaties.Count - 1; i >= 0; i--)
            {
                var treaty = Treaties[i];
                if (treaty.EndTick <= tick || !KingdomAlive(treaty.A) || !KingdomAlive(treaty.B)) Treaties.RemoveAt(i);
            }
        }

        public bool FindProposal(int id, out DiplomacyProposal proposal)
        {
            foreach (var p in Proposals)
                if (p.Id == id) { proposal = p; return true; }
            proposal = default;
            return false;
        }

        public bool RemoveProposal(int id, out DiplomacyProposal proposal)
        {
            if (FindProposal(id, out proposal))
            {
                Proposals.RemoveAll(p => p.Id == id);
                return true;
            }
            return false;
        }

        public KingdomPolicy PolicyOf(int kingdom) =>
            Policies.TryGetValue(kingdom, out var p) ? p : KingdomPolicy.Balanced;

        public void SetPolicy(int kingdom, KingdomPolicy policy) => Policies[kingdom] = policy;

        // --- names (Bölüm 6.2) ---

        public string Word(int phonology, ref SimRandom rng)
        {
            var sets = Content.NameSets.Sets;
            if (sets.Length == 0) return "X";
            var set = sets[math.clamp(phonology, 0, sets.Length - 1)];
            var sb = new StringBuilder(12);
            string pattern = set.Pattern[rng.Range(0, set.Pattern.Length)];
            if (pattern == "SYMBOL") return sb.Append(Symbols[rng.Range(0, Symbols.Length)]).Append(rng.Range(1, 100)).ToString();
            foreach (char ch in pattern) sb.Append(ch == 'C' ? set.Onset[rng.Range(0, set.Onset.Length)] : set.Vowel[rng.Range(0, set.Vowel.Length)]);
            if (set.Coda.Length > 0 && rng.Chance(0.4f)) sb.Append(set.Coda[rng.Range(0, set.Coda.Length)]);
            if (set.Id == "lang.backwards") { var a = sb.ToString().ToCharArray(); Array.Reverse(a); sb.Clear().Append(a); }
            if (sb.Length == 0) sb.Append('a');
            sb[0] = char.ToUpperInvariant(sb[0]);
            return sb.ToString();
        }

        const string Symbols = "◇△○□☆▽";

        public int UnitName(int phonology, ref SimRandom rng)
        {
            string n = Word(phonology, ref rng);
            if (rng.Chance(0.3f)) n += " " + Word(phonology, ref rng);
            return Names.Add(n, false);
        }

        static readonly string[] CitySuffixes = { "ia", "heim", "gar", "ova" };

        public int CityName(int phonology, ref SimRandom rng)
        {
            string n = Word(phonology, ref rng);
            if (rng.Chance(0.3f)) n += CitySuffixes[rng.Range(0, CitySuffixes.Length)];
            return Names.Add(n, false);
        }

        public string RulerTitle(int cities)
        {
            var titles = Content.NameSets.RulerTitles;
            string t = titles.Length > 0 ? titles[0].Text : "";
            foreach (var x in titles) if (cities >= x.MinCities) t = x.Text;
            return t;
        }

        public string KingdomName(int k)
        {
            ref var d = ref Kingdom(k);
            string root = Names.Get(d.Root);
            string title = RulerTitle(math.max(1, d.Cities));
            return (Kingdoms.Headers[k].Uid & 1) == 0 && title.Length > 0 ? root + " " + title : root + "ya";
        }

        public int CreateKingdom(int capital, int species, EntityId king, long kingUid, long tick, ref SimRandom rng)
        {
            int phon = SpeciesPhonology[species];
            var sets = Content.NameSets.Sets;
            if (sets.Length > 1 && rng.Chance(0.5f)) phon = rng.Range(0, sets.Length); // variety until languages (Bölüm 6.8)
            int k = Kingdoms.Create(new KingdomData
            {
                King = king, Heir = EntityId.None, Capital = capital, Culture = -1, Religion = -1, Language = -1, Alliance = -1,
                Phonology = (byte)phon, Species = (ushort)species, Trait = Content.KingdomTraits.Count > 0 && rng.Chance(0.4f) ? rng.Range(0, Content.KingdomTraits.Count) : -1,
                LastWarEndTick = tick, // no war in the first years (Bölüm 6.5 cooldown reuse)
            }, kingUid, tick);
            ref var h = ref Kingdoms.Header(k);
            h.Color = rng.NextUInt() | 0xFFu;
            h.Banner = Banner.Random(ref rng);
            Kingdom(k).Root = Names.Add(Word(phon, ref rng), false);
            h.NameId = Kingdom(k).Root;
            return k;
        }

        public bool CultureHas(int culture, int trait) => trait >= 0 && Cultures.IsAlive(culture) && Cultures.Headers[culture].Traits.Has(trait);
        public bool ReligionHas(int religion, int trait) => trait >= 0 && Religions.IsAlive(religion) && Religions.Headers[religion].Traits.Has(trait);
        public bool LanguageHas(int language, int trait) => trait >= 0 && Languages.IsAlive(language) && Languages.Headers[language].Traits.Has(trait);

        public float TraitValue(float[] values, int k) => Kingdom(k).Trait >= 0 ? values[Kingdom(k).Trait] : 0f;

        // --- save (section 12) ---

        public void Write(BinaryWriter w)
        {
            Names.Write(w);
            Kingdoms.Write(w);
            Wars.Write(w);
            Alliances.Write(w);
            w.Write(Armies.Length);
            w.Write(MemoryMarshal.AsBytes(Armies.AsArray().AsReadOnlySpan()));
            var keys = new List<long>(_opinion.Keys);
            keys.Sort();
            w.Write(keys.Count);
            foreach (long k in keys) { w.Write(k); w.Write(_opinion[k]); }
            Cultures.Write(w);
            Languages.Write(w);
            Religions.Write(w);
            Clans.Write(w);
            Plots.Write(w);
            w.Write(Books.Length);
            w.Write(MemoryMarshal.AsBytes(Books.AsArray().AsReadOnlySpan()));
            w.Write(NextBookUid);
            w.Write(History.Length);
            w.Write(MemoryMarshal.AsBytes(History.AsArray().AsReadOnlySpan()));
            w.Write(HistorySidecarMagic);
            w.Write(HistorySidecarVersion);
            w.Write(NextHistoryEventId);
            w.Write(HistoryDetails.Length);
            w.Write(MemoryMarshal.AsBytes(HistoryDetails.AsArray().AsReadOnlySpan()));
            w.Write(BiographyTracks.Length);
            w.Write(MemoryMarshal.AsBytes(BiographyTracks.AsArray().AsReadOnlySpan()));
            w.Write(BiographyEvents.Length);
            w.Write(MemoryMarshal.AsBytes(BiographyEvents.AsArray().AsReadOnlySpan()));
            w.Write(WorldSummaries.Length);
            w.Write(MemoryMarshal.AsBytes(WorldSummaries.AsArray().AsReadOnlySpan()));
            w.Write(WorldSummaryPopulationSplits.Length);
            w.Write(MemoryMarshal.AsBytes(WorldSummaryPopulationSplits.AsArray().AsReadOnlySpan()));
        }

        public void Read(BinaryReader r)
        {
            _recentDisasterCacheTick = long.MinValue;
            Names.Read(r);
            Kingdoms.Read(r);
            Wars.Read(r);
            Alliances.Read(r);
            Armies.Resize(r.ReadInt32(), NativeArrayOptions.ClearMemory);
            var bytes = MemoryMarshal.AsBytes(Armies.AsArray().AsSpan());
            if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated armies");
            _opinion.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) { long k = r.ReadInt64(); _opinion[k] = r.ReadInt16(); }
            Cultures.Read(r);
            Languages.Read(r);
            Religions.Read(r);
            Clans.Read(r);
            Plots.Read(r);
            ReadList(r, Books);
            NextBookUid = r.ReadInt64();
            ReadList(r, History);
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                int magic = r.ReadInt32();
                int version = r.ReadInt32();
                if (magic != HistorySidecarMagic || (version < 1 || version > HistorySidecarVersion))
                    throw new InvalidDataException("Unsupported history metadata block");
                NextHistoryEventId = r.ReadInt64();
                ReadList(r, HistoryDetails);
                if (HistoryDetails.Length != History.Length)
                    throw new InvalidDataException("History metadata count does not match history entries");
                BiographyTracks.Clear();
                BiographyEvents.Clear();
                WorldSummaries.Clear();
                WorldSummaryPopulationSplits.Clear();
                if (version >= 2)
                {
                    ReadList(r, BiographyTracks);
                    ReadList(r, BiographyEvents);
                    if (BiographyTracks.Length > MaxBiographyTracks || BiographyEvents.Length != BiographyTracks.Length * BiographyEventsPerUnit)
                        throw new InvalidDataException("Biography storage has invalid bounds");
                    for (int i = 0; i < BiographyTracks.Length; i++)
                    {
                        var track = BiographyTracks[i];
                        if (track.EventCount > BiographyEventsPerUnit || track.NextEvent >= BiographyEventsPerUnit)
                            throw new InvalidDataException("Biography ring has invalid cursor values");
                    }
                }
                if (version >= 3)
                {
                    ReadList(r, WorldSummaries);
                    if (WorldSummaries.Length > MaxWorldSummaries)
                        throw new InvalidDataException("World summary storage exceeds its limit");
                    if (version >= 4)
                    {
                        int splitCount = r.ReadInt32();
                        if (splitCount != WorldSummaries.Length || splitCount > MaxWorldSummaries)
                            throw new InvalidDataException("World summary population split count does not match summaries");
                        WorldSummaryPopulationSplits.Resize(splitCount, NativeArrayOptions.ClearMemory);
                        var splitBytes = MemoryMarshal.AsBytes(WorldSummaryPopulationSplits.AsArray().AsSpan());
                        if (r.Read(splitBytes) != splitBytes.Length) throw new EndOfStreamException("Truncated world summary population splits");
                    }
                    else
                    {
                        for (int i = 0; i < WorldSummaries.Length; i++)
                            WorldSummaryPopulationSplits.Add(WorldSummaryPopulationSplit.Unknown);
                    }
                }
            }
            else
            {
                // Saves written before history metadata used only HistoryEntry records.
                HistoryDetails.Resize(History.Length, NativeArrayOptions.ClearMemory);
                NextHistoryEventId = 1;
                for (int i = 0; i < History.Length; i++)
                {
                    var entry = History[i];
                    HistoryDetails[i] = new HistoryMetadata
                    {
                        EventId = NextHistoryEventId++,
                        CauseEventId = FindCauseEventId(i, entry),
                        Importance = BaseImportance(entry.Template),
                    };
                }
                BiographyTracks.Clear();
                BiographyEvents.Clear();
                WorldSummaries.Clear();
                WorldSummaryPopulationSplits.Clear();
            }
            RebuildBiographyIndex();
            RebuildWarPairs();
        }

        static void ReadList<T>(BinaryReader r, NativeList<T> list) where T : unmanaged
        {
            list.Resize(r.ReadInt32(), NativeArrayOptions.ClearMemory);
            var bytes = MemoryMarshal.AsBytes(list.AsArray().AsSpan());
            if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated meta list");
        }

        // Bölüm 6.13: oldest entries go first once the cap is reached.
        public void Record(HistoryKind kind, long tick, long a, long b = 0, long c = 0, int extra = 0)
            => RecordWithMetadata(kind, tick, a, b, c, extra, 0, HistoryMetadataFlags.None);

        public void RecordScaled(HistoryKind kind, long tick, long a, long b, long c, int extra, int armyScale)
            => RecordWithMetadata(kind, tick, a, b, c, extra, armyScale, HistoryMetadataFlags.None);

        public void RecordWithMetadata(HistoryKind kind, long tick, long a, long b, long c, int extra, int armyScale, HistoryMetadataFlags flags)
        {
            if (History.Length >= MaxHistory)
            {
                TrimHistory();
                _recentDisasterCacheTick = long.MinValue;
            }
            var entry = new HistoryEntry { Tick = tick, Template = (ushort)kind, A = a, B = b, C = c, Extra = extra };
            History.Add(entry);
            HistoryDetails.Add(new HistoryMetadata
            {
                EventId = NextHistoryEventId++,
                CauseEventId = FindCauseEventId(History.Length - 1, entry, flags),
                Importance = HistoryImportanceScore.CalculateWithScales(BaseImportance(entry.Template), ParticipantScale(a, b, c), armyScale),
                Flags = (ushort)flags,
            });
            if (kind == HistoryKind.Disaster) _recentDisasterCacheTick = long.MinValue;
        }

        public bool HasRecentDisasterForCity(long cityUid, long tick, long lookback)
        {
            if (cityUid == 0 || lookback < 0) return false;
            if (_recentDisasterCacheTick != tick || _recentDisasterCacheLookback != lookback)
            {
                _recentDisasterCities.Clear();
                for (int i = History.Length - 1; i >= 0; i--)
                {
                    var entry = History[i];
                    if (entry.Tick > tick) continue;
                    if (tick - entry.Tick > lookback) break;
                    if (entry.Template != (ushort)HistoryKind.Disaster || entry.A == 0) continue;
                    bool alreadyAdded = false;
                    for (int d = 0; d < _recentDisasterCities.Length; d++)
                        if (_recentDisasterCities[d] == entry.A) { alreadyAdded = true; break; }
                    if (!alreadyAdded) _recentDisasterCities.Add(entry.A);
                }
                _recentDisasterCacheTick = tick;
                _recentDisasterCacheLookback = lookback;
            }
            for (int i = 0; i < _recentDisasterCities.Length; i++)
                if (_recentDisasterCities[i] == cityUid) return true;
            return false;
        }

        public void RecordWorldSummary(in WorldSummary summary)
            => RecordWorldSummary(summary, WorldSummaryPopulationSplit.Unknown);

        public void RecordWorldSummary(in WorldSummary summary, in WorldSummaryPopulationSplit populationSplit)
        {
            for (int i = 0; i < WorldSummaries.Length; i++)
            {
                var existing = WorldSummaries[i];
                if (existing.Kind == summary.Kind && existing.StartYear == summary.StartYear && existing.EndYear == summary.EndYear
                    && existing.EraFrom == summary.EraFrom && existing.EraTo == summary.EraTo) return;
            }
            if (WorldSummaries.Length >= MaxWorldSummaries)
            {
                WorldSummaries.RemoveAt(0);
                if (WorldSummaryPopulationSplits.Length > 0) WorldSummaryPopulationSplits.RemoveAt(0);
            }
            WorldSummaries.Add(summary);
            WorldSummaryPopulationSplits.Add(populationSplit);
        }

        public bool TryGetBiography(long uid, out int index)
        {
            if (uid != 0) return _biographyIndex.TryGetValue(uid, out index);
            index = -1;
            return false;
        }

        public bool TrackBiography(long uid, long birthTick, ushort species, int nameId, long motherUid, long fatherUid)
        {
            if (uid == 0) return false;
            if (_biographyIndex.TryGetValue(uid, out int existing))
            {
                var current = BiographyTracks[existing];
                if (current.NameId < 0 && nameId >= 0) current.NameId = nameId;
                if (current.MotherUid == 0) current.MotherUid = motherUid;
                if (current.FatherUid == 0) current.FatherUid = fatherUid;
                BiographyTracks[existing] = current;
                return true;
            }
            if (BiographyTracks.Length >= MaxBiographyTracks) return false;
            int index = BiographyTracks.Length;
            BiographyTracks.Add(new BiographyTrack
            {
                Uid = uid, BirthTick = birthTick, Species = species, NameId = nameId,
                MotherUid = motherUid, FatherUid = fatherUid,
            });
            int start = BiographyEvents.Length;
            BiographyEvents.Resize(start + BiographyEventsPerUnit, NativeArrayOptions.ClearMemory);
            _biographyIndex.Add(uid, index);
            return true;
        }

        public void AddBiographyEvent(long uid, long tick, BiographyEventKind kind, long relatedUid = 0, int value = 0)
        {
            if (!_biographyIndex.TryGetValue(uid, out int index)) return;
            var track = BiographyTracks[index];
            int slot = track.NextEvent;
            BiographyEvents[index * BiographyEventsPerUnit + slot] = new BiographyEvent
            { Tick = tick, RelatedUid = relatedUid, Value = value, Kind = kind };
            track.NextEvent = (byte)((slot + 1) % BiographyEventsPerUnit);
            if (track.EventCount < BiographyEventsPerUnit) track.EventCount++;
            BiographyTracks[index] = track;
        }

        public BiographyEvent GetBiographyEvent(long uid, int chronologicalIndex)
        {
            if (!_biographyIndex.TryGetValue(uid, out int index)) return default;
            var track = BiographyTracks[index];
            if (chronologicalIndex < 0 || chronologicalIndex >= track.EventCount) return default;
            int first = (track.NextEvent - track.EventCount + BiographyEventsPerUnit) % BiographyEventsPerUnit;
            return BiographyEvents[index * BiographyEventsPerUnit + (first + chronologicalIndex) % BiographyEventsPerUnit];
        }

        public void SetBiographyDeath(long uid, long tick, long killerUid, DeathCause cause, long mateUid,
            long motherUid, long fatherUid, ushort species, byte level, ushort kills, int nameId, long birthTick)
        {
            if (!_biographyIndex.TryGetValue(uid, out int index)) return;
            var track = BiographyTracks[index];
            track.DeathTick = tick;
            track.KillerUid = killerUid;
            track.DeathCause = (byte)cause;
            track.MateUid = mateUid;
            if (track.MotherUid == 0) track.MotherUid = motherUid;
            if (track.FatherUid == 0) track.FatherUid = fatherUid;
            track.Species = species;
            track.LevelAtDeath = level;
            track.KillsAtDeath = kills;
            if (track.NameId < 0) track.NameId = nameId;
            if (track.BirthTick == 0) track.BirthTick = birthTick;
            track.IsDead = 1;
            BiographyTracks[index] = track;
            AddBiographyEvent(uid, tick, BiographyEventKind.Death, killerUid, (int)cause);
        }

        void RebuildBiographyIndex()
        {
            _biographyIndex.Clear();
            for (int i = 0; i < BiographyTracks.Length; i++)
            {
                long uid = BiographyTracks[i].Uid;
                if (uid == 0 || _biographyIndex.ContainsKey(uid))
                    throw new InvalidDataException("Biography UIDs must be unique and nonzero");
                _biographyIndex.Add(uid, i);
            }
        }

        ushort BaseImportance(ushort template)
        {
            if (template >= HistoryKeys.Keys.Length) return 10;
            string key = HistoryKeys.Keys[template];
            return Content.HistoryKinds.TryGet(key, out var def) ? (ushort)Math.Max(0, Math.Min(100, def.Importance)) : (ushort)10;
        }

        int ParticipantScale(long a, long b, long c)
        {
            int maxMembers = 0;
            for (int i = 0; i < Kingdoms.Count; i++)
            {
                long uid = Kingdoms.Headers[i].Uid;
                if ((uid == a || uid == b || uid == c) && Kingdoms.Headers[i].MemberCount > maxMembers)
                    maxMembers = Kingdoms.Headers[i].MemberCount;
            }
            if (maxMembers <= 1) return 0;
            return Math.Min(20, (int)Math.Floor(Math.Log(maxMembers, 2) * 3));
        }

        long FindCauseEventId(int beforeIndex, HistoryEntry entry, HistoryMetadataFlags flags = HistoryMetadataFlags.None)
        {
            HistoryKind kind = (HistoryKind)entry.Template;
            if (kind == HistoryKind.KingdomFell)
            {
                long lookback = 3L * SimConst.TicksPerYear;
                for (int i = beforeIndex - 1; i >= 0; i--)
                {
                    var cause = History[i];
                    HistoryKind causeKind = (HistoryKind)cause.Template;
                    bool cityLossMatches = causeKind == HistoryKind.CityRuined
                        ? cause.B == entry.A
                        : (causeKind == HistoryKind.CityCaptured || causeKind == HistoryKind.Rebellion) && cause.B == entry.A;
                    if (!cityLossMatches || entry.Tick < cause.Tick || entry.Tick - cause.Tick > lookback) continue;
                    return HistoryDetails[i].EventId;
                }
                return 0;
            }
            if (kind == HistoryKind.WarDeclared && IsResourceShortageWar(entry))
            {
                long lookback = 3L * SimConst.TicksPerYear;
                for (int i = beforeIndex - 1; i >= 0; i--)
                {
                    var cause = History[i];
                    if (cause.Template == (ushort)HistoryKind.PopulationMigrated && HistoryDetails[i].CauseEventId != 0
                        && (cause.C == entry.A || cause.C == entry.B)
                        && entry.Tick >= cause.Tick && entry.Tick - cause.Tick <= lookback)
                        return HistoryDetails[i].EventId;
                }
                for (int i = beforeIndex - 1; i >= 0; i--)
                {
                    var cause = History[i];
                    if (cause.Template != (ushort)HistoryKind.Famine || cause.B == 0
                        || (cause.B != entry.A && cause.B != entry.B)
                        || entry.Tick < cause.Tick || entry.Tick - cause.Tick > lookback) continue;
                    return HistoryDetails[i].EventId;
                }
                return 0;
            }
            if (kind == HistoryKind.PopulationMigrated)
            {
                long lookback = 3L * SimConst.TicksPerYear;
                bool disasterMigration = (flags & HistoryMetadataFlags.DisasterMigration) != 0;
                for (int i = beforeIndex - 1; i >= 0; i--)
                {
                    var cause = History[i];
                    HistoryKind causeKind = disasterMigration ? HistoryKind.Disaster : HistoryKind.Famine;
                    if (cause.Template != (ushort)causeKind || cause.A != entry.A
                        || entry.Tick < cause.Tick || entry.Tick - cause.Tick > lookback) continue;
                    return HistoryDetails[i].EventId;
                }
                return 0;
            }
            if (kind == HistoryKind.NewKing)
            {
                long lookback = 3L * SimConst.TicksPerYear;
                for (int i = beforeIndex - 1; i >= 0; i--)
                {
                    var cause = History[i];
                    if (cause.Template != (ushort)HistoryKind.KingDied || cause.A != entry.A
                        || entry.Tick < cause.Tick || entry.Tick - cause.Tick > lookback) continue;
                    return HistoryDetails[i].EventId;
                }
                return 0;
            }
            if (kind == HistoryKind.Peace)
            {
                for (int i = beforeIndex - 1; i >= 0; i--)
                    if (History[i].Template == (ushort)HistoryKind.WarDeclared && History[i].C == entry.A)
                        return HistoryDetails[i].EventId;
                return 0;
            }
            if (kind == HistoryKind.CityCaptured || kind == HistoryKind.Rebellion)
            {
                long from = entry.B;
                long to = entry.C;
                if (from == 0 || to == 0) return 0;
                for (int i = beforeIndex - 1; i >= 0; i--)
                {
                    var cause = History[i];
                    if (cause.Template != (ushort)HistoryKind.WarDeclared) continue;
                    if ((cause.A == from && cause.B == to) || (cause.A == to && cause.B == from))
                        return HistoryDetails[i].EventId;
                }
            }
            return 0;
        }

        static bool IsResourceShortageWar(HistoryEntry entry) =>
            ((uint)entry.Extra & 0xC000FF00u) == (0x40000000u | ((uint)WarCause.ResourceShortage << 8));

        void TrimHistory()
        {
            int ordinary = 0;
            for (int i = 0; i < History.Length; i++)
            {
                if (!IsSpeciesHistory(History[i].Template)) ordinary++;
            }
            int remove = Math.Max(0, ordinary - (MaxHistory - MaxHistory / 10));
            bool removeOldestRegardless = remove == 0;
            if (removeOldestRegardless) remove = 1;
            int read = 0, write = 0, removed = 0;
            for (; read < History.Length; read++)
            {
                var entry = History[read];
                bool drop = removeOldestRegardless
                    ? read == 0
                    : !IsSpeciesHistory(entry.Template) && removed < remove;
                if (drop) { removed++; continue; }
                History[write++] = entry;
                HistoryDetails[write - 1] = HistoryDetails[read];
            }
            History.Resize(write, NativeArrayOptions.UninitializedMemory);
            HistoryDetails.Resize(write, NativeArrayOptions.UninitializedMemory);
        }

        static bool IsSpeciesHistory(ushort template) => template == (ushort)HistoryKind.SpeciesEndangered || template == (ushort)HistoryKind.SpeciesExtinct;

        public void Dispose()
        {
            Cultures.Dispose();
            Languages.Dispose();
            Religions.Dispose();
            Clans.Dispose();
            Plots.Dispose();
            if (Books.IsCreated) Books.Dispose();
            if (History.IsCreated) History.Dispose();
            if (HistoryDetails.IsCreated) HistoryDetails.Dispose();
            _biographyIndex.Clear();
            if (BiographyTracks.IsCreated) BiographyTracks.Dispose();
            if (BiographyEvents.IsCreated) BiographyEvents.Dispose();
            if (WorldSummaries.IsCreated) WorldSummaries.Dispose();
            if (WorldSummaryPopulationSplits.IsCreated) WorldSummaryPopulationSplits.Dispose();
            if (_recentDisasterCities.IsCreated) _recentDisasterCities.Dispose();
            Kingdoms.Dispose();
            Wars.Dispose();
            Alliances.Dispose();
            if (Armies.IsCreated) Armies.Dispose();
        }
    }

    // NameId -> string; repeated names get a Roman numeral when asked (kings, dynasties).
    public sealed class NamePool
    {
        readonly List<string> _names = new List<string>();
        readonly Dictionary<string, int> _uses = new Dictionary<string, int>();

        public int Count => _names.Count;
        public string Get(int id) => id >= 0 && id < _names.Count ? _names[id] : "";

        public int Add(string name, bool numbered)
        {
            _uses.TryGetValue(name, out int used);
            _uses[name] = used + 1;
            _names.Add(numbered && used > 0 ? name + " " + Roman(used + 1) : name);
            return _names.Count - 1;
        }

        public void Rename(int id, string name)
        {
            if (id >= 0 && id < _names.Count) _names[id] = name;
        }

        static string Roman(int n)
        {
            string[] r = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            int[] v = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var sb = new StringBuilder();
            for (int i = 0; i < v.Length; i++) while (n >= v[i]) { sb.Append(r[i]); n -= v[i]; }
            return sb.ToString();
        }

        public void Write(BinaryWriter w)
        {
            w.Write(_names.Count);
            foreach (var n in _names) w.Write(n);
            var keys = new List<string>(_uses.Keys);
            keys.Sort(StringComparer.Ordinal);
            w.Write(keys.Count);
            foreach (var k in keys) { w.Write(k); w.Write(_uses[k]); }
        }

        public void Read(BinaryReader r)
        {
            _names.Clear();
            _uses.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) _names.Add(r.ReadString());
            int u = r.ReadInt32();
            for (int i = 0; i < u; i++) { string k = r.ReadString(); _uses[k] = r.ReadInt32(); }
        }
    }
}
