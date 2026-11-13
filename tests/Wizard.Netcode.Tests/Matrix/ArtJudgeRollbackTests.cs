using System.Collections.Generic;
using System.Linq;
using Wizard.Content;
using Wizard.Netcode.Tests.Duel.Client;
using Wizard.Netcode.Tests.Duel.Host;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Matrix
{
    // 수락 조건 8("내 K 직전에 상대의 적중으로 결정이 깨지는 확정이 늦게 도착하는 사례에서, 다시 돌린 판정이 호스트와 같다.
    // 판정기 상태가 복원 대상이다", CONTRACTS 6.4 끝)의 지금 시험할 수 있는 부분.
    //
    // 지금 결투 세계에는 판정기가 없다(시제품 폴더의 판정기는 연습에서만 켜진다. 결투의 Arts 훅은 M5-ART-01).
    // 연습의 지갑은 무한이라(시제품의 결정 지갑, GDD 13) "결정이 깨진다"도 만들 수 없다. 그래서 이 파일은 같은 성질을 판정기가 켜진 연습 세계에서 잰다:
    // 늦게 확정된 입력 때문에 예측과 판정기 상태(재사용 대기, 보관된 K, 커서·방향 시각)가 갈린 시간선을 스냅숏(또는 상태 바이트)으로
    // 되감아 확정 입력으로 다시 돌리면, 판정 이벤트와 틱마다의 상태 해시가 처음부터 확정 입력으로 돌린 세계(= 호스트)와 같다.
    // 결투 세계에서 결정이 깨지는 사례 그 자체는 M2-NET-06 결과 보고의 '확인하지 못한 것'과 OPEN-QUESTIONS 에 올렸다.
    public sealed class ArtJudgeRollbackTests
    {
        private const uint End = 40U;

        // 판정 이벤트(섬 간격 보고, 보관, 발동, 불발)
        private static readonly SimEventType[] JudgeTypes =
        {
            SimEventType.PracticeGapReport, SimEventType.ArtKBuffered, SimEventType.ArtCast, SimEventType.ArtCastDebt,
            SimEventType.ArtFizzleNoCrystal, SimEventType.ArtFizzleLate,
        };

        // 재사용 대기: 확정 열에는 틱 1 의 K(발동, 대기 끝 25틱)가 있지만 예측에는 늦게 와서 없다.
        // 예측은 틱 20 의 K 를 바로 발동으로 판정하고, 확정은 그 K 를 보관했다가 대기 끝(틱 25)에 낸다
        [Fact]
        public void RollbackOverCooldownRejudgesBufferedKLikeHost()
        {
            var truth = new List<InputEvent>
            {
                Ev(1, 0, LogicalKey.Skill, true), Ev(2, 0, LogicalKey.Skill, false),
                Ev(20, 128, LogicalKey.Skill, true), Ev(21, 0, LogicalKey.Skill, false),
            };
            List<InputEvent> predicted = truth.Where(e => e.Tick >= 20U).ToList();
            Run host = Straight(truth);
            Run rolled = RollbackViaSnapshot(1U, predicted, 24U, truth);

            Assert.Equal(new uint[] { 1, 25 }, host.Of(SimEventType.ArtCast));
            Assert.Equal(new uint[] { 20 }, host.Of(SimEventType.ArtKBuffered));
            Assert.Equal(new uint[] { 20 }, rolled.PredictedOf(SimEventType.ArtCast));   // 예측의 판정은 정말로 달랐다
            AssertSameAsHost(host, rolled, 1U);
        }

        // 커서: 자세 + 방향을 넣은 뒤(틱 20) 스냅숏. 예측에는 확정에서 버려진 K(틱 21)가 끼어 커서를 비우고 대기를 걸었다.
        // 되감아 확정 열(틱 23 의 K)로 다시 돌리면 방향 시각과 커서가 살아 있어 호스트와 같은 기술·섬 간격으로 판정된다
        [Fact]
        public void RollbackMidCommandRestoresCursorAndDirTime()
        {
            var truth = new List<InputEvent>
            {
                Ev(10, 0, LogicalKey.Stance, true),
                Ev(20, 37, LogicalKey.Down, true),
                Ev(23, 37, LogicalKey.Skill, true), Ev(24, 0, LogicalKey.Skill, false),
            };
            List<InputEvent> predicted = truth.Where(e => e.Tick >= 21U).ToList();
            predicted.Insert(0, Ev(21, 0, LogicalKey.Skill, true));
            predicted.Insert(1, Ev(22, 0, LogicalKey.Skill, false));
            Run host = Straight(truth);
            Run rolled = RollbackViaSnapshot(21U, predicted, 30U, truth);

            SimEvent gap = host.Events.Single(e => e.Type == SimEventType.PracticeGapReport);
            Assert.Equal(768L, gap.V);                                                // 3틱 = 768 입력 단위
            Assert.NotEqual(
                Summ(rolled.Predicted.Where(e => e.Type == SimEventType.PracticeGapReport)),
                Summ(host.Events.Where(e => e.Type == SimEventType.PracticeGapReport)));
            AssertSameAsHost(host, rolled, 21U);
        }

        // 재접속·해시 불일치 복구의 길(WriteState → ReadState)로 되감아도 같다
        [Fact]
        public void RollbackViaStateBytesRejudgesLikeHost()
        {
            var truth = new List<InputEvent>
            {
                Ev(1, 0, LogicalKey.Skill, true), Ev(2, 0, LogicalKey.Skill, false),
                Ev(10, 0, LogicalKey.Stance, true),
                Ev(20, 128, LogicalKey.Skill, true), Ev(21, 0, LogicalKey.Skill, false),
            };
            List<InputEvent> predicted = truth.Where(e => e.Tick >= 10U).ToList();
            Run host = Straight(truth);

            SimWorld w = NewWorld();
            var sink = new ListSink();
            StepRange(w, new List<InputEvent>(), 1U, sink);
            var bytes = new Wizard.Core.ByteWriter();
            w.WriteState(bytes);
            byte[] saved = bytes.ToArray();
            var predSink = new ListSink();
            StepRange(w, predicted, 24U, predSink);
            Assert.Contains(predSink.Events, e => e.Type == SimEventType.ArtCast && e.Tick == 20U);
            w.ReadState(saved);
            var rolled = new Run();
            StepRange(w, truth, End, rolled.Sink, rolled.Hashes);
            rolled.Predicted = predSink.Events;
            AssertSameAsHost(host, rolled, 1U);
        }

        // ── 도구

        private sealed class Run
        {
            public readonly ListSink Sink = new ListSink();
            public readonly Dictionary<uint, ulong> Hashes = new Dictionary<uint, ulong>();
            public List<SimEvent> Predicted = new List<SimEvent>();

            public List<SimEvent> Events => Sink.Events;

            public uint[] Of(SimEventType t) => Events.Where(e => e.Type == t).Select(e => e.Tick).ToArray();

            public uint[] PredictedOf(SimEventType t) => Predicted.Where(e => e.Type == t).Select(e => e.Tick).ToArray();
        }

        // 순번은 상태와 해시에 들지 않는다(CLAUDE.md 3절). 이 파일의 입력은 (틱, 오프셋)이 겹치지 않으므로 그 값으로 준다
        private static InputEvent Ev(uint tick, byte offset, LogicalKey key, bool down) => new InputEvent(tick, offset, key, down, tick * 256U + offset);

        private static SimWorld NewWorld()
        {
            ContentDb db = HostKit.Db;
            return SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());
        }

        // 처음부터 확정 입력으로 돌린 세계(호스트와 같다)
        private static Run Straight(List<InputEvent> truth)
        {
            var run = new Run();
            StepRange(NewWorld(), truth, End, run.Sink, run.Hashes);
            return run;
        }

        // from 틱 앞에서 스냅숏 → 예측 입력으로 predictedEnd 까지 → 되감기 → 확정 입력으로 End 까지
        private static Run RollbackViaSnapshot(uint from, List<InputEvent> predicted, uint predictedEnd, List<InputEvent> truth)
        {
            SimWorld w = NewWorld();
            StepRange(w, truth.Where(e => e.Tick < from).ToList(), from, new ListSink());
            SimSnapshot snap = w.CreateSnapshotBuffer();
            w.SaveSnapshot(snap);
            var predSink = new ListSink();
            StepRange(w, predicted, predictedEnd, predSink);
            Assert.NotEqual(Straight(truth).Hashes[predictedEnd], w.ComputeStateHash());   // 예측이 정말로 갈라졌다(판정기 상태가 해시에 든다)
            w.LoadSnapshot(snap);
            var run = new Run { Predicted = predSink.Events };
            StepRange(w, truth, End, run.Sink, run.Hashes);
            return run;
        }

        private static void StepRange(SimWorld w, List<InputEvent> events, uint end, ListSink sink, Dictionary<uint, ulong>? hashes = null)
        {
            while (w.Tick < end)
            {
                PlayerInput[] inputs = events.Where(e => e.Tick == w.Tick).Select(e => new PlayerInput(HostKit.S0, e)).ToArray();
                InputOrder.Sort(inputs);
                w.Step(inputs, System.ReadOnlySpan<RunCommand>.Empty, sink);
                if (hashes != null)
                {
                    hashes[w.Tick] = w.ComputeStateHash();
                }
            }
        }

        private static void AssertSameAsHost(Run host, Run rolled, uint from)
        {
            Assert.Equal(
                Summ(host.Events.Where(e => e.Tick >= from && JudgeTypes.Contains(e.Type))),
                Summ(rolled.Events.Where(e => JudgeTypes.Contains(e.Type))));
            Assert.Equal(
                Summ(host.Events.Where(e => e.Tick >= from)),
                Summ(rolled.Events));
            Assert.NotEmpty(rolled.Hashes);
            foreach (KeyValuePair<uint, ulong> h in rolled.Hashes)
            {
                Assert.True(host.Hashes[h.Key] == h.Value, $"state hash differs from host at tick {h.Key}");
            }
        }

        private static string Summ(IEnumerable<SimEvent> events) =>
            string.Join(";", events.Select(e => $"{e.Type}@{e.Tick}/{e.Slot.Value}:{e.A},{e.B},{e.C},{e.V},{e.X},{e.Y}"));
    }
}
