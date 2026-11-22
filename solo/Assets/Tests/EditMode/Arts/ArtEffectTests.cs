using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>
    /// 비기 15종의 효과와 계약 벡터. 기대값은 04 요약서 9.2·9.3절, 02 요약서 8.3절, 03 요약서 12절에서 가져왔다.
    /// 가짜 Spells의 표준 T = 10(마력탄 하나 낀 +0 지팡이, A-12). 플레이어는 (12, 7)에서 동쪽을 본다.
    /// </summary>
    public class ArtEffectTests
    {
        const float T = 10f;
        const float Eps = 1e-3f;

        static ArtKit Cast(string charId, int crystals, string inputs, int stage5Ticks = 1)
        {
            var kit = new ArtKit(charId, crystals);
            kit.Seq(inputs);
            kit.Stage5(stage5Ticks);
            return kit;
        }

        static List<float> Angles(ArtKit kit)
        {
            var list = new List<float>();
            foreach (var r in kit.Spells.Executes) list.Add(Mathf.Repeat(r.DirDegrees, 360f));
            list.Sort();
            return list;
        }

        // ------------------------------------------------------------ 계약 벡터: 공격 허용(공통 규칙 7)

        [Test]
        public void S00_Rule7__KInCampDoesNothing()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.W.Run.Phase = RunPhase.Camp;
            kit.Seq("F, K+2304");
            kit.Stage5(3);
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFired));
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFizzled));
            Assert.AreEqual(5, kit.P.Crystals);
            Assert.AreEqual(0, kit.Spells.Executes.Count);
            Assert.IsFalse(kit.HasDecision);
            // 방향은 늘 판정기에 들어간다(판정기는 계속 듣는다).
            Assert.AreEqual(1, kit.CountEvents(SimEventType.CommandDirInput));
            Assert.AreEqual(kit.Trie.Find(CmdDir.Forward).Id, kit.Parser.Cursor);
        }

        [Test]
        public void S00_Rule7__KDuringBossIntroAttackLockDoesNothing()
        {
            var kit = new ArtKit(Ids.Char.C1, 0);
            kit.W.Run.Phase = RunPhase.BossIntro;
            kit.P.AttackLocked = true;
            kit.K();
            kit.Stage5();
            // 결정 0이어도 불발 이벤트가 나지 않는다(버릴 뿐이다).
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFizzled));
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFired));

            kit.P.Crystals = 5;
            kit.W.Run.Phase = RunPhase.WaveCombat;
            kit.K(100);
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtFired), "AttackLocked alone blocks K");
            Assert.AreEqual(5, kit.P.Crystals);
            kit.P.AttackLocked = false;
            kit.K(100);
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtFired));
            Assert.AreEqual(4, kit.P.Crystals);
        }

        [Test]
        public void S00_Rule7__BufferedKIsDroppedWhenAttackNotAllowed()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.W.Tick = (int)(ArtKit.T0 / SimConst.UnitsPerTick);
            kit.K();
            kit.Stage5();
            kit.K(4700);
            Assert.IsTrue(kit.Parser.HasBuffered);
            kit.W.Run.Phase = RunPhase.LootPick;
            kit.Stage5();
            Assert.IsFalse(kit.Parser.HasBuffered);
            kit.W.Run.Phase = RunPhase.WaveCombat;
            kit.Stage5(60);
            Assert.AreEqual(1, kit.CountEvents(SimEventType.ArtFired), "the dropped K never fires");
        }

        [Test]
        public void S00_Rule7__KPressWhileNotAllowedDropsBufferedK()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.K();
            kit.K(4700);
            Assert.IsTrue(kit.Parser.HasBuffered);
            kit.P.InputLocked = true;
            kit.K(100);
            Assert.IsFalse(kit.Parser.HasBuffered);
        }

        [Test]
        public void S00_Rule7__PendingPrepIsDroppedWhenAttackNotAllowed()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            var e = kit.AddEnemy(15f, 7f);
            kit.Seq("F, F+2304, K+2304");
            kit.Stage5(2);
            kit.W.Run.Phase = RunPhase.WaveClearing;
            kit.Stage5(10);
            Assert.AreEqual(0, kit.Spells.CountTo(e.Id));
        }

        // ------------------------------------------------------------ 계약 벡터: 최근 입력·미리보기

        [Test]
        public void S13_8_5__RecentInputsCarryGapAndAllowedGap()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            long t0 = kit.Now;
            kit.Seq("F, B+2304, B+2611");
            var list = new List<RecentInput>();
            kit.Arts.GetRecentInputs(kit.P, list);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(CmdDir.Forward, list[0].Dir);
            Assert.AreEqual(t0, list[0].TimeU);
            Assert.AreEqual(0, list[0].GapU);
            Assert.AreEqual(0, list[0].AllowedGapU);
            Assert.IsFalse(list[0].Chained);
            Assert.AreEqual(CmdDir.Back, list[1].Dir);
            Assert.AreEqual(t0 + 2304, list[1].TimeU);
            Assert.AreEqual(2304, list[1].GapU);
            Assert.AreEqual(3072, list[1].AllowedGapU, "F→B edge = 0.2s");
            Assert.IsTrue(list[1].Chained);
            Assert.AreEqual(2611, list[2].GapU);
            Assert.AreEqual(2765, list[2].AllowedGapU, "FB→B edge = 0.18s");
            Assert.IsTrue(list[2].Chained);
        }

        [Test]
        public void S13_8_5__RecentInputsDefaultGapIs4608()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("B, F+2304");
            var list = new List<RecentInput>();
            kit.Arts.GetRecentInputs(kit.P, list);
            Assert.AreEqual(4608, list[1].AllowedGapU);
            Assert.IsTrue(list[1].Chained);

            // 간선이 없는 자리(L 다음 F, 제자 1)와 0.3초를 넘긴 자리도 기본 간격으로 잰다.
            kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("L, F+2304, F+5000");
            list.Clear();
            kit.Arts.GetRecentInputs(kit.P, list);
            Assert.AreEqual(4608, list[1].AllowedGapU);
            Assert.IsFalse(list[1].Chained);
            Assert.AreEqual(5000, list[2].GapU);
            Assert.AreEqual(4608, list[2].AllowedGapU);
            Assert.IsFalse(list[2].Chained);
        }

        [Test]
        public void S03_6__RecentInputsBrokenLinkAndCapOfFive()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("F, B+3379");
            var list = new List<RecentInput>();
            kit.Arts.GetRecentInputs(kit.P, list);
            Assert.AreEqual(3379, list[1].GapU);
            Assert.AreEqual(3072, list[1].AllowedGapU);
            Assert.IsFalse(list[1].Chained, "gap over the edge limit → broken link");
            Assert.AreEqual(0, kit.LastEvent(SimEventType.CommandDirInput).B);

            kit = new ArtKit(Ids.Char.C1, 5);
            kit.Seq("L, R+2304, L+2304, R+2304, L+2304, R+2304, F+2304");
            kit.Arts.GetRecentInputs(kit.P, list);
            Assert.AreEqual(5, list.Count, "into is cleared, at most 5");
            Assert.AreEqual(CmdDir.Forward, list[4].Dir);
            Assert.AreEqual(CmdDir.Left, list[0].Dir);
            for (int i = 1; i < list.Count; i++) Assert.Greater(list[i].TimeU, list[i - 1].TimeU);
        }

        [Test]
        public void S13_8_5__PreviewCarriesLastDirTimeAndJustWindow()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now);
            Assert.AreEqual(-1, pv.LastDirTimeU);
            Assert.AreEqual(1075, pv.JustWindowU);
            Assert.AreEqual(Ids.Art.Burst, pv.ArtId);
            Assert.AreEqual(1f, pv.CooldownRatio, Eps);
            Assert.IsFalse(pv.Buffered);

            kit.Dir(CmdDir.Forward, 500);
            long tDir = kit.Now;
            pv = kit.Arts.Preview(kit.W, kit.P, kit.Now + 300);
            Assert.AreEqual(tDir, pv.LastDirTimeU);
            Assert.AreEqual(1075, pv.JustWindowU);

            kit.K(300);
            long tK = kit.Now;
            pv = kit.Arts.Preview(kit.W, kit.P, tK);
            Assert.AreEqual(-1, pv.LastDirTimeU, "K resets the command");
            Assert.AreEqual(0f, pv.CooldownRatio, Eps);
            Assert.AreEqual(0.5f, kit.Arts.Preview(kit.W, kit.P, tK + 3072).CooldownRatio, Eps);
            Assert.AreEqual(1f, kit.Arts.Preview(kit.W, kit.P, tK + 6144).CooldownRatio, Eps);
            kit.K(4700);
            Assert.IsTrue(kit.Arts.Preview(kit.W, kit.P, kit.Now).Buffered);
        }

        [Test]
        public void S03_6__PreviewOutOfStanceIsAlwaysBurst()
        {
            var kit = new ArtKit(Ids.Char.C1, 0, inStance: false);
            var pv = kit.Arts.Preview(kit.W, kit.P, kit.Now);
            Assert.AreEqual(Ids.Art.Burst, pv.ArtId);
            Assert.AreEqual(1, pv.Cost);
            Assert.IsTrue(pv.Fizzle);
            Assert.AreEqual(ArtFizzleReason.NoCrystal, pv.Reason);
        }

        // ------------------------------------------------------------ 공용 비기

        [Test]
        public void S03_12_2__Burst_OneTreeAtDoubleDamage()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.P.Facing = Dir8.NE;
            kit.K();
            kit.Stage5();
            Assert.AreEqual(1, kit.Spells.Executes.Count);
            var r = kit.Spells.Executes[0];
            Assert.AreEqual(2f, r.DamageMul, Eps);
            Assert.AreEqual(ShotSource.ManaDischarge, r.Source);
            Assert.IsTrue(r.HeatExempt);
            Assert.IsFalse(r.IsCrit);
            Assert.AreEqual(45f, r.DirDegrees, Eps);
            Assert.AreEqual(kit.P.Pos, r.Pos);
            Assert.AreSame(kit.P.Spell, r.Tree);
            Assert.AreEqual(1f, r.CharacterMul, Eps);
            Assert.AreEqual(0, kit.Player.VentCalls);
        }

        [Test]
        public void S03_TV_B33__BurstWhileHeatLockedKeepsTheLock()
        {
            var kit = new ArtKit(Ids.Char.C1, 5, inStance: false);
            kit.P.Locked = true;
            kit.K();
            kit.Stage5();
            Assert.AreEqual(Ids.Art.Burst, kit.Last.Art);
            Assert.AreEqual(1, kit.Spells.Executes.Count);
            Assert.IsTrue(kit.P.Locked);
        }

        [Test]
        public void S03_12_2__Recoil_TreeWithKnockbackAndStepBack()
        {
            var kit = Cast(Ids.Char.C1, 5, "B, K+2304");
            Assert.AreEqual(1, kit.Spells.Executes.Count);
            var r = kit.Spells.Executes[0];
            Assert.AreEqual(1f, r.DamageMul, Eps);
            Assert.AreEqual(ShotHitEffect.RecoilKnockback, r.HitEffect);
            Assert.AreEqual(ShotSource.ArtOther, r.Source);
            Assert.AreEqual(1, kit.Player.Moves.Count);
            Assert.AreEqual(-2f, kit.Player.Moves[0].Delta.x, Eps);
            Assert.AreEqual(0f, kit.Player.Moves[0].Delta.y, Eps);
            Assert.IsFalse(kit.Player.Moves[0].StopAtEnemies);
            Assert.AreEqual(1, kit.CountEvents(SimEventType.PlayerMovedByArt, Ids.Art.Recoil));
        }

        [Test]
        public void S03_12_2__Fan_ThreeBranches()
        {
            var kit = Cast(Ids.Char.C1, 5, "F, K+2304");
            Assert.AreEqual(3, kit.Spells.Executes.Count);
            CollectionAssert.AreEqual(new[] { 0f, 15f, 345f }, Angles(kit));
            foreach (var r in kit.Spells.Executes)
                Assert.AreEqual(Mathf.Approximately(Mathf.Repeat(r.DirDegrees, 360f), 0f) ? 1f : 0.7f, r.DamageMul, Eps);
        }

        [Test]
        public void S03_12_2__Wings_TwoSides()
        {
            var kit = Cast(Ids.Char.C1, 5, "L, K+2304");
            Assert.AreEqual(2, kit.Spells.Executes.Count);
            CollectionAssert.AreEqual(new[] { 90f, 270f }, Angles(kit));
            foreach (var r in kit.Spells.Executes) Assert.AreEqual(1f, r.DamageMul, Eps);
        }

        [Test]
        public void S03_12_2__Octastar_EightDirections()
        {
            var kit = Cast(Ids.Char.C1, 5, "F, B+2304, K+2304");
            Assert.AreEqual(8, kit.Spells.Executes.Count);
            CollectionAssert.AreEqual(new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f }, Angles(kit));
            Assert.AreEqual(1f, kit.Spells.Executes[0].DamageMul, Eps);
            Assert.AreEqual(0f, kit.Spells.Executes[0].DirDegrees, Eps);
            for (int i = 1; i < 8; i++) Assert.AreEqual(0.6f, kit.Spells.Executes[i].DamageMul, Eps);
            // 정면 외 뒤쪽부터 생략되도록 뒤(180도)가 맨 마지막이다(U-19).
            Assert.AreEqual(180f, Mathf.Repeat(kit.Spells.Executes[7].DirDegrees, 360f), Eps);
        }

        [Test]
        public void S03_12_2__Afterimage_MovesAlongLastPhysicalDirection()
        {
            var kit = Cast(Ids.Char.C3, 4, "L, L+2304, K+2304");
            Assert.AreEqual(Ids.Art.Afterimage, kit.Last.Art);
            Assert.AreEqual(1, kit.Player.Moves.Count);
            // 동쪽을 볼 때 왼쪽 = 화면 위(N) = 시뮬레이션 (0, −1). 2.5칸.
            Assert.AreEqual(0f, kit.Player.Moves[0].Delta.x, Eps);
            Assert.AreEqual(-2.5f, kit.Player.Moves[0].Delta.y, Eps);
            Assert.IsTrue(kit.Player.Moves[0].StopAtEnemies);
            Assert.AreEqual(1, kit.Player.Invulns.Count);
            Assert.AreEqual(9, kit.Player.Invulns[0].Ticks);
            Assert.IsTrue(kit.Player.Invulns[0].GrazeWindow);
            Assert.AreEqual(0, kit.Spells.Executes.Count);
            Assert.AreEqual(0, kit.Spells.Directs.Count);
        }

        [Test]
        public void S03_12_2__Spear_ThreeTLineAfterFiveTickPrep()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.P.Pos = new Vector2(2f, 7f);
            var onLine = kit.AddEnemy(13f, 7.5f);     // 거리 11, 옆으로 0.5 ≤ 0.25 + 몸 0.4
            var offLine = kit.AddEnemy(8f, 8f);       // 옆으로 1.0
            var tooFar = kit.AddEnemy(15f, 7f);       // 거리 13 > 사거리 12
            var behind = kit.AddEnemy(0.5f, 7f);
            kit.Seq("F, F+2304, K+2304");
            int judge = kit.W.Tick;
            kit.Stage5(5);
            Assert.AreEqual(0, kit.Spells.Directs.Count, "prep 0.08s = 5 ticks");
            kit.Stage5();
            Assert.AreEqual(1, kit.Spells.Directs.Count);
            Assert.AreEqual(judge + 5, kit.Spells.DirectTicks[0]);
            var d = kit.Spells.Directs[0];
            Assert.AreEqual(onLine.Id, d.TargetEnemyId);
            Assert.AreEqual(3f * T, d.Amount, Eps);
            Assert.IsTrue(d.IgnoresArmor);
            Assert.AreEqual(DamageOrigin.Art, d.Origin);
            Assert.AreEqual(Ids.Element.Arcane, d.Element, "core block (magic bolt) element");
            Assert.AreEqual(Ids.Art.Spear, d.SourceId);
            Assert.AreEqual(0, kit.Spells.CountTo(offLine.Id) + kit.Spells.CountTo(tooFar.Id) + kit.Spells.CountTo(behind.Id));
            Assert.AreEqual(0, kit.Spells.Executes.Count, "the spear does not run the link slots");
            Assert.AreEqual(0, kit.Enemies.Ccs.Count);
            var line = kit.LastEvent(SimEventType.ArtLine);
            Assert.AreEqual(14f, line.Pos2.x, Eps);
            Assert.AreEqual(0.5f, line.F, Eps);
        }

        [Test]
        public void S03_12_2__ThunderSpear_SixTAndStun_BossHalts()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            var normal = kit.AddEnemy(15f, 7f);
            var boss = kit.AddEnemy(18f, 7f, tags: EnemyTags.Boss, boss: true);
            kit.Seq("F, F+2304, K+500");
            kit.Stage5();
            Assert.AreEqual(Ids.Art.ThunderSpear, kit.Last.Art);
            Assert.AreEqual(6f * T, kit.Spells.TotalTo(normal.Id), Eps);
            Assert.AreEqual(6f * T, kit.Spells.TotalTo(boss.Id), Eps);
            Assert.AreEqual(1, kit.Enemies.Ccs.Count);
            Assert.AreEqual(normal.Id, kit.Enemies.Ccs[0].EnemyId);
            Assert.AreEqual(CcKind.Stun, kit.Enemies.Ccs[0].Kind);
            Assert.AreEqual(60f, kit.Enemies.Ccs[0].Amount, Eps);
            Assert.AreEqual(CcSource.ThunderSpear, kit.Enemies.Ccs[0].Source);
            Assert.AreEqual(1, kit.Bosses.Halts.Count);
            Assert.AreEqual(boss.Id, kit.Bosses.Halts[0].EnemyId);
            Assert.AreEqual(18, kit.Bosses.Halts[0].Ticks);
        }

        [Test]
        public void S03_TV_B34__VentWhileLockedDealsFourTAndUnlocks()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.P.Locked = true;
            kit.P.HeatHu = SimConst.HeatToHu(100f);
            var near = kit.AddEnemy(14f, 7f);
            var far = kit.AddEnemy(16f, 7f);
            kit.Seq("B, F+2304, K+2304");
            Assert.AreEqual(Ids.Art.Vent, kit.Last.Art);
            kit.Stage5(4);
            Assert.AreEqual(0, kit.Spells.Directs.Count, "prep 0.06s = 4 ticks");
            Assert.IsTrue(kit.P.Locked);
            kit.Stage5();
            Assert.AreEqual(4f * T, kit.Spells.TotalTo(near.Id), Eps);
            Assert.AreEqual(0, kit.Spells.CountTo(far.Id), "radius 3");
            Assert.IsFalse(kit.P.Locked);
            Assert.AreEqual(0, kit.P.HeatHu);
            Assert.AreEqual(1, kit.Threats.Clears.Count);
            Assert.AreEqual(3f, kit.Threats.Clears[0].Radius, Eps);
            Assert.AreEqual(Ids.Element.Arcane, kit.Spells.Directs[0].Element);
        }

        [Test]
        public void S03_TV_B35__VentAtHeat50DealsTwoT()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.P.HeatHu = SimConst.HeatToHu(50f);
            var near = kit.AddEnemy(12f, 9.5f);
            kit.Seq("B, F+2304, K+2304");
            kit.Stage5(5);
            Assert.AreEqual(2f * T, kit.Spells.TotalTo(near.Id), Eps);
            Assert.AreEqual(0, kit.P.HeatHu);
        }

        [Test]
        public void S00_Rule2__PendingDeathAndBurrowedAreNotAreaTargets()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.P.HeatHu = SimConst.HeatToHu(50f);
            var alive = kit.AddEnemy(13f, 7f);
            var dying = kit.AddEnemy(13f, 8f);
            dying.PendingDeath = true;
            var burrowed = kit.AddEnemy(11f, 7f);
            burrowed.Burrowed = true;
            kit.Seq("B, F+2304, K+2304");
            kit.Stage5(5);
            Assert.AreEqual(1, kit.Spells.Directs.Count);
            Assert.AreEqual(alive.Id, kit.Spells.Directs[0].TargetEnemyId);
        }

        [Test]
        public void S03_12_2__Bloom_RepeatsAtRoundedIntervalsAndClearsOnLastWave()
        {
            var kit = new ArtKit(Ids.Char.C5, 7);
            kit.Seq(ArtKit.BloomInput);
            int judge = kit.W.Tick;
            kit.Stage5(70);
            // 준비 15틱 뒤, i번째 파동 = round(i × 7.2): 0, 7, 14, 22, 29, 36, 43.
            int[] offsets = { 0, 7, 14, 22, 29, 36, 43 };
            Assert.AreEqual(7 * 8, kit.Spells.Executes.Count);
            for (int i = 0; i < 7; i++)
                for (int k = 0; k < 8; k++)
                    Assert.AreEqual(judge + 15 + offsets[i], kit.Spells.ExecuteTicks[i * 8 + k], "wave " + i);
            for (int i = 0; i < kit.Spells.Executes.Count; i++)
            {
                var r = kit.Spells.Executes[i];
                Assert.AreEqual(i % 8 == 0 ? 2f : 1.2f, r.DamageMul, Eps);
                Assert.AreEqual(1f, r.CharacterMul, Eps, "bloom leaves 0 crystals → +0%");
            }
            Assert.AreEqual(1, kit.Threats.Clears.Count);
            Assert.AreEqual(judge + 15 + 43, kit.Threats.Clears[0].Tick);
            Assert.AreEqual(3f, kit.Threats.Clears[0].Radius, Eps);
            Assert.AreEqual(7, kit.CountEvents(SimEventType.ArtPulse, Ids.Art.Bloom));
            CollectionAssert.AreEqual(new[] { Ids.Art.Bloom }, kit.Run.ArtsFired, "만개 발동은 Run.OnArtFired로 한 번 알린다");
            CollectionAssert.Contains(kit.Traits.CrystalArgs, 0);
        }

        [Test]
        public void S03_A_08__BloomBudget160DropsLastRepeats()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.Spells.SpawnedPerExecute = 10;
            kit.Seq(ArtKit.BloomInput);
            int judge = kit.W.Tick;
            kit.Stage5(70);
            // 한 번의 비기 160개: 파동 하나가 80개 → 두 파동까지만 나가고 뒤의 반복은 생략된다.
            Assert.AreEqual(16, kit.Spells.Executes.Count);
            Assert.AreEqual(judge + 15, kit.Spells.ExecuteTicks[0]);
            Assert.AreEqual(judge + 15 + 7, kit.Spells.ExecuteTicks[15]);
            Assert.AreEqual(160, kit.Spells.Executes[0].ProjectileBudget);
            Assert.AreEqual(10, kit.Spells.Executes[15].ProjectileBudget);
            Assert.AreEqual(1, kit.Threats.Clears.Count, "the last wave still clears shots");
        }

        // ------------------------------------------------------------ 고유 비기

        [Test]
        public void S04_5_2__Kiln_EightPulsesOfQuarterT_SlowAndExpiry()
        {
            var kit = new ArtKit(Ids.Char.C1, 5);
            var inside = kit.AddEnemy(14f, 7f);
            var outside = kit.AddEnemy(15f, 7f);
            kit.Seq("B, B+2304, K+2304");
            int cast = kit.W.Tick;
            kit.Stage5();
            var z = kit.Zone(PlayerZoneKind.Kiln);
            Assert.IsNotNull(z);
            Assert.AreEqual(2.5f, z.Radius, Eps);
            Assert.AreEqual(kit.P.Pos, z.Pos);
            Assert.AreEqual(cast + 240, z.UntilTick);
            Assert.AreEqual(1920, kit.P.KilnAbsorbLeftHu, "heat 16 = 1,920 HU");
            Assert.IsTrue(kit.P.InKilnZone);
            Assert.AreEqual(1, kit.CountEvents(SimEventType.PlayerZoneSpawned));

            kit.Stage5(29);
            Assert.AreEqual(0, kit.Spells.Directs.Count);
            kit.Stage5();
            Assert.AreEqual(1, kit.Spells.CountTo(inside.Id));
            Assert.AreEqual(cast + 30, kit.Spells.DirectTicks[0]);
            Assert.AreEqual(0.25f * T, kit.Spells.Directs[0].Amount, Eps);
            Assert.AreEqual(Ids.Element.Fire, kit.Spells.Directs[0].Element);
            Assert.AreEqual(DamageOrigin.Zone, kit.Spells.Directs[0].Origin);

            kit.Stage5(240);
            Assert.AreEqual(8, kit.Spells.CountTo(inside.Id));
            Assert.AreEqual(2f * T, kit.Spells.TotalTo(inside.Id), Eps);
            Assert.AreEqual(cast + 240, kit.Spells.DirectTicks[7]);
            Assert.AreEqual(0, kit.Spells.CountTo(outside.Id));
            Assert.IsNull(kit.Zone(PlayerZoneKind.Kiln));
            Assert.AreEqual(0, kit.P.KilnAbsorbLeftHu);
            Assert.IsFalse(kit.P.InKilnZone);
            Assert.AreEqual(1, kit.CountEvents(SimEventType.PlayerZoneExpired));

            // 감속 −20%: 지대 안의 적에게만, 틱마다 1틱짜리로 다시 건다(나가면 바로 풀린다).
            foreach (var s in kit.Enemies.Slows)
            {
                Assert.AreEqual(inside.Id, s.EnemyId);
                Assert.AreEqual(SlowSlot.Kiln, s.Slot);
                Assert.AreEqual(0.2f, s.Strength, Eps);
                Assert.AreEqual(1, s.Ticks);
            }
            Assert.AreEqual(241, kit.Enemies.Slows.Count);
        }

        [Test]
        public void S04_15_5_71__KilnAbsorbsFourMagicBoltCritShots()
        {
            // 02 요약서 8.3절 검산: 마력탄(열 4 = 480 HU) 임계 발사 1~4발의 열을 먹고(16 → 0) 5발째부터는 먹지 않는다.
            var kit = Cast(Ids.Char.C1, 5, "B, B+2304, K+2304");
            int heat = TestContent.Db.Block(Ids.Block.MagicBolt).HeatHu;
            Assert.AreEqual(480, heat);
            for (int shot = 1; shot <= 4; shot++)
            {
                Assert.AreEqual(480, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, heat), "shot " + shot);
                Assert.AreEqual(1920 - 480 * shot, kit.P.KilnAbsorbLeftHu);
            }
            Assert.AreEqual(0, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, heat), "shot 5");
            Assert.AreEqual(4, kit.CountEvents(SimEventType.KilnAbsorbed));
        }

        [Test]
        public void S04_5_2__KilnAbsorbPartialOutsideAndRecast()
        {
            var kit = Cast(Ids.Char.C1, 5, "B, B+2304, K+2304");
            // 남은 양보다 크면 남은 만큼만 먹는다.
            Assert.AreEqual(1800, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, 1800));
            Assert.AreEqual(120, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, 480));
            Assert.AreEqual(0, kit.P.KilnAbsorbLeftHu);

            // 새로 깔면 이전 것은 사라지고 흡수량은 새 16(이어받지 않는다).
            int firstId = kit.Zone(PlayerZoneKind.Kiln).Id;
            kit.Now += 20000;
            kit.Seq("B, B+2304, K+2304");
            kit.Stage5();
            Assert.AreEqual(1920, kit.P.KilnAbsorbLeftHu);
            Assert.AreNotEqual(firstId, kit.Zone(PlayerZoneKind.Kiln).Id);
            Assert.AreEqual(1, kit.W.PlayerZones.Count);

            // 지대 밖(경계 포함 2.5칸)에서는 먹지 않는다.
            kit.P.Pos = kit.Zone(PlayerZoneKind.Kiln).Pos + new Vector2(2.5f, 0f);
            Assert.AreEqual(480, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, 480));
            kit.P.Pos += new Vector2(0.1f, 0f);
            Assert.AreEqual(0, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, 480));
            kit.Stage5();
            Assert.IsFalse(kit.P.InKilnZone);
        }

        [Test]
        public void S04_5_3__BoltDash_PathDamageStaggerInvulnAndOneGraze()
        {
            var kit = new ArtKit(Ids.Char.C2, 5);
            var onPath = kit.AddEnemy(14f, 7.6f);     // 옆으로 0.6 ≤ 0.5 + 몸 0.4
            var offPath = kit.AddEnemy(14f, 9f);
            var beyond = kit.AddEnemy(18f, 7f);       // 4칸 길(12 → 16)의 끝에서 2칸 밖
            kit.W.EnemyShots.Add(new EnemyShotEntity { Id = kit.W.NewEntityId(), CastId = 7, Pos = new Vector2(13f, 7f), Radius = 0.1f });
            kit.W.EnemyShots.Add(new EnemyShotEntity { Id = kit.W.NewEntityId(), CastId = 8, Pos = new Vector2(15f, 7f), Radius = 0.1f });
            kit.W.EnemyShots.Add(new EnemyShotEntity { Id = kit.W.NewEntityId(), CastId = 9, Pos = new Vector2(15f, 3f), Radius = 0.1f });
            kit.Seq("L, F+2304, K+2304");
            kit.Stage5();
            Assert.AreEqual(Ids.Art.BoltDash, kit.Last.Art);
            Assert.AreEqual(1, kit.Player.Moves.Count);
            Assert.AreEqual(4f, kit.Player.Moves[0].Delta.x, Eps);
            Assert.IsFalse(kit.Player.Moves[0].StopAtEnemies, "passes through enemies");
            Assert.IsTrue(kit.Player.Moves[0].StopAtObstacles);
            Assert.AreEqual(16f, kit.P.Pos.x, Eps);
            Assert.AreEqual(12, kit.Player.Invulns[0].Ticks);
            Assert.IsFalse(kit.Player.Invulns[0].GrazeWindow);

            Assert.AreEqual(1, kit.Spells.Directs.Count);
            Assert.AreEqual(onPath.Id, kit.Spells.Directs[0].TargetEnemyId);
            Assert.AreEqual(3f * T, kit.Spells.Directs[0].Amount, Eps);
            Assert.AreEqual(Ids.Element.Lightning, kit.Spells.Directs[0].Element);
            Assert.AreEqual(0, kit.Spells.CountTo(offPath.Id) + kit.Spells.CountTo(beyond.Id));
            Assert.AreEqual(1, kit.Enemies.Ccs.Count);
            Assert.AreEqual(CcKind.Stagger, kit.Enemies.Ccs[0].Kind);
            Assert.AreEqual(36f, kit.Enemies.Ccs[0].Amount, Eps);
            Assert.AreEqual(CcSource.BoltDash, kit.Enemies.Ccs[0].Source);

            // 길 위의 적 탄이 둘이어도 스침은 1회.
            Assert.AreEqual(1, kit.Player.Grazes.Count);
            Assert.AreEqual(GrazeKind.BoltDash, kit.Player.Grazes[0].Kind);
            Assert.AreEqual(7, kit.Player.Grazes[0].CastId);
        }

        [Test]
        public void S04_5_4__CopyCircle_TwelveCopiesEverySecondMagicBoltShot()
        {
            // 02 요약서 8.3절 검산: 마력탄(간격 0.15초 = 9틱)이면 내 발사 2번에 1번 따라 쏘고, 12번 쏘면 6초가 남아도 사라진다.
            var kit = new ArtKit(Ids.Char.C3, 4);
            kit.Seq("F, L+2304, K+2304");
            kit.Stage5();
            var z = kit.Zone(PlayerZoneKind.CopyCircle);
            Assert.IsNotNull(z);
            Vector2 circlePos = z.Pos;
            Assert.AreEqual(12, z.ChargesLeft);
            Assert.AreEqual(kit.W.Tick - 1 + 360, z.UntilTick);
            kit.P.Pos += new Vector2(3f, 0f);
            kit.P.Facing = Dir8.N;

            int interval = TestContent.Db.Block(Ids.Block.MagicBolt).IntervalTicks;
            Assert.AreEqual(9, interval);
            int startTick = kit.W.Tick;
            int shots = 0, shotsAtLastCopy = 0, tickAtLastCopy = 0;
            for (int t = 0; t < 360 && kit.Zone(PlayerZoneKind.CopyCircle) != null; t++)
            {
                if (t % interval == 0)
                {
                    int before = kit.Spells.Executes.Count;
                    shots++;
                    kit.Arts.OnPlayerFired(kit.W, kit.P, shots);
                    bool copied = kit.Spells.Executes.Count > before;
                    Assert.AreEqual(shots % 2 == 1, copied, "shot " + shots);
                    if (copied) { shotsAtLastCopy = shots; tickAtLastCopy = kit.W.Tick; }
                }
                kit.Stage5();
            }
            Assert.AreEqual(12, kit.Spells.Executes.Count);
            Assert.AreEqual(23, shotsAtLastCopy);
            Assert.Less(tickAtLastCopy - startTick, 360, "gone before the 6 seconds are up");
            Assert.IsNull(kit.Zone(PlayerZoneKind.CopyCircle));
            foreach (var r in kit.Spells.Executes)
            {
                Assert.AreEqual(0.5f, r.DamageMul, Eps);
                Assert.AreEqual(ShotSource.CopyCircle, r.Source);
                Assert.IsTrue(r.HeatExempt);
                Assert.IsFalse(r.IsCrit);
                Assert.AreEqual(circlePos, r.Pos, "the circle does not move");
                Assert.AreEqual(90f, r.DirDegrees, Eps, "my facing at fire time");
            }
            // 사라진 뒤의 발사는 따라 쏘지 않는다.
            kit.Arts.OnPlayerFired(kit.W, kit.P, 99);
            Assert.AreEqual(12, kit.Spells.Executes.Count);
        }

        [Test]
        public void S04_5_4__CopyCircle_ExpiresAfterSixSecondsAndIgnoresArtTrees()
        {
            var kit = new ArtKit(Ids.Char.C3, 4);
            kit.Seq("F, L+2304, K+2304");
            kit.Stage5();
            // 비기의 트리 실행(마력 방출)은 따라 쏘지 않는다.
            kit.ExitStance();
            kit.K(20000);
            kit.Stage5();
            Assert.AreEqual(1, kit.Spells.Executes.Count);
            Assert.AreEqual(ShotSource.ManaDischarge, kit.Spells.Executes[0].Source);
            kit.Stage5(357);
            Assert.IsNotNull(kit.Zone(PlayerZoneKind.CopyCircle));
            kit.Stage5(2);
            Assert.IsNull(kit.Zone(PlayerZoneKind.CopyCircle));
        }

        static ArtKit ScreenKit()
        {
            var kit = new ArtKit(Ids.Char.C4, 6);
            kit.Seq("B, L+2304, K+2304");
            kit.Stage5();
            Assert.AreEqual(Ids.Art.IceScreen, kit.Last.Art);
            return kit;
        }

        [Test]
        public void S04_5_5__IceScreen_GeometryAndBlocking()
        {
            var kit = ScreenKit();
            var z = kit.Zone(PlayerZoneKind.IceScreen);
            Assert.IsNotNull(z);
            // 시야(동) 정면 1.5칸, 폭 3 × 두께 0.5, 4초.
            Assert.AreEqual(13.5f, z.Pos.x, Eps);
            Assert.AreEqual(7f, z.Pos.y, Eps);
            Assert.AreEqual(3f, z.Width, Eps);
            Assert.AreEqual(0.5f, z.Thickness, Eps);
            Assert.AreEqual(Dir8.E, z.Facing);
            Assert.AreEqual(kit.W.Tick - 1 + 240, z.UntilTick);

            int id;
            Assert.IsTrue(kit.Arts.SegmentHitsScreen(kit.W, new Vector2(16f, 7f), new Vector2(12f, 7f), out id));
            Assert.AreEqual(z.Id, id);
            Assert.IsTrue(kit.Arts.SegmentHitsScreen(kit.W, new Vector2(16f, 8.4f), new Vector2(12f, 8.4f), out id));
            Assert.IsFalse(kit.Arts.SegmentHitsScreen(kit.W, new Vector2(16f, 8.6f), new Vector2(12f, 8.6f), out id), "beside the wall");
            Assert.AreEqual(-1, id);
            Assert.IsFalse(kit.Arts.SegmentHitsScreen(kit.W, new Vector2(16f, 7f), new Vector2(14f, 7f), out id), "stops short of the wall");
            Assert.IsFalse(kit.Arts.SegmentHitsScreen(kit.W, new Vector2(14f, 5f), new Vector2(14f, 9f), out id), "parallel, in front");
        }

        [Test]
        public void S04_5_5__IceScreen_WalkerBlockedDasherBreaksAndIsStaggered()
        {
            var kit = ScreenKit();
            var z = kit.Zone(PlayerZoneKind.IceScreen);
            var walker = kit.AddEnemy(14.2f, 7f);
            var behind = kit.AddEnemy(12.6f, 7f);
            var farFront = kit.AddEnemy(16.5f, 7f);
            Assert.IsFalse(kit.Arts.OnEnemyTouchedScreen(kit.W, z.Id, walker, false), "a walking enemy cannot pass and does not break it");
            Assert.IsNotNull(kit.Zone(PlayerZoneKind.IceScreen));
            Assert.AreEqual(0, kit.Spells.Directs.Count);

            Assert.IsTrue(kit.Arts.OnEnemyTouchedScreen(kit.W, z.Id, walker, true));
            Assert.IsNull(kit.Zone(PlayerZoneKind.IceScreen));
            Assert.AreEqual(1, kit.Enemies.Ccs.Count);
            Assert.AreEqual(CcKind.Stagger, kit.Enemies.Ccs[0].Kind);
            Assert.AreEqual(90f, kit.Enemies.Ccs[0].Amount, Eps);
            Assert.AreEqual(CcSource.IceScreen, kit.Enemies.Ccs[0].Source);
            var ev = kit.LastEvent(SimEventType.PlayerZoneExpired);
            Assert.AreEqual(1, ev.B, "broken");
            // 파편: 벽 앞쪽(시전자 반대편) 폭 3 × 길이 2에 3T 냉기 1회.
            Assert.AreEqual(1, kit.Spells.Directs.Count);
            Assert.AreEqual(walker.Id, kit.Spells.Directs[0].TargetEnemyId);
            Assert.AreEqual(3f * T, kit.Spells.Directs[0].Amount, Eps);
            Assert.AreEqual(Ids.Element.Ice, kit.Spells.Directs[0].Element);
            Assert.AreEqual(0, kit.Spells.CountTo(behind.Id) + kit.Spells.CountTo(farFront.Id));
            Assert.IsFalse(kit.Arts.OnEnemyTouchedScreen(kit.W, z.Id, walker, true), "already gone");
        }

        [Test]
        public void S04_5_5__IceScreen_HeavyAndBossBreakWithoutStagger()
        {
            var kit = ScreenKit();
            var heavy = kit.AddEnemy(14.2f, 7f, tags: EnemyTags.Heavy);
            Assert.IsTrue(kit.Arts.OnEnemyTouchedScreen(kit.W, kit.Zone(PlayerZoneKind.IceScreen).Id, heavy, false));
            Assert.IsNull(kit.Zone(PlayerZoneKind.IceScreen));
            Assert.AreEqual(0, kit.Enemies.Ccs.Count);

            kit = ScreenKit();
            var boss = kit.AddEnemy(14.2f, 7f, tags: EnemyTags.Boss, boss: true);
            Assert.IsTrue(kit.Arts.OnEnemyTouchedScreen(kit.W, kit.Zone(PlayerZoneKind.IceScreen).Id, boss, true));
            Assert.AreEqual(0, kit.Enemies.Ccs.Count);
        }

        [Test]
        public void S04_5_5__IceScreen_ExpiryShardsAndRecastWithoutShards()
        {
            var kit = ScreenKit();
            var front = kit.AddEnemy(15f, 6f);
            kit.Stage5(238);
            Assert.IsNotNull(kit.Zone(PlayerZoneKind.IceScreen));
            Assert.AreEqual(0, kit.Spells.Directs.Count);
            kit.Stage5(2);
            Assert.IsNull(kit.Zone(PlayerZoneKind.IceScreen));
            Assert.AreEqual(3f * T, kit.Spells.TotalTo(front.Id), Eps);
            Assert.AreEqual(0, kit.LastEvent(SimEventType.PlayerZoneExpired).B, "expired, not broken");
            int id;
            Assert.IsFalse(kit.Arts.SegmentHitsScreen(kit.W, new Vector2(16f, 7f), new Vector2(12f, 7f), out id));

            // 다시 깔면 이전 것은 파편 없이 사라진다(Q-13 [임시]).
            kit = ScreenKit();
            front = kit.AddEnemy(15f, 6f);
            int firstId = kit.Zone(PlayerZoneKind.IceScreen).Id;
            kit.Now += 20000;
            kit.Seq("B, L+2304, K+2304");
            kit.Stage5();
            Assert.AreEqual(0, kit.Spells.Directs.Count);
            Assert.AreNotEqual(firstId, kit.Zone(PlayerZoneKind.IceScreen).Id);
            Assert.AreEqual(1, kit.W.PlayerZones.Count);
        }

        [Test]
        public void S04_5_6__ShadowHand_PullsTowardCenterDamagesAndStaggers()
        {
            var kit = new ArtKit(Ids.Char.C5, 5);
            var far = kit.AddEnemy(19f, 7f);          // 중심 (15, 7)에서 4칸 → 3칸 당김
            var close = kit.AddEnemy(16f, 7f);        // 1칸 → 1칸(중심을 넘지 않는다)
            var outside = kit.AddEnemy(15f, 12f);     // 5칸 > 반경 4.5
            kit.Seq("L, R+2304, K+2304");
            kit.Stage5();
            var pulse = kit.LastEvent(SimEventType.ArtPulse);
            Assert.AreEqual(15f, pulse.Pos.x, Eps);
            Assert.AreEqual(7f, pulse.Pos.y, Eps);
            Assert.AreEqual(4.5f, pulse.F, Eps);

            // TV-B30: 결정 5에서 2개짜리 → 남은 3개 → 그림자 계약 +9%.
            Assert.AreEqual(3f * T * 1.09f, kit.Spells.TotalTo(far.Id), Eps);
            Assert.AreEqual(3f * T * 1.09f, kit.Spells.TotalTo(close.Id), Eps);
            Assert.AreEqual(Ids.Element.Dark, kit.Spells.Directs[0].Element);
            Assert.AreEqual(0, kit.Spells.CountTo(outside.Id));
            CollectionAssert.Contains(kit.Traits.CrystalArgs, 3);

            float pullFar = 0f, pullClose = 0f;
            int staggers = 0;
            foreach (var c in kit.Enemies.Ccs)
            {
                Assert.AreEqual(CcSource.ShadowHand, c.Source);
                if (c.Kind == CcKind.Pull && c.EnemyId == far.Id) { pullFar = c.Amount; Assert.AreEqual(-1f, c.Dir.x, Eps); }
                if (c.Kind == CcKind.Pull && c.EnemyId == close.Id) pullClose = c.Amount;
                if (c.Kind == CcKind.Stagger) { staggers++; Assert.AreEqual(30f, c.Amount, Eps); }
            }
            Assert.AreEqual(3f, pullFar, Eps);
            Assert.AreEqual(1f, pullClose, Eps);
            Assert.AreEqual(2, staggers);
        }

        [Test]
        public void S04_5_6__ShadowHand_CenterIsClampedInsideTheWall()
        {
            var kit = new ArtKit(Ids.Char.C5, 5);
            kit.P.Pos = new Vector2(kit.W.ArenaWidth - 1f, 7f);
            kit.Seq("L, R+2304, K+2304");
            kit.Stage5();
            Assert.AreEqual(kit.W.ArenaWidth, kit.LastEvent(SimEventType.ArtPulse).Pos.x, Eps);
        }

        [Test]
        public void S04_7_4__OnCreditArtGetsNoShadowContractBonus()
        {
            // 외상 비기는 +0%(쓴 뒤 결정 0).
            var kit = new ArtKit(Ids.Char.C5, 1);
            var e = kit.AddEnemy(16f, 7f);
            kit.Seq("L, R+2304, K+2304");
            kit.Stage5();
            Assert.AreEqual(1, kit.P.Debt);
            Assert.AreEqual(3f * T, kit.Spells.TotalTo(e.Id), Eps);
        }

        [Test]
        public void S00_5_4__ClearZonesRemovesEverythingWithoutShards()
        {
            var kit = Cast(Ids.Char.C1, 5, "B, B+2304, K+2304");
            Assert.AreEqual(1920, kit.P.KilnAbsorbLeftHu);
            kit.Arts.ClearZones(kit.W);
            Assert.IsNull(kit.Zone(PlayerZoneKind.Kiln));
            Assert.AreEqual(0, kit.P.KilnAbsorbLeftHu);
            Assert.IsFalse(kit.P.InKilnZone);
            Assert.AreEqual(0, kit.Arts.AbsorbKilnHeat(kit.W, kit.P, 480));

            var ice = ScreenKit();
            var front = ice.AddEnemy(15f, 7f);
            ice.Arts.ClearZones(ice.W);
            Assert.IsNull(ice.Zone(PlayerZoneKind.IceScreen));
            Assert.AreEqual(0, ice.Spells.CountTo(front.Id));
        }

        [Test]
        public void S03_A_01__EveryTreeArtIsHeatExemptAndNeverCrit()
        {
            string[] inputs = { "K", "B, K+2304", "F, K+2304", "L, K+2304", "F, B+2304, K+2304", ArtKit.BloomInput };
            foreach (var input in inputs)
            {
                var kit = new ArtKit(Ids.Char.C1, 5);
                kit.Seq(input);
                kit.Stage5(70);
                Assert.Greater(kit.Spells.Executes.Count, 0, input);
                foreach (var r in kit.Spells.Executes)
                {
                    Assert.IsTrue(r.HeatExempt, input);
                    Assert.IsFalse(r.IsCrit, input);
                    Assert.Less(r.ShotId, 0, "art shot ids never collide with J shot ids");
                }
            }
        }

        [Test]
        public void S00_2_1__FactoryRegistersArtSystem()
        {
            var set = SimModuleSet.CreateDefault();
            Assert.IsInstanceOf<ArtSystem>(set.Arts);
        }

        [Test]
        public void S00_12__TwoRunsWithSameInputsMatch()
        {
            var a = new ArtKit(Ids.Char.C1, 5);
            var b = new ArtKit(Ids.Char.C1, 5);
            foreach (var kit in new[] { a, b })
            {
                kit.AddEnemy(14f, 7f);
                kit.Seq("B, B+2304, K+2304");
                kit.Stage5(100);
                kit.Seq("F, F+2304, K+500");
                kit.Stage5(20);
            }
            Assert.AreEqual(a.W.Events.Count, b.W.Events.Count);
            for (int i = 0; i < a.W.Events.Count; i++)
            {
                Assert.AreEqual(a.W.Events[i].Type, b.W.Events[i].Type);
                Assert.AreEqual(a.W.Events[i].Tick, b.W.Events[i].Tick);
                Assert.AreEqual(a.W.Events[i].Id, b.W.Events[i].Id);
            }
            Assert.AreEqual(a.Spells.Directs.Count, b.Spells.Directs.Count);
        }
    }
}
