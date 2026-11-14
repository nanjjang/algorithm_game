using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Records;
using Wizard.Solo.Meta.Settlement;
using Wizard.Solo.Save;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.Screens.Settlement;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Enhance;
using Wizard.Solo.View.Stats;
using RunStats = Wizard.Solo.View.Stats.RunStats;

namespace Wizard.Solo.Tests.Records
{
    sealed class Wp4Clock : IClock
    {
        public DateTime Now => new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Local);
        public DateTime UtcNow => Now.ToUniversalTime();
    }

    /// <summary>판 끝 요약: 표시 이벤트 → 사인·최고 순간·팁·스승 대사(WP4).</summary>
    public class RunRecapTests
    {
        const int Wolf = 11, Boss = 20;

        static SimEvent Ev(SimEventType t, int tick, int slot = 0, int e = 0, int a = 0, int b = 0, float f = 0f, string id = null) =>
            new SimEvent { Type = t, Tick = tick, Slot = slot, EntityId = e, A = a, B = b, F = f, Id = id };

        static SimEvent Hit(int tick, int enemy, PlayerDamageSource src) => Ev(SimEventType.PlayerHit, tick, 0, enemy, 10, 0, (int)src);

        static RunSummary Summary(RunEndReason end, int reached = 7, int cleared = 6) => new RunSummary
        {
            RunId = "run-1", CharId = Ids.Char.C1, EndReason = end, ReachedWave = reached, ClearedDirect = cleared, LastClearedWave = cleared,
            TotalKills = 42
        };

        static RecapTuning Tuning() => RecapTuning.From(new JObject
        {
            ["many_hits"] = 2, ["low_graze_per_wave"] = 2, ["heat_locks_many"] = 4, ["short_run_wave"] = 3, ["graze_chain_praise"] = 15,
            ["boss_pattern_window_seconds"] = 4
        });

        RunStatsCollector _c;

        [SetUp]
        public void SetUp()
        {
            _c = new RunStatsCollector { PatternWindowTicks = 240 };
            _c.Begin("run-1", Ids.Char.C1);
        }

        [Test]
        public void WP4_Recap__DeathCauseIsLastHitWithSpeciesAndTelegraph()
        {
            _c.Feed(Ev(SimEventType.EnemySpawned, 1, -1, Wolf, id: "enemy.wolf_bramble"));
            _c.Feed(Hit(100, Wolf, PlayerDamageSource.Contact));
            _c.Feed(Hit(300, Wolf, PlayerDamageSource.Dash));
            _c.Feed(Hit(500, Wolf, PlayerDamageSource.Dash));
            RunStats s = _c.For("run-1");
            Assert.IsNotNull(s);
            Assert.AreEqual(3, s.HitsTaken);
            Assert.AreEqual("enemy.wolf_bramble", s.LastHit.DefId);
            Assert.AreEqual(PlayerDamageSource.Dash, s.LastHit.Source);
            Assert.IsTrue(s.LastHit.Telegraphed, "돌진에는 예고 띠가 있다");
            Assert.AreEqual(2, s.MostHit.Count);

            RunRecap r = RunRecap.Build(Summary(RunEndReason.HpZero), s, new CharacterRecord { Id = 1 }, false, Tuning());
            Assert.AreEqual(RecapEnd.Died, r.End);
            Assert.AreEqual("enemy.wolf_bramble", r.CauseDefId);
            Assert.AreEqual(RunRecap.SourceKey(PlayerDamageSource.Dash), r.CauseAttackKey);
            Assert.AreEqual(RunRecap.TagKey(PlayerDamageSource.Dash), r.CauseTagKey);
            Assert.IsTrue(r.CauseTelegraphed);
            // 늑대 돌진에 두 번 → "옆으로 비켜 스치기" 팁, 적·공격·횟수가 채워진다.
            Assert.AreEqual(RunRecap.P + "tip.dash", r.TipKey);
            Assert.AreEqual("enemy.wolf_bramble", r.TipEnemyKey);
            Assert.AreEqual(2, r.TipCount);
        }

        [Test]
        public void WP4_Recap__BossHitTakesRecentPatternName()
        {
            _c.Feed(Ev(SimEventType.BossIntroBegin, 1, -1, Boss, id: "boss.kiln_boar"));
            _c.Feed(Ev(SimEventType.BossPatternStarted, 100, -1, Boss, id: "stomp"));
            _c.Feed(Hit(150, Boss, PlayerDamageSource.Telegraph));
            RunStats s = _c.For("run-1");
            Assert.AreEqual("stomp", s.LastHit.Pattern);
            Assert.AreEqual(RunRecap.P + "pattern.stomp", RunRecap.AttackKey(s.LastHit));
            // 창(240틱)이 지난 뒤의 피격은 패턴 없이 분류만.
            _c.Feed(Hit(1000, Boss, PlayerDamageSource.Telegraph));
            Assert.IsNull(_c.For("run-1").LastHit.Pattern);
            // 몸통 박치기는 패턴이 아니다.
            _c.Feed(Ev(SimEventType.BossPatternStarted, 1100, -1, Boss, id: "stomp"));
            _c.Feed(Hit(1110, Boss, PlayerDamageSource.Contact));
            Assert.IsNull(_c.For("run-1").LastHit.Pattern);
        }

        [Test]
        public void WP4_Recap__GrazeChainResetsOnHitAndScriptedIsNotACause()
        {
            for (int i = 0; i < 5; i++) _c.Feed(Ev(SimEventType.GrazeAwarded, 10 + i));
            _c.Feed(Hit(50, 0, PlayerDamageSource.Projectile));
            for (int i = 0; i < 3; i++) _c.Feed(Ev(SimEventType.GrazeAwarded, 60 + i));
            _c.Feed(Hit(100, 0, PlayerDamageSource.Scripted));
            RunStats s = _c.For("run-1");
            Assert.AreEqual(8, s.Grazes);
            Assert.AreEqual(5, s.MaxGrazeChain);
            Assert.AreEqual(1, s.HitsTaken, "각본 되비침은 맞은 횟수·사인에 넣지 않는다");
            Assert.AreEqual(PlayerDamageSource.Projectile, s.LastHit.Source);
        }

        [Test]
        public void WP4_Recap__HighlightsAndPersonalBests()
        {
            _c.Feed(Ev(SimEventType.EnemyDamaged, 5, 0, 3, 77));
            _c.Feed(Ev(SimEventType.EnemyDamaged, 6, 0, 3, 120));
            _c.Feed(Ev(SimEventType.EnemyDamaged, 7, -1, 3, 999));   // 환경 피해는 세지 않는다
            _c.Feed(Ev(SimEventType.ComboChanged, 8, 0, 0, 33, 1));
            _c.Feed(Ev(SimEventType.ProjectileSpawned, 9, 0, 1, 0, 0, id: "block.fireball"));
            _c.Feed(Ev(SimEventType.ProjectileSpawned, 10, 0, 2, 0, 0, id: "block.fireball"));
            _c.Feed(Ev(SimEventType.ProjectileSpawned, 11, 0, 3, 0, 0, id: "block.magic_bolt"));
            _c.Feed(Ev(SimEventType.ProjectileSpawned, 12, 0, 4, 0, 1, id: "block.magic_bolt"));   // 하위 스코프 탄은 세지 않는다
            RunStats s = _c.For("run-1");
            Assert.AreEqual(120, s.MaxHitDamage);
            Assert.AreEqual(33, s.MaxCombo);
            Assert.AreEqual("block.fireball", s.TopBlockId);
            Assert.AreEqual(2, s.TopBlockShots);

            var rec = new CharacterRecord { Id = 1, BestHit = 100, BestCombo = 40 };
            RunRecap r = RunRecap.Build(Summary(RunEndReason.Abandon), s, rec, false, Tuning());
            Assert.IsTrue(r.Bests.Hit);
            Assert.IsFalse(r.Bests.Combo);
            Assert.AreEqual(6, r.Highlights.Count);
            Assert.AreEqual(42, r.Highlights[4].Value, "처치는 판 요약의 TotalKills");
            Assert.AreEqual(7, r.Highlights[5].Value);
            Assert.IsTrue(r.Highlights[5].IsWave);
            Assert.AreEqual(RecapEnd.Abandoned, r.End);
            Assert.IsFalse(r.HasCause, "포기한 판에는 사인이 없다");
            Assert.AreEqual(RunRecap.P + "master.new_best.2", r.MasterLineKey, "개인 최고가 있으면 스승이 칭찬(도달 7 → 둘째 갈래)");

            Assert.IsTrue(RunHighlightBook.Apply(rec, s.MaxHitDamage, s.MaxGrazeChain, s.MaxCombo));
            Assert.AreEqual(120, rec.BestHit);
            Assert.AreEqual(40, rec.BestCombo, "작은 값은 덮어쓰지 않는다");
            Assert.IsFalse(RunHighlightBook.Apply(rec, 120, 0, 10));
        }

        [Test]
        public void WP4_Recap__TipFallbacksAndMasterBranches()
        {
            // 맞지 않았고 비기를 안 썼다 → 비기 팁. 짧은 판(웨이브 2) → 짧은 판 대사.
            _c.Feed(Ev(SimEventType.ShotFired, 1));
            RunStats s = _c.For("run-1");
            RunRecap r = RunRecap.Build(Summary(RunEndReason.HpZero, 2, 1), s, new CharacterRecord { Id = 1 }, false, Tuning());
            Assert.AreEqual(RunRecap.P + "tip.use_arts", r.TipKey);
            Assert.AreEqual(RunRecap.P + "master.short.1", r.MasterLineKey);

            // 복수 처치 판이 가장 앞선다.
            var sum = Summary(RunEndReason.HpZero, 21, 20);
            sum.KilledPhantomAtWave20 = true;
            Assert.AreEqual(RunRecap.P + "master.revenge", RunRecap.Build(sum, s, null, true, Tuning()).MasterLineKey);

            // 통계 없이(자동 정산) → 사인 모름, 처치·도달만 값이 있다.
            RunRecap none = RunRecap.Build(Summary(RunEndReason.HpZero), null, null, false, Tuning());
            Assert.AreEqual(RecapEnd.Unknown, none.End);
            Assert.IsTrue(none.Highlights[0].Unknown);
            Assert.IsFalse(none.Highlights[4].Unknown);
            Assert.AreEqual(RunRecap.P + "tip.default", none.TipKey);
        }

        [Test]
        public void WP4_Recap__ForOtherRunIsNull()
        {
            _c.Feed(Ev(SimEventType.ShotFired, 1));
            Assert.IsNull(_c.For("run-2"), "다른 판(자동 정산 등)의 통계는 주지 않는다");
            var fresh = new RunStatsCollector();
            fresh.Begin("run-3", Ids.Char.C2);
            Assert.IsNull(fresh.For("run-3"), "이벤트를 하나도 받지 않은 판은 통계 없음");
        }

        [Test]
        public void WP4_Recap__AllRecapKeysAreInNameTable()
        {
            INameTable n = TestContent.Names;
            var keys = new List<string>
            {
                RunRecap.TitleKey, RunRecap.CauseTitleKey, RunRecap.CauseLineKey, RunRecap.CauseAbandonKey, RunRecap.CauseUnknownKey,
                RunRecap.UnknownEnemyKey, RunRecap.CauseHitsKey, RunRecap.CauseMostKey, RunRecap.HighlightTitleKey, RunRecap.HighlightNewKey,
                RunRecap.HighlightNoneKey, RunRecap.HighlightShotsKey, RunRecap.TipTitleKey, RunRecap.HlMaxHit, RunRecap.HlGrazeChain,
                RunRecap.HlCombo, RunRecap.HlTopBlock, RunRecap.HlKills, RunRecap.HlReached, RunRecap.MasterNameKey
            };
            foreach (PlayerDamageSource src in Enum.GetValues(typeof(PlayerDamageSource)))
            {
                keys.Add(RunRecap.SourceKey(src));
                keys.Add(RunRecap.TagKey(src));
            }
            foreach (BossDef b in TestContent.Db.Bosses)
                if (b.Patterns != null)
                    foreach (var pat in b.Patterns) if (!pat.Passive) keys.Add(RunRecap.P + "pattern." + pat.Key);
            foreach (string k in keys) Assert.IsTrue(n.Has(k), "name key missing: " + k);
        }
    }

    /// <summary>지팡이별 강화 기록: 결과마다 세기, 저장·불러오기, 옛 저장 호환(WP4).</summary>
    public class EnhanceRecordTests
    {
        [Test]
        public void WP4_EnhanceRecord__CountsEachOutcomeAndShownVsActual()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            WandData w = kit.Set(5, 5, 0);
            int shown = kit.Preview(w).SuccessPct;
            kit.Succeed(w);           // +5 → +6 도전 성공
            w = kit.Set(5, 5, 0);
            kit.Fail(w);              // +6 도전 실패(하락 가능 단계면 하락)
            w = kit.Set(5, 5, 0);
            kit.Mutate(w);            // 변이 제시: 결정 전에는 결과를 세지 않는다
            Assert.AreEqual(0, w.EnhanceStats.Mutations);
            kit.Resolve(w, true);     // 받음 = 변이
            WandEnhanceStats s = w.EnhanceStats;
            Assert.AreEqual(3, s.Attempts);
            Assert.AreEqual(1, s.Successes);
            Assert.AreEqual(1, s.Mutations);
            Assert.AreEqual(1, s.Drops + s.Keeps, "실패 하나는 하락 또는 유지");
            StageEnhanceStats st = s.Find(6);
            Assert.AreEqual(3, st.Attempts);
            Assert.AreEqual(1, st.Successes);
            Assert.AreEqual(shown * 3, st.ShownPctSum, "같은 구성 세 번 → 표기 확률 합 = 3 × 표기");
            Assert.AreEqual(100.0 / 3, EnhanceRecordBook.ActualPct(st), 1e-9);
            Assert.AreEqual(shown, EnhanceRecordBook.AverageShownPct(st), 1e-9);
        }

        [Test]
        public void WP4_EnhanceRecord__GreatSuccessCountsAsSuccessAndGreat()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            WandData w = kit.Set(4, 4, 0);
            kit.Great(w);
            Assert.AreEqual(1, w.EnhanceStats.Successes);
            Assert.AreEqual(1, w.EnhanceStats.GreatSuccesses);
            Assert.AreEqual(1, w.EnhanceStats.Find(5).Successes);
        }

        [Test]
        public void WP4_EnhanceRecord__RecordsDoNotChangeProbabilityOrCost()
        {
            var kit = new EnhanceKit();
            kit.Rich();
            WandData w = kit.Set(6, 6, 0);
            ProbBreakdown before = kit.Preview(w);
            w.EnhanceStats.Attempts = 999;
            w.EnhanceStats.Drops = 500;
            w.EnhanceStats.Stage(7).Attempts = 400;
            ProbBreakdown after = kit.Preview(w);
            Assert.AreEqual(before.SuccessBp, after.SuccessBp);
            Assert.AreEqual(before.GreatBp, after.GreatBp);
            Assert.AreEqual(before.MutationEndBp, after.MutationEndBp);
            Assert.AreEqual(before.CostGold, after.CostGold);
            Assert.AreEqual(before.StoneCount, after.StoneCount);
        }

        string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "wizard-solo-wp4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
        }

        ProfileStore Store() =>
            new ProfileStore(SavePaths.ProfileDirectory(_root), () => NewProfile.Create(TestContent.Db, new Wp4Clock()), TestContent.Db.DataVersion);

        [Test]
        public void WP4_EnhanceRecord__SaveAndLoadRoundTrip()
        {
            ProfileStore s = Store();
            s.LoadOrCreate(out _, out _);
            WandData w = s.Current.Wands[0];
            w.EnhanceStats.Attempts = 12;
            w.EnhanceStats.Successes = 5;
            w.EnhanceStats.GreatSuccesses = 1;
            w.EnhanceStats.Keeps = 4;
            w.EnhanceStats.Drops = 2;
            w.EnhanceStats.Mutations = 1;
            StageEnhanceStats st = w.EnhanceStats.Stage(8);
            st.Attempts = 7; st.Successes = 2; st.ShownPctSum = 175;
            s.Current.Challenges.Done.Add("c1.wave_15");
            s.Current.Challenges.DoneAt.Add("2026-11-15T09:00:00+09:00");
            CharacterRecord c = s.Current.Character(1);
            c.TotalGrazes = 321; c.BestRunGrazes = 80; c.BossNoHitClears = 1; c.RevengeDone = true; c.BestHit = 444;
            s.Save();

            ProfileStore again = Store();
            again.LoadOrCreate(out bool created, out bool restored);
            Assert.IsFalse(created);
            Assert.IsFalse(restored);
            WandData w2 = again.Current.Wand(w.Id);
            Assert.AreEqual(12, w2.EnhanceStats.Attempts);
            Assert.AreEqual(5, w2.EnhanceStats.Successes);
            Assert.AreEqual(1, w2.EnhanceStats.GreatSuccesses);
            Assert.AreEqual(4, w2.EnhanceStats.Keeps);
            Assert.AreEqual(2, w2.EnhanceStats.Drops);
            Assert.AreEqual(1, w2.EnhanceStats.Mutations);
            Assert.AreEqual(175, w2.EnhanceStats.Find(8).ShownPctSum);
            Assert.IsTrue(again.Current.Challenges.IsDone("c1.wave_15"));
            CharacterRecord c2 = again.Current.Character(1);
            Assert.AreEqual(321, c2.TotalGrazes);
            Assert.AreEqual(80, c2.BestRunGrazes);
            Assert.AreEqual(1, c2.BossNoHitClears);
            Assert.IsTrue(c2.RevengeDone);
            Assert.AreEqual(444, c2.BestHit);
        }

        [Test]
        public void WP4_EnhanceRecord__OldSaveWithoutNewFieldsLoadsEmpty()
        {
            // 새 필드(enhance_stats, challenges, 캐릭터 누적 기록)를 지운 옛 저장 본문 → 빈 기록으로 읽힌다(저장 버전 그대로).
            ProfileStore s = Store();
            s.LoadOrCreate(out _, out _);
            JObject j = JObject.FromObject(s.Current, Newtonsoft.Json.JsonSerializer.Create(SaveJson.Settings));
            j.Remove("challenges");
            foreach (JObject wj in (JArray)j["wands"]) wj.Remove("enhance_stats");
            foreach (JObject cj in (JArray)j["characters"]) { cj.Remove("total_grazes"); cj.Remove("best_hit"); }
            ProfileData old = j.ToObject<ProfileData>(Newtonsoft.Json.JsonSerializer.Create(SaveJson.Settings));
            Assert.AreEqual(1, old.SchemaVersion);
            Assert.IsNotNull(old.Challenges);
            Assert.AreEqual(0, old.Challenges.Done.Count);
            Assert.IsNotNull(old.Wands[0].EnhanceStats);
            Assert.AreEqual(0, old.Wands[0].EnhanceStats.Attempts);
            Assert.AreEqual(0, old.Character(1).TotalGrazes);
        }
    }

    /// <summary>강화 화면 규칙: 목표 단계에서 이어서 강화가 멈춘다, 재화 경고, 하락 뒤 안내(WP4).</summary>
    public class EnhanceScreenRecordRuleTests
    {
        static EnhanceResult R(EnhanceOutcome o, int before, int after) =>
            new EnhanceResult { Outcome = o, StageBefore = before, StageAfter = after };

        [Test]
        public void WP4_TargetStage__ContinueStopsExactlyAtTarget()
        {
            Assert.AreEqual(ContinueStop.None, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.Success, 7, 8), 9, false));
            Assert.AreEqual(ContinueStop.TargetReached, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.Success, 8, 9), 9, false));
            Assert.AreEqual(ContinueStop.None, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.FailNoDrop, 8, 8), 9, false));
            // 하락 멈춤을 끄면 하락해도 이어 간다.
            Assert.AreEqual(ContinueStop.None, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.FailDrop, 8, 7), 9, false));
            Assert.AreEqual(ContinueStop.Dropped, ContinuousEnhanceModel.StopAfter(R(EnhanceOutcome.FailDrop, 8, 7), 9, true));
        }

        [Test]
        public void WP4_TargetStage__ServiceRunUntilTargetStops()
        {
            // 진짜 서비스로 성공만 이어 시도: 목표 +8에 닿는 순간 멈춘다(+7은 마일스톤이므로 목표를 마일스톤 뒤로 둔다).
            var kit = new EnhanceKit();
            kit.Rich();
            WandData w = kit.Set(7, 7, 0);
            var cont = new ContinuousEnhanceModel(false);
            cont.Start(new List<string>(), false, 8);
            ContinueStop stop = ContinueStop.None;
            for (int i = 0; i < 5 && stop == ContinueStop.None; i++)
            {
                EnhanceResult r = kit.Succeed(w);
                cont.Count(r);
                stop = ContinuousEnhanceModel.StopAfter(r, cont.TargetStage, cont.StopOnDrop);
            }
            Assert.AreEqual(ContinueStop.TargetReached, stop);
            Assert.AreEqual(8, w.Stage);
            Assert.AreEqual(1, cont.Attempts);
        }

        [Test]
        public void WP4_Funds__WarnsBeforeRunningOut()
        {
            Assert.AreEqual(FundsWarning.None, EnhanceRecordModel.Funds(2000, 10, 1000, 2, false));
            Assert.AreEqual(FundsWarning.LastGold, EnhanceRecordModel.Funds(1999, 10, 1000, 2, false));
            Assert.AreEqual(FundsWarning.LastStone, EnhanceRecordModel.Funds(5000, 3, 1000, 2, false));
            Assert.AreEqual(FundsWarning.ShortGold, EnhanceRecordModel.Funds(999, 10, 1000, 2, false));
            Assert.AreEqual(FundsWarning.ShortStone, EnhanceRecordModel.Funds(5000, 1, 1000, 2, false));
            Assert.AreEqual(FundsWarning.None, EnhanceRecordModel.Funds(0, 0, 1000, 2, true), "최대 단계는 경고 없음");
        }

        [Test]
        public void WP4_AfterDrop__ShownOnlyAfterSettledDrop()
        {
            Assert.IsTrue(EnhanceRecordModel.ShowAfterDrop(R(EnhanceOutcome.FailDrop, 6, 5), true));
            Assert.IsFalse(EnhanceRecordModel.ShowAfterDrop(R(EnhanceOutcome.FailDrop, 6, 5), false), "의식 중에는 결과를 미리 보이지 않는다");
            Assert.IsFalse(EnhanceRecordModel.ShowAfterDrop(R(EnhanceOutcome.FailNoDrop, 3, 3), true));
            Assert.IsFalse(EnhanceRecordModel.ShowAfterDrop(null, true));
        }

        [Test]
        public void WP4_RecordText__AllKeysAndPercentFormat()
        {
            INameTable n = TestContent.Names;
            string[] keys =
            {
                EnhanceRecordModel.TitleKey, EnhanceRecordModel.SummaryKey, EnhanceRecordModel.StageLineKey, EnhanceRecordModel.StageNoneKey,
                EnhanceRecordModel.SameRulesKey, EnhanceRecordModel.TableTitleKey, EnhanceRecordModel.TableRowKey, EnhanceRecordModel.EmptyKey,
                EnhanceRecordModel.WarnLastGoldKey, EnhanceRecordModel.WarnLastStoneKey, EnhanceRecordModel.WarnShortGoldKey,
                EnhanceRecordModel.WarnShortStoneKey, EnhanceRecordModel.AfterDropKey, EnhanceRecordModel.AfterDropAuraKey
            };
            foreach (string k in keys) Assert.IsTrue(n.Has(k), "name key missing: " + k);
            Assert.AreEqual("25", EnhanceRecordModel.Pct(25.0));
            Assert.AreEqual("33.3", EnhanceRecordModel.Pct(100.0 / 3));
            var s = new WandEnhanceStats();
            StageEnhanceStats st = s.Stage(8);
            st.Attempts = 9; st.Successes = 2; st.ShownPctSum = 225;
            string line = EnhanceRecordModel.StageText(n, s, 8);
            StringAssert.Contains("25%", line);
            StringAssert.Contains("2/9", line);
            StringAssert.Contains("22.2%", line);
        }
    }

    /// <summary>도전 과제 달성 판정과 보상(WP4).</summary>
    public class ChallengeTests
    {
        static List<ChallengeDef> Defs() => ChallengeBook.Parse(JObject.Parse(@"{ ""challenges"": [
            { ""id"": ""t.wave"", ""character"": 1, ""kind"": ""best_wave"", ""target"": 15, ""reward"": { ""kind"": ""title"", ""id"": ""title.challenge.1.wave_15"" } },
            { ""id"": ""t.graze"", ""character"": 1, ""kind"": ""total_grazes"", ""target"": 10, ""reward"": { ""kind"": ""library_slot"", ""count"": 1 } },
            { ""id"": ""t.boss"", ""character"": 1, ""kind"": ""boss_no_hit"", ""target"": 1, ""reward"": { ""kind"": ""cosmetic"", ""id"": ""cosmetic.seat.rocking_chair"" } },
            { ""id"": ""t.all"", ""character"": 0, ""aggregate"": ""min"", ""kind"": ""best_wave"", ""target"": 10, ""reward"": { ""kind"": ""title"", ""id"": ""title.x"" } },
            { ""id"": ""t.sum"", ""character"": 0, ""aggregate"": ""sum"", ""kind"": ""total_grazes"", ""target"": 12 }
        ] }"));

        static ProfileData Profile() => NewProfile.Create(TestContent.Db, new Wp4Clock());

        static RunSummary Run(int grazesPerWave, int waves, bool bossNoHit)
        {
            var s = new RunSummary { RunId = "r", CharId = Ids.Char.C1, ClearedDirect = waves, LastClearedWave = waves, ReachedWave = waves + 1 };
            for (int w = 1; w <= waves; w++)
            {
                bool boss = w % 10 == 0;
                s.WaveResults.Add(new WaveResult
                {
                    Wave = w,
                    Stats = new WaveStats { Wave = w, Grazes = grazesPerWave, KillCount = 3, IsBoss = boss, BossKilled = boss, NoHit = boss ? bossNoHit : w % 2 == 0 }
                });
            }
            return s;
        }

        [Test]
        public void WP4_Challenge__RunCountersAccumulate()
        {
            var rec = new CharacterRecord { Id = 1 };
            ChallengeBook.AddRunCounters(rec, Run(2, 10, true));
            Assert.AreEqual(20, rec.TotalGrazes);
            Assert.AreEqual(30, rec.TotalKills);
            Assert.AreEqual(20, rec.BestRunGrazes);
            Assert.AreEqual(1, rec.BossNoHitClears);
            Assert.AreEqual(5, rec.BestRunNoHitWaves, "짝수 웨이브 4개 + 무피격 보스 1");
            ChallengeBook.AddRunCounters(rec, Run(1, 3, false));
            Assert.AreEqual(23, rec.TotalGrazes);
            Assert.AreEqual(20, rec.BestRunGrazes, "한 판 최고는 줄지 않는다");
        }

        [Test]
        public void WP4_Challenge__EvaluateGrantsOnceAndKeepsDone()
        {
            ProfileData p = Profile();
            List<ChallengeDef> defs = Defs();
            int libBefore = p.LibraryExpansions;
            Assert.AreEqual(0, ChallengeBook.Evaluate(p, defs, "t0").Count);

            CharacterRecord c1 = p.Character(1);
            c1.BestWave = 15;
            c1.TotalGrazes = 10;
            List<string> got = ChallengeBook.Evaluate(p, defs, "t1");
            CollectionAssert.AreEqual(new[] { "t.wave", "t.graze" }, got);
            Assert.Contains("title.challenge.1.wave_15", p.Titles.Owned);
            Assert.AreEqual(libBefore + 1, p.LibraryExpansions);
            // 한 번 이룬 과제는 다시 주지 않는다.
            Assert.AreEqual(0, ChallengeBook.Evaluate(p, defs, "t2").Count);
            Assert.AreEqual(libBefore + 1, p.LibraryExpansions);
            // 기록이 줄어도(되돌림 등) 이룬 표시는 남는다.
            c1.BestWave = 0;
            Assert.IsTrue(ChallengeBook.Progress(p, defs[0]).Done);
        }

        [Test]
        public void WP4_Challenge__AggregatesAcrossDisciples()
        {
            ProfileData p = Profile();
            List<ChallengeDef> defs = Defs();
            for (int c = 1; c <= 4; c++) p.Character(c).BestWave = 12;
            Assert.AreEqual(0, ChallengeBook.ValueOf(p, defs[3]), "다섯 모두(min): 제자 5가 0");
            p.Character(5).BestWave = 10;
            Assert.AreEqual(10, ChallengeBook.ValueOf(p, defs[3]));
            for (int c = 1; c <= 5; c++) p.Character(c).TotalGrazes = 3;
            Assert.AreEqual(15, ChallengeBook.ValueOf(p, defs[4]), "합(sum)");
            List<string> got = ChallengeBook.Evaluate(p, defs, "t");
            Assert.Contains("t.all", got);
            Assert.Contains("t.sum", got);
        }

        [Test]
        public void WP4_Challenge__NextPicksClosestUndone()
        {
            ProfileData p = Profile();
            List<ChallengeDef> defs = Defs();
            p.Character(1).TotalGrazes = 9;   // 9/10 = 0.9
            p.Character(1).BestWave = 3;      // 3/15 = 0.2
            Assert.AreEqual("t.graze", ChallengeBook.Next(p, defs, 1).Id);
            p.Character(1).TotalGrazes = 10;
            ChallengeBook.Evaluate(p, defs, "t");
            Assert.AreEqual("t.wave", ChallengeBook.Next(p, defs, 1).Id);
        }

        [Test]
        public void WP4_Challenge__RealTableLoadsAndHasNamesAndRewards()
        {
            List<ChallengeDef> defs = ChallengeBook.Load(TestContent.Db);
            Assert.GreaterOrEqual(defs.Count, 30);
            INameTable n = TestContent.Names;
            var ids = new HashSet<string>();
            for (int c = 1; c <= Ids.Char.Count; c++) Assert.Greater(ChallengeBook.For(defs, c).Count, 0, "char " + c);
            foreach (ChallengeDef d in defs)
            {
                Assert.IsTrue(ids.Add(d.Id), "duplicate id " + d.Id);
                Assert.IsTrue(n.Has(ChallengeBook.NameKey(d.Id)), "name " + d.Id);
                Assert.IsTrue(n.Has(ChallengeText.KindKey(d)), "kind text " + ChallengeText.KindKey(d));
                Assert.AreNotEqual(ChallengeRewardKind.None, d.Reward, "reward " + d.Id);
                if (d.Reward != ChallengeRewardKind.LibrarySlot) Assert.IsTrue(n.Has(d.RewardId), "reward name " + d.RewardId);
                Assert.Greater(d.Target, 0);
            }
        }

        [Test]
        public void WP4_Challenge__SettlementCommitAppliesChallenges()
        {
            string root = Path.Combine(Path.GetTempPath(), "wizard-solo-wp4c-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var clock = new Wp4Clock();
                var store = new ProfileStore(SavePaths.ProfileDirectory(root), () => NewProfile.Create(TestContent.Db, clock), TestContent.Db.DataVersion);
                store.LoadOrCreate(out _, out _);
                var svc = new SettlementService(new GameContext { Content = TestContent.Db, Profiles = store, Clock = clock }, new Pcg32(5UL));
                RunSummary s = Run(1, 10, true);
                s.RunId = "wp4-commit";
                s.Mode = RunMode.Endless;
                s.WandId = store.Current.Character(1).EquippedWandId;
                s.EndReason = RunEndReason.HpZero;
                Result<SettlementResult> r = svc.Commit(s, new int[0]);
                Assert.IsTrue(r.Ok, "commit " + r.Error);
                Assert.Contains("c1.boss_no_hit", r.Value.NewChallenges, "보스 웨이브를 맞지 않고 넘긴 판");
                Assert.IsTrue(store.Current.Challenges.IsDone("c1.boss_no_hit"));
                Assert.AreEqual(10, store.Current.Character(1).TotalGrazes);
                // 디스크에서 다시 읽어도 남아 있다.
                var again = new ProfileStore(SavePaths.ProfileDirectory(root), () => NewProfile.Create(TestContent.Db, clock), TestContent.Db.DataVersion);
                again.LoadOrCreate(out _, out _);
                Assert.IsTrue(again.Current.Challenges.IsDone("c1.boss_no_hit"));
                Assert.Contains("title.challenge.1.boss_clean", again.Current.Titles.Owned);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }
    }
}
