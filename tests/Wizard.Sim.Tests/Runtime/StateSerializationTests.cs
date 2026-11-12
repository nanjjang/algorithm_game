using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Core.Hash;
using Xunit;

namespace Wizard.Sim.Tests.Runtime
{
    // CONTRACTS 3.2 "상태 절과 해시", 1.4. 절마다 (절 id u16, 내용). 풀 절은 개수가 0이면 생략. 해시 = State_Write 의 해시 모드
    public sealed class StateSerializationTests
    {
        private static readonly ContentDb Db = WorldKit.EmptyDb();

        // 보관 흐름(CONTRACTS 1.3 의 "세계가 보관")
        private static readonly RngStream[] Held =
        {
            RngStream.WaveSpawn, RngStream.HpShard, RngStream.Event, RngStream.EnemyAi,
            RngStream.Mutation, RngStream.Weather, RngStream.BossAi, RngStream.DuelWild,
        };

        // 스칼라 절(풀 절 5·9·10·11 은 비었으므로 없다)
        private static readonly ushort[] ScalarIdsBeforeRng = { 1, 2, 3, 4, 6, 7, 8, 12, 13, 14, 15 };

        private static byte[] StateBytes(SimWorld world)
        {
            var w = new ByteWriter();
            world.WriteState(w);
            return w.ToArray();
        }

        // 모듈이 하나도 없는 세계의 정규 바이트를 계약에서 직접 조립한다
        private static byte[] ExpectedEmptyWorld(SimMode mode, int players, ulong seed, uint tick, int wave)
        {
            var w = new ByteWriter();
            foreach (ushort id in ScalarIdsBeforeRng)
            {
                w.U16(id);
            }
            w.U16(16);
            w.U8((byte)mode);
            w.U8((byte)players);
            w.U64(seed);
            w.U32(tick);
            w.U8(0);              // is_over
            w.I32(0);             // last_entity_id
            foreach (RngStream s in Held)
            {
                Pcg32 g = RngStreams.Derive(seed, s, wave);
                w.U64(g.State);
                w.U64(g.Inc);
            }
            w.U16(17);
            return w.ToArray();
        }

        [Fact]
        public void EmptyEndlessWorld_WritesScalarSectionsOnly()
        {
            SimWorld world = WorldKit.NewEndless(Db, 42UL);
            Assert.Equal(ExpectedEmptyWorld(SimMode.Endless, 1, 42UL, 0U, 0), StateBytes(world));
        }

        [Fact]
        public void EmptyDuelWorld_WritesScalarSectionsOnly()
        {
            SimWorld world = WorldKit.NewDuel(Db, 7UL);
            var sink = new RecordingSink();
            for (int i = 0; i < 3; i++)
            {
                world.Step(ReadOnlySpan<Wizard.Sim.Input.PlayerInput>.Empty, ReadOnlySpan<Wizard.Sim.Input.RunCommand>.Empty, sink);
            }
            Assert.Equal(ExpectedEmptyWorld(SimMode.Duel, 2, 7UL, 3U, 0), StateBytes(world));
        }

        [Fact]
        public void ComputeStateHash_IsFnv1aOfWriteState()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            world.HeldRng(RngStream.EnemyAi).NextU32();
            world.NewEntityId();
            Assert.Equal(StateHasher.Compute(StateBytes(world)), world.ComputeStateHash());
        }

        [Fact]
        public void WriteState_AppendsToTarget()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            var w = new ByteWriter();
            w.U8(0xEE);
            world.WriteState(w);
            byte[] all = w.ToArray();
            Assert.Equal(0xEE, all[0]);
            Assert.Equal(StateBytes(world), all[1..]);
        }

        [Fact]
        public void WriteThenRead_IntoFreshWorld_GivesSameHashAndBytes()
        {
            SimWorld a = WorldKit.NewEndless(Db, 99UL);
            var sink = new RecordingSink();
            for (int i = 0; i < 37; i++)
            {
                a.Step(ReadOnlySpan<Wizard.Sim.Input.PlayerInput>.Empty, ReadOnlySpan<Wizard.Sim.Input.RunCommand>.Empty, sink);
            }
            a.HeldRng(RngStream.WaveSpawn).NextU32();
            a.HeldRng(RngStream.Mutation).NextU32();
            a.NewEntityId();
            a.NewEntityId();
            a.MarkOver();
            byte[] bytes = StateBytes(a);

            SimWorld b = WorldKit.NewEndless(Db, 99UL);
            Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
            b.ReadState(bytes);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
            Assert.Equal(bytes, StateBytes(b));
            Assert.Equal(37U, b.Tick);
            Assert.True(b.IsOver);
            Assert.Equal(3, b.NewEntityId().Value);
            Assert.Equal(a.HeldRng(RngStream.Mutation).NextU32(), b.HeldRng(RngStream.Mutation).NextU32());
        }

        [Fact]
        public void ReadState_RejectsOtherShapesAndBrokenBytes()
        {
            byte[] endless = StateBytes(WorldKit.NewEndless(Db, 1UL));
            Assert.Throws<SimInvariantException>(() => WorldKit.NewEndless(Db, 2UL).ReadState(endless));    // 다른 씨앗
            Assert.Throws<SimInvariantException>(() => WorldKit.NewDuel(Db, 1UL).ReadState(endless));       // 다른 모드·플레이어 수
            Assert.Throws<SimInvariantException>(() => WorldKit.NewEndless(Db, 1UL).ReadState(endless.AsSpan(0, endless.Length - 1)));

            byte[] trailing = new byte[endless.Length + 1];
            endless.CopyTo(trailing, 0);
            Assert.Throws<SimInvariantException>(() => WorldKit.NewEndless(Db, 1UL).ReadState(trailing));

            byte[] wrongOrder = (byte[])endless.Clone();
            wrongOrder[0] = 2;                                                                            // 첫 절이 Player(1)가 아니다
            Assert.Throws<SimInvariantException>(() => WorldKit.NewEndless(Db, 1UL).ReadState(wrongOrder));
        }

        // 풀 절의 틀: 개수 0이면 바이트도 해시도 남지 않는다. 그래서 풀 모듈을 새로 더해도 그 풀이 빈 세계의 해시는 그대로다
        [Fact]
        public void EmptyPoolSection_LeavesNoBytesAndKeepsHash()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            byte[] before = StateBytes(world);
            ulong hashBefore = world.ComputeStateHash();

            // 새 풀 절(Enemies)을 가진 모듈이 State_Write 에서 하는 일을 그대로 흉내 낸다: 빈 풀이면 TryBeginPoolSection 이 false
            var target = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(target);
            foreach (byte b in before)
            {
                w.U8(b);
            }
            Assert.False(SimWorld.TryBeginPoolSection(ref w, StateSection.Enemies, 0));
            Assert.Equal(before, target.ToArray());
            Assert.Equal(hashBefore, w.Hash);

            StateWriter h = StateWriter.ForHash();
            foreach (byte b in before)
            {
                h.U8(b);
            }
            Assert.False(SimWorld.TryBeginPoolSection(ref h, StateSection.Telegraph, 0));
            Assert.Equal(hashBefore, h.Hash);
        }

        [Fact]
        public void NonEmptyPoolSection_WritesIdAndIsFoundOnRead()
        {
            var target = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(target);
            Assert.True(SimWorld.TryBeginPoolSection(ref w, StateSection.Enemies, 3));
            w.Count(3);
            Assert.Equal(new byte[] { 9, 0, 3, 0, 0, 0 }, target.ToArray());
            Assert.Throws<SimInvariantException>(() =>
            {
                StateWriter x = StateWriter.ForHash();
                SimWorld.TryBeginPoolSection(ref x, StateSection.Enemies, -1);
            });

            // 읽기: 다음 절이 그 풀이면 id 를 먹고 true, 아니면 아무것도 먹지 않고 false
            var r = new StateReader(target.Written);
            Assert.False(SimWorld.TryBeginPoolSection(ref r, StateSection.Spell));
            Assert.Equal(6, r.Remaining);
            Assert.True(SimWorld.TryBeginPoolSection(ref r, StateSection.Enemies));
            Assert.Equal(3, r.Count());
            Assert.False(SimWorld.TryBeginPoolSection(ref r, StateSection.Telegraph));     // 끝
        }

        [Fact]
        public void HeldStreams_AreDerivedFromSeedAndRederivable()
        {
            SimWorld world = WorldKit.NewEndless(Db, 5UL);
            foreach (RngStream s in Held)
            {
                Pcg32 expected = RngStreams.Derive(5UL, s, 0);
                Assert.Equal(expected.State, world.HeldRng(s).State);
            }
            world.HeldRng(RngStream.WaveSpawn).NextU32();
            world.DeriveHeldRng(RngStream.WaveSpawn, 11);
            Assert.Equal(RngStreams.Derive(5UL, RngStream.WaveSpawn, 11).State, world.HeldRng(RngStream.WaveSpawn).State);
            world.DeriveAllHeldRng(21);
            Assert.Equal(RngStreams.Derive(5UL, RngStream.BossAi, 21).State, world.HeldRng(RngStream.BossAi).State);

            // 호출 때 파생하는 흐름은 세계가 보관하지 않는다
            Assert.Throws<SimInvariantException>(() => world.HeldRng(RngStream.Loot));
            Assert.Throws<SimInvariantException>(() => world.DeriveHeldRng(RngStream.WaveComp, 1));
        }

        [Fact]
        public void HeldStreamConsumption_ChangesHash()
        {
            SimWorld world = WorldKit.NewEndless(Db);
            ulong h0 = world.ComputeStateHash();
            world.HeldRng(RngStream.HpShard).NextU32();
            Assert.NotEqual(h0, world.ComputeStateHash());
        }
    }
}
