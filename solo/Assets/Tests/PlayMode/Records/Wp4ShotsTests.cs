using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.Screens.Meta;
using Wizard.Solo.Screens.Settlement;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.PlayMode.Flow;
using Wizard.Solo.UI;
using Wizard.Solo.View.Stats;

namespace Wizard.Solo.Tests.PlayMode.Records
{
    /// <summary>
    /// WP4 화면 확인(창 모드 스크린숏 Logs/shots/flow/wp4/): 판 끝 요약(사인·최고 순간·팁·스승)과 축하, 새 도전 과제,
    /// 강화 화면의 기록 칸·재화 경고·하락 뒤 안내, 기록의 도전 과제 탭, 메인 메뉴의 다음 목표.
    /// </summary>
    public class Wp4ShotsTests
    {
        sealed class FixedRoll : IEnhanceRandom
        {
            readonly int _roll;
            public FixedRoll(int roll) { _roll = roll; }
            public int Next(int maxExclusive) => _roll;
        }

        FlowKit _k;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_k != null)
            {
                FlowKit.Log("wp4 shots: " + string.Join(", ", _k.Shots));
                _k.Stop();
            }
            yield return null;
        }

        static SimEvent Ev(SimEventType t, int tick, int slot = 0, int e = 0, int a = 0, int b = 0, float f = 0f, string id = null) =>
            new SimEvent { Type = t, Tick = tick, Slot = slot, EntityId = e, A = a, B = b, F = f, Id = id };

        [UnityTest]
        public IEnumerator Recap_Enhance_Records_MainMenu()
        {
            _k = new FlowKit();
            _k.Start("wp4");
            var ctx = _k.Ctx;
            ProfileData p = ctx.Profile;
            CharacterRecord c1 = p.Character(1);
            c1.BestHit = 60; c1.BestCombo = 20; c1.BestGrazeChain = 4; c1.BestWave = 9; c1.BestScore = 900;

            // ---- 판 끝 요약: 늑대 돌진에 쓰러진 웨이브 14 판(통계는 표시 이벤트로 채운다)
            var stats = new RunStatsCollector { PatternWindowTicks = 240 };
            stats.Begin("wp4-run", Ids.Char.C1);
            stats.Feed(Ev(SimEventType.EnemySpawned, 1, -1, 7, id: "enemy.wolf_bramble"));
            stats.Feed(Ev(SimEventType.EnemySpawned, 2, -1, 8, id: "enemy.slime_moss"));
            for (int i = 0; i < 9; i++) stats.Feed(Ev(SimEventType.GrazeAwarded, 10 + i));
            stats.Feed(Ev(SimEventType.PlayerHit, 40, 0, 8, 10, 30, (int)PlayerDamageSource.Contact));
            for (int i = 0; i < 6; i++) stats.Feed(Ev(SimEventType.GrazeAwarded, 50 + i));
            stats.Feed(Ev(SimEventType.ComboChanged, 60, 0, 0, 47, 1));
            stats.Feed(Ev(SimEventType.EnemyDamaged, 61, 0, 7, 148));
            for (int i = 0; i < 30; i++) stats.Feed(Ev(SimEventType.ProjectileSpawned, 70 + i, 0, 100 + i, 0, 0, id: "block.fireball"));
            stats.Feed(Ev(SimEventType.PlayerHit, 200, 0, 7, 10, 20, (int)PlayerDamageSource.Dash));
            stats.Feed(Ev(SimEventType.PlayerHit, 400, 0, 7, 10, 0, (int)PlayerDamageSource.Dash));
            var tuning = RecapTuning.From(ctx.Content);
            _k.Screens.Register(ScreenId.Settlement, () => new SettlementScreen(stats, tuning));

            var summary = new RunSummary
            {
                RunId = "wp4-run", CharId = Ids.Char.C1, WandId = c1.EquippedWandId, Mode = RunMode.Endless, StartWave = 1,
                ReachedWave = 14, ClearedDirect = 13, LastClearedWave = 13, EndReason = RunEndReason.HpZero, TotalKills = 212, Purse = 1300
            };
            for (int w = 1; w <= 13; w++)
            {
                bool boss = w == 10;
                var r = new WaveResult { Wave = w, Stats = new WaveStats { Wave = w, Grazes = 4, KillCount = 15, IsBoss = boss, BossKilled = boss, NoHit = boss || w % 3 == 0 } };
                r.Score.Clear = 100; r.Score.Total = 100;
                summary.WaveResults.Add(r);
            }
            _k.Screens.Push(ScreenId.Settlement, ScreenArgs.From(ScreenId.Battle).With(ScreenArgKeys.RunSummary, summary));
            yield return _k.WaitTop(ScreenId.Settlement);
            yield return _k.Seconds(1.2f);
            yield return _k.Shot("01-settlement-recap");
            yield return _k.Seconds(1.6f);
            yield return _k.Shot("02-settlement-recap-celebrate");
            _k.Click("str.common.next");
            yield return _k.Seconds(0.4f);
            _k.Click("str.common.next");
            yield return _k.Seconds(0.5f);
            yield return _k.Shot("03-settlement-takeaways-challenges");
            Assert.IsTrue(p.Challenges.IsDone("c1.boss_no_hit"), "보스 웨이브 무피격 → 도전 과제");
            Assert.AreEqual(148, p.Character(1).BestHit, "판 끝 요약의 개인 최고가 적힌다");
            _k.Click(SettlementNameKeys.ToMenu);
            yield return _k.WaitTop(ScreenId.MainMenu);

            // ---- 메인 메뉴: 복수 중이면 허상까지 n웨이브 + 다음 도전 과제
            p.Flags.PhantomMet = true;
            ctx.Profiles.Save();
            yield return _k.Seconds(0.5f);
            yield return _k.Shot("04-main-menu-goals");

            // ---- 기록 > 도전 과제 탭
            _k.Screens.Push(ScreenId.Records, ScreenArgs.From(ScreenId.MainMenu));
            yield return _k.WaitTop(ScreenId.Records);
            var rs = _k.TopScreen;
            rs.GetType().GetMethod("SelectTab", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rs, new object[] { RecordsModel.ChallengeTab });
            yield return _k.Seconds(0.6f);
            yield return _k.Shot("05-records-challenges");
            _k.Key(MenuKey.Back);
            yield return _k.WaitTop(ScreenId.MainMenu);

            // ---- 강화: 하락(최소 연출이라 즉시 결과) → 하락 뒤 안내 + 다음 비용 경고 + 기록 칸
            ctx.Enhance = new EnhanceService(ctx, new FixedRoll(9999));
            SettingsOptions.SetEffectsLevel(ctx.Settings.Current, 2);
            var wand = p.Wand(c1.EquippedWandId);
            wand.Stage = wand.RecordPeak = wand.FloorPeak = 7;
            p.Items[Ids.Item.StoneMid] = new ItemStack { Qty = 50 };
            p.Items[Ids.Item.StoneLow] = new ItemStack { Qty = 50 };
            ProbBreakdown b = ctx.Enhance.Preview(wand.Id, new string[0], false);
            p.Currencies.Gold = b.CostGold * 2 + b.CostGold / 2;
            _k.Screens.Push(ScreenId.Enhance, ScreenArgs.From(ScreenId.MainMenu).With(ScreenArgKeys.WandId, wand.Id));
            yield return _k.WaitUntil(() => _k.Screens.Top == ScreenId.Enhance || _k.Screens.Top == ScreenId.EnhanceTutorial, "enhance");
            if (_k.Screens.Top == ScreenId.EnhanceTutorial) { _k.Key(MenuKey.Back); yield return _k.WaitTop(ScreenId.Enhance); }
            var es = (EnhanceScreen)_k.TopScreen;
            yield return _k.Seconds(0.4f);
            _k.Click(EnhanceNameKeys.Enhance);
            yield return _k.WaitUntil(() => es.Model.Phase == EnhancePhase.ResultCard, "result card");
            Assert.AreEqual(EnhanceOutcome.FailDrop, es.Model.Shown.Outcome);
            _k.Key(MenuKey.Cancel);   // 결과 카드 닫기(K)
            yield return _k.Seconds(0.4f);
            yield return _k.Shot("06-enhance-after-drop-warning");
            Assert.AreEqual(1, wand.EnhanceStats.Attempts);
            Assert.AreEqual(1, wand.EnhanceStats.Drops);
            Assert.AreEqual(0, _k.Errors.Count, string.Join("\n", _k.Errors));
        }
    }
}
