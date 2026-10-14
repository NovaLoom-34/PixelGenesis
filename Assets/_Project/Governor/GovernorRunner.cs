using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PG.Core;
using PG.Powers;
using PG.Sim;
using UnityEngine;

namespace PG.Governor
{
    public enum GovernorConnectionStatus : byte { Unknown, Testing, Ok, Failed }

    // Drives the LLM governors: builds snapshots on the main thread, calls the brain on worker
    // threads, validates the answers back on the main thread and pushes them into the edit
    // command queue. The simulation itself never calls the LLM, so determinism and replay are
    // untouched (DECISIONS #97/#98).
    //
    // Spec limits (all in GovernorSettings): one decision request per kingdom per CooldownSeconds
    // (events for the same kingdom merge into that request), MaxConcurrent requests in flight
    // overall, MaxCallsPerHour model calls. Late answers (stale snapshot uid) are dropped; the
    // player's kingdoms are left alone; a failing brain falls back to the built-in game AI.
    public sealed class GovernorRunner
    {
        public static GovernorRunner Active; // set by Boot; the settings / brain windows read this

        public readonly GovernorSettings Settings;
        public readonly DecisionLog Log = new DecisionLog();
        public int InFlight => Volatile.Read(ref _inFlight);

        public GovernorConnectionStatus Status;
        public string StatusMessage = "";
        public DateTime StatusUtc;

        readonly IKingdomBrain _brain;
        readonly bool _takesControl; // only the LLM brain drives kingdoms exclusively
        SimWorld _sim;
        readonly ConcurrentQueue<Result> _done = new ConcurrentQueue<Result>();
        readonly ConcurrentQueue<ConnectionResult> _statusDone = new ConcurrentQueue<ConnectionResult>();
        readonly HashSet<int> _controlled = new HashSet<int>();   // kingdoms the governors took over
        readonly HashSet<int> _playerTaken = new HashSet<int>();  // kingdoms the player explicitly controls
        readonly HashSet<int> _inFlightKingdoms = new HashSet<int>();
        readonly Dictionary<int, int> _consecutiveFailures = new Dictionary<int, int>();
        readonly Dictionary<int, DateTime> _nextAllowedUtc = new Dictionary<int, DateTime>();
        int _inFlight;
        int _callsThisHour;
        DateTime _hourStartUtc = DateTime.UtcNow;
        int _lastYear = int.MinValue;
        int _lastMonth = -1;
        int _tokensAtMonthStart;

        sealed class Result
        {
            public int Kingdom;
            public long KingdomUid;   // snapshot uid: stale answers are dropped
            public long Tick;
            public int Year;
            public GovernorDecision Decision;
            public Exception Error;
        }

        public GovernorRunner(GovernorSettings settings, IKingdomBrain brain)
        {
            Settings = settings ?? new GovernorSettings();
            _brain = brain;
            _takesControl = brain is GovernorBrain;
        }

        public void Bind(SimWorld sim)
        {
            _sim = sim;
            _lastYear = int.MinValue;
            _lastMonth = -1;
            _done.Clear();
            Volatile.Write(ref _inFlight, 0);
            _inFlightKingdoms.Clear();
            _consecutiveFailures.Clear();
            _nextAllowedUtc.Clear();
            // Restored from the decision log (save section 15): whose kingdoms are these?
            _controlled.Clear();
            _playerTaken.Clear();
            _controlled.UnionWith(Log.Governed);
            _playerTaken.UnionWith(Log.PlayerTaken);
            if (sim == null) return;
            // Urgent reactions (still cooldown-limited): kingdom founded, war declared on us,
            // city lost, famine, and diplomacy proposals waiting for our answer.
            sim.Events.Subscribe<KingdomFoundedEvent>(e => { if (_sim != null && KingdomInScope(e.Kingdom)) RequestUrgent(e.Kingdom); });
            sim.Events.Subscribe<WarDeclaredEvent>(e =>
            {
                if (_sim != null && KingdomInScope(e.Defender)) RequestUrgent(e.Defender);
            });
            sim.Events.Subscribe<CityRuinedEvent>(e =>
            {
                if (_sim != null && _sim.Civ != null)
                {
                    int k = _sim.Civ.KingdomOf(e.City);
                    if (k >= 0 && KingdomInScope(k)) RequestUrgent(k);
                }
            });
            sim.Events.Subscribe<FamineEvent>(e =>
            {
                if (_sim != null && _sim.Civ != null)
                {
                    int k = _sim.Civ.KingdomOf(e.City);
                    if (k >= 0 && KingdomInScope(k)) RequestUrgent(k);
                }
            });
            sim.Events.Subscribe<DiplomacyProposalEvent>(e =>
            {
                if (_sim != null && KingdomInScope(e.To)) RequestUrgent(e.To);
            });
        }

        // Called once per frame from GameBootstrap, always on the main thread.
        public void Update(EditCommandQueue queue)
        {
            if (_brain == null) return;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                PumpStatus();
                if (_sim != null) PumpResults(queue);
                return;
            }
            PumpStatus();
            if (_sim == null || !Settings.Enabled) return;

            long tick = _sim.Clock.Tick;
            int month = (int)(tick / SimConst.TicksPerMonth);
            if (month != _lastMonth)
            {
                _lastMonth = month;
                _tokensAtMonthStart = _brain is GovernorBrain g ? g.TokensUsed : 0;
            }

            PumpResults(queue);

            int year = _sim.Clock.Year;
            if (year != _lastYear)
            {
                _lastYear = year;
                ScheduleYear();
            }
        }

        // Yearly strategic decisions for every kingdom in scope.
        public void ScheduleYear()
        {
            if (_sim == null) return;
            foreach (int k in KingdomsInScope()) TryStart(k);
        }

        void RequestUrgent(int kingdom) => TryStart(kingdom);

        // Real-time cooldown per kingdom; events for the same kingdom merge into one request.
        bool CooldownPassed(int kingdom)
        {
            if (_inFlightKingdoms.Contains(kingdom)) return false;
            if (_nextAllowedUtc.TryGetValue(kingdom, out var at) && DateTime.UtcNow < at) return false;
            return true;
        }

        void TryStart(int kingdom)
        {
            if (_sim == null || !Settings.Enabled) return;
            if (!_sim.Meta.KingdomAlive(kingdom)) return;
            if (Volatile.Read(ref _inFlight) >= Math.Max(1, Settings.MaxConcurrent)) return;
            if (CallsThisHour() + _inFlightKingdoms.Count >= Math.Max(1, Settings.MaxCallsPerHour)) return;
            if (!CooldownPassed(kingdom)) return;
            if (_playerTaken.Contains(kingdom)) return; // the player owns this kingdom's strategy
            if (_brain is GovernorBrain g && TokensThisMonth(g) >= Settings.MonthlyTokenBudget) return;

            var snapshot = KingdomSnapshotBuilder.Build(_sim, kingdom);
            long uid = snapshot.KingdomUid;
            Interlocked.Increment(ref _inFlight);
            _inFlightKingdoms.Add(kingdom);
            _nextAllowedUtc[kingdom] = DateTime.UtcNow.AddSeconds(Math.Max(1, Settings.CooldownSeconds));
            long startTick = _sim.Clock.Tick;
            int startYear = _sim.Clock.Year;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                _ = RunBrowserDecision(snapshot, kingdom, uid, startTick, startYear);
            }
            else Task.Run(() =>
            {
                Result r;
                try { r = new Result { Kingdom = kingdom, KingdomUid = uid, Tick = startTick, Year = startYear, Decision = _brain.Decide(snapshot) }; }
                catch (Exception e) { r = new Result { Kingdom = kingdom, KingdomUid = uid, Tick = startTick, Year = startYear, Error = e }; }
                _done.Enqueue(r);
            });
        }

        async Task RunBrowserDecision(KingdomSnapshot snapshot, int kingdom, long uid, long tick, int year)
        {
            Result r;
            try
            {
                if (!(_brain is IAsyncKingdomBrain asyncBrain)) throw new InvalidOperationException("Tarayıcı AI istemcisi asenkron değil.");
                r = new Result { Kingdom = kingdom, KingdomUid = uid, Tick = tick, Year = year, Decision = await asyncBrain.DecideAsync(snapshot) };
            }
            catch (Exception e) { r = new Result { Kingdom = kingdom, KingdomUid = uid, Tick = tick, Year = year, Error = e }; }
            _done.Enqueue(r);
        }

        int CallsThisHour()
        {
            if ((DateTime.UtcNow - _hourStartUtc).TotalHours >= 1)
            {
                _hourStartUtc = DateTime.UtcNow;
                _callsThisHour = 0;
            }
            return _callsThisHour;
        }

        int TokensThisMonth(GovernorBrain g) => g.TokensUsed - _tokensAtMonthStart;

        void PumpStatus()
        {
            while (_statusDone.TryDequeue(out var s))
            {
                Status = s.Ok ? GovernorConnectionStatus.Ok : GovernorConnectionStatus.Failed;
                StatusMessage = s.Message;
                StatusUtc = DateTime.UtcNow;
                if (s.Ok && _brain is GovernorBrain { } brain && brain.Client is IJsonFormatClient client)
                {
                    client.SetJsonFormat(s.JsonFormatSupported);
                    Settings.JsonFormatSupported = s.JsonFormatSupported;
                    Settings.Save();
                }
            }
        }

        public void TestConnection()
        {
            Status = GovernorConnectionStatus.Testing;
            StatusMessage = "testing…";
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            if (InFlight != 0 || Status == GovernorConnectionStatus.Testing)
            {
                Status = GovernorConnectionStatus.Failed;
                StatusMessage = "Bağlantı testi veya AI isteği sürerken yeniden denemeyin.";
                return;
            }
            if (web) _ = RunBrowserConnectionTest();
            else Task.Run(() => _statusDone.Enqueue(ConnectionTester.Test(Settings)));
        }

        async Task RunBrowserConnectionTest()
        {
            try { _statusDone.Enqueue(await ConnectionTester.TestAsync(Settings, true)); }
            catch (Exception e) { _statusDone.Enqueue(new ConnectionResult { Ok = false, Message = e.Message }); }
        }

        // Panel toggle: hand a kingdom to the AI (or back to the player).
        public void SetKingdomAI(int kingdom, bool on)
        {
            if (on)
            {
                _playerTaken.Remove(kingdom);
                Log.SetPlayerTaken(kingdom, false);
                if (_takesControl)
                {
                    _controlled.Add(kingdom);
                    Log.SetGoverned(kingdom, true);
                }
            }
            else
            {
                _controlled.Remove(kingdom);
                Log.SetGoverned(kingdom, false);
                _playerTaken.Add(kingdom);
                Log.SetPlayerTaken(kingdom, true);
            }
        }

        void PumpResults(EditCommandQueue queue)
        {
            while (_done.TryDequeue(out var r))
            {
                Volatile.Write(ref _inFlight, Math.Max(0, _inFlight - 1));
                _inFlightKingdoms.Remove(r.Kingdom);
                if (!_sim.Meta.KingdomAlive(r.Kingdom) || _sim.Meta.Kingdoms.Header(r.Kingdom).Uid != r.KingdomUid)
                    continue; // stale answer: the kingdom died or the world moved on — drop silently

                if (r.Error != null)
                {
                    int fails = _consecutiveFailures.TryGetValue(r.Kingdom, out int f) ? f + 1 : 1;
                    _consecutiveFailures[r.Kingdom] = fails;
                    Debug.LogWarning($"[Governor] kingdom {r.Kingdom} decision failed ({fails}): {r.Error.Message}");
                    if (_takesControl && fails >= 3 && _controlled.Remove(r.Kingdom))
                    {
                        Log.SetGoverned(r.Kingdom, false);
                        queue.Metas.Add(new MetaCommand { Order = MetaOrder.Release, Kingdom = r.Kingdom }); // sim-AI takes over again
                    }
                    continue;
                }
                _consecutiveFailures[r.Kingdom] = 0;
                if (_brain is GovernorBrain g) _callsThisHour += Math.Max(1, g.LastCalls);

                var rejected = new List<string>();
                var commands = DecisionValidator.Validate(r.Decision, _sim, rejected);
                if (_takesControl && !r.Decision.IsWait() && _controlled.Add(r.Kingdom))
                {
                    Log.SetGoverned(r.Kingdom, true);
                    queue.Metas.Insert(0, new MetaCommand { Order = MetaOrder.Control, Kingdom = r.Kingdom });
                }
                queue.Metas.AddRange(commands.Metas);
                queue.Policies.AddRange(commands.Policies);

                var names = new List<string>();
                foreach (var c in commands.Metas) names.Add(c.Order + (c.Other >= 0 ? "(" + c.Other + ")" : ""));
                foreach (var p in commands.Policies)
                    names.Add($"SetPolicy(food {p.Food:0}, housing {p.Housing:0}, production {p.Production:0}, defense {p.Defense:0}, culture {p.Culture:0}, tax {p.Tax:0})");
                var entry = Log.Add(r.Tick, r.Year, r.Kingdom, r.Decision.Thought, names, rejected);
                foreach (var (name, thought) in r.Decision.AdvisorOpinions) entry.AddAdvisor(name, thought);
            }
        }

        bool KingdomInScope(int kingdom)
        {
            if (_sim == null || !_sim.Meta.KingdomAlive(kingdom)) return false;
            if (_playerTaken.Contains(kingdom)) return false;
            return Contains(KingdomsInScope(), kingdom);
        }

        IEnumerable<int> KingdomsInScope()
        {
            var meta = _sim.Meta;
            if (Settings.Scope == GovernorSettings.ScopeKind.All)
            {
                for (int k = 0; k < meta.Kingdoms.Count; k++)
                    if (meta.KingdomAlive(k) && !_playerTaken.Contains(k)) yield return k;
                yield break;
            }
            if (Settings.Scope == GovernorSettings.ScopeKind.Controlled)
            {
                for (int k = 0; k < meta.Kingdoms.Count; k++)
                    if (meta.KingdomAlive(k) && (meta.Kingdoms.Header(k).Flags & MetaFlags.PlayerControlled) != 0) yield return k;
                yield break;
            }
            // TopN by cached power.
            var top = new List<(int index, float power)>();
            for (int k = 0; k < meta.Kingdoms.Count; k++)
                if (meta.KingdomAlive(k) && !_playerTaken.Contains(k)) top.Add((k, meta.Kingdom(k).Power));
            top.Sort((a, b) =>
            {
                int byPower = b.power.CompareTo(a.power);
                return byPower != 0 ? byPower : a.index.CompareTo(b.index);
            });
            for (int i = 0; i < top.Count && i < Math.Max(1, Settings.TopN); i++) yield return top[i].index;
        }

        static bool Contains(IEnumerable<int> source, int value)
        {
            foreach (var v in source) if (v == value) return true;
            return false;
        }
    }

    static class GovernorBrainExtensions
    {
        public static bool IsWait(this GovernorDecision d)
        {
            if (d.Actions.Count == 0) return false;
            foreach (var a in d.Actions) if (a.Kind != GovernorActionKind.Wait) return false;
            return true;
        }
    }
}
