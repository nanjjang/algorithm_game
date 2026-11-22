using System;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;

namespace Wizard.Solo.Tests.RunModuleTests
{
    /// <summary>야영: 모닥불·출발 표식(D-49·D-52), 회복, 새벽 야영, 해금 차례(04 요약서 11.3절). 06 요약서 4.3절.</summary>
    public class RunFlowCampTests
    {
        static Rig AtCamp(int clearedWave, Action<RunConfig> tweak = null, Action<Rig> before = null)
        {
            var r = Rig.InCombatAt(clearedWave, tweak, before);
            r.ToCamp();
            return r;
        }

        static void HoldJ(Rig r, bool down) => r.P.Keys.Down[(int)LogicalKey.Fire] = down;

        // D-52: 모닥불 = 전장 가운데, 출발 표식 = 모닥불 + (4, 0), 반경 1.5 / 1.0
        [Test]
        public void D52__CampPositionsAndRadii_WrittenToRunState()
        {
            var r = AtCamp(1);
            Assert.AreEqual(new Vector2(12f, 7f), r.Run.CampFirePos);
            Assert.AreEqual(new Vector2(16f, 7f), r.Run.DepartMarkerPos);
            Assert.AreEqual(1.5f, r.Run.CampInteractRadius);
            Assert.AreEqual(1.0f, r.Run.DepartMarkerRadius);
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
            Assert.IsTrue(r.W.PlayerControlActive);

            var start = Rig.Create(c => c.StartWave = 11);
            Assert.AreEqual(new Vector2(12f, 7f), start.Run.CampFirePos);
            Assert.AreEqual(new Vector2(16f, 7f), start.Run.DepartMarkerPos);
            Assert.AreEqual(1.5f, start.Run.CampInteractRadius);
            Assert.AreEqual(1.0f, start.Run.DepartMarkerRadius);
        }

        [Test]
        public void Contract__JNearFire_OpensCampScreen_JOutsideDoesNothing()
        {
            var r = AtCamp(1);
            int changed = r.Count(SimEventType.CampChanged);

            r.P.Pos = r.Run.CampFirePos + new Vector2(1.6f, 0f);
            r.StepPressJ();
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub, "반경 밖의 J는 아무 일 없다");
            Assert.AreEqual(changed, r.Count(SimEventType.CampChanged));

            r.P.Pos = r.Run.CampFirePos + new Vector2(1.4f, 0f);
            r.Step(3);
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub, "누르지 않으면 열리지 않는다");
            r.StepPressJ();
            Assert.AreEqual(CampSubState.CampScreen, r.Run.CampSub);
            Assert.AreEqual(changed + 1, r.Count(SimEventType.CampChanged));
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.IsFalse(r.W.PlayerControlActive, "야영 화면 동안 플레이어는 못 움직인다");

            r.Cmd(RunCommandKind.CampClose);
            r.Step();
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
            Assert.AreEqual(changed + 2, r.Count(SimEventType.CampChanged));
        }

        [Test]
        public void Contract__StartCamp_HasSameFireInteraction()
        {
            var r = Rig.Create(c => c.StartWave = 11);
            Assert.AreEqual(RunPhase.StartCamp, r.Run.Phase);
            r.P.Pos = r.Run.CampFirePos + new Vector2(0f, 3f);
            r.StepPressJ();
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
            r.P.Pos = r.Run.CampFirePos + new Vector2(0f, 1f);
            r.StepPressJ();
            Assert.AreEqual(CampSubState.CampScreen, r.Run.CampSub);
            Assert.AreEqual(RunPhase.StartCamp, r.Run.Phase);
            r.Cmd(RunCommandKind.CampClose);
            r.Step();
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
        }

        [Test]
        public void Contract__CampOpenCommand_IsTheSameTransition()
        {
            var r = AtCamp(1);
            r.P.Pos = new Vector2(2f, 2f);
            r.Cmd(RunCommandKind.CampOpen);
            r.Step();
            Assert.AreEqual(CampSubState.CampScreen, r.Run.CampSub);
            // 화면이 떠 있는 동안에는 전장 상호작용을 읽지 않는다
            r.P.Pos = r.Run.DepartMarkerPos;
            HoldJ(r, true);
            r.Step(70);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(CampSubState.CampScreen, r.Run.CampSub);
        }

        // 06 요약서 4.3절: 출발 표식 위에서 J를 60틱 누르면 출발, 59틱에 떼면 취소
        [Test]
        public void S07_4_3__DepartHold_60Ticks_Departs()
        {
            var r = AtCamp(1);
            r.P.Pos = r.Run.DepartMarkerPos;
            HoldJ(r, true);
            int startTick = r.W.Tick;
            r.Step();
            Assert.AreEqual(CampSubState.DepartHold, r.Run.CampSub);
            Assert.AreEqual(startTick, r.Run.DepartHoldStartTick);
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.DepartHold, out ev));
            Assert.AreEqual(startTick, ev.A);
            r.Step(58);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase, "59틱까지는 아직");
            Assert.AreEqual(CampSubState.DepartHold, r.Run.CampSub);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase, "60틱째에 출발");
            Assert.AreEqual(2, r.W.Wave.Wave);
            Assert.AreEqual(-1, r.Run.DepartHoldStartTick);
        }

        [Test]
        public void S07_4_3__DepartHold_ReleasedAt59_Cancels()
        {
            var r = AtCamp(1);
            r.P.Pos = r.Run.DepartMarkerPos;
            HoldJ(r, true);
            r.Step(59);
            Assert.AreEqual(CampSubState.DepartHold, r.Run.CampSub);
            HoldJ(r, false);
            r.Step();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
            Assert.AreEqual(-1, r.Run.DepartHoldStartTick);
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.DepartHold, out ev));
            Assert.AreEqual(0, ev.A, "취소는 A = 0");
            // 다시 누르면 처음부터 센다
            HoldJ(r, true);
            r.Step(59);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
        }

        [Test]
        public void Contract__DepartHold_LeavingRadius_Cancels()
        {
            var r = AtCamp(1);
            r.P.Pos = r.Run.DepartMarkerPos + new Vector2(0.9f, 0f);
            HoldJ(r, true);
            r.Step(30);
            Assert.AreEqual(CampSubState.DepartHold, r.Run.CampSub);
            r.P.Pos = r.Run.DepartMarkerPos + new Vector2(1.1f, 0f);
            r.Step();
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
            r.Step(100);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase, "반경 밖에서는 눌러도 출발하지 않는다");
            Assert.AreEqual(CampSubState.FireIdle, r.Run.CampSub);
        }

        [Test]
        public void Contract__StartCamp_DepartHoldWorksToo()
        {
            var r = Rig.Create(c => c.StartWave = 21);
            r.P.Pos = r.Run.DepartMarkerPos;
            HoldJ(r, true);
            r.Step(59);
            Assert.AreEqual(RunPhase.StartCamp, r.Run.Phase);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            Assert.AreEqual(21, r.W.Wave.Wave);
        }

        [Test]
        public void S07_4_3__CampDepartCommand_FromScreen()
        {
            var r = AtCamp(3);
            r.Cmd(RunCommandKind.CampOpen);
            r.Step();
            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            Assert.AreEqual(4, r.Run.Wave);
            Assert.IsNull(r.Run.NextPlan);
        }

        [Test]
        public void S07_3_2__NextPlanBuiltOnCampEntry()
        {
            var r = Rig.InCombatAt(4);
            int builds = r.Waves.BuildCalls;
            r.ToCamp();
            Assert.AreEqual(builds + 1, r.Waves.BuildCalls);
            Assert.AreEqual(5, r.Run.NextPlan.Wave);
            var plan = r.Run.NextPlan;
            r.Depart();
            Assert.AreSame(plan, r.W.Wave.Plan, "야영에서 미리 만든 구성으로 시작한다");
            Assert.AreEqual(builds + 1, r.Waves.BuildCalls);
        }

        // ---- 회복(TV-190·191): w = 5 → 55 / 82 / 82, w = 10 → 80 / 120

        [Test]
        public void S07_TV_190__HealCost_Wave5_55_82_82()
        {
            var r = AtCamp(5);
            r.Run.Purse = 1000;
            r.P.Hp = 100;
            Assert.AreEqual(55, r.W.Mods.Run.HealCost(r.W));
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(945, r.Run.Purse);
            Assert.AreEqual(200, r.P.Hp, "1회 = 체력 1칸");
            Assert.AreEqual(82, r.W.Mods.Run.HealCost(r.W));
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(945 - 82, r.Run.Purse);
            Assert.AreEqual(82, r.W.Mods.Run.HealCost(r.W), "두 번째부터 1.5배(누적 아님)");
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(945 - 82 - 82, r.Run.Purse);
            Assert.AreEqual(400, r.P.Hp);
            Assert.AreEqual(3, r.Run.HealsThisCamp);
        }

        [Test]
        public void S07_TV_191__HealCost_Wave10_80_120()
        {
            var r = AtCamp(10);
            r.Run.Purse = 1000;
            r.P.Hp = 100;
            Assert.AreEqual(80, r.W.Mods.Run.HealCost(r.W));
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(920, r.Run.Purse);
            Assert.AreEqual(120, r.W.Mods.Run.HealCost(r.W));
        }

        [Test]
        public void S07_4_3__Heal_OnlyFromPurse_NotAtFullHp_ResetsNextCamp()
        {
            var r = AtCamp(5);
            r.P.Hp = 100;
            r.Run.Purse = 54;
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(54, r.Run.Purse, "지갑이 모자라면 못 한다");
            Assert.AreEqual(100, r.P.Hp);
            Assert.AreEqual(0, r.Run.HealsThisCamp);

            r.Run.Purse = 500;
            r.P.Hp = r.P.MaxHp;
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(500, r.Run.Purse, "가득이면 회복하지 않는다");

            r.P.Hp = 100;
            r.Cmd(RunCommandKind.CampHeal);
            r.Step();
            Assert.AreEqual(1, r.Run.HealsThisCamp);
            r.Depart();
            r.ToCamp();
            Assert.AreEqual(0, r.Run.HealsThisCamp);
            Assert.AreEqual(30 + 5 * 6, r.W.Mods.Run.HealCost(r.W), "다음 야영은 다시 기본 값(방금 넘긴 웨이브 6)");
        }

        // TV-193: 새벽 야영(보스 뒤) 진입 때 체력 1칸 무료 3/8 → 4/8
        [Test]
        public void S07_TV_193__DawnCamp_FreeHealOneCell()
        {
            var r = Rig.InCombatAt(10);
            r.P.Hp = 300;
            Assert.AreEqual(8, r.P.MaxHpCells);
            r.ToLoot();
            Assert.AreEqual(300, r.P.Hp, "야영에 들어갈 때 준다");
            r.PickAndConfirm();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            Assert.IsTrue(r.Run.DawnCamp);
            Assert.AreEqual(400, r.P.Hp);
            Assert.AreEqual(400, r.Run.Purse, "무료(지갑은 보스 웨이브 400 그대로)");
            SimEvent ev;
            Assert.IsTrue(r.TryLast(SimEventType.CampEntered, out ev));
            Assert.AreEqual(1, ev.A);

            var normal = AtCamp(4);
            Assert.IsFalse(normal.Run.DawnCamp);
            Assert.IsTrue(normal.TryLast(SimEventType.CampEntered, out ev));
            Assert.AreEqual(0, ev.A);
        }

        [Test]
        public void S07_4_3__NormalCamp_NoFreeHeal()
        {
            var r = Rig.InCombatAt(4);
            r.P.Hp = 300;
            r.ToCamp();
            Assert.AreEqual(300, r.P.Hp);
        }

        // TV-196: 야영에서는 조립, 회복, 비기 선택만 한다. 강화 기능 없음
        [Test]
        public void S07_TV_196__CampHasNoEnhance()
        {
            foreach (string name in Enum.GetNames(typeof(RunCommandKind)))
            {
                StringAssert.DoesNotContain("Enhance", name);
                StringAssert.DoesNotContain("Upgrade", name);
            }
            foreach (var m in typeof(RunFlow).GetMembers(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                StringAssert.DoesNotContain("Enhance", m.Name);
            // 야영에서 받는 명령을 전부 넣어도 지팡이 단계는 변하지 않는다
            var r = AtCamp(5);
            r.P.Wand.Stage = 3;
            r.Run.Purse = 100000;
            foreach (RunCommandKind kind in Enum.GetValues(typeof(RunCommandKind)))
            {
                if (kind == RunCommandKind.GiveUp || kind == RunCommandKind.CampDepart) continue;
                r.Cmd(kind, 0, 1);
            }
            r.Step();
            Assert.AreEqual(3, r.P.Wand.Stage);
            Assert.AreEqual(0, r.P.Wand.TempStageDrop);
        }

        // ---- 해금 차례(04 요약서 11.3절)

        [Test]
        public void S03_15_3__TurnPerClearedWave1To6_NotAfter()
        {
            var r = Rig.InCombatAt(1, null, rig => rig.Arts.DefaultCandidates = new[] { Ids.Art.Fan, Ids.Art.Wings });
            r.ToCamp();
            Assert.AreEqual(1, r.Run.PendingArtTurns.Count);
            Assert.IsNotNull(r.Run.CurrentArtOffer);
            Assert.AreEqual(1, r.Run.CurrentArtOffer.TurnWave);
            CollectionAssert.AreEqual(new[] { Ids.Art.Fan, Ids.Art.Wings }, r.Run.CurrentArtOffer.Candidates);
            Assert.AreEqual(1, r.Count(SimEventType.ArtOffered));

            // 웨이브 7에서 시작: 출발 야영에 1~6의 차례가 온다. 다 고르고(후보가 떨어지면 남은 차례는 쓰인다) 웨이브 7을 넘긴다.
            var late = Rig.Create(c => c.StartWave = 7, rig => rig.Arts.DefaultCandidates = new[] { Ids.Art.Fan, Ids.Art.Wings });
            Assert.AreEqual(6, late.Run.PendingArtTurns.Count);
            for (int guard = 0; guard < 10 && late.Run.CurrentArtOffer != null; guard++)
            {
                late.Cmd(RunCommandKind.ArtPick, 0, 0, late.Run.CurrentArtOffer.Candidates[0]);
                late.Step();
            }
            Assert.AreEqual(0, late.Run.PendingArtTurns.Count);
            Assert.AreEqual(2, late.Run.NewArtsThisRun.Count);
            late.Depart();
            late.ToCamp();
            Assert.AreEqual(0, late.Run.PendingArtTurns.Count, "웨이브 7은 차례를 주지 않는다");
            Assert.IsNull(late.Run.CurrentArtOffer);
        }

        [Test]
        public void S03_15_3__MustPickAllBeforeDeparting()
        {
            var r = Rig.InCombatAt(1, null, rig => rig.Arts.DefaultCandidates = new[] { Ids.Art.Fan, Ids.Art.Wings });
            r.ToCamp();
            // [출발] 명령은 거절
            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            // 출발 표식의 J는 야영 화면(비기 탭)으로 보낸다
            r.P.Pos = r.Run.DepartMarkerPos;
            HoldJ(r, true);
            r.Step();
            Assert.AreEqual(CampSubState.CampScreen, r.Run.CampSub);
            Assert.AreEqual(RunPhase.Camp, r.Run.Phase);
            HoldJ(r, false);

            // 후보가 아닌 것은 못 고른다
            r.Cmd(RunCommandKind.ArtPick, 0, 0, Ids.Art.Bloom);
            r.Step();
            Assert.IsNotNull(r.Run.CurrentArtOffer);
            Assert.AreEqual(0, r.Run.NewArtsThisRun.Count);

            r.Cmd(RunCommandKind.ArtPick, 0, 0, Ids.Art.Wings);
            r.Step();
            Assert.IsNull(r.Run.CurrentArtOffer);
            Assert.AreEqual(0, r.Run.PendingArtTurns.Count);
            CollectionAssert.AreEqual(new[] { Ids.Art.Wings }, r.Run.NewArtsThisRun);
            Assert.IsTrue(r.P.UnlockedArts.Contains(Ids.Art.Wings), "이 판에 바로 적용(Arts.Unlock)");

            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
            CollectionAssert.AreEqual(new[] { Ids.Art.Wings }, r.W.Mods.Run.BuildSummary(r.W).NewArts);
        }

        // U-R6: 시작 웨이브로 1~6을 건너뛴 판은 출발 야영에서 차례대로 고른다
        [Test]
        public void S03_15_3__SkippedRun_TurnsQueuedInStartCamp_InOrder()
        {
            var r = Rig.Create(c => c.StartWave = 11, rig =>
            {
                rig.Arts.Candidates[1] = new[] { Ids.Art.Fan, Ids.Art.Wings };
                rig.Arts.Candidates[2] = new[] { Ids.Art.Wings, Ids.Art.Spear };
                rig.Arts.Candidates[3] = new string[0];                              // 후보 없는 차례는 보여 주지 않고 쓴다
                rig.Arts.Candidates[4] = new[] { Ids.Art.ThunderSpear };             // 후보 1개면 1개만
                rig.Arts.Candidates[5] = new string[0];
                rig.Arts.Candidates[6] = new[] { Ids.Art.Bloom, Ids.Art.Vent };
            });
            Assert.AreEqual(RunPhase.StartCamp, r.Run.Phase);
            Assert.AreEqual(6, r.Run.PendingArtTurns.Count);
            Assert.AreEqual(1, r.Run.CurrentArtOffer.TurnWave);

            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            Assert.AreEqual(RunPhase.StartCamp, r.Run.Phase, "다 고르기 전에는 못 떠난다");

            r.Cmd(RunCommandKind.ArtPick, 0, 0, Ids.Art.Fan);
            r.Step();
            Assert.AreEqual(2, r.Run.CurrentArtOffer.TurnWave);
            r.Cmd(RunCommandKind.ArtPick, 0, 0, Ids.Art.Spear);
            r.Step();
            Assert.AreEqual(4, r.Run.CurrentArtOffer.TurnWave, "빈 차례 3은 건너뛴다");
            Assert.AreEqual(1, r.Run.CurrentArtOffer.Candidates.Count);
            r.Cmd(RunCommandKind.ArtPick, 0, 0, Ids.Art.ThunderSpear);
            r.Step();
            Assert.AreEqual(6, r.Run.CurrentArtOffer.TurnWave);
            r.Cmd(RunCommandKind.ArtPick, 0, 0, Ids.Art.Bloom);
            r.Step();
            Assert.IsNull(r.Run.CurrentArtOffer);
            Assert.AreEqual(0, r.Run.PendingArtTurns.Count);
            CollectionAssert.AreEqual(new[] { Ids.Art.Fan, Ids.Art.Spear, Ids.Art.ThunderSpear, Ids.Art.Bloom }, r.Run.NewArtsThisRun);

            r.Cmd(RunCommandKind.CampDepart);
            r.Step();
            Assert.AreEqual(RunPhase.WaveCombat, r.Run.Phase);
        }

        [Test]
        public void S03_15_3__SkipTo4_QueuesOnlyTurns1To3()
        {
            // 건너뛰기는 10 단위지만 규칙은 min(startWave − 1, 6)이다
            var r = Rig.Create(c => c.StartWave = 4, rig => rig.Arts.DefaultCandidates = new[] { Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Spear, Ids.Art.Vent });
            Assert.AreEqual(3, r.Run.PendingArtTurns.Count);
        }

        [Test]
        public void S03_15_3__NoCandidates_TurnConsumedSilently_CanDepart()
        {
            var r = Rig.InCombatAt(2);
            r.ToCamp();
            Assert.AreEqual(0, r.Run.PendingArtTurns.Count);
            Assert.IsNull(r.Run.CurrentArtOffer);
            Assert.AreEqual(0, r.Count(SimEventType.ArtOffered));
            r.Depart();
        }
    }

}
