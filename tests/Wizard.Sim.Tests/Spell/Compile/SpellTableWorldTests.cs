using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;
using Wizard.Sim.Spell;
using Wizard.Sim.Spell.Compile;
using Wizard.Sim.Tests.Runtime;
using Xunit;
using static Wizard.Sim.Tests.Spell.Compile.CompileKit;

namespace Wizard.Sim.Tests.Spell.Compile
{
    // 수락 조건 "SpellTable 이 세계 상태다"(CONTRACTS 3.2, DECISIONS-TECH T-57)를 세계(SimWorld)에서 본다:
    // 핸들은 추가 순서의 색인이고 세계 상태는 정수뿐이다. 길이가 스냅숏에, 길이·원천·현재 핸들이 WriteState 와 해시에 들어간다.
    // 새 핸들의 문은 세계 생성과 Step 0단계뿐이고 실행 경로는 컴파일하지도 할당하지도 않는다.
    public sealed class SpellTableWorldTests
    {
        private static readonly CompileSource A = Dalgu(Bolt, L(Split3, Trig, Fireball, __));
        private static readonly CompileSource B = Dalgu(Fireball, L(Bolt, __, __, __));
        private static readonly CompileSource C = Dalgu(Ice, L(Size, Pierce, __, __));
        private static readonly CompileSource Invalid = Dalgu(__, L(__, __, __, __));

        private static SimWorld NewWorld(ulong seed = 7UL) =>
            SimWorld.CreateEndless(Db, SimConfig.ForEndless(Db), new EndlessSetup { Seed = seed, StartWave = 1 });

        private static SpellHandle AddAtCreation(SimWorld w, CompileSource s) => w.AddSpellAtCreation(s.Core, s.Links, s.Profile);

        // 0단계의 문: 지금 틱에 적용 중인 명령을 손에 든 쪽(Stage00 훅)만 연다. 조립이 확정되는 명령은 Depart 다
        private static RunCommand DepartNow(SimWorld w, uint seq = 1U) => WorldKit.Cmd(w.Tick, seq, RunCommandOp.Depart);

        private static SpellHandle AddInStageZero(SimWorld w, CompileSource s)
        {
            RunCommand c = DepartNow(w);
            return w.AddSpell(in c, s.Core, s.Links, s.Profile);
        }

        private static void StepIdle(SimWorld w, int ticks)
        {
            var sink = new RecordingSink();
            for (int i = 0; i < ticks; i++)
            {
                sink.Clear();
                w.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
            }
        }

        private static byte[] State(SimWorld w)
        {
            var bytes = new ByteWriter();
            w.WriteState(bytes);
            return bytes.ToArray();
        }

        [Fact]
        public void Handle_IsAddOrderIndex_SameSourceSameHandle()
        {
            SimWorld w = NewWorld();
            Assert.True(w.SpellTableOn);
            Assert.Equal(0, w.SpellCount);
            Assert.Equal(0, AddAtCreation(w, A).Value);
            Assert.Equal(1, AddAtCreation(w, B).Value);
            Assert.Equal(0, AddAtCreation(w, A).Value);                 // 같은 원천(바이트가 같다)은 있던 핸들
            Assert.Equal(2, w.AddSpellAtCreation(C.Core, C.Links, C.Profile).Value);
            Assert.Equal(3, w.SpellCount);
            // 표에 든 것은 컴파일 결과 그대로다(세계 밖의 컴파일러와 같은 순수 함수)
            Assert.Equal(Print(Build(A)), Print(w.SpellAt(new SpellHandle(0))));
            Assert.Equal(Build(C).Hash, w.SpellAt(new SpellHandle(2)).Hash);
            Assert.Throws<SimInvariantException>(() => w.SpellAt(new SpellHandle(3)));
            Assert.Throws<SimInvariantException>(() => w.SpellAt(new SpellHandle(-1)));
        }

        [Fact]
        public void StateHash_CoversLengthSourcesOrderAndActiveHandle()
        {
            SimWorld empty = NewWorld();
            SimWorld a = NewWorld();
            AddAtCreation(a, A);
            SimWorld ab = NewWorld();
            AddAtCreation(ab, A);
            AddAtCreation(ab, B);
            SimWorld ba = NewWorld();
            AddAtCreation(ba, B);
            AddAtCreation(ba, A);
            SimWorld ac = NewWorld();
            AddAtCreation(ac, A);
            AddAtCreation(ac, C);
            ulong[] hashes =
            {
                empty.ComputeStateHash(), a.ComputeStateHash(), ab.ComputeStateHash(), ba.ComputeStateHash(), ac.ComputeStateHash(),
            };
            for (int i = 0; i < hashes.Length; i++)
            {
                for (int k = i + 1; k < hashes.Length; k++)
                {
                    Assert.NotEqual(hashes[i], hashes[k]);
                }
            }
            // 같은 표, 현재 핸들만 다르다
            SimWorld ab2 = NewWorld();
            AddAtCreation(ab2, A);
            AddAtCreation(ab2, B);
            Assert.Equal(ab.ComputeStateHash(), ab2.ComputeStateHash());
            ab.SetActiveSpellAtCreation(new PlayerSlot(0), new SpellHandle(0));
            ab2.SetActiveSpellAtCreation(new PlayerSlot(0), new SpellHandle(1));
            Assert.NotEqual(ab.ComputeStateHash(), ab2.ComputeStateHash());
        }

        // 수락 조건: 스냅숏 뒤 새 핸들 생성 → 복원 → 다른 순서로 다시 생성해도 해시가 같다(같은 순서로 만든 세계와)
        [Fact]
        public void Snapshot_NewHandles_Restore_ReAddInAnotherOrder_SameHashAsAWorldThatAddedInThatOrder()
        {
            SimWorld w = NewWorld();
            w.SetActiveSpellAtCreation(new PlayerSlot(0), AddAtCreation(w, A));
            StepIdle(w, 3);
            SimSnapshot snap = w.CreateSnapshotBuffer();
            w.SaveSnapshot(snap);
            ulong atSave = w.ComputeStateHash();

            Assert.Equal(1, AddInStageZero(w, B).Value);
            Assert.Equal(2, AddInStageZero(w, C).Value);
            ulong bThenC = w.ComputeStateHash();
            Assert.NotEqual(atSave, bThenC);

            w.LoadSnapshot(snap);
            Assert.Equal(1, w.SpellCount);                               // 길이가 스냅숏에 들어 있다
            Assert.Equal(atSave, w.ComputeStateHash());
            Assert.Throws<SimInvariantException>(() => w.SpellAt(new SpellHandle(1)));

            Assert.Equal(1, AddInStageZero(w, C).Value);                  // 다른 순서: C 가 먼저
            Assert.Equal(2, AddInStageZero(w, B).Value);
            ulong cThenB = w.ComputeStateHash();
            Assert.NotEqual(bThenC, cThenB);                             // 순서가 상태다

            SimWorld fresh = NewWorld();
            fresh.SetActiveSpellAtCreation(new PlayerSlot(0), AddAtCreation(fresh, A));
            StepIdle(fresh, 3);
            AddInStageZero(fresh, C);
            AddInStageZero(fresh, B);
            Assert.Equal(fresh.ComputeStateHash(), cThenB);
            Assert.Equal(State(fresh), State(w));

            // 되감은 뒤 같은 순서로 다시 채우면 저장하지 않고 진행한 것과 같다
            w.LoadSnapshot(snap);
            AddInStageZero(w, B);
            AddInStageZero(w, C);
            Assert.Equal(bThenC, w.ComputeStateHash());
            StepIdle(w, 5);
            StepIdle(fresh, 5);
            Assert.NotEqual(fresh.ComputeStateHash(), w.ComputeStateHash());
        }

        // 수락 조건: WriteState → 새 세계 ReadState 뒤 모든 핸들이 실행된다(원천에서 다시 컴파일)
        [Fact]
        public void WriteState_ReadStateIntoANewWorld_EveryHandleIsCompiledAgain()
        {
            SimWorld w = NewWorld();
            AddAtCreation(w, A);
            SpellHandle hb = AddAtCreation(w, B);
            AddAtCreation(w, C);
            AddAtCreation(w, Invalid);                                   // 무효 조립도 원천으로 남는다
            w.SetActiveSpellAtCreation(new PlayerSlot(0), hb);
            StepIdle(w, 10);
            byte[] state = State(w);

            SimWorld other = NewWorld();
            AddAtCreation(other, C);                                     // 읽기는 표 전체를 덮어쓴다
            other.ReadState(state);
            Assert.Equal(4, other.SpellCount);
            for (int i = 0; i < 4; i++)
            {
                CompiledSpell x = w.SpellAt(new SpellHandle(i)), y = other.SpellAt(new SpellHandle(i));
                Assert.NotSame(x, y);                                    // 객체는 세계마다 따로 만든 캐시다
                Assert.Equal(x.Hash, y.Hash);
                Assert.Equal(x.IsValid, y.IsValid);
                Assert.Equal(Print(x), Print(y));
            }
            Assert.False(other.SpellAt(new SpellHandle(3)).IsValid);
            Assert.True(other.TryGetActiveSpell(0, out SpellHandle active));
            Assert.Equal(hb.Value, active.Value);
            Assert.Equal(w.ComputeStateHash(), other.ComputeStateHash());
            Assert.Equal(state, State(other));
            // 받은 세계도 같은 원천에는 같은 핸들을 준다
            Assert.Equal(2, AddInStageZero(other, C).Value);
            StepIdle(w, 20);
            StepIdle(other, 20);
            Assert.Equal(w.ComputeStateHash(), other.ComputeStateHash());
        }

        [Fact]
        public void NewHandles_OnlyAtCreationAndStageZero()
        {
            SimWorld w = NewWorld();
            SpellHandle a = AddAtCreation(w, A);                         // 세계 생성: 첫 Step 전
            w.SetActiveSpellAtCreation(new PlayerSlot(0), a);
            StepIdle(w, 1);
            // 첫 Step 뒤에는 생성의 문이 닫힌다
            Assert.Throws<SimInvariantException>(() => AddAtCreation(w, B));
            Assert.Throws<SimInvariantException>(() => w.SetActiveSpellAtCreation(new PlayerSlot(0), a));
            Assert.Equal(1, w.SpellCount);
            // 0단계의 문: 지금 틱의 명령이어야 한다(지난 틱·다음 틱의 명령, 이 세계에 없는 슬롯은 안 된다)
            RunCommand stale = WorldKit.Cmd(w.Tick - 1U, 1U, RunCommandOp.Depart);
            RunCommand early = WorldKit.Cmd(w.Tick + 1U, 1U, RunCommandOp.Depart);
            RunCommand otherSlot = WorldKit.Cmd(w.Tick, 1U, RunCommandOp.Depart, slot: 1);
            Assert.Throws<SimInvariantException>(() => w.AddSpell(in stale, B.Core, B.Links, B.Profile));
            Assert.Throws<SimInvariantException>(() => w.AddSpell(in early, B.Core, B.Links, B.Profile));
            Assert.Throws<SimInvariantException>(() => w.AddSpell(in otherSlot, B.Core, B.Links, B.Profile));
            Assert.Throws<SimInvariantException>(() => w.SetActiveSpell(in stale, new PlayerSlot(0), a));
            Assert.Equal(1, w.SpellCount);
            RunCommand now = DepartNow(w);
            SpellHandle b = w.AddSpell(in now, B.Core, B.Links, B.Profile);
            w.SetActiveSpell(in now, new PlayerSlot(0), b);
            Assert.Equal(1, b.Value);
            Assert.True(w.TryGetActiveSpell(0, out SpellHandle active));
            Assert.Equal(1, active.Value);
            // 표에 없는 핸들은 현재 주문이 될 수 없다
            Assert.Throws<SimInvariantException>(() => w.SetActiveSpell(in now, new PlayerSlot(0), new SpellHandle(2)));
            Assert.Throws<SimInvariantException>(() => w.SetActiveSpell(in now, new PlayerSlot(3), b));
        }

        // 수락 조건: 5~10단계의 실행 경로에서 컴파일이 일어나지 않는다(할당 0). 실행 경로가 쓰는 것은 SpellAt·TryGetActiveSpell 이다
        [Fact]
        public void ExecutionPath_ReadsHandles_WithoutCompilingOrAllocating()
        {
            SimWorld w = NewWorld();
            SpellHandle a = AddAtCreation(w, A);
            AddAtCreation(w, B);
            w.SetActiveSpellAtCreation(new PlayerSlot(0), a);
            var sink = new RecordingSink();
            SimSnapshot snap = w.CreateSnapshotBuffer();
            CompiledSpell before = w.SpellAt(a);

            long Run()
            {
                long start = GC.GetAllocatedBytesForCurrentThread();
                long sum = 0L;
                for (int i = 0; i < 200; i++)
                {
                    sink.Clear();
                    w.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, sink);
                    w.TryGetActiveSpell(0, out SpellHandle h);
                    CompiledSpell s = w.SpellAt(h);
                    sum += s.ExecutedCount(false) + s.ExecutedCount(true) + s.Scopes[0].FanOffsetCenti(0);
                    sum += (long)(w.ComputeStateHash() & 1UL);
                    w.SaveSnapshot(snap);
                    w.LoadSnapshot(snap);
                }
                Assert.True(sum != 0L);
                return GC.GetAllocatedBytesForCurrentThread() - start;
            }

            Run();                                                       // 데우기(JIT, 지연 초기화)
            // 병렬로 도는 다른 시험의 일회성 할당이 이 스레드에 잡힐 수 있어 같은 구간을 여러 번 재고 최솟값을 본다.
            // 경로가 할당하면 모든 실행에서 0보다 크다
            long min = long.MaxValue;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                min = Math.Min(min, Run());
            }
            Assert.Equal(0L, min);
            Assert.Equal(2, w.SpellCount);
            Assert.Same(before, w.SpellAt(a));                           // 다시 컴파일하지 않았다
        }

        // 현재 주문이 정해지면 열 모듈에 그 주문의 발사 열과 간격이 들어간다(spec 02 7.2 "과열 명세로 넘기는 값")
        [Fact]
        public void ActiveSpell_ArmsHeat_WithFireHeatAndCoreInterval()
        {
            SimWorld w = NewWorld();
            Assert.False(w.TryGetActiveSpell(0, out _));
            SpellHandle a = AddAtCreation(w, A);
            w.SetActiveSpellAtCreation(new PlayerSlot(0), a);
            HeatShotProfile shot = w.HeatAt(0).Shot;
            Assert.True(shot.Armed);
            Assert.Equal(Bolt, shot.Core);
            Assert.Equal(HuTenths(268), shot.HeatHu);                    // TV-D06 의 조립: 26.8
            Assert.Equal(9, shot.IntervalTicks);                         // 마력탄의 간격(TV-A06)

            // 거울상 변이: J 발사 1회의 열은 x1.5(spec 02 HT-7)
            SimWorld m = NewWorld();
            CompileSource mirrored = Dalgu(Bolt, L(__, __, __, __), mutations: Mut(MutationId.MirrorImage));
            m.SetActiveSpellAtCreation(new PlayerSlot(0), AddAtCreation(m, mirrored));
            Assert.Equal(Hu(6), m.HeatAt(0).Shot.HeatHu);

            // 무효 조립이 현재 주문이면 발사 주문을 비운다
            RunCommand now = DepartNow(w);
            SpellHandle bad = w.AddSpell(in now, Invalid.Core, Invalid.Links, Invalid.Profile);
            w.SetActiveSpell(in now, new PlayerSlot(0), bad);
            Assert.False(w.HeatAt(0).Shot.Armed);
            SpellHandle b = w.AddSpell(in now, B.Core, B.Links, B.Profile);
            w.SetActiveSpell(in now, new PlayerSlot(0), b);
            Assert.True(w.HeatAt(0).Shot.Armed);
            Assert.Equal(Fireball, w.HeatAt(0).Shot.Core);
            Assert.Equal(Hu(22), w.HeatAt(0).Shot.HeatHu);               // 18 + 4
            Assert.Equal(36, w.HeatAt(0).Shot.IntervalTicks);
        }

        // 스냅숏은 같은 표의 앞머리일 때만 실린다(결투의 확정 세계 → 예측 세계). 다른 표의 스냅숏이면 핸들이 다른 주문을 가리키게 된다
        [Fact]
        public void Snapshot_AcrossWorlds_NeedsTheSameTablePrefix()
        {
            SimWorld confirmed = NewWorld();
            AddAtCreation(confirmed, A);
            SimWorld predicted = NewWorld();
            AddAtCreation(predicted, A);
            AddAtCreation(predicted, B);
            SimSnapshot snap = confirmed.CreateSnapshotBuffer();
            confirmed.SaveSnapshot(snap);
            predicted.LoadSnapshot(snap);
            Assert.Equal(1, predicted.SpellCount);
            Assert.Equal(confirmed.ComputeStateHash(), predicted.ComputeStateHash());

            SimWorld stranger = NewWorld();
            AddAtCreation(stranger, B);
            Assert.Throws<SimInvariantException>(() => stranger.LoadSnapshot(snap));
            SimWorld shorter = NewWorld();
            Assert.Throws<SimInvariantException>(() => shorter.LoadSnapshot(snap));   // 표가 스냅숏보다 짧다
        }

        [Fact]
        public void Duel_EachSlotHasItsOwnActiveHandle_AndItSurvivesStateAndSnapshot()
        {
            SimWorld NewDuel() => WorldKit.NewDuel(Db);
            SimWorld w = NewDuel();
            SpellHandle a = AddAtCreation(w, A), b = AddAtCreation(w, B);
            w.SetActiveSpellAtCreation(new PlayerSlot(0), b);
            w.SetActiveSpellAtCreation(new PlayerSlot(1), a);
            Assert.True(w.TryGetActiveSpell(0, out SpellHandle s0));
            Assert.True(w.TryGetActiveSpell(1, out SpellHandle s1));
            Assert.Equal((1, 0), (s0.Value, s1.Value));
            Assert.Throws<SimInvariantException>(() => w.TryGetActiveSpell(2, out _));

            SimSnapshot snap = w.CreateSnapshotBuffer();
            w.SaveSnapshot(snap);
            SimWorld other = NewDuel();
            other.ReadState(State(w));
            Assert.True(other.TryGetActiveSpell(0, out SpellHandle o0));
            Assert.True(other.TryGetActiveSpell(1, out SpellHandle o1));
            Assert.Equal((1, 0), (o0.Value, o1.Value));
            Assert.Equal(w.ComputeStateHash(), other.ComputeStateHash());

            w.SetActiveSpellAtCreation(new PlayerSlot(0), a);
            Assert.NotEqual(other.ComputeStateHash(), w.ComputeStateHash());
            w.LoadSnapshot(snap);
            Assert.True(w.TryGetActiveSpell(0, out SpellHandle back));
            Assert.Equal(1, back.Value);
            Assert.Equal(other.ComputeStateHash(), w.ComputeStateHash());
        }

        // 블록이 없는 팩(뼈대 시험의 빈 팩)에서는 모듈이 꺼진다: 절의 내용이 비고 문은 닫혀 있다
        [Fact]
        public void PackWithoutBlocks_ModuleIsOff()
        {
            ContentDb emptyDb = WorldKit.EmptyDb();
            SimWorld w = WorldKit.NewEndless(emptyDb);
            Assert.False(w.SpellTableOn);
            Assert.Equal(0, w.SpellCount);
            Assert.Throws<SimInvariantException>(() => w.AddSpellAtCreation(A.Core, A.Links, A.Profile));
            SimWorld other = WorldKit.NewEndless(emptyDb);
            other.ReadState(State(w));
            Assert.Equal(w.ComputeStateHash(), other.ComputeStateHash());
        }

        [Fact]
        public void TwoWorlds_DoNotShareATable()
        {
            SimWorld x = NewWorld(), y = NewWorld();
            AddAtCreation(x, A);
            Assert.Equal(1, x.SpellCount);
            Assert.Equal(0, y.SpellCount);
            Assert.Equal(0, AddAtCreation(y, B).Value);
            Assert.Equal(Print(Build(A)), Print(x.SpellAt(new SpellHandle(0))));
            Assert.Equal(Print(Build(B)), Print(y.SpellAt(new SpellHandle(0))));
        }
    }
}
