using System;
using System.Collections.Generic;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Stats
{
    /// <summary>한 판 동안 같은 것(종 + 피해 분류 + 보스 패턴)에 맞은 횟수.</summary>
    public sealed class RunHitTally
    {
        /// <summary>때린 개체의 종 id(enemy.* / boss.*). 모르면 null.</summary>
        public string DefId;
        public PlayerDamageSource Source;
        /// <summary>보스 패턴 열쇠(보스가 때렸고 패턴 직후일 때만). 없으면 null.</summary>
        public string Pattern;
        public int Count;
        /// <summary>마지막으로 맞은 틱(같은 횟수면 늦은 것을 앞에 둔다).</summary>
        public int LastTick;

        public bool Same(string defId, PlayerDamageSource src, string pattern) => DefId == defId && Source == src && Pattern == pattern;
        /// <summary>예고가 있는 공격(바닥 예고·돌진 띠).</summary>
        public bool Telegraphed => RunStatsCollector.IsTelegraphed(Source);
    }

    /// <summary>판 끝 요약에 쓰는 표시 통계(표시 이벤트만 모은 것 — 시뮬레이션 상태·점수·보상과 무관하다).</summary>
    public sealed class RunStats
    {
        public string RunId;
        public string CharId;
        /// <summary>판 중 이벤트를 하나라도 받았다(판 중에 부팅해 들어온 자동 정산은 false).</summary>
        public bool HasData;
        public int HitsTaken;
        public bool HasLastHit;
        public RunHitTally LastHit;
        public readonly List<RunHitTally> Hits = new List<RunHitTally>();
        public int MaxHitDamage;
        public int Grazes;
        public int MaxGrazeChain;
        public int MaxCombo;
        public string TopBlockId;
        public int TopBlockShots;
        public int ShotsFired;
        public int ArtsFired;
        public int ArtsFizzled;
        public int HeatLocks;
        public int Kills;
        public int ElitesKilled;
        public int BossesKilled;
        public int Crits;
        public int WeakHits;

        /// <summary>가장 많이 맞은 것(같으면 나중에 맞은 것). 없으면 null.</summary>
        public RunHitTally MostHit
        {
            get
            {
                RunHitTally best = null;
                for (int i = 0; i < Hits.Count; i++)
                {
                    RunHitTally h = Hits[i];
                    if (best == null || h.Count > best.Count || (h.Count == best.Count && h.LastTick > best.LastTick)) best = h;
                }
                return best;
            }
        }
    }

    /// <summary>
    /// 판 끝 요약용 통계 수집기(표시 쪽). IGameSession.SimEventRaised로 본판(원정)의 표시 이벤트만 받아 센다 — 시뮬레이션에 상태를 더하지 않으므로
    /// 결정론·스냅숏·해시와 무관하다. 연습·튜토리얼·보조 판은 세지 않는다. 판이 끝나면(RunEnded) Last에 그 판의 통계가 남는다.
    /// 사인: PlayerHit(E = 때린 적 id, F = PlayerDamageSource)과 EnemySpawned/BossIntroBegin(E → 종 id), BossPatternStarted(E, Id = 패턴)를 잇는다.
    /// </summary>
    public sealed class RunStatsCollector
    {
        const int PlayerSlot = 0;
        const string BossPrefix = "boss.";

        readonly Dictionary<int, string> _defById = new Dictionary<int, string>();
        readonly Dictionary<int, string> _patternById = new Dictionary<int, string>();
        readonly Dictionary<int, int> _patternTickById = new Dictionary<int, int>();
        readonly Dictionary<string, int> _shotsByBlock = new Dictionary<string, int>();
        int _chain;
        SimWorld _world;
        IGameSession _session;

        /// <summary>보스 패턴 시작 뒤 이 틱 안에 맞으면 그 패턴에 맞은 것으로 본다(DataSolo/Recap.json boss_pattern_window_seconds).</summary>
        /// <summary>0 이하 = 시간 제한 없이 마지막 패턴.</summary>
        public int PatternWindowTicks;

        /// <summary>지금 세는 판(없으면 null).</summary>
        public RunStats Current { get; private set; }
        /// <summary>마지막으로 끝난(또는 지금 세는) 판의 통계.</summary>
        public RunStats Last { get; private set; }

        public static bool IsTelegraphed(PlayerDamageSource s) => s == PlayerDamageSource.Telegraph || s == PlayerDamageSource.Dash;

        /// <summary>세션에 붙는다(부팅 때 한 번). 세션이 없으면 아무것도 하지 않는다.</summary>
        public void Attach(IGameSession session)
        {
            if (session == null || ReferenceEquals(_session, session)) return;
            _session = session;
            session.RunStarted += OnRunStarted;
            session.SimEventRaised += OnSimEvent;
            session.RunEnded += OnRunEnded;
        }

        void OnRunStarted(SimWorld w)
        {
            if (w == null || w.Run == null || w.Run.Config == null || w.Run.Config.Mode != RunMode.Endless) { _world = null; return; }
            Begin(w.Run.Config.RunId, w.Run.Config.CharId);
            _world = w;
        }

        void OnSimEvent(SimWorld w, SimEvent e)
        {
            if (_world == null || !ReferenceEquals(w, _world)) return;
            Feed(e);
        }

        void OnRunEnded(RunSummary s)
        {
            _world = null;
            Current = null;
        }

        /// <summary>새 판을 센다(시험은 이것과 Feed만으로 쓴다).</summary>
        public void Begin(string runId, string charId)
        {
            _defById.Clear();
            _patternById.Clear();
            _patternTickById.Clear();
            _shotsByBlock.Clear();
            _chain = 0;
            Current = new RunStats { RunId = runId, CharId = charId };
            Last = Current;
        }

        /// <summary>그 판의 통계(RunId가 맞을 때만. 다르면 null — 자동 정산처럼 이 실행에서 세지 않은 판).</summary>
        public RunStats For(string runId)
        {
            if (Last == null || !Last.HasData) return null;
            return string.Equals(Last.RunId ?? string.Empty, runId ?? string.Empty, StringComparison.Ordinal) ? Last : null;
        }

        public void Feed(SimEvent e)
        {
            RunStats s = Current;
            if (s == null) return;
            s.HasData = true;
            switch (e.Type)
            {
                case SimEventType.EnemySpawned:
                    if (!string.IsNullOrEmpty(e.Id)) _defById[e.EntityId] = e.Id;
                    break;
                case SimEventType.BossIntroBegin:
                    if (!string.IsNullOrEmpty(e.Id)) _defById[e.EntityId] = e.Id;
                    break;
                case SimEventType.BossPatternStarted:
                    _patternById[e.EntityId] = e.Id;
                    _patternTickById[e.EntityId] = e.Tick;
                    break;
                case SimEventType.PlayerHit:
                    if (e.Slot != PlayerSlot) break;
                    OnHit(s, e);
                    break;
                case SimEventType.GrazeAwarded:
                    if (e.Slot != PlayerSlot) break;
                    s.Grazes++;
                    _chain++;
                    if (_chain > s.MaxGrazeChain) s.MaxGrazeChain = _chain;
                    break;
                case SimEventType.ComboChanged:
                    if (e.Slot == PlayerSlot && e.A > s.MaxCombo) s.MaxCombo = e.A;
                    break;
                case SimEventType.EnemyDamaged:
                    if (e.Slot != PlayerSlot) break;
                    if (e.A > s.MaxHitDamage) s.MaxHitDamage = e.A;
                    if ((e.B & DamageFlags.Crit) != 0) s.Crits++;
                    if ((e.B & DamageFlags.Weak) != 0) s.WeakHits++;
                    break;
                case SimEventType.EnemyKilled:
                    if (e.B == 3) break;   // 보상 없는 처치(졸개 정리 등)는 세지 않는다
                    s.Kills++;
                    if (e.B == 1) s.ElitesKilled++;
                    else if (e.B == 2) s.BossesKilled++;
                    break;
                case SimEventType.ProjectileSpawned:
                    if (e.Slot != PlayerSlot || string.IsNullOrEmpty(e.Id) || e.B != 0) break;   // 스코프 깊이 0(발사한 탄)만
                    _shotsByBlock.TryGetValue(e.Id, out int n);
                    n++;
                    _shotsByBlock[e.Id] = n;
                    if (n > s.TopBlockShots || (n == s.TopBlockShots && string.CompareOrdinal(e.Id, s.TopBlockId) < 0))
                    {
                        s.TopBlockShots = n;
                        s.TopBlockId = e.Id;
                    }
                    break;
                case SimEventType.ShotFired:
                    if (e.Slot == PlayerSlot) s.ShotsFired++;
                    break;
                case SimEventType.ArtFired:
                    if (e.Slot == PlayerSlot) s.ArtsFired++;
                    break;
                case SimEventType.ArtFizzled:
                    if (e.Slot == PlayerSlot) s.ArtsFizzled++;
                    break;
                case SimEventType.HeatLockEntered:
                    if (e.Slot == PlayerSlot) s.HeatLocks++;
                    break;
            }
        }

        void OnHit(RunStats s, SimEvent e)
        {
            _chain = 0;
            var src = (PlayerDamageSource)(int)e.F;
            if (src == PlayerDamageSource.Scripted) return;   // 첫 만남의 되비침(각본)은 사인이 아니다
            s.HitsTaken++;
            string def = null;
            if (e.EntityId != 0) _defById.TryGetValue(e.EntityId, out def);
            if (def == null && e.EntityId != 0 && _world != null) def = LookupDef(_world, e.EntityId);
            string pattern = null;
            if (def != null && def.StartsWith(BossPrefix, StringComparison.Ordinal) && src != PlayerDamageSource.Contact
                && _patternById.TryGetValue(e.EntityId, out string key) && _patternTickById.TryGetValue(e.EntityId, out int pt)
                && (PatternWindowTicks <= 0 || e.Tick - pt <= PatternWindowTicks))
                pattern = key;

            RunHitTally t = null;
            for (int i = 0; i < s.Hits.Count; i++) if (s.Hits[i].Same(def, src, pattern)) { t = s.Hits[i]; break; }
            if (t == null) { t = new RunHitTally { DefId = def, Source = src, Pattern = pattern }; s.Hits.Add(t); }
            t.Count++;
            t.LastTick = e.Tick;
            s.LastHit = t;
            s.HasLastHit = true;
        }

        static string LookupDef(SimWorld w, int id)
        {
            if (w.Enemies == null) return null;
            for (int i = 0; i < w.Enemies.Count; i++)
                if (w.Enemies[i] != null && w.Enemies[i].Id == id) return w.Enemies[i].DefId;
            return null;
        }
    }
}
