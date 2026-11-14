using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 통합 (라)(마)(바): 판 상태 기계와 정산·강화·저장. 진짜 모듈·서비스 전부를 쓰고, 입력(봇)과 강화 난수만 정해 둔다.
    /// 기대값은 요약서에서 가져왔다(06 요약서 4절 전리품·야영, 05 요약서 8·10절 보스·허상, 01 요약서 10절 정산, 07 요약서 4절 강화).
    /// </summary>
    public class RunFlowIntegrationTests
    {
        IntegrationKit _k;

        [SetUp] public void SetUp() { _k = new IntegrationKit(); }
        [TearDown] public void TearDown() { _k.Dispose(); }

        /// <summary>PhaseChanged 이벤트에서 뽑은 판 상태의 흐름(중복 없이).</summary>
        List<RunPhase> PhaseTrail()
        {
            var l = new List<RunPhase>();
            foreach (var e in _k.Events(SimEventType.PhaseChanged)) l.Add((RunPhase)e.A);
            return l;
        }

        static bool InOrder(List<RunPhase> trail, params RunPhase[] seq)
        {
            int i = 0;
            foreach (var p in trail) if (i < seq.Length && p == seq[i]) i++;
            return i == seq.Length;
        }

        // ------------------------------------------------------------ (라) 클리어 → 전리품 → 야영 → 다음 웨이브

        [Test]
        public void Integ_D__Wave1Clear_Loot_Camp_Wave2()
        {
            _k.StartRun(1, seed: 5UL);
            Assert.IsTrue(_k.AutoPlay(9000, w => w.Run.Phase == RunPhase.LootPick, god: true),
                "웨이브 1을 클리어하고 전리품에 도착해야 한다(Phase = " + _k.W.Run.Phase + ", 적 " + _k.W.Enemies.Count + ")");
            TestContext.WriteLine("웨이브 1 클리어까지 " + _k.W.Tick + "틱, 처치 " + _k.W.Run.TotalKills + ", 점수 " + _k.W.Run.Score + ", 전리품 카드 " + _k.W.Run.Loot.Cards.Count);
            Assert.AreEqual(1, _k.W.Run.LastClearedWave);
            Assert.AreEqual(1, _k.W.Run.ClearedDirect);
            Assert.GreaterOrEqual(_k.Count(SimEventType.WaveCleared), 1);
            Assert.IsNotNull(_k.W.Run.Loot, "전리품 제안");
            Assert.Greater(_k.W.Run.Loot.Cards.Count, 0);
            Assert.Greater(_k.W.Run.TotalKills, 0);
            Assert.Greater(_k.W.Run.Score, 0, "웨이브 1 점수");
            int wavesBefore = _k.W.Run.WaveResults.Count;
            Assert.AreEqual(1, wavesBefore, "웨이브 결과 한 줄");

            // 전리품을 고르면 야영으로, 야영에서 출발하면 웨이브 2가 시작된다.
            Assert.IsTrue(_k.AutoPlay(1200, w => w.Run.Phase == RunPhase.Camp, god: true), "전리품 뒤 야영(Phase = " + _k.W.Run.Phase + ")");
            Assert.AreEqual(2, _k.W.Run.Wave, "야영 동안 Wave = 다음에 싸울 웨이브");
            Assert.IsNotNull(_k.W.Run.NextPlan);
            Assert.AreEqual(2, _k.W.Run.NextPlan.Wave);
            Assert.IsTrue(_k.AutoPlay(1200, w => w.Run.Phase == RunPhase.WaveCombat && w.Wave != null && w.Wave.Wave == 2, god: true),
                "야영에서 출발하면 웨이브 2 전투(Phase = " + _k.W.Run.Phase + ")");

            Assert.IsTrue(InOrder(PhaseTrail(), RunPhase.WavePrelude, RunPhase.WaveCombat, RunPhase.WaveClearing, RunPhase.LootPick,
                RunPhase.Camp, RunPhase.WavePrelude, RunPhase.WaveCombat), "판 상태 흐름이 요약서 순서여야 한다");
            Assert.GreaterOrEqual(_k.Count(SimEventType.CampEntered), 1);
            Assert.GreaterOrEqual(_k.Count(SimEventType.WaveStarted), 2);
        }

        // ------------------------------------------------------------ (마) 웨이브 10: 허상 첫 만남, 보스(가마등) 처치

        void StartAtWave10(bool phantomFirst)
        {
            _k.StartRun(1, seed: 10UL, cfg => { cfg.StartWave = 10; cfg.PhantomFirst = phantomFirst; });
            Assert.AreEqual(RunPhase.StartCamp, _k.W.Run.Phase, "시작 웨이브가 1보다 크면 출발 야영에서 시작");
            Assert.AreEqual(10, _k.W.Run.Wave);
        }

        [Test]
        public void Integ_E1__Wave10_PhantomFirstEncounter_Collapse_Rise_DawnCamp()
        {
            StartAtWave10(phantomFirst: true);
            bool tweaked = false;
            bool ok = _k.AutoPlay(9000, w =>
            {
                // 시간을 줄이려고 허상 체력만 바닥(50%) 바로 위로 깎는다. 그 뒤는 진짜 탄이 깎아 바닥에 닿게 한다.
                if (!tweaked && w.Boss != null)
                {
                    var body = w.FindEnemy(w.Boss.EnemyId);
                    if (body != null && body.HpFloor > 0f)
                    {
                        body.Hp = body.HpFloor + 25f;
                        tweaked = true;
                    }
                }
                return w.Run.PhantomFirstDone;
            }, god: true);
            Assert.IsTrue(ok, "허상 첫 만남이 끝나 일어남까지 가야 한다(Phase = " + _k.W.Run.Phase + ", Boss = " + (_k.W.Boss != null ? _k.W.Boss.State.ToString() : "없음") + ")");
            Assert.IsTrue(tweaked, "허상이 나와야 한다");

            // 흐름: 등장 → (체력 50%) 금이 감 → 되비침 → 쓰러짐 → 일어남.
            Assert.GreaterOrEqual(_k.Count(SimEventType.BossIntroBegin), 1);
            Assert.AreEqual(Ids.Boss.MirrorPhantom, _k.Events(SimEventType.BossIntroBegin)[0].Id);
            Assert.GreaterOrEqual(_k.Count(SimEventType.BossIntroEnd), 1);
            Assert.GreaterOrEqual(_k.Count(SimEventType.PhantomMirrorCrack), 1, "허상 체력이 바닥에 닿음");
            Assert.GreaterOrEqual(_k.Count(SimEventType.PhantomReflection), 1);
            Assert.GreaterOrEqual(_k.Count(SimEventType.PhantomCollapse), 1);
            Assert.GreaterOrEqual(_k.Count(SimEventType.PlayerDowned), 1, "되비침이 쓰러뜨린다");
            Assert.GreaterOrEqual(_k.Count(SimEventType.PlayerRose), 1, "쓰러짐 뒤 일어남");
            int crack = _k.FirstTick(SimEventType.PhantomMirrorCrack), refl = _k.FirstTick(SimEventType.PhantomReflection),
                col = _k.FirstTick(SimEventType.PhantomCollapse), rose = _k.FirstTick(SimEventType.PlayerRose);
            Assert.That(crack, Is.LessThanOrEqualTo(refl));
            Assert.That(refl, Is.LessThan(col));
            Assert.That(col, Is.LessThan(rose));

            // 판은 끝나지 않는다(DEC 1.21). 일어나면 체력 가득, 새벽 야영으로 웨이브 11.
            Assert.AreNotEqual(RunPhase.RunEnded, _k.W.Run.Phase);
            Assert.IsTrue(_k.AutoPlay(1500, w => w.InCamp, god: false), "일어난 뒤 야영(Phase = " + _k.W.Run.Phase + ")");
            Assert.AreEqual(11, _k.W.Run.Wave, "일어남 뒤 다음 웨이브는 11");
            Assert.IsTrue(_k.W.Run.DawnCamp, "새벽 야영");
            Assert.IsTrue(_k.W.Run.RevengeActive, "첫 만남 뒤는 복수 중");
            Assert.IsNull(_k.W.Boss, "보스는 걷혀 있어야 한다");
            Assert.AreEqual(_k.P.MaxHp, _k.P.Hp, "일어나면 체력 가득");
            Assert.IsFalse(_k.P.Downed);
            Assert.AreEqual(0, _k.P.Debt, "빚 0");
        }

        [Test]
        public void Integ_E2__Wave10_KilnBoarKill_Clearing_Loot_DawnCamp()
        {
            StartAtWave10(phantomFirst: false);
            bool tweaked = false;
            bool ok = _k.AutoPlay(9000, w =>
            {
                // 시간을 줄이려고 체력만 깎아 시작한다. 처치는 진짜 탄이 한다.
                if (!tweaked && w.Boss != null)
                {
                    var body = w.FindEnemy(w.Boss.EnemyId);
                    if (body != null) { body.Hp = Mathf.Min(body.Hp, 120f); tweaked = true; }
                }
                return w.Run.Phase == RunPhase.WaveClearing;
            }, god: true);
            Assert.IsTrue(ok, "보스를 처치해 클리어 연출까지 가야 한다(Phase = " + _k.W.Run.Phase + ")");
            Assert.IsTrue(tweaked);
            Assert.AreEqual(Ids.Boss.KilnBoar, _k.Events(SimEventType.BossIntroBegin)[0].Id, "웨이브 10 보스는 가마등");
            Assert.GreaterOrEqual(_k.Count(SimEventType.BossKilled), 1);
            Assert.AreEqual(0, _k.Count(SimEventType.PhantomReflection), "허상이 아니다");
            Assert.AreEqual(10, _k.W.Run.LastClearedWave);
            Assert.AreEqual(SimConst.Ticks(_k.W.Content.Camp.BossClearSeconds), _k.W.Run.ClearingTicks, "보스 처치 클리어 연출 길이(camp.json boss_clear_seconds)");

            Assert.IsTrue(_k.AutoPlay(1500, w => w.Run.Phase == RunPhase.LootPick, god: true));
            Assert.IsNull(_k.W.Boss, "전리품 때는 보스가 걷혀 있다");
            Assert.IsTrue(_k.AutoPlay(1500, w => w.Run.Phase == RunPhase.Camp, god: true), "전리품 뒤 야영(Phase = " + _k.W.Run.Phase + ")");
            Assert.IsTrue(_k.W.Run.DawnCamp, "보스 뒤 새벽 야영");
            Assert.AreEqual(11, _k.W.Run.Wave);
            Assert.IsTrue(InOrder(PhaseTrail(), RunPhase.BossIntro, RunPhase.WaveCombat, RunPhase.WaveClearing, RunPhase.LootPick, RunPhase.Camp));
        }

        // ------------------------------------------------------------ (바) 사망 → 정산 → 프로필 → 강화 → 저장·다시 읽기

        [Test]
        public void Integ_F__Death_Settlement_Profile_Enhance_SaveReload()
        {
            _k.StartRun(1, seed: 3UL);
            var wandId = _k.Config.WandId;
            Assert.AreEqual(1, _k.Profile.RunsStarted, "BuildRunConfig가 runs_started를 올린다");

            // 웨이브 1을 하나 클리어하고(점수·골드를 얻고) 다음 전투에서 죽는다.
            Assert.IsTrue(_k.AutoPlay(9000, w => w.Run.Phase == RunPhase.LootPick, god: true));
            Assert.IsTrue(_k.AutoPlay(2400, w => w.Run.Phase == RunPhase.WaveCombat && w.Wave.Wave == 2, god: true));
            // 진짜 피격 경로로 쓰러뜨린다(QueueHit → 10단계 확정 → Run.OnPlayerDowned).
            var hit = new PlayerHitRequest { AttackId = 1, CastId = 1, SourceEnemyId = 0, DamageCells = 99, Source = PlayerDamageSource.Contact, FromPos = _k.P.Pos };
            _k.P.InvulnUntilTick = 0;
            Assert.IsTrue(_k.W.Mods.Player.QueueHit(_k.W, _k.P, hit));
            _k.Step(god: false);
            Assert.AreEqual(RunPhase.RunEnded, _k.W.Run.Phase, "체력 0이면 판이 끝난다");
            Assert.AreEqual(RunEndReason.HpZero, _k.W.Run.EndReason);
            Assert.GreaterOrEqual(_k.Count(SimEventType.RunEnded), 1);

            // 정산.
            var summary = _k.W.Mods.Run.BuildSummary(_k.W);
            Assert.AreEqual(Ids.Char.C1, summary.CharId);
            Assert.AreEqual(wandId, summary.WandId);
            Assert.AreEqual(1, summary.ClearedDirect);
            Assert.AreEqual(RunEndReason.HpZero, summary.EndReason);
            Assert.Greater(summary.Score, 0);

            long goldBefore = _k.Profile.Currencies.Gold;
            var view = _k.Ctx.Settlement.Prepare(summary);
            Assert.AreEqual(summary.Score, view.Summary.Score);
            var res = _k.Ctx.Settlement.Commit(summary, _k.Ctx.Settlement.AutoPick(view));
            Assert.IsTrue(res.Ok, "정산: " + res.Error);
            Assert.IsNotNull(res.Value.Record);

            // 프로필 반영: 최고 기록, 판 목록, 정산한 판 표시, 골드.
            var rec = _k.Profile.Character(1);
            Assert.GreaterOrEqual(rec.BestWave, 1);
            Assert.Greater(rec.BestScore, 0);
            Assert.AreEqual(1, _k.Profile.RunHistory.Count);
            Assert.AreEqual(summary.RunId, _k.Profile.RunHistory[0].RunId);
            Assert.AreEqual("HpZero", _k.Profile.RunHistory[0].EndReason);
            Assert.AreEqual(summary.RunId, _k.Profile.LastSettledRunId);
            Assert.GreaterOrEqual(_k.Profile.Currencies.Gold, goldBefore, "골드는 줄지 않는다");
            // 같은 판은 한 번만 정산된다.
            Assert.IsFalse(_k.Ctx.Settlement.Commit(summary, new List<int>()).Ok, "같은 판 두 번째 정산은 거절");

            // 강화 한 번(난수 고정: 판정값 0 → 가장 낮은 값이라 성공 구간). 비용(골드·강화석)은 시도 순간에 빠진다.
            var wand = _k.Profile.Wand(wandId);
            Assert.AreEqual(0, wand.Stage);
            _k.Profile.Currencies.Gold += 100000;
            _k.Profile.Items[Ids.Item.StoneLow].Qty += 10;
            long goldPre = _k.Profile.Currencies.Gold;
            int stonePre = _k.Profile.Items[Ids.Item.StoneLow].Qty;
            var preview = _k.Ctx.Enhance.Preview(wandId, new string[0], false);
            Assert.AreEqual(ErrorCode.None, preview.Blocked, "강화 가능");
            _k.EnhanceRng.Push(0, 0);
            var er = _k.Ctx.Enhance.Enhance(wandId, new string[0], false);
            Assert.IsTrue(er.Ok, "강화: " + er.Error);
            Assert.IsTrue(er.Value.Outcome == EnhanceOutcome.Success || er.Value.Outcome == EnhanceOutcome.GreatSuccess, "판정값 0은 성공: " + er.Value.Outcome);
            Assert.Greater(er.Value.StageAfter, 0);
            Assert.AreEqual(goldPre - preview.CostGold, _k.Profile.Currencies.Gold, "골드 비용");
            Assert.AreEqual(stonePre - preview.StoneCount, _k.Profile.Items[Ids.Item.StoneLow].Qty, "강화석 비용");
            int stageAfter = wand.Stage;
            long goldAfter = _k.Profile.Currencies.Gold;
            int stoneAfter = _k.Profile.Items[Ids.Item.StoneLow].Qty;
            Assert.AreEqual(er.Value.StageAfter, stageAfter);

            // 저장 후 다시 읽기: 디스크에서 새로 읽은 프로필이 같다.
            var reloaded = _k.Relaunch();
            var p2 = reloaded.Current;
            Assert.AreEqual(stageAfter, p2.Wand(wandId).Stage, "강화한 단계가 저장되어 있다");
            Assert.AreEqual(goldAfter, p2.Currencies.Gold);
            Assert.AreEqual(stoneAfter, p2.Items[Ids.Item.StoneLow].Qty);
            Assert.AreEqual(1, p2.RunHistory.Count);
            Assert.AreEqual(summary.RunId, p2.LastSettledRunId);
            Assert.AreEqual(rec.BestWave, p2.Character(1).BestWave);
            Assert.AreEqual(rec.BestScore, p2.Character(1).BestScore);
            Assert.AreEqual(1, p2.RunsStarted);
        }
    }
}
