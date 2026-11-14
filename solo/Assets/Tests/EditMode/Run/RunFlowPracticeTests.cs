using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.RunModuleTests
{
    /// <summary>연습·튜토리얼 방(RunPhase.Practice). 00 문서 5.5절, 08 요약서 6.4·8.5절, D-57·D-59·D-77.</summary>
    public class RunFlowPracticeTests
    {
        static Rig Practice(System.Action<RunConfig> tweak = null)
        {
            return Rig.Create(c => { c.Mode = RunMode.Practice; if (tweak != null) tweak(c); });
        }

        static Rig Tutorial(int step = 1)
        {
            return Rig.Create(c => { c.Mode = RunMode.Tutorial; c.TutorialStep = step; });
        }

        static EnemyState Dummy(Rig r)
        {
            EnemyState found = null;
            foreach (var e in r.W.Enemies) if (e.DefId == Ids.Enemy.Dummy && !e.Dead) found = e;
            Assert.IsNotNull(found, "허수아비가 없다");
            return found;
        }

        static float Report(Rig r, string key)
        {
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.PracticeReport, out ev, key), "수치 이벤트가 없다: " + key);
            return ev.F;
        }

        [Test]
        public void Contract__PracticeRoom_Setup()
        {
            var r = Practice(c => { c.PracticeEnemyFire = true; c.PracticeHeatFreeze = true; });
            Assert.AreEqual(RunPhase.Practice, r.Run.Phase);
            Assert.IsTrue(r.W.CombatActive);
            Assert.IsTrue(r.W.AttackAllowed(r.P));
            // 웨이브 없는 방: 기본값(웨이브 1, 상한 없음)
            Assert.AreEqual(1, r.W.Wave.Wave);
            Assert.AreEqual(0, r.W.Wave.TelegraphCap);
            Assert.IsNull(r.W.Wave.Plan);
            Assert.AreEqual(0, r.Waves.BeginCalls);
            // 허수아비
            Assert.AreEqual(1, r.Enemies.SpawnCalls);
            var dummy = Dummy(r);
            Assert.IsTrue(r.W.InArena(dummy.Pos, 1f));
            Assert.AreNotEqual(r.P.Pos, dummy.Pos);
            Assert.IsTrue(dummy.NoReward);
            // 옵션은 RunConfig에서 복사
            Assert.IsFalse(r.Run.PracticeInfiniteCrystal);
            Assert.IsTrue(r.Run.PracticeEnemyFire);
            Assert.IsTrue(r.Run.PracticeHeatFreeze);
            Assert.AreEqual(0, r.Run.Storage.Count);
            Assert.AreEqual(RunMode.Practice, r.W.Mods.Run.BuildSummary(r.W).Mode);
        }

        [Test]
        public void Contract__PracticeSetOption_TogglesFlagsOnly()
        {
            var r = Practice();
            Assert.IsFalse(r.Run.PracticeInfiniteCrystal);
            Assert.IsFalse(r.Run.PracticeEnemyFire);
            Assert.IsFalse(r.Run.PracticeHeatFreeze);

            r.Cmd(RunCommandKind.PracticeSetOption, (int)PracticeOption.InfiniteCrystal, 1);
            r.Step();
            Assert.IsTrue(r.Run.PracticeInfiniteCrystal);
            Assert.IsFalse(r.Run.PracticeEnemyFire);
            r.Cmd(RunCommandKind.PracticeSetOption, (int)PracticeOption.EnemyFire, 1);
            r.Cmd(RunCommandKind.PracticeSetOption, (int)PracticeOption.HeatFreeze, 1);
            r.Step();
            Assert.IsTrue(r.Run.PracticeEnemyFire);
            Assert.IsTrue(r.Run.PracticeHeatFreeze);
            r.Cmd(RunCommandKind.PracticeSetOption, (int)PracticeOption.InfiniteCrystal, 0);
            r.Cmd(RunCommandKind.PracticeSetOption, (int)PracticeOption.EnemyFire, 0);
            r.Step();
            Assert.IsFalse(r.Run.PracticeInfiniteCrystal);
            Assert.IsFalse(r.Run.PracticeEnemyFire);
            Assert.IsTrue(r.Run.PracticeHeatFreeze);
            // 플래그만 바꾼다: 열·결정은 건드리지 않는다
            r.P.HeatHu = 1234;
            r.P.Crystals = 2;
            r.Cmd(RunCommandKind.PracticeSetOption, (int)PracticeOption.HeatFreeze, 0);
            r.Step();
            Assert.AreEqual(1234, r.P.HeatHu);
            Assert.AreEqual(2, r.P.Crystals);
        }

        [Test]
        public void Contract__PracticeReset_ClearsDummyShotsZonesAndPlayerMeters()
        {
            var r = Practice();
            var old = Dummy(r);
            r.P.Crystals = 3;
            r.P.Debt = 1;
            int reset = r.Player.ResetCalls;
            r.Cmd(RunCommandKind.PracticeReset);
            r.Step();
            Assert.IsTrue(old.Dead);
            Assert.AreEqual(1, r.Enemies.RemoveAllCalls);
            Assert.AreEqual(1, r.Threats.ClearAllCalls);
            Assert.AreEqual(1, r.Spells.ClearCalls);
            Assert.AreEqual(1, r.Arts.ClearZoneCalls);
            Assert.AreEqual(reset + 1, r.Player.ResetCalls, "열·콤보는 Player.ResetForWaveEnd로");
            Assert.AreEqual(0, r.P.Crystals);
            Assert.AreEqual(0, r.P.Debt);
            Assert.AreNotSame(old, Dummy(r));
            Assert.AreEqual(RunPhase.Practice, r.Run.Phase);
        }

        // 08 요약서 8.5절: 허수아비 수치(DPS, 최근 10초 피해, 임계 비율, 결정 1개당 피해). practice.json report_interval_seconds 0.5초마다
        [Test]
        public void S13_8_5__PracticeReport_EveryHalfSecond_FromDummyHpLoss()
        {
            var r = Practice();
            var dummy = Dummy(r);
            Assert.AreEqual(0, r.Count(SimEventType.PracticeReport));
            // 틱 0~29: 틱마다 10씩 맞힌다(9단계의 피해를 흉내 — 가득에서 깎는다)
            for (int t = 0; t < 30; t++)
            {
                dummy.Hp = dummy.MaxHp - 10f;
                r.Step();
                Assert.AreEqual(dummy.MaxHp, dummy.Hp, "틱 끝에 가득으로 되돌린다");
            }
            Assert.AreEqual(0, r.Count(SimEventType.PracticeReport), "0.5초(30틱)가 되기 전");
            r.Step();
            Assert.AreEqual(4, r.Count(SimEventType.PracticeReport), "네 값이 한 번에");
            Assert.AreEqual(300f, Report(r, PracticeReportKeys.RecentDamage), 0.01f);
            Assert.AreEqual(300f, Report(r, PracticeReportKeys.Dps), 0.01f, "첫 1초 안에는 1초로 나눈다");
            Assert.AreEqual(0f, Report(r, PracticeReportKeys.HeatRatio));
            Assert.AreEqual(0f, Report(r, PracticeReportKeys.DamagePerCrystal));
            r.Step(30);
            Assert.AreEqual(8, r.Count(SimEventType.PracticeReport));
            // 이벤트는 플레이어와 무관(Slot −1)
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.PracticeReport, out ev));
            Assert.AreEqual(-1, ev.Slot);
        }

        // "최근 10초 피해": 10초(600틱) 창
        [Test]
        public void S13_8_5__RecentDamage_TenSecondWindow()
        {
            var r = Practice();
            var dummy = Dummy(r);
            for (int t = 0; t < 10; t++)
            {
                dummy.Hp = dummy.MaxHp - 10f;
                r.Step();
            }
            r.Step(590);
            // 틱 599까지 돌았다. 마지막 보고는 틱 570: 창(틱 0~570)에 100 전부
            Assert.AreEqual(100f, Report(r, PracticeReportKeys.RecentDamage), 0.01f);
            r.Step();
            // 틱 600의 보고: 창 = 틱 1~600 → 틱 0의 10이 빠진다
            Assert.AreEqual(90f, Report(r, PracticeReportKeys.RecentDamage), 0.01f);
            r.Step(30);
            Assert.AreEqual(0f, Report(r, PracticeReportKeys.RecentDamage), 0.01f);
            // DPS는 첫 적중부터의 평균이라 0이 아니다
            Assert.AreEqual(100f / (631f / 60f), Report(r, PracticeReportKeys.Dps), 0.01f);
        }

        [Test]
        public void S13_8_5__HeatRatio_CritShotsOverShots_AndDamagePerCrystal()
        {
            var r = Practice();
            var dummy = Dummy(r);
            r.P.Derived.CritStartHu = 85 * SimConst.HuPerHeat;
            r.P.Crystals = 3;
            r.Step();
            // 보통 발사 3번(열이 임계 아래), 임계 발사 1번
            for (int i = 0; i < 3; i++)
            {
                r.P.HeatHu = 10 * SimConst.HuPerHeat;
                r.P.LastFireTick = r.W.Tick;
                dummy.Hp = dummy.MaxHp - 50f;
                r.Step();
                r.Step();
            }
            r.P.HeatHu = 85 * SimConst.HuPerHeat;
            r.P.LastFireTick = r.W.Tick;
            dummy.Hp = dummy.MaxHp - 50f;
            r.Step();
            // 결정 2개를 썼다(Player.SpendCrystals가 부르는 훅)
            r.W.Mods.Run.OnCrystalsSpent(r.W, r.P, 2);
            r.Step();
            r.Step(40);
            Assert.AreEqual(0.25f, Report(r, PracticeReportKeys.HeatRatio), 0.0001f);
            Assert.AreEqual(200f / 2f, Report(r, PracticeReportKeys.DamagePerCrystal), 0.01f);
        }

        [Test]
        public void S13_8_5__PracticeReset_ZeroesTheReport()
        {
            var r = Practice();
            var dummy = Dummy(r);
            for (int t = 0; t < 20; t++)
            {
                dummy.Hp = dummy.MaxHp - 10f;
                r.P.LastFireTick = r.W.Tick;
                r.Step();
            }
            r.Cmd(RunCommandKind.PracticeReset);
            r.Step(31);
            Assert.AreEqual(0f, Report(r, PracticeReportKeys.Dps));
            Assert.AreEqual(0f, Report(r, PracticeReportKeys.RecentDamage));
            Assert.AreEqual(0f, Report(r, PracticeReportKeys.HeatRatio));
        }

        [Test]
        public void Contract__Practice_AcceptsLoadoutCommands()
        {
            var r = Practice();
            int recompiles = r.Player.RecompileCalls;
            r.Cmd(RunCommandKind.LoadoutMove, 1, 3);
            r.Step();
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[2].BlockId);
            Assert.IsTrue(r.P.Loadout.Links[0].IsEmpty);
            Assert.AreEqual(recompiles + 1, r.Player.RecompileCalls);
            r.Cmd(RunCommandKind.LoadoutRemoveAll);
            r.Step();
            Assert.IsTrue(r.P.Loadout.Core.IsEmpty);
            Assert.AreEqual(2, r.Run.LibraryBench.Count);
            Assert.AreEqual(RunPhase.Practice, r.Run.Phase);
        }

        [Test]
        public void Contract__Practice_NoRewardsNoRunEnd()
        {
            var r = Practice();
            r.Kill(5, elite: true);
            r.W.Mods.Run.OnPlayerHit(r.W, r.P);
            r.W.Mods.Run.OnGraze(r.W, r.P);
            Assert.AreEqual(0, r.Run.Purse);
            Assert.AreEqual(0, r.Run.DropChances.Count);
            Assert.AreEqual(0, r.W.Drops.Count);
            // 적 탄 쏘기 옵션으로 체력이 바닥나도 판은 끝나지 않는다
            r.P.Hp = 0;
            r.P.Downed = true;
            r.W.Mods.Run.OnPlayerDowned(r.W, r.P);
            Assert.AreEqual(RunPhase.Practice, r.Run.Phase);
            Assert.AreEqual(1, r.Player.RiseCalls);
            Assert.IsFalse(r.P.Downed);
            Assert.AreEqual(0, r.Count(SimEventType.RunEnded));
            // 연습 방에서 나가는 포기는 받는다
            r.Cmd(RunCommandKind.GiveUp);
            r.Step();
            Assert.AreEqual(RunPhase.RunEnded, r.Run.Phase);
        }

        // ---- 튜토리얼(08 요약서 6.4절, D-59)

        [Test]
        public void D59__TutorialRoom_Setup_BlockInStorage_InfiniteCrystal_Step()
        {
            var r = Tutorial();
            Assert.AreEqual(RunPhase.Practice, r.Run.Phase);
            Assert.IsTrue(r.Run.PracticeInfiniteCrystal, "결정 무한을 켠다(④ 반동 사격)");
            Assert.AreEqual(1, r.Run.TutorialStep);
            Assert.AreEqual(1, r.Run.Storage.Count);
            // 일반 등급 고정 순서에서 투사체가 아닌 첫 블록
            Assert.AreEqual(Ids.Block.SplitTwo, r.Run.Storage[0].BlockId);
            Assert.IsFalse(r.Run.Storage[0].FromLibrary);
            Assert.AreNotEqual(0, r.Run.Storage[0].Uid);
            Assert.IsNotNull(Dummy(r));

            var from3 = Tutorial(3);
            Assert.AreEqual(3, from3.Run.TutorialStep);
            // 연습 모드에는 튜토리얼 블록이 없다
            Assert.AreEqual(0, Practice().Run.Storage.Count);
        }

        static int StepDoneCount(Rig r, int step)
        {
            int n = 0;
            foreach (var ev in r.W.Events) if (ev.Type == SimEventType.TutorialStepDone && ev.A == step) n++;
            return n;
        }

        static void Walk(Rig r, float cells, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                r.P.Pos += new Vector2(cells / ticks, 0f);
                r.Step();
            }
        }

        // ① 1칸 이상 이동 + 발사 1회
        [Test]
        public void S13_6_4__Step1_MoveOneCellAndFireOnce()
        {
            var r = Tutorial();
            r.Step();
            Walk(r, 1.2f, 12);
            Assert.AreEqual(0, StepDoneCount(r, 1), "이동만으로는 안 된다");
            r.P.LastFireTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 1));
            r.P.LastFireTick = r.W.Tick;
            r.Step(5);
            Walk(r, 2f, 10);
            Assert.AreEqual(1, StepDoneCount(r, 1), "단계마다 한 번만 낸다");
            Assert.AreEqual(1, r.Run.TutorialStep, "넘기는 것은 화면의 TutorialAdvance");

            var fireOnly = Tutorial();
            fireOnly.Step();
            fireOnly.P.LastFireTick = fireOnly.W.Tick;
            fireOnly.Step();
            Walk(fireOnly, 0.9f, 9);
            Assert.AreEqual(0, StepDoneCount(fireOnly, 1), "0.9칸은 모자란다");
            Walk(fireOnly, 0.2f, 2);
            Assert.AreEqual(1, StepDoneCount(fireOnly, 1));
        }

        // ② 자세를 누른 채 1칸 이동
        [Test]
        public void S13_6_4__Step2_MoveOneCellWhileInStance()
        {
            var r = Tutorial(2);
            r.Step();
            Walk(r, 3f, 30);
            Assert.AreEqual(0, StepDoneCount(r, 2), "자세 없이 걸은 것은 세지 않는다");
            r.P.Stance = StanceState.Stance;
            Walk(r, 0.9f, 9);
            Assert.AreEqual(0, StepDoneCount(r, 2));
            Walk(r, 0.2f, 2);
            Assert.AreEqual(1, StepDoneCount(r, 2));
            Assert.AreEqual(0, StepDoneCount(r, 1));
        }

        // ③ 임계 사격 1회
        [Test]
        public void S13_6_4__Step3_OneCritShot()
        {
            var r = Tutorial(3);
            r.P.Derived.CritStartHu = 85 * SimConst.HuPerHeat;
            r.Step();
            r.P.HeatHu = 85 * SimConst.HuPerHeat - 1;
            r.P.LastFireTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(0, StepDoneCount(r, 3), "발사 뒤 열이 임계 시작에 못 미치면 보통 발사");
            r.Step(3);
            r.P.HeatHu = 85 * SimConst.HuPerHeat;
            r.P.LastFireTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 3), "정확히 85는 임계");

            // 넘쳐서 잠긴 발사도 임계로 나간다
            var locked = Tutorial(3);
            locked.P.Derived.CritStartHu = 85 * SimConst.HuPerHeat;
            locked.Step();
            locked.P.Locked = true;
            locked.P.LockStartTick = locked.W.Tick;
            locked.P.LastFireTick = locked.W.Tick;
            locked.Step();
            Assert.AreEqual(1, StepDoneCount(locked, 3));
        }

        // ④ 반동 사격 1회
        [Test]
        public void S13_6_4__Step4_OneRecoilShot()
        {
            var r = Tutorial(4);
            r.Step(3);
            // 보통 탄은 세지 않는다
            r.W.Projectiles.Add(new ProjectileEntity { Id = r.W.NewEntityId(), OwnerSlot = 0, SpawnTick = r.W.Tick, Source = ShotSource.Fire });
            r.P.LastFireTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(0, StepDoneCount(r, 4));
            // 다른 비기는 세지 않는다
            r.W.Mods.Run.OnArtFired(r.W, r.P, Ids.Art.Burst);
            r.Step();
            Assert.AreEqual(0, StepDoneCount(r, 4));
            r.W.Mods.Run.OnArtFired(r.W, r.P, Ids.Art.Recoil);
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 4));
        }

        // ⑤ 블록 1개 끼움: Practice 단계의 LoadoutMove로 빈 칸에 블록이 들어가면 완료
        [Test]
        public void S13_6_4__Step5_InsertBlockIntoEmptySlot()
        {
            var r = Tutorial(5);
            r.Step();
            // 칸끼리 옮기는 것은 "끼움"이 아니다
            r.Cmd(RunCommandKind.LoadoutMove, 1, 4);
            r.Step();
            Assert.AreEqual(0, StepDoneCount(r, 5));
            // 보관함의 연습 블록을 빈 칸에
            r.Cmd(RunCommandKind.LoadoutMove, 100, 2);
            r.Step();
            Assert.AreEqual(Ids.Block.SplitTwo, r.P.Loadout.Links[1].BlockId);
            Assert.AreNotEqual(0, r.P.Loadout.Links[1].WildUid);
            Assert.AreEqual(0, r.Run.Storage.Count);
            Assert.AreEqual(1, StepDoneCount(r, 5));
            Assert.AreEqual(1, r.Count(SimEventType.TutorialStepDone));
        }

        [Test]
        public void Contract__TutorialAdvance_MovesStep_AlsoAsSkip_EndsAt6()
        {
            var r = Tutorial();
            r.Step();
            // 완료 전 넘김(건너뛰기)
            r.Cmd(RunCommandKind.TutorialAdvance, 2);
            r.Step();
            Assert.AreEqual(2, r.Run.TutorialStep);
            Assert.AreEqual(0, r.Count(SimEventType.TutorialStepDone));
            // 앞 단계에서 한 일은 다음 단계의 조건에 세지 않는다
            r.P.Stance = StanceState.Stance;
            Walk(r, 1.5f, 15);
            Assert.AreEqual(1, StepDoneCount(r, 2));
            r.Cmd(RunCommandKind.TutorialAdvance, 3);
            r.Step();
            Assert.AreEqual(3, r.Run.TutorialStep);
            r.Cmd(RunCommandKind.TutorialAdvance, 6);
            r.Step();
            Assert.AreEqual(6, r.Run.TutorialStep, "6 = 끝");
            r.P.LastFireTick = r.W.Tick;
            Walk(r, 3f, 10);
            Assert.AreEqual(1, r.Count(SimEventType.TutorialStepDone), "끝난 뒤에는 더 내지 않는다");
            r.Cmd(RunCommandKind.TutorialAdvance, 99);
            r.Step();
            Assert.AreEqual(6, r.Run.TutorialStep);

            // 연습 모드에서는 받지 않는다
            var practice = Practice();
            practice.Cmd(RunCommandKind.TutorialAdvance, 3);
            practice.Step();
            Assert.AreEqual(0, practice.Run.TutorialStep);
            practice.P.LastFireTick = practice.W.Tick;
            Walk(practice, 3f, 10);
            Assert.AreEqual(0, practice.Count(SimEventType.TutorialStepDone));
        }

        [Test]
        public void Contract__FullTutorialRun_FiveStepsInOrder()
        {
            var r = Tutorial();
            r.P.Derived.CritStartHu = 85 * SimConst.HuPerHeat;
            r.Step();
            // ①
            Walk(r, 1.2f, 12);
            r.P.LastFireTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 1));
            r.Cmd(RunCommandKind.TutorialAdvance, 2);
            r.Step();
            // ②
            r.P.Stance = StanceState.Stance;
            Walk(r, 1.3f, 13);
            r.P.Stance = StanceState.None;
            Assert.AreEqual(1, StepDoneCount(r, 2));
            r.Cmd(RunCommandKind.TutorialAdvance, 3);
            r.Step();
            // ③
            r.P.HeatHu = 90 * SimConst.HuPerHeat;
            r.P.LastFireTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 3));
            r.Cmd(RunCommandKind.TutorialAdvance, 4);
            r.Step();
            // ④
            r.W.Mods.Run.OnArtFired(r.W, r.P, Ids.Art.Recoil);
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 4));
            r.Cmd(RunCommandKind.TutorialAdvance, 5);
            r.Step();
            // ⑤
            r.Cmd(RunCommandKind.LoadoutMove, 100, 3);
            r.Step();
            Assert.AreEqual(1, StepDoneCount(r, 5));
            r.Cmd(RunCommandKind.TutorialAdvance, 6);
            r.Step();
            Assert.AreEqual(6, r.Run.TutorialStep);
            Assert.AreEqual(5, r.Count(SimEventType.TutorialStepDone));
            Assert.AreEqual(RunMode.Tutorial, r.W.Mods.Run.BuildSummary(r.W).Mode);
        }
    }
}
