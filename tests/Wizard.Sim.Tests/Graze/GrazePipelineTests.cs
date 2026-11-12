using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Player;
using Wizard.Sim.Tests.Runtime;
using Xunit;
using static Wizard.Sim.Tests.Graze.GrazeKit;

namespace Wizard.Sim.Tests.Graze
{
    // 작업 카드의 수락: 같은 CastId 의 탄 여러 발이 스쳐도 보상은 한 번이다, 피격 판정이 스침 판정보다 먼저다.
    // "먼저"의 순서 쪽(9단계에서 Graze 훅이 피격 후보를 넣는 훅들의 뒤)은 Runtime/HookTableTests 가 Step 의 소스로 본다(T-50).
    // 여기서는 판정 쪽을 본다: 그 틱의 피격 후보가 있으면 어떤 스침도 인정하지 않는다.
    // 그리고 Graze 절의 결정론(두 번 실행, 스냅숏, 직렬화 왕복, 할당 0), 결투의 스침 간격, 비기 스침, 시전 묶음 지우기, 빈 팩.
    public sealed class GrazePipelineTests
    {
        // GrazeAwarded 가 나는 순간(같은 틱 안)에 뒤 단계 모듈이 읽을 값을 본다
        private sealed class ProbeSink : ISimEventSink
        {
            public SimWorld? World;
            public readonly List<int> AwardsSeen = new List<int>();

            public void Emit(in SimEvent e)
            {
                if (e.Type == SimEventType.GrazeAwarded)
                {
                    AwardsSeen.Add(World!.GrazeAwardsThisTick(0));
                }
            }
        }

        private static PlayerRig Ready(uint tick = 10) => ReadyAt(tick, PlayerRig.Endless());

        [Fact]
        public void ManyBulletsOfOneCastInOneTickAwardOnce()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            uint t = rig.Tick;
            for (int k = 0; k < 6; k++)
            {
                Offer(rig, BulletAt(rig, 50 + k, 21, 620 + 10 * k));
            }
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(50, rig.Log.Single(SimEventType.GrazeAwarded).Source.Value);   // id 가 가장 작은 탄
            Assert.Equal(t, rig.World.GrazeAt(0).AwardTick);
            Assert.Equal(1, rig.World.GrazeAt(0).AwardCount);

            // 다음 틱들에도 같은 시전은 다시 인정하지 않는다
            rig.Log.Clear();
            for (int k = 0; k < 4; k++)
            {
                Offer(rig, BulletAt(rig, 60 + k, 21, 650));
                rig.Step();
            }
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void AHitInTheSameTickBlocksEveryGrazeRegardlessOfIds()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            Offer(rig, BulletAt(rig, 2, 1, 700));
            Offer(rig, BulletAt(rig, 3, 2, 650));
            rig.World.QueuePlayerHit(P0, new EntityId(99), 2);         // 공격 id 가 탄보다 커도 피격이 먼저
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.PlayerHit));
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(0, rig.World.GrazeCastCount(0));

            // 피격 뒤 무적이 끝나면 같은 시전도 인정한다(이미 받은 보상이 없었다)
            rig.RunTo(rig.Tick + 60U);
            Assert.False(rig.World.PlayerInvulnerable(0));
            Offer(rig, BulletAt(rig, 4, 1, 700));
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void GrazeBeforeALaterHitKeepsItsReward()
        {
            // "그 탄이 나중에 맞더라도 이미 받은 보상은 그대로다"(8.3)
            PlayerRig rig = Ready();
            Offer(rig, BulletAt(rig, 5, 30, 700));
            rig.Step();
            uint until = rig.World.GrazeAt(0).BuffUntil;
            rig.World.QueuePlayerHit(P0, new EntityId(5), 1);
            rig.Step();
            Assert.Equal(until, rig.World.GrazeAt(0).BuffUntil);
            Assert.True(rig.World.GrazeCastRecorded(0, 30));
        }

        [Fact]
        public void AwardCountIsVisibleWithinTheTick()
        {
            PlayerRig rig = Ready();
            var probe = new ProbeSink { World = rig.World };
            Offer(rig, BulletAt(rig, 10, 1, 700));
            Offer(rig, BulletAt(rig, 11, 2, 700));
            rig.World.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, probe);
            Assert.Equal(new[] { 1, 2 }, probe.AwardsSeen.ToArray());
        }

        [Fact]
        public void NonGrazeableThreatIsIgnored()
        {
            PlayerRig rig = Ready();
            rig.Log.Clear();
            GrazeThreat t = BulletAt(rig, 12, 3, 700);
            t.Grazeable = false;
            Offer(rig, t);
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void DownedPlayerDoesNotGraze()
        {
            PlayerRig rig = Ready();
            rig.World.QueuePlayerHit(P0, new EntityId(1), 6);          // 6칸 → 0
            rig.Step();
            Assert.Equal(0, rig.P.HpPoints);
            rig.RunTo(rig.Tick + 120U);
            rig.Log.Clear();
            Offer(rig, BulletAt(rig, 13, 4, 700));
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void ReleasedCastCanBeGrazedAgain()
        {
            PlayerRig rig = Ready();
            Offer(rig, BulletAt(rig, 14, 5, 700));
            rig.Step();
            Assert.True(rig.World.GrazeCastRecorded(0, 5));
            rig.World.ReleaseGrazeCast(5);                              // 시전의 모든 탄이 사라졌다(8.2)
            Assert.False(rig.World.GrazeCastRecorded(0, 5));
            rig.Log.Clear();
            Offer(rig, BulletAt(rig, 15, 5, 700));
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        [Fact]
        public void GrazedCastListStaysBoundedByDroppingTheOldest()
        {
            PlayerRig rig = Ready();
            int total = SimWorld.GrazedCastCapacity + 3;
            for (int c = 1; c <= total; c++)
            {
                Offer(rig, BulletAt(rig, c, c, 700));
            }
            rig.Step();
            Assert.Equal(SimWorld.GrazedCastCapacity, rig.World.GrazeCastCount(0));
            Assert.False(rig.World.GrazeCastRecorded(0, 1));
            Assert.False(rig.World.GrazeCastRecorded(0, 3));
            Assert.True(rig.World.GrazeCastRecorded(0, 4));
            Assert.True(rig.World.GrazeCastRecorded(0, total));
            Assert.Equal(total, rig.World.GrazeAt(0).AwardCount);
        }

        [Fact]
        public void BadOffersAreRejected()
        {
            PlayerRig rig = Ready();
            Assert.Throws<SimInvariantException>(() => rig.World.OfferGrazeThreat(new PlayerSlot(1), BulletAt(rig, 1, 1, 700)));
            Assert.Throws<SimInvariantException>(() => rig.World.OfferGrazeThreat(P0, BulletAt(rig, 1, 0, 700)));      // 시전 없음
            Assert.Throws<SimInvariantException>(() => rig.World.OfferGrazeThreat(P0, BulletAt(rig, 0, 1, 700)));      // 개체 없음
            Assert.Throws<SimInvariantException>(() => rig.World.OfferGrazeThreat(P0, BulletAt(rig, 1, 1, 700, -1)));  // 음수 반지름
            for (int k = 0; k < SimWorld.GrazeOfferCapacity; k++)
            {
                rig.World.OfferGrazeThreat(P0, BulletAt(rig, k + 1, 1, 2000));
            }
            Assert.Throws<SimInvariantException>(() => rig.World.OfferGrazeThreat(P0, BulletAt(rig, 9999, 1, 2000)));
        }

        // ── 결투(spec 01 12.2): 상대 1명당 1.0초에 1회, 보상 줄어듦(가속 없음)

        private static PlayerRig DuelReady()
        {
            PlayerRig rig = PlayerRig.Duel();
            // 결투 수치는 duel_rules 데이터가 채울 자리(SimConfig.FillDuel, 아직 없음)라 명세 값 60틱을 시험이 넣는다(spec 01 12.1)
            rig.World.Config.GrazeCooldownTicks = 60;
            rig.RunTo(10);
            return rig;
        }

        [Fact]
        public void DuelGrazeWithinOneSecondIsCosmeticOnly()
        {
            PlayerRig rig = DuelReady();
            rig.Log.Clear();
            uint t0 = rig.Tick;
            Offer(rig, BulletAt(rig, 20, 1, 700));
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeBuffStarted));    // 결투에는 가속이 없다
            Assert.False(rig.World.GrazeBuffActive(0));
            Assert.Equal(t0 + 60U, rig.World.GrazeAt(0).CooldownReadyTick);

            rig.RunTo(t0 + 59U);
            Offer(rig, BulletAt(rig, 21, 2, 700));
            rig.Step();
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeAwarded));
            SimEvent cosmetic = rig.Log.Single(SimEventType.GrazeCosmeticOnly);
            Assert.Equal(2, cosmetic.A);
            Assert.True(rig.World.GrazeCastRecorded(0, 2));                     // 묶음당 1회: 연출만 난 시전도 다시 인정하지 않는다
            Assert.Equal(1, rig.World.GrazeAt(0).AwardCount);
            Assert.Equal(t0, rig.World.GrazeAt(0).AwardTick);

            Offer(rig, BulletAt(rig, 22, 3, 700));                              // t0 + 60: 다시 인정
            rig.Step();
            Assert.Equal(2, rig.Log.Count(SimEventType.GrazeAwarded));
            Assert.Equal(t0 + 120U, rig.World.GrazeAt(0).CooldownReadyTick);
        }

        [Fact]
        public void DuelHitRadiusIsTheConfigValue()
        {
            PlayerRig rig = DuelReady();
            rig.World.Config.HitRadiusOverride = Milli(300);
            Assert.Equal(Milli(300), rig.World.GrazeHitRadius(0));
            rig.Log.Clear();
            // 결투 피격 0.30: 거리 0.40(≤ 0.30 + 0.12)은 피격 원 안이라 스침이 아니다
            Offer(rig, BulletAt(rig, 23, 1, 400));
            rig.Step();
            Assert.Equal(0, rig.Log.Count(SimEventType.GrazeAwarded));
        }

        // ── 비기 스침(spec 01 8.1 10번)

        [Fact]
        public void SkillGrazeAwardsWithoutACastAndIgnoresInvulnerability()
        {
            PlayerRig rig = Ready();
            rig.World.GrantPlayerInvuln(P0, rig.Tick + 30U);           // 벼락 질주의 무적
            rig.Log.Clear();
            uint t = rig.Tick;
            rig.World.AwardSkillGraze(P0, rig.Log);
            SimEvent e = rig.Log.Single(SimEventType.GrazeAwarded);
            Assert.Equal((int)GrazeKind.Skill, e.B);
            Assert.Equal(0, e.A);
            Assert.True(e.Source.IsNone);
            Assert.Equal(1, rig.World.GrazeAwardsThisTick(0));
            Assert.Equal(0, rig.World.GrazeCastCount(0));
            Assert.Equal(t + 90U, rig.World.GrazeAt(0).BuffUntil);
        }

        // ── 웨이브 끝의 가속 초기화(spec 01 10.6)

        [Fact]
        public void EndGrazeBuffClearsTheBuffOnceAndKeepsGrazedCasts()
        {
            PlayerRig rig = Ready();
            Offer(rig, BulletAt(rig, 30, 6, 700));
            rig.Step();
            Assert.True(rig.World.GrazeBuffActive(0));
            rig.Log.Clear();

            uint t = rig.Tick;
            rig.World.EndGrazeBuff(P0, rig.Log);
            Assert.False(rig.World.GrazeBuffActive(0));
            Assert.Equal(0U, rig.World.GrazeAt(0).BuffUntil);
            Assert.Equal(t, rig.Log.Single(SimEventType.GrazeBuffEnded).Tick);
            Assert.True(rig.World.GrazeCastRecorded(0, 6));            // 명세는 가속만 지운다

            rig.World.EndGrazeBuff(P0, rig.Log);                        // 꺼진 가속을 다시 꺼도 이벤트는 없다
            rig.RunTo(t + 200U);
            Assert.Equal(1, rig.Log.Count(SimEventType.GrazeBuffEnded));
            Assert.Throws<SimInvariantException>(() => rig.World.EndGrazeBuff(new PlayerSlot(1), rig.Log));
        }

        // ── 빈 팩: 모듈이 꺼진다

        [Fact]
        public void EmptyPackTurnsTheModuleOff()
        {
            SimWorld w = WorldKit.NewEndless(WorldKit.EmptyDb());
            ulong before = w.ComputeStateHash();
            w.OfferGrazeThreat(P0, GrazeThreat.Bullet(new EntityId(1), 1, FixVec2.Zero, Milli(120)));
            Assert.Equal(0, w.GrazeOfferCount(0));
            Assert.Equal(before, w.ComputeStateHash());
            w.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, new RecordingSink());
        }

        // ── 결정론

        // 틱마다 정해진 꼴로 후보를 넣는다: 시전 묶음은 몇 틱마다 바뀌고, 거리는 띠 안팎을 오가며, 가끔 맞는다
        private static void Feed(PlayerRig rig, uint t)
        {
            var rng = new Pcg32(0xC0FFEEUL + t, 7UL);
            int n = (int)(t % 4U);
            for (int k = 0; k < n; k++)
            {
                int id = (int)(t * 8U) + k + 1;
                int cast = (int)(t / 5U) + k + 1;
                long d = 200 + rng.NextInt(900);
                Offer(rig, BulletAt(rig, id, cast, d));
            }
            if (t % 9U == 0U)
            {
                FixVec2 center = RightOf(rig.P.Pos, Milli(1000 + rng.NextInt(900)));
                Offer(rig, GrazeThreat.CircleZone(new EntityId((int)(t * 8U) + 7), (int)t + 10_000, center, Fix64.One,
                                                  t % 2U == 0U, rig.P.Pos, rig.World.GrazeHitRadius(0)));
            }
            if (t % 53U == 0U)
            {
                rig.World.QueuePlayerHit(P0, new EntityId((int)t + 1), 1);
            }
            if (t % 31U == 0U)
            {
                rig.World.ReleaseGrazeCast((int)(t / 5U));
            }
        }

        private static Key[] Keys(uint t)
        {
            switch (t % 40U)
            {
                case 0U: return new[] { PlayerRig.Press(LogicalKey.Right, 3) };
                case 7U: return new[] { PlayerRig.Release(LogicalKey.Right, 9) };
                case 20U: return new[] { PlayerRig.Press(LogicalKey.Left, 1) };
                case 27U: return new[] { PlayerRig.Release(LogicalKey.Left, 2) };
                default: return Array.Empty<Key>();
            }
        }

        private static ulong[] Run(PlayerRig rig, uint from, uint to)
        {
            var hashes = new ulong[to - from];
            for (uint t = from; t < to; t++)
            {
                if (t > 0U)
                {
                    Feed(rig, t);
                }
                rig.Step(Keys(t));
                hashes[t - from] = rig.World.ComputeStateHash();
            }
            return hashes;
        }

        [Fact]
        public void TwoRunsGiveSameHashes()
        {
            PlayerRig a = PlayerRig.Endless(5UL);
            PlayerRig b = PlayerRig.Endless(5UL);
            ulong[] ha = Run(a, 0, 700);
            Assert.Equal(ha, Run(b, 0, 700));
            Assert.True(a.Log.Count(SimEventType.GrazeAwarded) > 20);         // 실제로 스쳤다
            Assert.True(a.Log.Count(SimEventType.GrazeBuffEnded) > 0);
            Assert.True(new HashSet<ulong>(ha).Count > 100);
        }

        [Fact]
        public void SnapshotLoadReplaysToSameHashes()
        {
            PlayerRig rig = PlayerRig.Endless(9UL);
            Run(rig, 0, 300);
            Offer(rig, BulletAt(rig, 77_777, 88_888, 700));                     // 틱 경계에 남은 후보도 되감긴다
            SimSnapshot snap = rig.World.CreateSnapshotBuffer();
            rig.World.SaveSnapshot(snap);
            ulong saved = rig.World.ComputeStateHash();
            ulong[] forward = Run(rig, 300, 650);
            rig.World.LoadSnapshot(snap);
            Assert.Equal(saved, rig.World.ComputeStateHash());
            Assert.Equal(1, rig.World.GrazeOfferCount(0));
            Assert.Equal(forward, Run(rig, 300, 650));
        }

        [Fact]
        public void WriteStateReadStateRoundTripContinuesIdentically()
        {
            PlayerRig a = PlayerRig.Endless(3UL);
            Run(a, 0, 250);
            Offer(a, BulletAt(a, 77_777, 88_888, 700));
            Assert.True(a.World.GrazeCastCount(0) > 0);
            var w = new ByteWriter();
            a.World.WriteState(w);
            byte[] bytes = w.ToArray();

            PlayerRig b = PlayerRig.Endless(3UL);
            b.World.ReadState(bytes);
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            var w2 = new ByteWriter();
            b.World.WriteState(w2);
            Assert.Equal(bytes, w2.ToArray());
            Assert.Equal(Run(a, 250, 600), Run(b, 250, 600));
        }

        [Fact]
        public void GrazeStateChangesTheHash()
        {
            PlayerRig a = Ready();
            PlayerRig b = Ready();
            Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
            Offer(a, BulletAt(a, 1, 1, 700));
            Assert.NotEqual(a.World.ComputeStateHash(), b.World.ComputeStateHash());   // 후보도 상태다
            a.Step();
            b.Step();
            Assert.NotEqual(a.World.ComputeStateHash(), b.World.ComputeStateHash());   // 시전 기록, 가속
        }

        [Fact]
        public void ReadStateRejectsBrokenGrazeSection()
        {
            PlayerRig a = Ready();
            Offer(a, BulletAt(a, 1, 1, 700));
            a.Step();
            var w = new ByteWriter();
            a.World.WriteState(w);
            byte[] good = w.ToArray();
            PlayerRig b = Ready();
            b.World.ReadState(good);

            // 시전 묶음 목록의 값(첫 칸)을 0으로 깨면 거절한다. 위치는 바이트를 찾아서 정한다(Graze 절 id 3 뒤)
            byte[] bad = (byte[])good.Clone();
            int at = FindCastValue(bad, 1);
            bad[at] = 0;
            Assert.Throws<SimInvariantException>(() => PlayerRig.Endless().World.ReadState(bad));
        }

        // Graze 절에서 "개수 1, 값 castId" 의 값 위치. 절 id(u16 3) 뒤 GrazeState 17바이트 다음이 개수다
        private static int FindCastValue(byte[] bytes, int castId)
        {
            const int StateBytes = 4 + 1 + 4 + 4 + 4;
            for (int i = 0; i + 2 + StateBytes + 8 <= bytes.Length; i++)
            {
                if (bytes[i] == 3 && bytes[i + 1] == 0)
                {
                    int c = i + 2 + StateBytes;
                    if (BitsAt(bytes, c) == 1 && BitsAt(bytes, c + 4) == castId)
                    {
                        return c + 4;
                    }
                }
            }
            throw new InvalidOperationException("graze section not found");
        }

        private static int BitsAt(byte[] b, int i) => b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24);

        // 각본 하나(1000틱): 후보 8개(큰 id 부터 넣어 정렬도 돌게), 시전 지우기, 피격. 새 세계에서 돌리고 그동안 이 스레드가 할당한 바이트를 돌려준다
        private static long AllocatedByOneScriptedRun(out int events)
        {
            PlayerRig rig = Ready();
            SimWorld world = rig.World;
            var sink = new CountingSink();
            FixVec2 center = rig.P.Pos;
            Fix64 r = Milli(120);
            var threats = new GrazeThreat[64];
            for (int k = 0; k < threats.Length; k++)
            {
                threats[k] = GrazeThreat.Bullet(new EntityId(k + 1), k + 1, RightOf(center, Milli(500 + 5 * k)), r);
            }
            int tick = 0;

            void One()
            {
                for (int k = 0; k < 8; k++)
                {
                    int idx = (tick * 8 + 7 - k) % threats.Length;
                    world.OfferGrazeThreat(P0, in threats[idx]);
                }
                if (tick % 17 == 0)
                {
                    world.ReleaseGrazeCast((tick % threats.Length) + 1);
                }
                if (tick % 250 == 40)
                {
                    world.QueuePlayerHit(P0, new EntityId(tick + 1000), 1);   // 네 번: 6칸이라 쓰러지지 않고 끝까지 스친다
                }
                world.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
                tick++;
            }

            One();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                One();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            events = sink.Count;
            Assert.True(rig.P.HpPoints > 0);
            Assert.True(world.GrazeAt(0).AwardTick > world.Tick - 500U);       // 지워진 시전을 다시 스치는 일이 뒤쪽 절반에도 있었다
            return allocated;
        }

        // Step 이 어느 경로에서든 관리 힙에 할당하면 같은 각본을 돌릴 때마다 0보다 크다. 그래서 새 세계로 세 번 돌려 가장 작은 값이 0인지 본다.
        // 한 번만 재면 가끔 실패했다(전체를 되풀이해 돌릴 때 12번에 2번). 잡힌 자리와 크기가 실행마다 달랐고(틱 986에서 176바이트,
        // 다른 실행에서는 4,592바이트) 이 시험만 따로 돌리면 0이었다. 같은 각본에서 자리가 달라지므로 Step 의 경로가 하는 할당이 아니고,
        // 다른 시험이 병렬로 돌 때 이 스레드에 잡히는 런타임 쪽의 일회성 할당으로 본다(원인 확인과 도우미로 옮기는 일은 FIX-1)
        [Fact]
        public void StepDoesNotAllocateAfterWarmUp()
        {
            long least = long.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                long allocated = AllocatedByOneScriptedRun(out int events);
                Assert.True(events > 0);
                least = Math.Min(least, allocated);
            }
            Assert.Equal(0L, least);
        }
    }
}
