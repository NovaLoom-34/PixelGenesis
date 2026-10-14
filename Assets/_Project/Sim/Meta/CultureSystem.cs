using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.7-6.10 and 6.12: cultures, languages, religions, clans and books.
    // Monthly: city majorities, missing memberships, slow drift to the city majority, kingdom metas, member counts, clans, reading.
    // Yearly: conquest assimilation, sermons, scripts and dialects, book writing and decay.
    public sealed class CultureSystem : ISimSystem
    {
        public const float DriftChance = 0.01f, ConquestChance = 0.05f, SermonChance = 0.1f, InquisitionChance = 0.2f;
        public const float TraitChance = 0.4f, ScriptChance = 0.3f, DialectChance = 0.02f, WriteChance = 0.1f, ReadChance = 0.03f;
        public const int ScriptYears = 50, DialectZones = 40, BookCondition = 100, MaxReadTypes = 16;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 5;

        int[] _members = new int[0];
        int[] _oldest = new int[0];

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 2) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            var civ = ctx.Civ;
            for (int c = 0; c < civ.CityCount; c++)
                if (civ.CityAlive(c)) City(ctx, c, ref rng);
            Kingdoms(ctx);
            Clans(ctx, tick);
            Census(ctx, tick);
            Reading(ctx, ref rng);
            if (tick % SimConst.TicksPerYear != 2) return;
            for (int c = 0; c < civ.CityCount; c++)
                if (civ.CityAlive(c)) Yearly(ctx, c, ref rng);
            Languages(ctx, tick, ref rng);
            Books(ctx, tick, ref rng);
        }

        // --- cities ---

        void City(in SimContext ctx, int c, ref SimRandom rng)
        {
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var w = ctx.Units;
            var u = w.Store;
            ref var city = ref civ.City(c);
            var residents = civ.CityResidents[c];
            city.Culture = Majority(u.Culture, residents);
            city.Language = Majority(u.Language, residents);
            city.Religion = Majority(u.Religion, residents);
            foreach (int i in residents)
            {
                var ab = w.Abilities(i);
                if ((ab & AbilityFlags.CanHoldCulture) != 0)
                {
                    if (city.Culture < 0 || !meta.Cultures.IsAlive(city.Culture)) city.Culture = NewCulture(ctx, c, -1, u.Uid[i], ref rng);
                    if (u.Culture[i] < 0 || u.Culture[i] != city.Culture && !meta.CultureHas(u.Culture[i], meta.CulTrueRoots) && rng.Chance(DriftChance)) Set(u, i, ref u.Culture, city.Culture);
                }
                if ((ab & AbilityFlags.CanHoldLanguage) != 0 && (ab & AbilityFlags.CanSpeak) != 0)
                {
                    if (city.Language < 0 || !meta.Languages.IsAlive(city.Language)) city.Language = NewLanguage(ctx, c, -1, u.Uid[i], ref rng);
                    if (u.Language[i] < 0 || u.Language[i] != city.Language && rng.Chance(DriftChance)) u.Language[i] = city.Language;
                }
                if ((ab & AbilityFlags.CanHoldReligion) != 0 && city.Religion >= 0 && meta.Religions.IsAlive(city.Religion) && u.Religion[i] < 0)
                    Set(u, i, ref u.Religion, city.Religion);
            }
            // Bölüm 6.9: the first temple of a city without a faith founds one
            if ((city.Religion < 0 || !meta.Religions.IsAlive(city.Religion)) && civ.BldTemple >= 0 && civ.CountBuildings(c, civ.BldTemple, true) > 0 && residents.Count > 0 &&
                (w.Abilities(residents[0]) & AbilityFlags.CanHoldReligion) != 0)
            {
                long founder = u.IsAlive(city.Leader) ? u.Uid[city.Leader.Index] : u.Uid[residents[0]];
                city.Religion = NewReligion(ctx, c, founder, ref rng);
                foreach (int i in residents) if ((w.Abilities(i) & AbilityFlags.CanHoldReligion) != 0) Set(u, i, ref u.Religion, city.Religion);
            }
        }

        static void Set(UnitStore u, int i, ref Unity.Collections.NativeArray<int> array, int value)
        {
            if (array[i] == value) return;
            array[i] = value;
            u.StatsDirty[i] = 1; // culture/religion traits change stats
        }

        // Boyer-Moore vote: the majority when one exists, otherwise some frequent value (-1 = nobody holds one)
        static int Majority(Unity.Collections.NativeArray<int> values, System.Collections.Generic.List<int> residents)
        {
            int candidate = -1, count = 0;
            foreach (int i in residents)
            {
                int v = values[i];
                if (v < 0) continue;
                if (count == 0) { candidate = v; count = 1; }
                else count += v == candidate ? 1 : -1;
            }
            return candidate;
        }

        void Yearly(in SimContext ctx, int c, ref SimRandom rng)
        {
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            ref var city = ref civ.City(c);
            var residents = civ.CityResidents[c];
            int k = city.Kingdom;
            // conquered cities slowly take the ruling culture (about 20 years)
            int ruling = meta.KingdomAlive(k) ? meta.Kingdom(k).Culture : -1;
            if (ruling >= 0 && meta.Cultures.IsAlive(ruling) && ruling != city.Culture)
                foreach (int i in residents)
                    if (u.Culture[i] >= 0 && u.Culture[i] != ruling && !meta.CultureHas(u.Culture[i], meta.CulTrueRoots) && rng.Chance(ConquestChance)) Set(u, i, ref u.Culture, ruling);
            // sermons: a temple converts to the city faith (missionaries x1.5), inquisition forces it
            int faith = city.Religion;
            if (faith < 0 || civ.BldTemple < 0 || civ.CountBuildings(c, civ.BldTemple, true) == 0) return;
            float chance = SermonChance * (meta.ReligionHas(faith, meta.RelMissionaries) ? 1.5f : 1f);
            if (meta.ReligionHas(faith, meta.RelInquisition)) chance = math.max(chance, InquisitionChance);
            foreach (int i in residents)
                if (u.Religion[i] >= 0 && u.Religion[i] != faith && rng.Chance(chance)) Set(u, i, ref u.Religion, faith);
        }

        // --- creation (Bölüm 6.1: 40% chance of 1-2 traits, biome pools first) ---

        static BiomeDef BiomeOf(in SimContext ctx, int city)
        {
            int2 t = CityPlanner.Below(ctx.Civ.City(city).Center);
            var map = ctx.World;
            if (!map.InBounds(t.x, t.y)) return null;
            byte b = map.Biome[map.Index(t.x, t.y)];
            return b == 0 ? null : ctx.Content.Biomes[b - 1];
        }

        static void RandomTraits(ref TraitSet set, Registry<MetaTraitDef> defs, int[] pool, string skipGroup, ref SimRandom rng)
        {
            if (defs.Count == 0 || !rng.Chance(TraitChance)) return;
            int n = rng.Range(1, 3);
            for (int k = 0; k < n; k++)
            {
                int t = pool != null && pool.Length > 0 && rng.Chance(0.6f) ? pool[rng.Range(0, pool.Length)] : rng.Range(0, defs.Count);
                if (skipGroup != null && defs[t].Group == skipGroup) continue;
                set.Set(t, true);
            }
        }

        public static int Phonology(in SimContext ctx, int city)
        {
            var meta = ctx.Meta;
            int k = ctx.Civ.City(city).Kingdom;
            return meta.KingdomAlive(k) ? meta.Kingdom(k).Phonology : meta.SpeciesPhonology[ctx.Civ.City(city).Species];
        }

        public static int NewCulture(in SimContext ctx, int city, int parent, long founder, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var db = ctx.Content;
            int c = meta.Cultures.Create(new CultureData
            {
                OriginKingdom = ctx.Civ.City(city).Kingdom, OriginCity = city, ParentCulture = parent,
                Generation = parent >= 0 ? meta.Cultures.Items[parent].Generation + 1 : 0,
            }, founder, ctx.Clock.Tick);
            ref var h = ref meta.Cultures.Header(c);
            if (parent >= 0) h.Traits = meta.Cultures.Headers[parent].Traits;
            else RandomTraits(ref h.Traits, db.CultureTraits, BiomeOf(ctx, city)?.CultureTraits, "succession", ref rng);
            h.NameId = meta.Names.Add(meta.Word(Phonology(ctx, city), ref rng), false);
            h.Color = rng.NextUInt() | 0xFFu;
            h.Banner = Banner.Random(ref rng);
            ctx.Events.Publish(new MetaFoundedEvent(MetaKind.Culture, c));
            return c;
        }

        public static int NewLanguage(in SimContext ctx, int city, int parent, long founder, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var db = ctx.Content;
            int phon = Phonology(ctx, city);
            var pool = BiomeOf(ctx, city)?.LanguageTraits;
            int l = meta.Languages.Create(new LanguageData { ParentLanguage = parent, OriginCity = city }, founder, ctx.Clock.Tick);
            ref var h = ref meta.Languages.Header(l);
            if (parent >= 0)
            {
                h.Traits = meta.Languages.Headers[parent].Traits;
                meta.Languages.Data(l).Script = meta.Languages.Items[parent].Script;
                phon = meta.Languages.Items[parent].Phonology;
                if (db.NameSets.Sets.Length > 1 && rng.Chance(0.5f)) phon = rng.Range(0, db.NameSets.Sets.Length); // a dialect drifts in sound
            }
            else if (pool != null && pool.Length > 0 && rng.Chance(TraitChance))
            {
                int set = db.NameSets.IndexOf(db.LanguageTraits[pool[rng.Range(0, pool.Length)]].Id);
                if (set >= 0) phon = set;
            }
            RandomTraits(ref h.Traits, db.LanguageTraits, pool, "phonology", ref rng);
            int phonTrait = phon >= 0 && phon < db.NameSets.Sets.Length ? db.LanguageTraits.IdOrDefault(db.NameSets.Sets[phon].Id) : -1;
            if (phonTrait >= 0) h.Traits.Set(phonTrait, true);
            meta.Languages.Data(l).Phonology = (byte)math.max(0, phon);
            h.NameId = meta.Names.Add(meta.Word(phon, ref rng), false);
            h.Color = rng.NextUInt() | 0xFFu;
            ctx.Events.Publish(new MetaFoundedEvent(MetaKind.Language, l));
            return l;
        }

        // a deity archetype (biome pool first) and 0-2 doctrines
        public static int NewReligion(in SimContext ctx, int city, long founder, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var defs = ctx.Content.ReligionTraits;
            int r = meta.Religions.Create(new ReligionData { FounderUid = founder, HolyCity = city, DeityTrait = -1 }, founder, ctx.Clock.Tick);
            ref var h = ref meta.Religions.Header(r);
            var pool = BiomeOf(ctx, city)?.ReligionTraits;
            int deity = -1;
            if (pool != null && pool.Length > 0 && rng.Chance(0.6f)) deity = pool[rng.Range(0, pool.Length)];
            for (int t = 0; t < 16 && (deity < 0 || defs[deity].Group != "deity"); t++) deity = rng.Range(0, defs.Count);
            if (deity >= 0) { h.Traits.Set(deity, true); meta.Religions.Data(r).DeityTrait = deity; }
            int doctrines = rng.Range(0, 3);
            for (int n = 0; n < doctrines; n++)
            {
                int t = rng.Range(0, defs.Count);
                if (defs[t].Group != "deity") h.Traits.Set(t, true);
            }
            h.NameId = meta.Names.Add(meta.Word(Phonology(ctx, city), ref rng), false);
            h.Color = rng.NextUInt() | 0xFFu;
            h.Banner = Banner.Random(ref rng);
            ctx.Events.Publish(new MetaFoundedEvent(MetaKind.Religion, r));
            return r;
        }

        public static int NewClan(in SimContext ctx, int founder, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            int c = meta.Clans.Create(new ClanData { FounderUid = u.Uid[founder], Chief = u.IdOf(founder) }, u.Uid[founder], ctx.Clock.Tick);
            ref var h = ref meta.Clans.Header(c);
            RandomTraits(ref h.Traits, ctx.Content.ClanTraits, null, null, ref rng);
            int city = u.City[founder];
            h.NameId = meta.Names.Add(meta.Word(city >= 0 ? Phonology(ctx, city) : 0, ref rng), false);
            h.Color = rng.NextUInt() | 0xFFu;
            h.Banner = Banner.Random(ref rng);
            ctx.Events.Publish(new MetaFoundedEvent(MetaKind.Clan, c));
            return c;
        }

        // --- kingdoms, clans, census ---

        static void Kingdoms(in SimContext ctx)
        {
            var meta = ctx.Meta;
            var civ = ctx.Civ;
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                ref var d = ref meta.Kingdom(k);
                if (d.Capital < 0 || !civ.CityAlive(d.Capital)) continue;
                ref var cap = ref civ.City(d.Capital);
                d.Culture = cap.Culture;
                d.Religion = cap.Religion;
                d.Language = cap.Language;
                if (cap.Language >= 0 && meta.Languages.IsAlive(cap.Language)) d.Phonology = meta.Languages.Items[cap.Language].Phonology; // names follow the language (6.8)
            }
        }

        // every king founds a clan if he has none (Bölüm 6.10)
        static void Clans(in SimContext ctx, long tick)
        {
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                var king = meta.Kingdom(k).King;
                if (!u.IsAlive(king)) continue;
                int i = king.Index;
                if (u.Clan[i] < 0 || !meta.Clans.IsAlive(u.Clan[i]))
                {
                    u.Clan[i] = NewClan(ctx, i, ref rng);
                    u.StatsDirty[i] = 1;
                }
            }
        }

        void Census(in SimContext ctx, long tick)
        {
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            Count(ctx, meta.Cultures, u.Culture, tick, false);
            Count(ctx, meta.Languages, u.Language, tick, false);
            Count(ctx, meta.Religions, u.Religion, tick, false);
            Count(ctx, meta.Clans, u.Clan, tick, true);
        }

        void Count<T>(in SimContext ctx, MetaStore<T> store, Unity.Collections.NativeArray<int> member, long tick, bool clans) where T : unmanaged
        {
            int n = store.Count;
            if (_members.Length < n) { _members = new int[n * 2 + 8]; _oldest = new int[n * 2 + 8]; }
            System.Array.Clear(_members, 0, n);
            for (int m = 0; m < n; m++) _oldest[m] = -1;
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int m = member[i];
                if (m < 0 || m >= n) continue;
                _members[m]++;
                if (_oldest[m] < 0 || u.BirthTick[i] < u.BirthTick[_oldest[m]]) _oldest[m] = i;
            }
            for (int m = 0; m < n; m++)
            {
                if (!store.IsAlive(m)) continue;
                store.Header(m).MemberCount = _members[m];
                if (_members[m] == 0) { store.MarkDead(m, tick); ctx.Events.Publish(new MetaDiedEvent(store.Kind, m)); continue; }
                if (clans && _oldest[m] >= 0) ctx.Meta.Clans.Data(m).Chief = u.IdOf(_oldest[m]); // chief = eldest member
            }
        }

        // --- languages (Bölüm 6.8) ---

        static void Languages(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var civ = ctx.Civ;
            var db = ctx.Content;
            int count = meta.Languages.Count;
            for (int l = 0; l < count; l++)
            {
                if (!meta.Languages.IsAlive(l)) continue;
                ref var data = ref meta.Languages.Data(l);
                if (data.Script == 0 && tick - meta.Languages.Headers[l].FoundedTick >= ScriptYears * SimConst.TicksPerYear && rng.Chance(ScriptChance))
                {
                    int script = meta.LangScripts[rng.Range(0, meta.LangScripts.Length)];
                    if (script >= 0) meta.Languages.Header(l).Traits.Set(script, true);
                    data.Script = 1;
                }
                for (int s = 0; s < meta.LangScripts.Length; s++) if (meta.LanguageHas(l, meta.LangScripts[s])) data.Script = 1;
                // dialect split: spread over 3+ cities and a city far from the origin
                int cities = 0;
                for (int c = 0; c < civ.CityCount; c++) if (civ.CityAlive(c) && civ.City(c).Language == l) cities++;
                if (cities < 3 || data.OriginCity < 0 || data.OriginCity >= civ.CityCount) continue;
                float chance = DialectChance * (meta.LanguageHas(l, meta.LangManyDialects) ? 2f : 1f);
                float2 origin = civ.City(data.OriginCity).Center;
                for (int c = 0; c < civ.CityCount; c++)
                {
                    if (!civ.CityAlive(c) || civ.City(c).Language != l) continue;
                    if (math.distance((float2)civ.City(c).Center, origin) / WorldMap.ZoneSize < DialectZones || !rng.Chance(chance)) continue;
                    var u = ctx.Units.Store;
                    int leader = u.IsAlive(civ.City(c).Leader) ? civ.City(c).Leader.Index : -1;
                    int child = NewLanguage(ctx, c, l, leader >= 0 ? u.Uid[leader] : 0, ref rng);
                    foreach (int i in civ.CityResidents[c]) if (u.Language[i] == l) u.Language[i] = child;
                    civ.City(c).Language = child;
                    break;
                }
            }
        }

        // --- books (Bölüm 6.12) ---

        static void Books(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var civ = ctx.Civ;
            var u = ctx.Units.Store;
            var db = ctx.Content;
            var books = meta.Books;
            // decay; burned or lost libraries take their books
            for (int b = 0; b < books.Length; b++)
            {
                var book = books[b];
                if (book.Dead != 0) continue;
                bool shelf = book.Library >= 0 && book.Library < civ.Buildings.Length && civ.Buildings[book.Library].Complete;
                float decay = meta.LanguageHas(book.Language, meta.LangEternal) ? 0f : meta.CultureHas(civ.CityAlive(book.City) ? civ.City(book.City).Culture : -1, meta.CulLibraryKeepers) ? 0.2f : 1f;
                if (decay > 0f && rng.Chance(decay)) book.Condition = (byte)math.max(0, book.Condition - 1);
                if (!shelf || book.Condition == 0) book.Dead = 1;
                books[b] = book;
            }
            if (civ.BldLibrary < 0 || db.BookTypes.Count == 0) return;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                int library = civ.NearestBuilding(c, civ.BldLibrary, civ.City(c).Center);
                ref var city = ref civ.City(c);
                if (library < 0 || city.Language < 0 || !meta.Languages.IsAlive(city.Language) || meta.Languages.Items[city.Language].Script == 0) continue;
                float chance = WriteChance * (1f + (meta.Cultures.IsAlive(city.Culture) ? CultureValue(db, meta, city.Culture, "bookWrite") : 0f) / 100f);
                foreach (int i in civ.CityResidents[c])
                {
                    if (u.Job[i] - 1 != civ.JobScholar || !rng.Chance(chance)) continue;
                    int type = PickType(db, meta, city.Culture, city.Religion, ref rng);
                    books.Add(new BookData
                    {
                        Uid = meta.NextBookUid++, Type = (ushort)type, Language = city.Language, Culture = city.Culture, Religion = city.Religion,
                        AuthorUid = u.Uid[i], NameId = meta.Names.Add(meta.Word(meta.Languages.Items[city.Language].Phonology, ref rng), false),
                        Library = library, City = c, WrittenTick = tick, Condition = BookCondition,
                    });
                    meta.Languages.Data(city.Language).WrittenWorks++;
                    meta.Record(HistoryKind.BookWritten, tick, meta.NextBookUid - 1, u.Uid[i], 0, type);
                }
            }
        }

        static float CultureValue(ContentDB db, MetaWorld meta, int culture, string key)
        {
            float v = 0f;
            var traits = meta.Cultures.Headers[culture].Traits;
            if (traits.IsEmpty) return 0f;
            for (int t = 0; t < db.CultureTraits.Count; t++) if (traits.Has(t)) v += db.CultureTraits[t].Pct(key); // yearly, few cities
            return v;
        }

        static int PickType(ContentDB db, MetaWorld meta, int culture, int religion, ref SimRandom rng)
        {
            int science = db.BookTypes.IdOrDefault("book.science"), holy = db.BookTypes.IdOrDefault("book.holy");
            if (science >= 0 && meta.CultureHas(culture, meta.CulScholars) && rng.Chance(0.5f)) return science;
            if (holy >= 0 && religion >= 0 && rng.Chance(0.3f)) return holy;
            return rng.Range(0, db.BookTypes.Count);
        }

        // Monthly: a resident of a library city reads a book in a language they speak; one lasting bonus per book type.
        static void Reading(in SimContext ctx, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var books = meta.Books;
            if (books.Length == 0) return;
            var civ = ctx.Civ;
            var u = ctx.Units.Store;
            int readEvent = ctx.Content.HappinessEvents.IdOrDefault("hap.read_book");
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int c = u.City[i];
                if (c < 0 || u.Language[i] < 0 || (AgeStage)u.Age[i] < AgeStage.Adult || !rng.Chance(ReadChance * (u.Neuron(i, NeuronId.ReadBook) ? 2f : 1f))) continue; // bookworms read twice as often
                int b = rng.Range(0, books.Length);
                var book = books[b];
                if (book.Dead != 0 || book.City != c || book.Language != u.Language[i] || book.Type >= MaxReadTypes) continue;
                u.BooksRead[i] |= (ushort)(1 << book.Type);
                u.StatsDirty[i] = 1;
                if (readEvent >= 0) Happiness.Add(u, i, readEvent);
            }
        }
    }
}
