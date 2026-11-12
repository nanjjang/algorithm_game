using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Xunit;

namespace Wizard.Sim.Tests.Heat
{
    // spec 01 14.5 과열(T-H01~T-H37). 규칙은 spec 01 7절, 틱 순서는 CONTRACTS 3.2(1단계 잠금 풀림, 6단계 발사, 11단계 냉각).
    // 마력탄 = 열 4(480 HU), 간격 9틱, 피해 10(14절 머리말). 블록 수치는 팩(data/sim/blocks.json)에서 읽는다.
    // 다섯 캐릭터의 과열 수치는 아직 팩에 없어(M5-DAT-01) HeatData 가 명세 7.8 표의 값으로 만든 팩을 쓴다.
    // "열 n"처럼 세계를 0에서 그 값까지 데려갈 수 없는 given(96, 97, 50 …)은 세계가 쓰는 같은 HeatMachine 을
    // 세계의 단계 순서대로 불러 시험하고, 세계로 만들 수 있는 부분은 세계로도 확인한다.
    public sealed class HeatVectorTests
    {
        private const int MagicBoltHu = 480;

        private static ContentDb Db => HeatData.Baseline;

        private static int BoltDamage => Db.Block(BlockId.MagicBolt).BaseDamage.FloorToInt();

        // 기준값 캐릭터의 HeatState(팩의 수치) + 마력탄 주문 + 주어진 열
        private static HeatState Machine(int heatHu, ContentDb? db = null, BlockId core = BlockId.MagicBolt)
        {
            ContentDb d = db ?? Db;
            HeatState s = HeatState.Initial(d.Character(CharacterId.Baseline));
            BlockDef b = d.Block(core);
            s.Shot = new HeatShotProfile { Armed = true, Core = core, HeatHu = b.HeatHu, IntervalTicks = b.IntervalTicks };
            s.HeatHu = heatHu;
            return s;
        }

        // 세계의 한 틱과 같은 순서: 1단계 잠금 풀림 → 6단계 발사 → 11단계 냉각
        private static HeatFire MachineTick(ref HeatState s, uint t, bool held, ref int shotId)
        {
            Pcg32 rng = RngStreams.Derive(1UL, RngStream.Mutation, 0);
            HeatMachine.ProcessUnlock(ref s, t);
            HeatMachine.TryFire(ref s, t, held, false, true, false, true, ref rng, ref shotId, out HeatFire f);
            HeatMachine.Cool(ref s, t);
            return f;
        }

        // 꾹 누르기 1주기: 틱 0부터 J 를 누르고 잠길 때까지. 주기 = UnlockTick(풀리는 틱에 다음 주기의 첫 발이 나간다, T-H02)
        private static (List<SimEvent> Shots, uint Period) HoldOneCycle(ContentDb db)
        {
            HeatRig rig = HeatRig.Endless(db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
                Assert.True(rig.Tick < 2000);
            }
            return (rig.Shots(), rig.H.UnlockTick);
        }

        private static int CycleDamage(List<SimEvent> shots)
        {
            int sum = 0;
            foreach (SimEvent e in shots)
            {
                sum += HeatRig.Damage(e, BoltDamage);
            }
            return sum;
        }

        private static int CountCrit(List<SimEvent> shots) => shots.FindAll(e => e.C == 1).Count;

        private static double Dps(int damage, long ticks) => damage * (double)SimVersion.TickRate / ticks;

        // ── 꾹 누르기(7.3, 7.5, GDD 18)

        [Fact]
        public void S01_T_H01__HoldFromZeroLocksOn26thShotAt104()
        {
            HeatRig rig = HeatRig.Endless(Db);
            for (uint t = 0; t <= 225; t++)
            {
                rig.Step(true);
            }
            List<SimEvent> shots = rig.Shots();
            Assert.Equal(26, shots.Count);
            for (int k = 1; k <= 26; k++)
            {
                SimEvent e = shots[k - 1];
                Assert.Equal((uint)(9 * (k - 1)), e.Tick);                       // 발사 틱 0, 9, 18, …, 225
                Assert.Equal(MagicBoltHu * k, e.V);                              // k번째 발사 뒤 열 4k
                Assert.Equal(k >= 22 ? 1 : 0, e.C);                              // 1~21발 일반, 22발(88)~26발 임계
                Assert.Equal(k, e.A);                                            // shotId 1부터
                Assert.Equal((int)BlockId.MagicBolt, e.B);
            }
            Assert.Equal(5, rig.Log.Count(SimEventType.CritShotFired));
            SimEvent locked = rig.Log.Single(SimEventType.HeatLockEntered);
            Assert.Equal(225U, locked.Tick);                                     // 26발째(104)에 잠금
            Assert.Equal(414, locked.A);                                         // UnlockTick = 225 + 9 + 180
            Assert.Equal(9 + 180, locked.B);
            Assert.True(rig.H.Locked);
            Assert.Equal(414U, rig.H.UnlockTick);
            Assert.Equal(12480, rig.H.HeatHu);
        }

        [Fact]
        public void S01_T_H02__UnlockAt414ThenFireSameTick_Period414Dps4493()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.RunTo(0);
            while (rig.Tick <= 414)
            {
                rig.Step(true);
            }
            SimEvent unlocked = rig.Log.Single(SimEventType.HeatUnlocked);
            Assert.Equal(414U, unlocked.Tick);
            Assert.Equal((int)HeatUnlockReason.Timeout, unlocked.A);
            List<SimEvent> shots = rig.Shots();
            Assert.Equal(27, shots.Count);
            SimEvent next = shots[26];
            Assert.Equal(414U, next.Tick);                                       // 같은 틱에 다음 발사
            Assert.Equal(MagicBoltHu, next.V);                                   // 열 0 → 4
            Assert.True(rig.Log.Events.IndexOf(unlocked) < rig.Log.Events.IndexOf(next));

            List<SimEvent> cycle = shots.GetRange(0, 26);
            Assert.Equal(310, CycleDamage(cycle));                               // 21 × 10 + 5 × 20
            double dps = Dps(310, 414);
            Assert.Equal(44.93, Math.Round(dps, 2));
            Assert.InRange(dps, 44.9 * 0.99, 44.9 * 1.01);                       // 수락: 기준값 44.9 ± 1%
        }

        [Fact]
        public void S01_T_H03__Heat96PlusBoltIsExactly100_CritNoLock()
        {
            int id = 0;
            HeatState s = Machine(11520);
            HeatFire f = MachineTick(ref s, 0, true, ref id);
            Assert.Equal(HeatFireKind.Fired, f.Kind);
            Assert.Equal(12000, s.HeatHu);
            Assert.True(f.IsCrit);
            Assert.False(f.LockEntered);
            Assert.False(s.Locked);                                              // 정확히 100은 잠금이 아니다(">100")

            // 세계: 꾹 누르기의 25발째가 같은 경계(96 → 100)다
            HeatRig rig = HeatRig.Endless(Db);
            rig.RunTo(0);
            for (uint t = 0; t <= 216; t++)
            {
                rig.Step(true);
            }
            SimEvent e25 = rig.Shots()[24];
            Assert.Equal((12000, 1), ((int)e25.V, e25.C));
            Assert.False(rig.H.Locked);
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatLockEntered));
        }

        [Fact]
        public void S01_T_H04__Heat97PlusBoltIs101_CritAndLock()
        {
            int id = 0;
            HeatState s = Machine(11640);
            HeatFire f = MachineTick(ref s, 50, true, ref id);
            Assert.Equal(12120, s.HeatHu);
            Assert.True(f.IsCrit);
            Assert.True(f.LockEntered);
            Assert.True(s.Locked);
            Assert.Equal(50U + 9U + 180U, s.UnlockTick);
        }

        [Fact]
        public void S01_T_H05__HoldDalgu_7Crits_330Damage_414Ticks_4783Dps()
        {
            (List<SimEvent> shots, uint period) = HoldOneCycle(HeatData.Dalgu);
            Assert.Equal(26, shots.Count);
            Assert.Equal(7, CountCrit(shots));
            Assert.Equal(1, shots[19].C);                                        // 20발째(80)부터
            Assert.Equal(0, shots[18].C);
            Assert.Equal(330, CycleDamage(shots));
            Assert.Equal(414U, period);
            Assert.Equal(47.83, Math.Round(Dps(330, period), 2));
        }

        [Fact]
        public void S01_T_H06__HoldChorong_5Crits_310Damage_384Ticks_4844Dps()
        {
            (List<SimEvent> shots, uint period) = HoldOneCycle(HeatData.Chorong);
            Assert.Equal(26, shots.Count);
            Assert.Equal(5, CountCrit(shots));
            Assert.Equal(310, CycleDamage(shots));
            Assert.Equal(225U + 9U + 150U, period);
            Assert.Equal(48.44, Math.Round(Dps(310, period), 2));
        }

        [Fact]
        public void S01_T_H07__HoldGalpi_310Damage_444Ticks_4189Dps()
        {
            (List<SimEvent> shots, uint period) = HoldOneCycle(HeatData.Galpi);
            Assert.Equal(310, CycleDamage(shots));
            Assert.Equal(444U, period);
            Assert.Equal(41.89, Math.Round(Dps(310, period), 2));
        }

        [Fact]
        public void S01_T_H08__HoldSeonge_4Crits_300Damage_414Ticks_4348Dps()
        {
            (List<SimEvent> shots, uint period) = HoldOneCycle(HeatData.Seonge);
            Assert.Equal(26, shots.Count);
            Assert.Equal(4, CountCrit(shots));
            Assert.Equal(1, shots[22].C);                                        // 23발째(92)부터
            Assert.Equal(0, shots[21].C);
            Assert.Equal(300, CycleDamage(shots));
            Assert.Equal(414U, period);
            Assert.Equal(43.48, Math.Round(Dps(300, period), 2));
        }

        // ── 임계 구간과 냉각(7.3, 7.4)

        [Fact]
        public void S01_T_H09__From84FourCritShotsThenCoolFromTick48()
        {
            int id = 0;
            HeatState s = Machine(10080);
            var heats = new List<int>();
            for (uint t = 0; t <= 27; t++)
            {
                HeatFire f = MachineTick(ref s, t, true, ref id);
                if (f.Kind == HeatFireKind.Fired)
                {
                    Assert.True(f.IsCrit);
                    heats.Add(f.HeatAfterHu);
                }
            }
            Assert.Equal(new[] { 10560, 11040, 11520, 12000 }, heats.ToArray());  // 88, 92, 96, 100
            Assert.False(s.Locked);
            for (uint t = 28; t <= 47; t++)
            {
                MachineTick(ref s, t, false, ref id);
            }
            Assert.Equal(12000, s.HeatHu);                                       // 틱 47까지 냉각 없음
            MachineTick(ref s, 48, false, ref id);
            Assert.Equal(12000 - 90, s.HeatHu);                                  // 틱 48의 끝부터(27 + 9 + 12)
            for (uint t = 49; t <= 69; t++)
            {
                MachineTick(ref s, t, false, ref id);
            }
            Assert.Equal(10020, s.HeatHu);                                       // 틱 69의 끝에 83.5
            HeatFire again = MachineTick(ref s, 70, true, ref id);
            Assert.Equal(10500, again.HeatAfterHu);                              // 87.5
            Assert.True(again.IsCrit);

            // 세계: 꾹 누르기의 21발째(틱 180)가 열 84다. 같은 장면을 189틱 늦게 본다
            HeatRig rig = HeatRig.Endless(Db);
            for (uint t = 0; t <= 216; t++)
            {
                rig.Step(true);                                                  // 발사 189, 198, 207, 216 → 88, 92, 96, 100
            }
            Assert.Equal(new[] { 10560, 11040, 11520, 12000 },
                         rig.Shots().GetRange(21, 4).ConvertAll(e => (int)e.V).ToArray());
            while (rig.Tick <= 236)
            {
                rig.Step(false);
            }
            Assert.Equal(12000, rig.H.HeatHu);
            rig.Step(false);                                                     // 틱 237 = 216 + 9 + 12
            Assert.Equal(12000 - 90, rig.H.HeatHu);
            rig.RunTo(259);
            Assert.Equal(10020, rig.H.HeatHu);
            rig.Step(true);
            SimEvent last = rig.Shots()[25];
            Assert.Equal((259U, 10500L, 1), (last.Tick, last.V, last.C));
        }

        // 임계 유지 정책: 다음 발사가 100을 넘기지 않는 동안 쏘고(구간 끝까지), 열이 100 − 4n 이하로 식으면 다시 쏜다.
        // 열 0에서 시작해 임계 구간에 들어가는 첫 발(꾹 누르기와 같은 앞부분)부터 6,000틱을 잰다
        private static (double Dps, int Shots, int CritShots, int Locks) CritMaintain(ContentDb db)
        {
            HeatRig rig = HeatRig.Endless(db);
            int critStart = rig.World.HeatCritStartHu(0);
            int n = HeatRules.CritShotCount(MagicBoltHu, critStart);
            int resumeAt = HeatRules.MaxHeatHu - MagicBoltHu * n;
            bool firing = true;
            uint? start = null;
            while (start == null || rig.Tick < start.Value + 6000U)
            {
                int heat = rig.H.HeatHu;
                if (firing && heat + MagicBoltHu > HeatRules.MaxHeatHu)
                {
                    firing = false;
                }
                else if (!firing && heat <= resumeAt)
                {
                    firing = true;
                }
                int before = rig.Log.Events.Count;
                rig.Step(firing);
                if (start == null)
                {
                    for (int k = before; k < rig.Log.Events.Count; k++)
                    {
                        if (rig.Log.Events[k].Type == SimEventType.CritShotFired)
                        {
                            start = rig.Log.Events[k].Tick;
                        }
                    }
                }
                Assert.True(rig.Tick < 20000);
            }
            List<SimEvent> window = rig.Shots().FindAll(e => e.Tick >= start!.Value && e.Tick < start.Value + 6000U);
            int damage = 0;
            foreach (SimEvent e in window)
            {
                damage += HeatRig.Damage(e, BoltDamage);
            }
            return (Dps(damage, 6000), window.Count, CountCrit(window), rig.Log.Count(SimEventType.HeatLockEntered));
        }

        [Fact]
        public void S01_T_H10__CritMaintainFor6000Ticks_NoLock_AllCrit_DpsWithin1Percent()
        {
            var cases = new (ContentDb Db, double Tick, double Continuous)[]
            {
                (HeatData.Baseline, 69.6, 69.2),
                (HeatData.Dalgu, 73.8, 73.5),
                (HeatData.Chorong, 71.8, 71.4),
                (HeatData.Seonge, 65.6, 65.5),
            };
            foreach ((ContentDb db, double tick, double continuous) in cases)
            {
                (double dps, int shots, int crits, int locks) = CritMaintain(db);
                Assert.Equal(0, locks);
                Assert.True(shots > 0);
                Assert.Equal(shots, crits);                                      // 모든 발사 임계
                Assert.Equal(tick, Math.Round(dps, 1));                          // 틱 단위 기대값
                Assert.InRange(dps, continuous * 0.99, continuous * 1.01);       // 연속식과 ±1%
            }
        }

        [Fact]
        public void S01_T_H11__CritMaintainOverHoldRatioIsAtLeast1_4()
        {
            double maintain = CritMaintain(HeatData.Baseline).Dps;
            (List<SimEvent> shots, uint period) = HoldOneCycle(HeatData.Baseline);
            double hold = Dps(CycleDamage(shots), period);
            Assert.InRange(maintain, 69.2 * 0.99, 69.2 * 1.01);                  // 수락: 임계 유지 69.2 ± 1%
            Assert.InRange(hold, 44.9 * 0.99, 44.9 * 1.01);                      // 수락: 꾹 누르기 44.9 ± 1%
            Assert.True(maintain / hold >= 1.4, "ratio " + (maintain / hold));   // 원문 1.54
        }

        [Fact]
        public void S01_T_H12__CoolStartsAtEndOfTick21AfterBoltAtTick0()
        {
            HeatState s = Machine(6000);
            s.HasFired = true;
            s.LastFireTick = 0;
            s.LastFireIntervalTicks = 9;
            for (uint t = 0; t <= 20; t++)
            {
                HeatMachine.Cool(ref s, t);
                Assert.Equal(6000, s.HeatHu);                                    // 틱 0~20 냉각 없음
            }
            HeatMachine.Cool(ref s, 21);
            Assert.Equal(5910, s.HeatHu);                                        // 49.25 (틱당 0.75)
        }

        [Fact]
        public void S01_T_H13__ChorongCools100HuPerTick_60TicksIsExactly50()
        {
            HeatState s = Machine(6000, HeatData.Chorong);
            Assert.Equal(100, HeatRules.CoolPerTickHu(s.CoolHuPerTick, s.Mods.CoolPermille));
            for (uint t = 0; t < 60; t++)
            {
                HeatMachine.Cool(ref s, t);
            }
            Assert.Equal(0, s.HeatHu);
        }

        [Fact]
        public void S01_T_H14__LockedHeat104DoesNotCool()
        {
            HeatState s = Machine(12480);
            HeatMachine.EnterLock(ref s, 0, 9, 180);
            for (uint t = 1; t < 189; t++)
            {
                HeatMachine.Cool(ref s, t);
            }
            Assert.Equal(12480, s.HeatHu);

            HeatRig rig = HeatRig.Endless(Db);                                   // 세계: T-H01의 잠금 동안 열이 그대로다
            rig.RunTo(0);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            while (rig.Tick < 414)
            {
                rig.Step(false);
                Assert.Equal(12480, rig.H.HeatHu);
            }
        }

        [Fact]
        public void S01_T_H15__PressJWhileLockedFiresNothing_OneDryEvent()
        {
            HeatRig rig = HeatRig.Endless(Db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            rig.Step(true);                                                      // 잠금 중 계속 누르고 있는 것은 "누름"이 아니다
            rig.Step(false);
            int shots = rig.Shots().Count;
            rig.Log.Clear();
            rig.RunTo(300);
            rig.Step(true);                                                      // 틱 300에 누름
            Assert.Empty(rig.Shots());
            SimEvent dry = rig.Log.Single(SimEventType.FireDryWhileLocked);
            Assert.Equal(300U, dry.Tick);
            rig.Step(true);                                                      // 계속 누름: 더 내지 않는다
            Assert.Equal(1, rig.Log.Count(SimEventType.FireDryWhileLocked));
            Assert.Equal(26, shots);
        }

        [Fact]
        public void S01_T_H16__ArtKeyWhileLockedLeavesLockAsIs()
        {
            // 마력 방출(비기 ①)의 판정과 효과는 비기 모듈(M5)의 것이다. 과열 쪽 규칙은 "비기는 열을 올리지 않고 잠금을 풀지 않는다"(GDD 8):
            // 잠금 중에 K 와 방향 입력이 들어와도 과열 상태는 그대로다
            HeatRig rig = HeatRig.Endless(Db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            rig.Step(false);
            HeatState before = rig.H;
            var inputs = new[]
            {
                new PlayerInput(new PlayerSlot(0), new InputEvent(rig.Tick, 0, LogicalKey.Down, true, 1000)),
                new PlayerInput(new PlayerSlot(0), new InputEvent(rig.Tick, 40, LogicalKey.Skill, true, 1001)),
            };
            rig.World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, rig.Log);
            Assert.True(rig.H.Locked);
            Assert.Equal(before.UnlockTick, rig.H.UnlockTick);
            Assert.Equal(before.HeatHu, rig.H.HeatHu);
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatUnlocked));
        }

        [Fact]
        public void S01_T_H17__VentBlastWhileLockedUsesHeat100_UnlocksToZero()
        {
            HeatRig rig = HeatRig.Endless(Db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            Assert.Equal(12480, rig.H.HeatHu);
            int heat = rig.World.HeatConsumeForVentBlast(new PlayerSlot(0), rig.Log);
            Assert.Equal(100, heat);
            Assert.False(rig.H.Locked);
            Assert.Equal(0, rig.H.HeatHu);
            SimEvent e = rig.Log.Single(SimEventType.HeatUnlocked);
            Assert.Equal((int)HeatUnlockReason.VentBlast, e.A);
        }

        [Fact]
        public void S01_T_H18__VentBlastAtHeat60UsesHeat60_HeatZero()
        {
            HeatRig rig = HeatRig.Endless(Db);
            for (uint t = 0; t <= 126; t++)
            {
                rig.Step(true);                                                  // 15발 → 60
            }
            rig.Step(false);
            Assert.Equal(7200, rig.H.HeatHu);
            int heat = rig.World.HeatConsumeForVentBlast(new PlayerSlot(0), rig.Log);
            Assert.Equal(60, heat);                                              // T × (60 ÷ 25) = T × 2.4
            Assert.Equal(0, rig.H.HeatHu);
            Assert.False(rig.H.Locked);
            Assert.Equal(0, rig.Log.Count(SimEventType.HeatUnlocked));          // 잠겨 있지 않았다
        }

        // ── 발사 1회의 열(7.2). 분열 배수는 spec 02 4.2 표(2갈래 1.6, 3갈래 2.2), 거울상은 GDD 9(+50%)

        private const int Split2Permille = 1600;
        private const int Split3Permille = 2200;
        private const int MirrorPermille = 1500;

        private static int BlockHu(BlockId id) => Db.Block(id).HeatHu;

        private static int Heat(int mirror, params ShotHeatBlock[] blocks) =>
            ShotHeat.ShotHeatHu(blocks, ShotHeat.NoUnderline, 0, mirror);

        [Fact]
        public void S01_T_H19__BoltPlusSplit3Is8_8()
        {
            Assert.Equal(1056, Heat(ShotHeat.NoMirrorPermille,
                new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0), new ShotHeatBlock(0, 1, 0, Split3Permille)));
        }

        [Fact]
        public void S01_T_H20__BoltPlusSplit2Is6_4()
        {
            Assert.Equal(768, Heat(ShotHeat.NoMirrorPermille,
                new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0), new ShotHeatBlock(0, 1, 0, Split2Permille)));
        }

        [Fact]
        public void S01_T_H21__SecondSplitInSameScopeIsIgnored()
        {
            Assert.Equal(1056, Heat(ShotHeat.NoMirrorPermille,
                new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0),
                new ShotHeatBlock(0, 1, 0, Split3Permille),
                new ShotHeatBlock(0, 2, 0, Split3Permille)));
            Assert.Equal(1056, Heat(ShotHeat.NoMirrorPermille,                   // 첫 번째가 3갈래면 뒤의 2갈래도 무시
                new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0),
                new ShotHeatBlock(0, 1, 0, Split3Permille),
                new ShotHeatBlock(0, 2, 0, Split2Permille)));
        }

        [Fact]
        public void S01_T_H22__BoltWithMirrorIs6()
        {
            Assert.Equal(720, Heat(MirrorPermille, new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0)));
        }

        [Fact]
        public void S01_T_H23__BoltSplit3MirrorIs13_2()
        {
            Assert.Equal(1584, Heat(MirrorPermille,
                new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0), new ShotHeatBlock(0, 1, 0, Split3Permille)));
        }

        [Fact]
        public void S01_T_H24__TriggerScopeHeatCountsAtFire_22()
        {
            // 코어 마력탄 + 적중 시 발동(열 0) + (트리거 스코프) 거대 화염구 = 4 + 0 + 18. 적중 여부와 무관
            Assert.Equal(22 * HeatRules.HuPerHeat, Heat(ShotHeat.NoMirrorPermille,
                new ShotHeatBlock(0, 0, BlockHu(BlockId.MagicBolt), 0),
                new ShotHeatBlock(0, 1, 0, 0),
                new ShotHeatBlock(1, 2, BlockHu(BlockId.Fireball), 0)));
        }

        // ── 변이(7.6.3~7.6.5). 변이 수치는 변이 데이터(M5-CHR-05)가 넣는다. 여기서는 GDD 9의 값을 SetHeatModifiers 로 넣는다

        private static HeatModifiers Hypersensitive => new HeatModifiers
        {
            CritStartCapHu = 70 * HeatRules.HuPerHeat, ExtraLockTicks = 90, CoolPermille = 1000,
        };

        private static HeatModifiers Gluttony => new HeatModifiers
        {
            CoolPermille = 500, KillHeatHu = 4 * HeatRules.HuPerHeat, KillWindowCapHu = 20 * HeatRules.HuPerHeat,
        };

        private static HeatModifiers Unstable => new HeatModifiers
        {
            CoolPermille = 1000, UnstableChancePermille = 50, UnstableLockTicks = 120,
        };

        [Fact]
        public void S01_T_H25__HypersensitiveCritFrom70_Lock270PlusInterval()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.World.SetHeatModifiers(new PlayerSlot(0), Hypersensitive);
            Assert.Equal(8400, rig.World.HeatCritStartHu(0));
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            List<SimEvent> shots = rig.Shots();
            Assert.Equal(26, shots.Count);
            Assert.Equal(0, shots[16].C);                                        // 17발째 68
            Assert.Equal(1, shots[17].C);                                        // 18발째 72 ≥ 70
            Assert.Equal(225U + 9U + 180U + 90U, rig.H.UnlockTick);             // 잠금 3초 + 1.5초 = 270틱(+간격)
            Assert.Equal(9 + 270, rig.World.HeatLockTotalTicks(0));
        }

        [Fact]
        public void S01_T_H26__HypersensitiveWinsOverDalguAndSeonge()
        {
            foreach (ContentDb db in new[] { HeatData.Dalgu, HeatData.Seonge })
            {
                HeatRig rig = HeatRig.Endless(db);
                rig.World.SetHeatModifiers(new PlayerSlot(0), Hypersensitive);
                Assert.Equal(8400, rig.World.HeatCritStartHu(0));                // 가장 낮은 값 70
            }
            Assert.Equal(8400, HeatRules.CritStartHu(9600, 8400));
            Assert.Equal(8400, HeatRules.CritStartHu(10800, 8400));
            Assert.Equal(9600, HeatRules.CritStartHu(9600, 0));                  // 효과 없음
        }

        [Fact]
        public void S01_T_H27__GluttonyCools45HuPerTick()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.World.SetHeatModifiers(new PlayerSlot(0), Gluttony);
            for (uint t = 0; t <= 126; t++)
            {
                rig.Step(true);                                                  // 15발 → 7,200, 마지막 발사 126
            }
            rig.Step(false);
            rig.RunTo(147);                                                      // 126 + 9 + 12
            Assert.Equal(7200, rig.H.HeatHu);
            rig.Step(false);
            Assert.Equal(7200 - 45, rig.H.HeatHu);                               // 초당 22.5
            rig.Step(false);
            Assert.Equal(7200 - 90, rig.H.HeatHu);
        }

        [Fact]
        public void S01_T_H28__GluttonyThreeKillsInOneTickFrom50Is38()
        {
            HeatState s = Machine(6000);
            s.Mods = Gluttony;
            var hu = new int[SimVersion.TickRate];
            var ticks = new uint[SimVersion.TickRate];
            for (int k = 0; k < 3; k++)
            {
                HeatMachine.OnKill(ref s, 500, hu, ticks);
            }
            Assert.Equal(4560, s.HeatHu);                                        // 38 (−12)

            HeatRig rig = HeatRig.Endless(Db);                                   // 세계: 같은 연산이 슬롯의 창으로 돈다
            rig.World.SetHeatModifiers(new PlayerSlot(0), Gluttony);
            for (uint t = 0; t <= 126; t++)
            {
                rig.Step(true);                                                  // 60
            }
            rig.Step(false);
            for (int k = 0; k < 3; k++)
            {
                Assert.Equal(480, rig.World.HeatOnKill(new PlayerSlot(0)));
            }
            Assert.Equal(7200 - 1440, rig.H.HeatHu);
        }

        [Fact]
        public void S01_T_H29__GluttonyCapsAt20PerSecond_ResumesAfter60Ticks()
        {
            HeatState s = Machine(9600);
            s.Mods = Gluttony;
            var hu = new int[SimVersion.TickRate];
            var ticks = new uint[SimVersion.TickRate];
            for (int k = 0; k < 7; k++)
            {
                HeatMachine.OnKill(ref s, (uint)(100 + 5 * k), hu, ticks);       // 1초 안에 7마리
            }
            Assert.Equal(7200, s.HeatHu);                                        // 60 (−20에서 멈춤)
            Assert.Equal(0, HeatMachine.OnKill(ref s, 159, hu, ticks));          // 첫 처치 뒤 59틱: 아직 창 안
            Assert.Equal(7200, s.HeatHu);
            Assert.Equal(480, HeatMachine.OnKill(ref s, 160, hu, ticks));        // 60틱이 지나면 다시 깎인다
            Assert.Equal(6720, s.HeatHu);
        }

        // Mutation 흐름(세계 생성 때 wave 0으로 파생, SimWorld.Rng.cs)의 첫 뽑기가 "5% 안"인 씨앗
        private static ulong SeedWhoseFirstMutationDrawIs(bool inside)
        {
            for (ulong seed = 1; seed < 100000; seed++)
            {
                Pcg32 rng = RngStreams.Derive(seed, RngStream.Mutation, 0);
                if (rng.Chance(50, 1000) == inside)
                {
                    return seed;
                }
            }
            throw new InvalidOperationException("no seed");
        }

        private sealed class FireProbe : ISimEventSink
        {
            public SimWorld? World;
            public readonly List<HeatFire> Fires = new List<HeatFire>();
            public readonly EventLog Log = new EventLog();

            public void Emit(in SimEvent e)
            {
                Log.Emit(in e);
                if (e.Type == SimEventType.ShotFired && World!.HeatFiredThisTick(e.Slot.Value, out HeatFire f))
                {
                    Fires.Add(f);
                }
            }
        }

        [Fact]
        public void S01_T_H30__UnstableCoreBlowupLocksInterval36Plus120()
        {
            ulong seed = SeedWhoseFirstMutationDrawIs(true);
            HeatRig rig = HeatRig.Endless(Db, BlockId.Fireball, seed);
            rig.World.SetHeatModifiers(new PlayerSlot(0), Unstable);
            var probe = new FireProbe { World = rig.World };
            var inputs = new[] { new PlayerInput(new PlayerSlot(0), new InputEvent(0, 0, LogicalKey.Fire, true, 1)) };
            rig.World.Step(inputs, ReadOnlySpan<RunCommand>.Empty, probe);
            SimEvent boom = probe.Log.Single(SimEventType.UnstableCoreExploded);
            Assert.Equal(0U, boom.Tick);
            Assert.True(rig.H.Locked);
            Assert.Equal(36U + 120U, rig.H.UnlockTick);                          // 잠금(간격 36 + 120틱)
            Assert.Equal(36 + 120, rig.World.HeatLockTotalTicks(0));
            Assert.Equal(0, probe.Log.Count(SimEventType.HeatLockEntered));      // 과열 잠금이 아니다(끓어넘침 없음, 7.6.6 [임시])
            HeatFire f = Assert.Single(probe.Fires);
            Assert.True(f.UnstableBlowup);                                       // 콤보 0은 Combo 모듈이 이 값으로 한다(9.4)
            Assert.Equal(2160, rig.H.HeatHu);

            ulong calm = SeedWhoseFirstMutationDrawIs(false);
            HeatRig other = HeatRig.Endless(Db, BlockId.Fireball, calm);
            other.World.SetHeatModifiers(new PlayerSlot(0), Unstable);
            other.Step(true);
            Assert.Equal(0, other.Log.Count(SimEventType.UnstableCoreExploded));
            Assert.False(other.H.Locked);
        }

        [Fact]
        public void S01_T_H31__DuelUnstableCore1000ShotsNoBlowup_NoRngDraw()
        {
            HeatRig rig = HeatRig.Duel(Db, BlockId.Fireball, 7UL);
            rig.World.SetHeatModifiers(new PlayerSlot(0), Unstable);
            rig.World.SetHeatModifiers(new PlayerSlot(1), Unstable);
            Pcg32 rngBefore = rig.World.HeldRng(RngStream.Mutation);
            while (rig.Shots(0).Count < 1000 || rig.Shots(1).Count < 1000)
            {
                rig.StepAll(true, true);
                Assert.True(rig.Tick < 200000);
            }
            Assert.Equal(0, rig.Log.Count(SimEventType.UnstableCoreExploded));
            Pcg32 rngAfter = rig.World.HeldRng(RngStream.Mutation);
            Assert.Equal((rngBefore.State, rngBefore.Inc), (rngAfter.State, rngAfter.Inc));

            // 같은 발사를 무한 웨이브에서 하면 자폭이 난다(스위치만의 차이)
            HeatRig endless = HeatRig.Endless(Db, BlockId.Fireball, 7UL);
            endless.World.SetHeatModifiers(new PlayerSlot(0), Unstable);
            while (endless.Shots().Count < 1000)
            {
                endless.Step(true);
            }
            Assert.InRange(endless.Log.Count(SimEventType.UnstableCoreExploded), 20, 90);
        }

        // ── 파생 값(7.7)

        [Fact]
        public void S01_T_H32__CritShotCountForBoltAt85_80_90()
        {
            Assert.Equal(4, HeatRules.CritShotCount(MagicBoltHu, HeatData.Baseline.Character(CharacterId.Baseline).CritStartHu));
            Assert.Equal(6, HeatRules.CritShotCount(MagicBoltHu, HeatData.Dalgu.Character(CharacterId.Baseline).CritStartHu));
            Assert.Equal(3, HeatRules.CritShotCount(MagicBoltHu, HeatData.Seonge.Character(CharacterId.Baseline).CritStartHu));
        }

        [Fact]
        public void S01_T_H33__CritShotCountForHeat22At85Is1()
        {
            Assert.Equal(1, HeatRules.CritShotCount(22 * HeatRules.HuPerHeat, 10200));
        }

        [Fact]
        public void S01_T_H34__CritShotCountForHeat101IsZero()
        {
            Assert.Equal(0, HeatRules.CritShotCount(101 * HeatRules.HuPerHeat, 10200));
            Assert.Equal(HeatRules.CritShotCountUnlimited, HeatRules.CritShotCount(0, 10200));   // 발사당 열 0 → ∞
        }

        [Fact]
        public void S01_T_H35__Heat81IsImminentAtCrit85()
        {
            Assert.Equal(HeatRingState.Imminent, HeatRules.Ring(81 * HeatRules.HuPerHeat, 10200, false));
            Assert.Equal(HeatRingState.Imminent, HeatRules.Ring(70 * HeatRules.HuPerHeat, 10200, false));   // 70 ≤ 열
            Assert.Equal(HeatRingState.Rising, HeatRules.Ring(70 * HeatRules.HuPerHeat - 1, 10200, false));
        }

        [Fact]
        public void S01_T_H36__RingStatesAt69_85_95_0()
        {
            Assert.Equal(HeatRingState.Rising, HeatRules.Ring(69 * HeatRules.HuPerHeat, 10200, false));
            Assert.Equal(HeatRingState.Critical, HeatRules.Ring(85 * HeatRules.HuPerHeat, 10200, false));
            Assert.Equal(HeatRingState.Brink, HeatRules.Ring(95 * HeatRules.HuPerHeat, 10200, false));
            Assert.Equal(HeatRingState.Cold, HeatRules.Ring(0, 10200, false));
            Assert.Equal(HeatRingState.Brink, HeatRules.Ring(100 * HeatRules.HuPerHeat, 10200, false));   // 95 ≤ 열 ≤ 100
            Assert.Equal(HeatRingState.Critical, HeatRules.Ring(95 * HeatRules.HuPerHeat - 1, 10200, false));
            Assert.Equal(HeatRingState.Locked, HeatRules.Ring(104 * HeatRules.HuPerHeat, 10200, true));
        }

        [Fact]
        public void S01_T_H37__DalguHeat81IsCritical()
        {
            int dalguCrit = HeatData.Dalgu.Character(CharacterId.Baseline).CritStartHu;
            Assert.Equal(9600, dalguCrit);
            Assert.Equal(HeatRingState.Critical, HeatRules.Ring(81 * HeatRules.HuPerHeat, dalguCrit, false));
        }
    }
}
