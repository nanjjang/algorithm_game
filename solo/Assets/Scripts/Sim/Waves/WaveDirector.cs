using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Waves
{
    /// <summary>
    /// 웨이브 디렉터: 곡선, 구성 생성, 묶음 방출, 변덕, 지체 방지, 별똥, 날씨. 06 요약서 1~3절, 01 요약서 7절.
    /// 웨이브마다의 보조 상태(가시 띠 표, 마지막 숨의 표, 진행 바 분모)는 w.Wave 객체에 매달아 둔다 —
    /// 모듈 객체 하나가 세계 여럿을 번갈아 돌려도 섞이지 않는다(전역 상태 없음).
    /// </summary>
    public sealed class WaveDirector : WaveModule
    {
        /// <summary>진행 중인 웨이브 하나의 보조 상태(WaveState에 없는 것).</summary>
        sealed class WaveRuntime
        {
            public bool FirstReleased;
            // 좁은 들
            public ThornBorderDef Thorn;
            public int ThornTicket;
            public int ThornFallbackTick;
            public bool ThornHazardsSpawned;
            // 마지막 숨
            public DeathCircleDef DeathCircle;
            public readonly List<int> DeathTickets = new List<int>();
            // 진행 바
            public float InitialHp;
            public float PendingHp;
            // 떼 출현: 보상 없이 나온 늘린 개체(진행 바는 이들도 센다)
            public readonly HashSet<int> HordeExtraIds = new HashSet<int>();
            // 떼 출현: 지금 쏟아지는 무리(한 틱 안에서만 이어진다)
            public int PackTick = -1;
            public int PackLeft;
            public Vector2 PackAnchor;
            public Vector2 PackInward;
        }

        IContentDb _db;
        WavesDef _waves;
        WaveTuning _tuning = new WaveTuning();
        readonly List<Vector2> _spots = new List<Vector2>();

        bool _hordeEnabled = true;

        public WaveDirector() { }

        /// <summary>
        /// 떼 출현 손잡이(G09.json "horde")를 쓰는가. 기본 true(켜고 끄는 최종 값은 JSON의 enabled).
        /// false면 명세 그대로 — 명세 벡터 테스트가 끈다.
        /// </summary>
        public bool HordeEnabled
        {
            get { return _hordeEnabled; }
            set { _hordeEnabled = value; if (_db != null) _tuning = WaveTuning.From(_db, value); }
        }

        /// <summary>지금 쓰는 보충 수치(읽기만 한다).</summary>
        public WaveTuning Tuning => _tuning;
        /// <summary>곡선 함수(HpMul, Budget, TelegraphCap)를 세계 없이 바로 쓸 때.</summary>
        public WaveDirector(IContentDb db) { BindDb(db); }

        // 곡선 함수에는 세계·콘텐츠 인자가 없다 → 세계를 받는 메서드에 들어올 때마다 콘텐츠를 붙잡아 둔다.
        void Bind(SimWorld w) { if (w != null && w.Content != null && !ReferenceEquals(w.Content, _db)) BindDb(w.Content); }

        void BindDb(IContentDb db)
        {
            if (db == null || db.Waves == null) return;
            _db = db;
            _waves = db.Waves;
            _tuning = WaveTuning.From(db, _hordeEnabled);
        }

        WaveRuntime Runtime(WaveState ws)
        {
            return ws?.Scratch as WaveRuntime;
        }

        // ------------------------------------------------------------ 곡선

        /// <summary>체력 배율 H(w). 콘텐츠가 아직 안 붙었으면 1(웨이브 1의 값 — 연습 방은 이것으로 충분하다).</summary>
        public override float HpMul(int wave) => _waves != null ? WaveCurves.HpMul(_waves, wave) : 1f;
        public override int Budget(int wave) => _waves != null ? _tuning.Budget(_waves, wave) : 0;
        public override int TelegraphCap(int wave) => _waves != null ? WaveCurves.TelegraphCap(_waves, wave) : int.MaxValue;

        // ------------------------------------------------------------ 구성

        public override WavePlan BuildPlan(SimWorld w, int wave)
        {
            Bind(w);
            if (_db == null) return base.BuildPlan(w, wave);
            var plan = WavePlanner.Build(_db, _tuning, w.Mods.Bosses, w.Seed, wave, IsPhantomFirst(w, wave), BossPreviewOverrides(w, wave));
            // 떼 출현(솔로 조정): 명세 구성 위에 잡몹만 덧붙인다. 난수 없음 → 야영 미리보기와 실제가 같다.
            WaveHorde.Expand(_db, _tuning, plan);
            return plan;
        }

        /// <summary>
        /// 보스 시연(솔로 조정, G09.json boss_preview): 다음 웨이브가 보스 웨이브면 그 보스의 패턴 일부를 쓰는 일반 적을 고정 개수로 더한다.
        /// 고를 보스는 보스 웨이브가 실제로 쓸 값(Bosses.SelectBoss — 순수 함수)과 같다. 없으면 null(명세 그대로).
        /// </summary>
        PlanOverrides BossPreviewOverrides(SimWorld w, int wave)
        {
            if (_tuning.BossPreview.Count == 0 || DayCycle.IsBossWave(wave) || !DayCycle.IsBossWave(wave + 1)) return null;
            string boss = w.Mods.Bosses != null ? w.Mods.Bosses.SelectBoss(_db, wave + 1, IsPhantomFirst(w, wave + 1)) : null;
            List<SpeciesCountDef> list;
            if (boss == null || !_tuning.BossPreview.TryGetValue(boss, out list)) return null;
            return new PlanOverrides { ExtraFixed = list };
        }

        /// <summary>프로필이 처음 닿는 첫 보스 웨이브(10)인가: 판 시작 때 고정한 값이고, 이 판에서 이미 치렀으면 아니다.</summary>
        bool IsPhantomFirst(SimWorld w, int wave)
        {
            var cfg = w.Run != null ? w.Run.Config : null;
            return cfg != null && cfg.PhantomFirst && !w.Run.PhantomFirstDone && wave == DayCycle.DayLength;
        }

        // ------------------------------------------------------------ 웨이브 시작

        public override int BeginWave(SimWorld w, WavePlan plan)
        {
            Bind(w);
            if (plan == null) plan = BuildPlan(w, Math.Max(1, w.Run != null ? w.Run.Wave : 1));
            if (_db == null) return base.BeginWave(w, plan);

            string prevTod = w.Wave != null && w.Wave.Plan != null ? w.Wave.Plan.Tod : null;
            var ws = new WaveState { Wave = plan.Wave, Plan = plan, StartTick = w.Tick, LastBundleTick = w.Tick };
            // 점수 재료 중 구성에서 오는 것(Run과 점수 계산이 읽는다)
            ws.Stats.Wave = plan.Wave;
            ws.Stats.IsBoss = plan.BossId != null;
            ws.Stats.PhantomFirst = plan.PhantomFirst;
            ws.Stats.Whims.AddRange(plan.Whims);
            ws.TelegraphCap = TelegraphCap(plan.Wave);
            ws.BundleIntervalTicks = SimConst.Ticks(_waves.BundleIntervalSeconds);

            var rt = new WaveRuntime();
            // 변덕 적용(효과 필드가 있는 것만. 큰 무리·우두머리 행차·긴 밤은 구성 생성에서 이미 반영됐다)
            for (int i = 0; i < plan.Whims.Count; i++)
            {
                var m = _db.Modifier(plan.Whims[i]);
                if (m == null) continue;
                if (m.EnemyHpMultiplier.HasValue) ws.EnemyHpMul *= m.EnemyHpMultiplier.Value;
                if (m.BundleIntervalSeconds.HasValue) ws.BundleIntervalTicks = SimConst.Ticks(m.BundleIntervalSeconds.Value);
                if (m.EnemyShots != null)
                {
                    ws.EnemyShotCountMul *= m.EnemyShots.CountMultiplier;
                    ws.EnemyShotSpeedMul *= m.EnemyShots.SpeedMultiplier;
                }
                if (m.DeathCircle != null) { ws.LastBreath = true; rt.DeathCircle = m.DeathCircle; }
                if (m.ThornBorder != null) rt.Thorn = m.ThornBorder;
            }

            // 진행 바의 분모: 구성 전체의 최대 체력 합
            float hpMul = HpMul(plan.Wave);
            for (int i = 0; i < plan.Entries.Count; i++) rt.InitialHp += EntryHp(plan.Entries[i], hpMul, ws.EnemyHpMul);
            rt.PendingHp = rt.InitialHp;

            w.Wave = ws;
            ws.Scratch = rt;

            RollWeather(w, plan);
            RollStarfall(w, plan);

            if (prevTod != plan.Tod) w.Emit(SimEventType.TodChanged, id: plan.Tod);
            w.Emit(SimEventType.WaveStarted, a: plan.Wave, id: plan.Frame);

            return rt.Thorn != null ? BeginThornBorder(w, ws, rt) : 0;
        }

        float EntryHp(in WaveEntry entry, float hpMul, float whimMul)
        {
            var def = _db.Enemy(entry.Species);
            if (def == null) return 0f;
            return def.BaseHp * hpMul * (entry.Elite ? _waves.EliteHpMul : 1f) * whimMul;
        }

        // ------------------------------------------------------------ 좁은 들

        /// <summary>가장자리 띠 넷(위, 아래, 왼쪽, 오른쪽). 모서리는 위·아래 띠가 맡아 겹치지 않는다.</summary>
        void ThornBand(SimWorld w, float width, int index, out Vector2 pos, out Vector2 dir, out float length)
        {
            float half = width / 2;
            float aw = w.ArenaWidth, ah = w.ArenaHeight;
            switch (index)
            {
                case 0: pos = new Vector2(0f, half); dir = new Vector2(1f, 0f); length = aw; break;
                case 1: pos = new Vector2(0f, ah - half); dir = new Vector2(1f, 0f); length = aw; break;
                case 2: pos = new Vector2(half, width); dir = new Vector2(0f, 1f); length = ah - width * 2; break;
                default: pos = new Vector2(aw - half, width); dir = new Vector2(0f, 1f); length = ah - width * 2; break;
            }
        }

        const int ThornBandCount = 4;

        /// <summary>좁은 들의 예고를 청하고(상한·R5를 보지 않는다) 예고 틱 수를 돌려준다. 가시밭은 예고가 끝나는 틱에 깐다.</summary>
        int BeginThornBorder(SimWorld w, WaveState ws, WaveRuntime rt)
        {
            var tb = rt.Thorn;
            int ticks = SimConst.Ticks(tb.TelegraphSeconds);
            var req = new TelegraphRequest { OwnerEnemyId = 0, CastId = w.NewCastId(), Bypass = true, CountAsOne = true };
            for (int i = 0; i < ThornBandCount; i++)
            {
                Vector2 pos, dir; float length;
                ThornBand(w, tb.Width, i, out pos, out dir, out length);
                req.Zones.Add(new ZoneSpec
                {
                    Shape = ZoneShape.Band, Anchor = ZoneAnchor.Fixed, Pos = pos, Dir = dir, Width = tb.Width, Length = length,
                    TelegraphTicks = ticks, BaseDamage = tb.Damage, DamageSource = PlayerDamageSource.Lingering,
                    Grazeable = false, TargetSlot = -1
                });
            }
            rt.ThornTicket = w.Mods.Threats.RequestTelegraph(w, req);
            rt.ThornFallbackTick = w.Tick + ticks;
            return ticks;
        }

        /// <summary>예고가 끝나는 틱(판정 틱과 같은 틱의 7단계)에 웨이브 끝까지 남는 가시밭을 깐다.</summary>
        void SpawnThornHazards(SimWorld w, WaveRuntime rt)
        {
            if (rt.Thorn == null || rt.ThornHazardsSpawned) return;
            int start, detonate;
            if (!w.Mods.Threats.TryGetTiming(w, rt.ThornTicket, out start, out detonate))
            {
                // 표를 모르는 경우(예고가 아직 대기 중이거나 Threats가 빈 구현): 청한 틱 + 예고 길이로 센다
                if (w.Mods.Threats.Status(w, rt.ThornTicket) == TicketStatus.Waiting) return;
                detonate = rt.ThornFallbackTick;
            }
            if (w.Tick < detonate) return;
            rt.ThornHazardsSpawned = true;
            var tb = rt.Thorn;
            for (int i = 0; i < ThornBandCount; i++)
            {
                Vector2 pos, dir; float length;
                ThornBand(w, tb.Width, i, out pos, out dir, out length);
                w.Mods.Threats.SpawnHazard(w, new HazardSpec
                {
                    Kind = HazardKind.ThornBorder, OwnerEnemyId = 0, Shape = ZoneShape.Band, Pos = pos, Dir = dir,
                    Width = tb.Width, Length = length, DurationTicks = 0, BaseDamage = tb.Damage,
                    ContactCooldownTicks = SimConst.Ticks(tb.ContactIntervalSeconds), MaxCount = 0
                });
            }
        }

        /// <summary>지금 웨이브의 가시 띠 폭(좁은 들이 아니면 0).</summary>
        float ThornInset(SimWorld w)
        {
            var rt = Runtime(w.Wave);
            if (rt != null) return rt.Thorn != null ? rt.Thorn.Width : 0f;
            var plan = w.Wave != null ? w.Wave.Plan : null;
            if (plan == null || _db == null) return 0f;
            for (int i = 0; i < plan.Whims.Count; i++)
            {
                var m = _db.Modifier(plan.Whims[i]);
                if (m != null && m.ThornBorder != null) return m.ThornBorder.Width;
            }
            return 0f;
        }

        // ------------------------------------------------------------ 날씨·별똥 추첨 (웨이브 시작에 한 번)

        const string WhenWaveStart = "wave_start", WhenDayChange = "day_change", SpanTimed = "timed";

        void RollWeather(SimWorld w, WavePlan plan)
        {
            var weather = w.Weather;
            var rng = w.Rng(RngStream.Weather);
            var events = _db.WeatherEvents;

            // 지난 웨이브의 날씨가 남아 있으면 걷는다(EndWave를 거치지 않은 경우)
            ClearWeather(w);

            int day = DayCycle.Day(plan.Wave);
            int variants = Math.Max(1, _tuning.PropVariants);
            weather.PropVariant = (day - 1) % variants;
            if (DayCycle.Slot(plan.Wave) == 1)
            {
                weather.ShowersToday = 0;
                if (plan.Wave > 1)
                {
                    // 날이 바뀌었다: 풀밭 무늬·소품 배치가 다음 벌로, 버린 물건
                    w.Emit(SimEventType.DayChanged, a: day, b: weather.PropVariant);
                    if (events != null)
                        for (int i = 0; i < events.Count; i++)
                        {
                            var ev = events[i];
                            if (ev.When != WhenDayChange || !rng.RollPercent(ev.ChancePercent)) continue;
                            int v = rng.Range(0, Math.Max(1, ev.Variants));
                            if (ev.Id == Ids.Event.DiscardedItem) weather.DiscardedItemVariant = v;
                            w.Emit(SimEventType.WeatherStarted, a: v, id: ev.Id);
                        }
                }
            }

            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.When != WhenWaveStart) continue;
                // 별똥 결정은 starfall.json의 수치로 따로 뽑는다(이 줄은 목록의 자리)
                if (ev.Id == Ids.Event.StarCrystal) continue;
                if (ev.Tods != null && ev.Tods.Count > 0 && !ev.Tods.Contains(plan.Tod)) continue;
                bool timed = ev.Span == SpanTimed;
                if (timed && ev.MaxPerDay > 0 && weather.ShowersToday >= ev.MaxPerDay) continue;
                if (!rng.RollPercent(ev.ChancePercent)) continue;

                if (ev.Id == Ids.Event.MasterAdvice)
                {
                    // 훈수: 문구 번호(1부터) = event.master_advice.line.<n>
                    w.Emit(SimEventType.MasterAdvice, a: 1 + rng.Range(0, Math.Max(1, ev.Variants)));
                    continue;
                }
                if (timed)
                {
                    weather.ShowersToday++;
                    weather.ShowerUntilTick = w.Tick + SimConst.Ticks(ev.DurationSeconds);
                }
                if (!weather.Active.Contains(ev.Id)) weather.Active.Add(ev.Id);
                w.Emit(SimEventType.WeatherStarted, id: ev.Id);
            }
        }

        bool IsTimedWeather(string eventId)
        {
            var events = _db != null ? _db.WeatherEvents : null;
            if (events == null) return false;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Id == eventId) return events[i].Span == SpanTimed;
            return false;
        }

        void ClearWeather(SimWorld w)
        {
            var active = w.Weather.Active;
            for (int i = 0; i < active.Count; i++) w.Emit(SimEventType.WeatherEnded, id: active[i]);
            active.Clear();
        }

        void RollStarfall(SimWorld w, WavePlan plan)
        {
            var star = w.Starfall;
            star.Phase = StarfallPhase.None;
            star.Pos = default(Vector2);
            star.ScheduledTick = 0;
            star.PhaseStartTick = 0;
            star.NextRetryTick = 0;
            var sf = _db.Starfall;
            if (sf == null || plan.BossId != null || plan.Tod != sf.OnlyTod) return;
            var rng = w.Rng(RngStream.Starfall);
            if (!rng.RollPercent(sf.ChancePercent)) return;
            star.Phase = StarfallPhase.Scheduled;
            // 웨이브 시계(WaveTick) 기준: 웨이브 시작 뒤 from~to 초 사이 균등
            star.ScheduledTick = rng.Range(SimConst.Ticks(sf.DropWindowSeconds.From), SimConst.Ticks(sf.DropWindowSeconds.To) + 1);
        }

        // ------------------------------------------------------------ 7단계: 방출

        public override void Stage7_Spawns(SimWorld w)
        {
            Bind(w);
            var ws = w.Wave;
            var rt = Runtime(ws);
            if (rt == null || _db == null) return;

            SpawnThornHazards(w, rt);

            if (w.Run.Phase != RunPhase.WaveCombat) return;
            var plan = ws.Plan;
            if (plan == null || plan.BossId != null) return;
            if (ws.AllReleased && ws.SpawnQueue.Count == 0) return;
            // 플레이어가 없으면(전원 쓰러짐) 그 틱의 방출을 미룬다
            if (w.NearestPlayer(ArenaCenter(w)) == null) return;

            int alive = w.AliveEnemies();
            if (!ws.AllReleased)
            {
                // 첫 묶음은 웨이브 0틱. 다음 묶음은 간격이 지났거나, 대기열이 비었고 살아 있는 적이 3 이하면 바로
                bool due = !rt.FirstReleased
                           || w.Tick - ws.LastBundleTick >= ws.BundleIntervalTicks
                           || (ws.SpawnQueue.Count == 0 && alive <= _waves.BundleEarlyReleaseAlive);
                if (due) ReleaseNextBundle(w, ws, rt);
            }

            // 동시 상한: 자리가 나는 대로 한 마리씩(박쥐 무리도 낱마리로). 떼 출현이면 상한 × 배율
            int maxAlive = WaveHorde.MaxAlive(_tuning, _waves.MaxAlive);
            int n = 0;
            while (n < ws.SpawnQueue.Count && alive < maxAlive)
            {
                SpawnEntry(w, ws, rt, ws.SpawnQueue[n]);
                n++;
                alive++;
            }
            if (n > 0) ws.SpawnQueue.RemoveRange(0, n);
        }

        static Vector2 ArenaCenter(SimWorld w) => new Vector2(w.ArenaWidth / 2, w.ArenaHeight / 2);

        static bool HasEntriesFrom(WavePlan plan, int bundle)
        {
            for (int i = 0; i < plan.Entries.Count; i++) if (plan.Entries[i].Bundle >= bundle) return true;
            return false;
        }

        void ReleaseNextBundle(SimWorld w, WaveState ws, WaveRuntime rt)
        {
            var plan = ws.Plan;
            // 빈 묶음은 건너뛴다
            while (HasEntriesFrom(plan, ws.NextBundle))
            {
                int b = ws.NextBundle++;
                int added = 0;
                for (int i = 0; i < plan.Entries.Count; i++)
                    if (plan.Entries[i].Bundle == b) { ws.SpawnQueue.Add(plan.Entries[i]); added++; }
                if (added > 0) break;
            }
            ws.LastBundleTick = w.Tick;
            rt.FirstReleased = true;
            rt.PackLeft = 0;
            if (!HasEntriesFrom(plan, ws.NextBundle))
            {
                ws.NextBundle = Math.Max(ws.NextBundle, plan.BundleCount);
                ws.AllReleased = true;
            }
        }

        void SpawnEntry(SimWorld w, WaveState ws, WaveRuntime rt, in WaveEntry entry)
        {
            var def = _db.Enemy(entry.Species);
            rt.PendingHp -= EntryHp(entry, HpMul(ws.Wave), ws.EnemyHpMul);
            if (def == null) return;
            Vector2 pos = IsPackable(ws, def, entry) ? PackSpawnPoint(w, ws, rt, def) : PickSpawnPoint(w, def);
            // 떼 출현이 늘린 개체: 합계 유지면 보상(골드·점수·체력 조각 추첨) 없음 → 처치 보상 합계 = 명세 구성의 합계
            bool hordeNoReward = _tuning.HordeKeepRewardTotal && WaveHorde.IsExtra(entry);
            var spec = new EnemySpawnSpec { DefId = entry.Species, Elite = entry.Elite, Pos = pos, NoReward = hordeNoReward };
            var e = w.Mods.Enemies.Spawn(w, spec);
            if (hordeNoReward && e != null) rt.HordeExtraIds.Add(e.Id);
        }

        // ------------------------------------------------------------ 떼 출현: 무리 자리

        /// <summary>무리로 나오는가: 무리 출현이 켜졌고, 엘리트가 아니고, 잡몹이고, 안쪽에 나오는 종이 아니다.</summary>
        bool IsPackable(WaveState ws, EnemyDef def, in WaveEntry entry)
        {
            if (!_tuning.HordePacks || entry.Elite) return false;
            float wm, pm;
            if (InteriorSpawn(def, out wm, out pm)) return false;
            return WaveHorde.IsExtra(entry) || WaveHorde.IsNormalSpecies(_db, _tuning, def, ws.Plan != null ? ws.Plan.Whims : null);
        }

        /// <summary>
        /// 무리 한 마리의 자리. 무리가 없거나 다 찼거나 다른 틱이면 새 무리: 크기 = 그 웨이브의 [min, max](pack_size_by_wave 계단) 균등, 자리 = 보통 등장 자리(먼 두 변).
        /// 마리마다 무리 자리에서 반경 안 균등한 점을 뽑고, 바깥쪽 성분은 안쪽으로 뒤집고 전장 안(벽·가시 띠 안쪽)으로 자른다.
        /// 난수는 등장 흐름(RngStream.Spawn) 하나만 쓴다.
        /// </summary>
        Vector2 PackSpawnPoint(SimWorld w, WaveState ws, WaveRuntime rt, EnemyDef def)
        {
            var rng = w.Rng(RngStream.Spawn);
            if (rt.PackLeft <= 0 || rt.PackTick != w.Tick)
            {
                int lo, hi;
                _tuning.PackSizeRange(ws.Wave, out lo, out hi);
                rt.PackLeft = rng.Range(lo, hi + 1);
                rt.PackTick = w.Tick;
                rt.PackAnchor = PickEdgePoint(w, rng, def, out rt.PackInward);
            }
            rt.PackLeft--;
            float radius = def != null ? def.BodyRadius : 0f;
            float edge = ThornInset(w) + radius;
            float r = _tuning.HordePackScatterRadius * Mathf.Sqrt(rng.Value());
            float ang = rng.Range(0f, Mathf.PI * 2f);
            var off = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            float d = Vector2.Dot(off, rt.PackInward);
            if (d < 0f) off -= rt.PackInward * (2f * d);
            var pos = rt.PackAnchor + off;
            float aw = w.ArenaWidth, ah = w.ArenaHeight;
            pos.x = Mathf.Clamp(pos.x, Mathf.Min(edge, aw / 2), Mathf.Max(aw - edge, aw / 2));
            pos.y = Mathf.Clamp(pos.y, Mathf.Min(edge, ah / 2), Mathf.Max(ah - edge, ah / 2));
            return pos;
        }

        // ------------------------------------------------------------ 등장 자리

        public override Vector2 PickSpawnPoint(SimWorld w, EnemyDef def)
        {
            Bind(w);
            var rng = w.Rng(RngStream.Spawn);
            float inset = ThornInset(w);
            float radius = def != null ? def.BodyRadius : 0f;

            float wallMargin, playerMargin;
            if (InteriorSpawn(def, out wallMargin, out playerMargin))
                return PickInterior(w, rng, Mathf.Max(wallMargin, inset + radius), playerMargin);

            Vector2 inward;
            return PickEdgePoint(w, rng, def, out inward);
        }

        /// <summary>변 위의 등장 자리(06 요약서 2.2절). inward = 고른 변에서 전장 안쪽을 보는 단위 벡터.</summary>
        Vector2 PickEdgePoint(SimWorld w, Pcg32 rng, EnemyDef def, out Vector2 inward)
        {
            float inset = ThornInset(w);
            float radius = def != null ? def.BodyRadius : 0f;
            Vector2 center = ArenaCenter(w);
            var nearest = w.NearestPlayer(center);
            Vector2 pp = nearest != null ? nearest.Pos : center;
            float aw = w.ArenaWidth, ah = w.ArenaHeight;
            // 플레이어에게서 먼 두 변: 가로 변 하나 + 세로 변 하나. 정확히 가운데면 위(y = 0)·오른쪽(x = W)
            bool bottom = pp.y < ah / 2;
            bool right = pp.x <= aw / 2;
            // 변을 따라가는 범위: 모서리 1칸 제외(좁은 들이면 가시 띠 안쪽으로)
            float along = Mathf.Max(_tuning.SpawnCornerExclusion, inset + radius);
            float lenH = Mathf.Max(0f, aw - along * 2);
            float lenV = Mathf.Max(0f, ah - along * 2);
            // 변에서 떨어지는 거리: 몸이 벽(또는 가시 띠) 바로 안쪽에 닿게
            float edge = inset + radius;

            Vector2 pos = center;
            inward = new Vector2(0f, bottom ? -1f : 1f);
            int tries = Math.Max(1, _tuning.SpawnRetryCount);
            for (int i = 0; i < tries; i++)
            {
                float t = rng.Range(0f, lenH + lenV);
                bool horizontal = t < lenH;
                pos = horizontal
                    ? new Vector2(along + t, bottom ? ah - edge : edge)
                    : new Vector2(right ? aw - edge : edge, along + (t - lenH));
                inward = horizontal ? new Vector2(0f, bottom ? -1f : 1f) : new Vector2(right ? -1f : 1f, 0f);
                if (ClearOfEnemies(w, pos)) break;
            }
            return pos;
        }

        /// <summary>변이 아니라 전장 안쪽에 나오는 종인가(가시덩굴: 벽 2칸·플레이어 3칸 이상).</summary>
        bool InteriorSpawn(EnemyDef def, out float wallMargin, out float playerMargin)
        {
            wallMargin = _tuning.RootedWallClearance;
            playerMargin = _tuning.RootedPlayerClearance;
            if (def == null) return false;
            JObject ai = def.Ai;
            JToken wall = ai != null ? ai["spawn_wall_margin"] : null;
            JToken player = ai != null ? ai["spawn_player_margin"] : null;
            if (wall != null) wallMargin = wall.Value<float>();
            if (player != null) playerMargin = player.Value<float>();
            return wall != null || player != null || (def.TagFlags & EnemyTags.Rooted) != 0;
        }

        Vector2 PickInterior(SimWorld w, Pcg32 rng, float wallMargin, float playerMargin)
        {
            float aw = w.ArenaWidth, ah = w.ArenaHeight;
            wallMargin = Mathf.Min(wallMargin, Mathf.Min(aw, ah) / 2);
            Vector2 best = ArenaCenter(w);
            float bestDist = -1f;
            int tries = Math.Max(1, _tuning.SpawnRetryCount);
            for (int i = 0; i < tries; i++)
            {
                var pos = new Vector2(rng.Range(wallMargin, aw - wallMargin), rng.Range(wallMargin, ah - wallMargin));
                float d = NearestPlayerDistance(w, pos);
                if (d >= playerMargin && ClearOfEnemies(w, pos)) return pos;
                if (d > bestDist) { bestDist = d; best = pos; }
            }
            if (bestDist >= playerMargin) return best;
            // 뽑은 자리가 전부 플레이어에게 가까웠다: 벽 여백 안의 네 귀퉁이 가운데 플레이어에게서 가장 먼 곳
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2((i & 1) == 0 ? wallMargin : aw - wallMargin, (i & 2) == 0 ? wallMargin : ah - wallMargin);
                float d = NearestPlayerDistance(w, corner);
                if (d > bestDist) { bestDist = d; best = corner; }
            }
            return best;
        }

        static float NearestPlayerDistance(SimWorld w, Vector2 pos)
        {
            float best = float.MaxValue;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (!p.Active) continue;
                float d = (p.Pos - pos).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        bool ClearOfEnemies(SimWorld w, Vector2 pos)
        {
            float c = _tuning.SpawnEntityClearance;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (!e.Alive) continue;
                if ((e.Pos - pos).sqrMagnitude < c * c) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ 11단계: 시계·지체 방지·진행 바·별똥·소나기

        public override void Stage11_TickEnd(SimWorld w)
        {
            Bind(w);
            if (_db == null) return;
            TickTimedWeather(w);

            var ws = w.Wave;
            var rt = Runtime(ws);
            if (rt == null) return;

            bool combat = w.Run.Phase == RunPhase.WaveCombat;
            if (combat)
            {
                // 점수용 시계는 WaveCombat 동안만 흐른다(예고·등장·전리품·야영 구간은 넣지 않는다)
                ws.WaveTick++;
                TryStall(w, ws, rt);
            }
            UpdateRemainingRatio(w, ws, rt);
            if (combat) TickStarfall(w, ws);
        }

        /// <summary>지체 방지: 일반 웨이브의 시계가 stall_seconds에 닿으면 남은 묶음을 전부 대기열로(동시 상한은 그대로).</summary>
        void TryStall(SimWorld w, WaveState ws, WaveRuntime rt)
        {
            var plan = ws.Plan;
            if (ws.Stalled || plan == null || plan.BossId != null) return;
            if (ws.WaveTick < SimConst.Ticks(_waves.StallSeconds)) return;
            ws.Stalled = true;
            if (!ws.AllReleased)
            {
                int last = ws.NextBundle;
                for (int i = 0; i < plan.Entries.Count; i++) if (plan.Entries[i].Bundle > last) last = plan.Entries[i].Bundle;
                for (int b = ws.NextBundle; b <= last; b++)
                    for (int i = 0; i < plan.Entries.Count; i++)
                        if (plan.Entries[i].Bundle == b) ws.SpawnQueue.Add(plan.Entries[i]);
                ws.NextBundle = Math.Max(last + 1, plan.BundleCount);
                ws.AllReleased = true;
                ws.LastBundleTick = w.Tick;
                rt.FirstReleased = true;
            }
            w.Emit(SimEventType.WaveStalled, a: ws.Wave);
        }

        /// <summary>진행 바: 남은 적 체력 합 / 처음 합. 보스 웨이브는 보스 몸통의 체력 비율.</summary>
        void UpdateRemainingRatio(SimWorld w, WaveState ws, WaveRuntime rt)
        {
            var plan = ws.Plan;
            if (plan != null && plan.BossId != null)
            {
                if (w.Boss == null) return;
                var body = w.FindEnemy(w.Boss.EnemyId);
                ws.RemainingRatio = body != null && body.Alive && body.MaxHp > 0f ? Mathf.Clamp01(body.Hp / body.MaxHp) : 0f;
                return;
            }
            if (rt.InitialHp <= 0f) { ws.RemainingRatio = 0f; return; }
            float left = Mathf.Max(0f, rt.PendingHp);
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                // 구성으로 나온 개체만 센다(갈라진·불러낸 개체는 처음 합에 없다)
                if (!e.Alive || e.IsBoss || e.SummonerId != 0) continue;
                // 떼 출현이 보상 없이 낸 늘린 개체는 구성으로 나온 것이라 센다
                if (e.NoReward && !rt.HordeExtraIds.Contains(e.Id)) continue;
                left += Mathf.Max(0f, e.Hp);
            }
            ws.RemainingRatio = Mathf.Clamp01(left / rt.InitialHp);
        }

        void TickTimedWeather(SimWorld w)
        {
            var weather = w.Weather;
            if (weather.Active.Count == 0 || w.Tick < weather.ShowerUntilTick) return;
            for (int i = weather.Active.Count - 1; i >= 0; i--)
            {
                string id = weather.Active[i];
                if (!IsTimedWeather(id)) continue;
                weather.Active.RemoveAt(i);
                w.Emit(SimEventType.WeatherEnded, id: id);
            }
        }

        // ------------------------------------------------------------ 별똥 결정 (01 요약서 7.3절)

        void TickStarfall(SimWorld w, WaveState ws)
        {
            var star = w.Starfall;
            var sf = _db.Starfall;
            if (sf == null) return;
            switch (star.Phase)
            {
                case StarfallPhase.Scheduled:
                    if (ws.WaveTick < star.ScheduledTick || w.Tick < star.NextRetryTick) return;
                    Vector2 pos;
                    if (FindStarSpot(w, sf, out pos))
                    {
                        star.Phase = StarfallPhase.Telegraph;
                        star.Pos = pos;
                        star.PhaseStartTick = w.Tick;
                        w.Emit(SimEventType.StarfallTelegraph, a: SimConst.Ticks(sf.TelegraphSeconds), pos: pos);
                    }
                    else star.NextRetryTick = w.Tick + SimConst.Ticks(sf.RetrySeconds);
                    return;

                case StarfallPhase.Telegraph:
                    if (w.Tick - star.PhaseStartTick < SimConst.Ticks(sf.TelegraphSeconds)) return;
                    star.Phase = StarfallPhase.Landed;
                    star.PhaseStartTick = w.Tick;
                    w.Emit(SimEventType.StarfallLanded, pos: star.Pos);
                    return;

                case StarfallPhase.Landed:
                    if (w.Tick - star.PhaseStartTick >= SimConst.Ticks(sf.LandedSeconds))
                    {
                        star.Phase = StarfallPhase.Expired;
                        star.PhaseStartTick = w.Tick;
                        w.Emit(SimEventType.StarfallExpired, pos: star.Pos);
                        return;
                    }
                    // 줍기: 판정 중심과의 거리 ≤ pick_radius. 결정이 가득이면 GainCrystal이 false → 남겨 둔다
                    for (int i = 0; i < w.Players.Length; i++)
                    {
                        var p = w.Players[i];
                        if (!p.Active || p.Downed) continue;
                        if ((p.Pos - star.Pos).sqrMagnitude > sf.PickRadius * sf.PickRadius) continue;
                        if (!w.Mods.Player.GainCrystal(w, p, CrystalSource.Starfall)) continue;
                        for (int k = 1; k < sf.Crystals; k++) w.Mods.Player.GainCrystal(w, p, CrystalSource.Starfall);
                        star.Phase = StarfallPhase.Picked;
                        star.PhaseStartTick = w.Tick;
                        w.Emit(SimEventType.StarfallPicked, slot: p.Slot, pos: star.Pos);
                        return;
                    }
                    return;
            }
        }

        /// <summary>별똥 자리: 전장 가운데 영역의 격자 가운데, 플레이어에게서 player_clearance 이상이고
        /// 적·예고·남는 구역에서 hazard_clearance 이상인 곳을 균등하게 하나.</summary>
        bool FindStarSpot(SimWorld w, StarfallDef sf, out Vector2 pos)
        {
            pos = default(Vector2);
            float step = sf.GridStep;
            if (step <= 0f) return false;
            float x0 = (w.ArenaWidth - sf.Area.Width) / 2;
            float y0 = (w.ArenaHeight - sf.Area.Height) / 2;
            int nx = (int)Math.Round(sf.Area.Width / step) + 1;
            int ny = (int)Math.Round(sf.Area.Height / step) + 1;
            _spots.Clear();
            for (int iy = 0; iy < ny; iy++)
                for (int ix = 0; ix < nx; ix++)
                {
                    var c = new Vector2(x0 + ix * step, y0 + iy * step);
                    if (NearestPlayerDistance(w, c) < sf.PlayerClearance) continue;
                    if (!w.Mods.Threats.IsClear(w, c, sf.HazardClearance)) continue;
                    _spots.Add(c);
                }
            if (_spots.Count == 0) return false;
            pos = _spots[w.Rng(RngStream.Starfall).Range(0, _spots.Count)];
            _spots.Clear();
            return true;
        }

        // ------------------------------------------------------------ 클리어·사망·끝

        public override bool IsCleared(SimWorld w)
        {
            var ws = w.Wave;
            if (ws == null || ws.Plan == null || ws.Plan.BossId != null) return false;
            if (Runtime(ws) == null) return false;
            return ws.AllReleased && ws.SpawnQueue.Count == 0 && w.AliveEnemies() == 0;
        }

        /// <summary>마지막 숨: 사망 자리에 원 예고. 상한에 세지 않고, 길이 막히면 Threats가 늦게 청한 원부터 미룬다(D-64).</summary>
        public override void OnEnemyKilled(SimWorld w, EnemyState e)
        {
            var ws = w.Wave;
            if (e == null || ws == null || !ws.LastBreath || w.Run.Phase != RunPhase.WaveCombat) return;
            var rt = Runtime(ws);
            if (rt == null || rt.DeathCircle == null) return;
            var dc = rt.DeathCircle;
            // 주인을 죽은 적으로 두지 않는다(주인이 없어졌다고 요청이 버려지지 않게)
            var req = new TelegraphRequest
            {
                OwnerEnemyId = 0, CastId = w.NewCastId(), Bypass = false, StaggerIfBlockedTicks = SimConst.Ticks(dc.StaggerSeconds)
            };
            req.Zones.Add(new ZoneSpec
            {
                Shape = ZoneShape.Circle, Anchor = ZoneAnchor.Fixed, Pos = e.Pos, Radius = dc.Radius,
                TelegraphTicks = SimConst.Ticks(dc.TelegraphSeconds), BaseDamage = dc.Damage,
                DamageSource = PlayerDamageSource.Lingering, Grazeable = true, TargetSlot = -1
            });
            rt.DeathTickets.Add(w.Mods.Threats.RequestTelegraph(w, req));
        }

        public override void EndWave(SimWorld w)
        {
            Bind(w);
            var ws = w.Wave;
            var rt = Runtime(ws);
            if (rt != null)
            {
                // 아직 터지지 않은 마지막 숨의 원과 좁은 들 예고는 바로 사라진다(가시밭은 Threats.ClearAll이 걷는다)
                for (int i = 0; i < rt.DeathTickets.Count; i++) w.Mods.Threats.CancelTicket(w, rt.DeathTickets[i]);
                rt.DeathTickets.Clear();
                if (rt.Thorn != null && !rt.ThornHazardsSpawned) w.Mods.Threats.CancelTicket(w, rt.ThornTicket);
                rt.ThornHazardsSpawned = true;
            }
            if (ws != null) ws.SpawnQueue.Clear();

            // 남은 별똥은 사라진다
            var star = w.Starfall;
            if (star.Phase == StarfallPhase.Telegraph || star.Phase == StarfallPhase.Landed)
                w.Emit(SimEventType.StarfallExpired, pos: star.Pos);
            star.Phase = StarfallPhase.None;

            // 소나기·안개는 웨이브가 끝나면 그친다
            ClearWeather(w);
        }
    }
}
