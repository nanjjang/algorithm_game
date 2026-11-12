using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Xunit;
using static Wizard.Sim.Tests.Heat.Graze.GrazeHeatKit;

namespace Wizard.Sim.Tests.Heat.Graze
{
    // Heat 와 Graze 가 같이 도는 세계의 결정론(작업 카드의 수락 조건): 두 번 실행, 스냅숏 저장·복원 뒤 진행, WriteState/ReadState 왕복,
    // 그리고 Step 할당 0. 각본은 J 를 누르고 떼며, 9단계 스침 후보(시전 묶음이 바뀌고 거리가 띠 안팎을 오간다), 비기 스침,
    // 시전 지우기, 가끔의 피격을 넣는다. 그래서 잠금 해제·열 면제·임계 시작으로 올림이 실제로 여러 번 일어난다.
    public sealed class GrazeHeatDeterminismTests
    {
        private static ContentDb Db => HeatData.Baseline;

        private const int ScriptTicks = 2400;

        // 틱마다의 J(눌림/뗌). 길게 누르는 구간이 있어야 잠금까지 간다
        private static bool[] JScript(uint seed)
        {
            var rng = new Pcg32(seed, 5UL);
            var down = new bool[ScriptTicks];
            bool held = false;
            for (int t = 0; t < ScriptTicks; t++)
            {
                if (rng.NextInt(30) == 0)
                {
                    held = !held;
                }
                down[t] = held;
            }
            return down;
        }

        // Step 직전에 넣는 Graze 쪽 사건. 할당하지 않는다(할당 시험도 같은 함수를 쓴다)
        private static void Feed(SimWorld world, int t, GrazeThreat[] threats, ISimEventSink sink)
        {
            if (t == 0)
            {
                return;
            }
            if (t % 7 == 3)
            {
                int k = (t / 7) % threats.Length;
                GrazeThreat threat = threats[k];
                threat.CastId = (t / 21) + 1;                            // 세 번에 한 번 새 시전
                world.OfferGrazeThreat(P0, in threat);
            }
            if (t % 97 == 40)
            {
                world.AwardSkillGraze(P0, sink);
            }
            if (t % 50 == 11)
            {
                world.ReleaseGrazeCast((t / 21) + 1);
            }
            if (t % 600 == 300)
            {
                world.QueuePlayerHit(P0, new EntityId(t + 100_000), 1);   // 네 번: 쓰러지지 않는다
            }
        }

        // 거리 0.40 ~ 0.94(탄 반지름 0.12): 피격 원, 스침 띠, 바깥이 섞인다. 플레이어는 움직이지 않으므로 처음 자리 기준
        private static GrazeThreat[] Threats(SimWorld world)
        {
            FixVec2 p = world.PlayerAt(0).Pos;
            var threats = new GrazeThreat[12];
            for (int k = 0; k < threats.Length; k++)
            {
                threats[k] = GrazeThreat.Bullet(new EntityId(k + 1), 1, new FixVec2(p.X + Milli(400 + 45 * k), p.Y), Milli(120));
            }
            return threats;
        }

        private static ulong[] Run(HeatRig rig, bool[] script, GrazeThreat[] threats, int from, int to)
        {
            var hashes = new ulong[to - from];
            for (int t = from; t < to; t++)
            {
                Feed(rig.World, t, threats, rig.Log);
                rig.Step(script[t]);
                hashes[t - from] = rig.World.ComputeStateHash();
            }
            return hashes;
        }

        private static HeatRig NewRig(ulong seed) => HeatRig.Endless(Db, BlockId.MagicBolt, seed);

        [Fact]
        public void TwoRunsGiveSameHashes()
        {
            bool[] script = JScript(13U);
            HeatRig a = NewRig(5UL);
            HeatRig b = NewRig(5UL);
            ulong[] ha = Run(a, script, Threats(a.World), 0, ScriptTicks);
            Assert.Equal(ha, Run(b, script, Threats(b.World), 0, ScriptTicks));
            Assert.Equal(a.Log.Events.Count, b.Log.Events.Count);
            Assert.True(new HashSet<ulong>(ha).Count > 500);

            // 각본이 스침의 열 경로를 실제로 지났다
            Assert.True(a.Log.Count(SimEventType.GrazeAwarded) > 50);
            Assert.True(a.Log.Count(SimEventType.HeatExemptConsumed) > 10);
            Assert.True(a.Log.Events.FindAll(e => e.Type == SimEventType.HeatUnlocked && e.A == (int)Wizard.Sim.Heat.HeatUnlockReason.Graze).Count > 0);
            Assert.True(a.Log.Count(SimEventType.HeatLockEntered) > 0);
            Assert.True(a.World.PlayerAt(0).HpPoints > 0);
        }

        [Fact]
        public void SnapshotLoadReplaysToSameHashes()
        {
            bool[] script = JScript(17U);
            HeatRig rig = NewRig(9UL);
            GrazeThreat[] threats = Threats(rig.World);
            Run(rig, script, threats, 0, 1000);
            HeatRig keysAt1000 = NewRig(9UL);                            // 되감은 뒤 J 의 눌림 상태를 맞추는 데만 쓴다(시험 쪽 상태)
            Run(keysAt1000, script, Threats(keysAt1000.World), 0, 1000);
            GrazeThreat pending = BulletBeside(rig, 0, 77_777, 88_888);
            rig.World.OfferGrazeThreat(P0, in pending);                 // 틱 경계에 남은 후보도 되감긴다
            SimSnapshot snap = rig.World.CreateSnapshotBuffer();
            rig.World.SaveSnapshot(snap);
            ulong saved = rig.World.ComputeStateHash();
            ulong[] forward = Run(rig, script, threats, 1000, ScriptTicks);
            rig.World.LoadSnapshot(snap);
            Assert.Equal(saved, rig.World.ComputeStateHash());
            rig.SyncKeysFrom(keysAt1000);
            Assert.Equal(forward, Run(rig, script, threats, 1000, ScriptTicks));
        }

        [Fact]
        public void WriteStateReadStateRoundTripContinuesIdentically()
        {
            bool[] script = JScript(19U);
            HeatRig a = NewRig(3UL);
            GrazeThreat[] threats = Threats(a.World);
            Run(a, script, threats, 0, 900);
            var w = new ByteWriter();
            a.World.WriteState(w);
            byte[] bytes = w.ToArray();

            HeatRig b = NewRig(3UL);
            b.World.ReadState(bytes);
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            var w2 = new ByteWriter();
            b.World.WriteState(w2);
            Assert.Equal(bytes, w2.ToArray());
            b.SyncKeysFrom(a);
            Assert.Equal(Run(a, script, threats, 900, ScriptTicks), Run(b, script, threats, 900, ScriptTicks));
        }

        [Fact]
        public void GrazeHeatChangesTheHash()
        {
            HeatRig a = HeatRig.Endless(Db);
            HeatRig b = HeatRig.Endless(Db);
            a.Step(false);
            b.Step(false);
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            OfferGraze(b, 100, 1);
            a.Step(false);
            b.Step(false);
            Assert.NotEqual(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            Assert.NotEqual(a.H.HeatHu, b.H.HeatHu);
            Assert.True(b.H.ExemptArmed);
        }

        // 각본 하나를 새 세계에서 돌리고 그동안 이 스레드가 할당한 바이트를 돌려준다(첫 틱은 데우기)
        private static long AllocatedByOneScriptedRun(bool[] script, out int events, out int grazes)
        {
            HeatRig rig = NewRig(1UL);
            SimWorld world = rig.World;
            GrazeThreat[] threats = Threats(world);
            var sink = new GrazeCountingSink();
            var buffer = new PlayerInput[1];
            bool held = false;
            uint seq = 0;
            int tick = 0;

            void One()
            {
                int n = 0;
                bool want = script[tick];
                if (want != held)
                {
                    held = want;
                    buffer[0] = new PlayerInput(P0, new InputEvent(world.Tick, 0, LogicalKey.Fire, want, ++seq));
                    n = 1;
                }
                Feed(world, tick, threats, sink);
                world.Step(buffer.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
                tick++;
            }

            One();
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (tick < script.Length)
            {
                One();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            events = sink.Count;
            grazes = sink.Grazes;
            return allocated;
        }

        private sealed class GrazeCountingSink : ISimEventSink
        {
            public int Count;
            public int Grazes;

            public void Emit(in SimEvent e)
            {
                Count++;
                if (e.Type == SimEventType.GrazeAwarded)
                {
                    Grazes++;
                }
            }
        }

        // Step 이 어느 경로에서든 관리 힙에 할당하면 같은 각본을 돌릴 때마다 0보다 크다. 그래서 같은 각본을 새 세계로 세 번 돌려
        // 가장 작은 값이 0인지 본다(한 번 재기는 전체 실행에서 가끔 실패한다, FIX-1. GrazePipelineTests 와 같은 방법)
        [Fact]
        public void StepDoesNotAllocateAfterWarmUp()
        {
            bool[] script = JScript(23U);
            long least = long.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                long allocated = AllocatedByOneScriptedRun(script, out int events, out int grazes);
                Assert.True(events > 100);
                Assert.True(grazes > 50);
                least = Math.Min(least, allocated);
            }
            Assert.Equal(0L, least);
        }
    }
}
