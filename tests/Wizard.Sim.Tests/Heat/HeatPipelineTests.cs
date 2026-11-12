using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Wizard.Sim.Tests.Runtime;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Heat
{
    // Heat 절의 결정론(두 번 실행, 스냅숏, 직렬화 왕복, 할당 0), 표시 이벤트가 정해진 때 한 번씩만 나는지,
    // 다른 모듈이 부르는 연산(스침 8.4, 불가마 7.6.2, 초기화), 스위치(FireEnabled), 빈 팩에서 모듈이 꺼지는 것.
    public sealed class HeatPipelineTests
    {
        private static ContentDb Db => HeatData.Baseline;

        private static readonly PlayerSlot P0 = new PlayerSlot(0);

        // ── 표시 이벤트

        [Fact]
        public void RingEntryEventsFireOncePerClimb()
        {
            HeatRig rig = HeatRig.Endless(Db);
            while (rig.Tick <= 414 + 225)
            {
                rig.Step(true);                                                  // 두 주기
            }
            List<SimEvent> imminent = rig.Log.Of(SimEventType.HeatEnteredImminent);
            List<SimEvent> critical = rig.Log.Of(SimEventType.HeatEnteredCritical);
            List<SimEvent> brink = rig.Log.Of(SimEventType.HeatEnteredBrink);
            Assert.Equal(new uint[] { 153, 414 + 153 }, imminent.ConvertAll(e => e.Tick).ToArray());   // 18발째 72 ≥ 85 − 15
            Assert.Equal(new uint[] { 189, 414 + 189 }, critical.ConvertAll(e => e.Tick).ToArray());   // 22발째 88
            Assert.Equal(new uint[] { 207, 414 + 207 }, brink.ConvertAll(e => e.Tick).ToArray());      // 24발째 96
            Assert.Equal(2, rig.Log.Count(SimEventType.HeatLockEntered));
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatUnlocked));
            Assert.Equal(HeatRingState.Locked, rig.World.HeatRingOf(0));
        }

        [Fact]
        public void ShotEventsComeInSpecOrder()
        {
            HeatRig rig = HeatRig.Endless(Db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            int shot = rig.Log.Events.FindLastIndex(e => e.Type == SimEventType.ShotFired);
            Assert.Equal(SimEventType.CritShotFired, rig.Log.Events[shot + 1].Type);
            Assert.Equal(SimEventType.HeatLockEntered, rig.Log.Events[shot + 2].Type);
            Assert.Equal(rig.Log.Events[shot].A, rig.Log.Events[shot + 1].A);  // 같은 shotId
            Assert.Equal(26, rig.World.HeatLastShotId);
        }

        // ── 다른 모듈의 연산

        [Fact]
        public void Graze_WhileLocked_BreaksLockToCritStart_AndArmsExempt()
        {
            HeatRig rig = HeatRig.Endless(Db);
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            rig.Step(false);
            rig.Log.Clear();
            rig.World.HeatApplyGraze(P0, rig.Log);
            Assert.False(rig.H.Locked);
            Assert.Equal(10200, rig.H.HeatHu);                                   // 열 = 임계 시작(8.4)
            Assert.True(rig.H.ExemptArmed);
            Assert.Equal((int)HeatUnlockReason.Graze, rig.Log.Single(SimEventType.HeatUnlocked).A);
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptArmed));
            rig.World.HeatApplyGraze(P0, rig.Log);                               // 면제는 겹치지 않는다
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptArmed));

            rig.RunTo(234);                                                      // 마지막 발사(225)의 간격이 끝날 때까지
            Assert.Equal(10200, rig.H.HeatHu);                                   // 냉각 대기 중(225 + 9 + 12 = 246)
            rig.Step(true);                                                      // 다음 발사: 열 면제, 임계(10200 + 0 ≥ 10200)
            SimEvent s = rig.Shots()[^1];
            Assert.Equal((234U, 10200L, 1), (s.Tick, s.V, s.C));
            Assert.False(rig.H.ExemptArmed);
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatExemptConsumed));
            rig.RunTo(244);                                                      // 243에 한 발 더
            Assert.Equal(10200 + 480, rig.H.HeatHu);                             // 면제는 한 번뿐
        }

        [Fact]
        public void Graze_RaisesHeatOnlyWhenBelowCritStart()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.Step(false);
            rig.World.HeatApplyGraze(P0, rig.Log);
            Assert.Equal(10200, rig.H.HeatHu);
            Assert.Equal(1, rig.Log.Count(SimEventType.HeatEnteredCritical));
            HeatRig hot = HeatRig.Endless(Db);
            for (uint t = 0; t <= 207; t++)
            {
                hot.Step(true);                                                  // 24발 → 96
            }
            hot.Step(false);
            hot.World.HeatApplyGraze(P0, hot.Log);
            Assert.Equal(11520, hot.H.HeatHu);                                   // 이미 임계 시작 이상이면 그대로
        }

        [Fact]
        public void Kiln_AbsorbsOnlyCritShotHeatInsideZone()
        {
            // spec 01 7.6.2(흡수 16 = 1,920 HU). 지대의 생성과 효과는 고유 비기 작업(M5-CHR-02)의 것이고 여기서는 열 쪽 규칙만 본다
            HeatRig rig = HeatRig.Endless(Db);
            for (uint t = 0; t <= 180; t++)
            {
                rig.Step(true);                                                  // 21발 → 84
            }
            FixVec2 pos = rig.World.PlayerAt(0).Pos;
            rig.World.HeatStartKiln(P0, pos, Fix64.FromInt(2), 1920);
            rig.RunTo(217);                                                      // 189, 198, 207, 216: 임계 4발
            Assert.Equal(10080, rig.H.HeatHu);                                   // 열 없이 나갔다
            Assert.Equal(0, rig.H.KilnAbsorbLeftHu);
            Assert.Equal(4, rig.Log.Count(SimEventType.KilnAbsorbed));
            Assert.True(rig.Shots().GetRange(21, 4).TrueForAll(e => e.C == 1));
            rig.RunTo(226);
            Assert.Equal(10560, rig.H.HeatHu);                                   // 다 먹은 뒤에는 평소대로

            HeatRig outside = HeatRig.Endless(Db);
            for (uint t = 0; t <= 180; t++)
            {
                outside.Step(true);
            }
            FixVec2 far = outside.World.PlayerAt(0).Pos + new FixVec2(Fix64.FromInt(3), Fix64.Zero);
            outside.World.HeatStartKiln(P0, far, Fix64.FromInt(2), 1920);
            outside.RunTo(190);
            Assert.Equal(10560, outside.H.HeatHu);                               // 지대 밖: 흡수 없음
            Assert.Equal(0, outside.Log.Count(SimEventType.KilnAbsorbed));
        }

        [Fact]
        public void Reset_ClearsGaugeAndKeepsShotAndModifiers()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.World.SetHeatModifiers(P0, new HeatModifiers { CoolPermille = 500 });
            while (!rig.H.Locked)
            {
                rig.Step(true);
            }
            rig.World.HeatReset(P0, rig.Log);
            Assert.False(rig.H.Locked);
            Assert.Equal(0, rig.H.HeatHu);
            Assert.True(rig.H.Shot.Armed);
            Assert.Equal(500, rig.H.Mods.CoolPermille);
            Assert.Equal((int)HeatUnlockReason.Reset, rig.Log.Single(SimEventType.HeatUnlocked).A);
        }

        // ── 발사 규칙의 경계

        [Fact]
        public void TapInOneTickFires_TapDuringIntervalIsNotBuffered()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.Tap();                                                           // 틱 0에 누르고 뗌
            Assert.Single(rig.Shots());
            rig.RunTo(4);
            rig.Tap();                                                           // 간격 중(4 < 9): 버린다
            rig.RunTo(12);
            Assert.Single(rig.Shots());
            rig.Tap();
            Assert.Equal(2, rig.Shots().Count);
            Assert.Equal(12U, rig.Shots()[1].Tick);
        }

        [Fact]
        public void FireDisabledSwitchFiresNothing()
        {
            HeatRig rig = HeatRig.FireDisabled(Db);
            for (int t = 0; t < 30; t++)
            {
                rig.Step(true);
            }
            Assert.Empty(rig.Shots());
            Assert.Equal(0, rig.H.HeatHu);
        }

        [Fact]
        public void NoShotProfileFiresNothing()
        {
            HeatRig rig = HeatRig.Endless(Db);
            rig.World.ClearHeatShot(P0);
            for (int t = 0; t < 30; t++)
            {
                rig.Step(true);
            }
            Assert.Empty(rig.Shots());
        }

        [Fact]
        public void EmptyPack_ModuleIsOff()
        {
            SimWorld w = WorldKit.NewEndless(WorldKit.EmptyDb());
            Assert.False(w.HeatModuleOn);
            for (uint t = 0; t < 5; t++)
            {
                w.Step(new[] { WorldKit.In(0, t, 0, LogicalKey.Fire, t % 2 == 0, t + 1) }, ReadOnlySpan<RunCommand>.Empty, new NullEventSink());
            }
            Assert.Throws<SimInvariantException>(() => w.SetHeatShot(P0, BlockId.MagicBolt, 480, 9));
        }

        [Fact]
        public void SetHeatShotRejectsBadValues()
        {
            HeatRig rig = HeatRig.Endless(Db);
            Assert.Throws<SimInvariantException>(() => rig.World.SetHeatShot(P0, BlockId.MagicBolt, -1, 9));
            Assert.Throws<SimInvariantException>(() => rig.World.SetHeatShot(P0, BlockId.MagicBolt, 480, 0));
            Assert.Throws<SimInvariantException>(() => rig.World.SetHeatModifiers(P0, new HeatModifiers { CoolPermille = -1 }));
        }

        // ── 결정론

        private static readonly HeatModifiers BusyMods = new HeatModifiers
        {
            CoolPermille = 500, KillHeatHu = 480, KillWindowCapHu = 2400, UnstableChancePermille = 50, UnstableLockTicks = 120,
        };

        private static bool[] Script(int ticks, uint seed)
        {
            var rng = new Pcg32(seed, 3UL);
            var down = new bool[ticks];
            bool held = false;
            for (int t = 0; t < ticks; t++)
            {
                if (rng.NextInt(20) == 0)
                {
                    held = !held;
                }
                down[t] = held;
            }
            return down;
        }

        // 틱마다 J 를 맞추고, 가끔 처치·스침·방열 폭발을 넣는다(다른 모듈의 연산도 상태를 움직인다)
        private static ulong[] RunScript(HeatRig rig, bool[] script, int from, int to)
        {
            var hashes = new ulong[to - from];
            for (int t = from; t < to; t++)
            {
                if (t % 41 == 7)
                {
                    rig.World.HeatOnKill(P0);
                }
                if (t % 173 == 50)
                {
                    rig.World.HeatApplyGraze(P0, rig.Log);
                }
                if (t % 389 == 300)
                {
                    rig.World.HeatConsumeForVentBlast(P0, rig.Log);
                }
                rig.Step(script[t]);
                hashes[t - from] = rig.World.ComputeStateHash();
            }
            return hashes;
        }

        private static HeatRig Busy(ulong seed)
        {
            HeatRig rig = HeatRig.Endless(Db, BlockId.Fireball, seed);
            rig.World.SetHeatModifiers(P0, BusyMods);
            return rig;
        }

        [Fact]
        public void TwoRunsGiveSameHashes()
        {
            bool[] script = Script(3000, 11U);
            ulong[] a = RunScript(Busy(5UL), script, 0, 3000);
            ulong[] b = RunScript(Busy(5UL), script, 0, 3000);
            Assert.Equal(a, b);
            Assert.True(new HashSet<ulong>(a).Count > 500);
        }

        [Fact]
        public void SnapshotLoadReplaysToSameHashes()
        {
            bool[] script = Script(2400, 23U);
            HeatRig rig = Busy(9UL);
            RunScript(rig, script, 0, 1000);
            SimSnapshot snap = rig.World.CreateSnapshotBuffer();
            rig.World.SaveSnapshot(snap);
            ulong saved = rig.World.ComputeStateHash();
            ulong[] forward = RunScript(rig, script, 1000, 2400);
            rig.World.LoadSnapshot(snap);
            Assert.Equal(saved, rig.World.ComputeStateHash());
            Assert.Equal(forward, RunScript(rig, script, 1000, 2400));
        }

        [Fact]
        public void WriteStateReadStateRoundTripContinuesIdentically()
        {
            bool[] script = Script(2000, 31U);
            HeatRig a = Busy(3UL);
            RunScript(a, script, 0, 900);
            var w = new ByteWriter();
            a.World.WriteState(w);
            byte[] bytes = w.ToArray();

            SimWorld fresh = SimWorld.CreateEndless(Db, SimConfig.ForEndless(Db), new EndlessSetup { Seed = 3UL, StartWave = 1 });
            fresh.ReadState(bytes);
            Assert.Equal(a.World.ComputeStateHash(), fresh.ComputeStateHash());
            var w2 = new ByteWriter();
            fresh.WriteState(w2);
            Assert.Equal(bytes, w2.ToArray());
        }

        [Fact]
        public void RoundTripRigContinuesIdentically()
        {
            bool[] script = Script(2000, 37U);
            HeatRig a = Busy(4UL);
            HeatRig b = Busy(4UL);
            RunScript(a, script, 0, 800);
            var w = new ByteWriter();
            a.World.WriteState(w);
            b.World.ReadState(w.ToArray());
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            b.SyncKeysFrom(a);
            Assert.Equal(RunScript(a, script, 800, 2000), RunScript(b, script, 800, 2000));
        }

        [Fact]
        public void HeatStateIsInTheHash()
        {
            HeatRig a = HeatRig.Endless(Db);
            HeatRig b = HeatRig.Endless(Db);
            a.Step(false);
            b.Step(false);
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            b.World.HeatApplyGraze(P0, b.Log);
            Assert.NotEqual(a.World.ComputeStateHash(), b.World.ComputeStateHash());
        }

        // 같은 각본을 새 세계로 여러 번 돌려 가장 작은 값이 0인지 본다(AllocationProbe. 한 번 재기는 병렬 실행에서 가끔 흔들렸다, FIX-1)
        [Fact]
        public void StepDoesNotAllocateAfterWarmUp()
        {
            bool[] script = Script(1500, 47U);
            CountingSink? sink = null;
            long least = AllocationProbe.LeastBytes(() =>
            {
                HeatRig rig = Busy(1UL);
                SimWorld world = rig.World;
                var runSink = new CountingSink();
                sink = runSink;
                var buffer = new PlayerInput[1];
                bool held = false;
                uint seq = 0;
                int tick = 0;

                void One()
                {
                    int n = 0;
                    bool want = script[tick % script.Length];
                    if (want != held)
                    {
                        held = want;
                        buffer[0] = new PlayerInput(P0, new InputEvent(world.Tick, 0, LogicalKey.Fire, want, ++seq));
                        n = 1;
                    }
                    if (tick % 41 == 7)
                    {
                        world.HeatOnKill(P0);
                    }
                    if (tick % 173 == 50)
                    {
                        world.HeatApplyGraze(P0, runSink);
                    }
                    world.Step(buffer.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, runSink);
                    tick++;
                }

                One();
                return () =>
                {
                    for (int i = 0; i < 1400; i++)
                    {
                        One();
                    }
                };
            }, () => Assert.True(sink!.Count > 100));
            Assert.Equal(0L, least);
        }
    }
}
