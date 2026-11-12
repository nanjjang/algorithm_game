using System;
using Wizard.Content;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Client.Run
{
    // ARCHITECTURE 5·6.1·10.4, CONTRACTS 3.1·3.8·6.5, T-13. 무한 웨이브 판 하나: 세계(SimWorld) + 기록기(InputRecorder) + 틱 시계(SimClock).
    // 최소판이다. 출발 조립, 중간 지점 올리기, 종료 제출, 미완 판 처리는 M6-CLI-03(Run/Flow)이 이 위에 얹는다.
    //
    // 프레임(FixedTickDriver):
    //   session.OnInput(source, now);                                   // Drain → Quantize → 대기 줄
    //   while (session.Clock.NextTickDue(now)) { session.Step(session.Clock.Tick); session.Clock.Advance(); }
    //   alpha = session.Clock.Alpha(now);  router.Drain(session.Events);  views.Render(session.View, alpha);
    // 또는 같은 일을 하는 RunFrame(source, now).
    //
    // - 세계 설정은 재실행과 같은 SimConfig.ForEndless(db)다(ReplayRunner 가 같은 값으로 다시 돌린다). allowDebugOps 는 오프라인 개발 빌드의
    //   디버그 메뉴만 켠다(CONTRACTS 3.2). 디버그 op 이 든 기록은 재실행이 RecordCorrupt 로 본다(서버 프로필에서는 켜지 않는다).
    // - 머리말의 코어·팩 버전이 지금 돌리는 코어(SimVersion.CoreVersion)·팩(db.DataVersion)과 다르면 만들지 않는다(EndlessSetup: 맞는 코어·팩으로
    //   돌리는지는 호출자가 본다. 클라이언트는 출발 때).
    // - 기록의 end_tick 은 limits.MaxTicks 를 넘지 않는다: 그 틱에 닿으면 시계를 멈춘다. Finish 는 닫은 기록을 InputRecordCodec.TryRead 에
    //   같은 한도로 통과시킨다(서버의 1차 검사와 같은 문. T-49). 통과하지 못하면 이 코드의 결함이라 InvalidOperationException 이다.
    //   한도는 SimLimitsDef(data/sim/limits)의 값이다(RecordLimits).
    public sealed class RunSession
    {
        private readonly SessionCore _core;
        private readonly ReplayLimits _limits;

        public RunSession(ContentDb db, InputRecordHeader header, ReplayLimits limits, double startSeconds, bool allowDebugOps = false)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (header == null || header.Setup == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            EndlessSetup setup = header.Setup;
            if (header.FormatVersion != EndlessSetup.CanonicalFormatVersion)
            {
                throw new ArgumentException("header.FormatVersion is not the canonical format version", nameof(header));
            }
            if (!string.Equals(setup.CoreVersion, SimVersion.CoreVersion, StringComparison.Ordinal))
            {
                throw new ArgumentException("header.Setup.CoreVersion is not the running core", nameof(header));
            }
            if (setup.DataVersion != db.DataVersion)
            {
                throw new ArgumentException("header.Setup.DataVersion is not the loaded pack", nameof(header));
            }
            if (limits.MaxTicks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limits));
            }

            SimConfig config = SimConfig.ForEndless(db);
            config.AllowDebugOps = allowDebugOps;
            var recorder = new InputRecorder(header);     // 담기지 않는 설정이면 여기서 InvalidOperationException
            SimWorld world = SimWorld.CreateEndless(db, config, setup);
            _core = new SessionCore(world, recorder, startSeconds, limits.MaxTicks);
            _limits = limits;
            Header = header;
        }

        // 기록의 한도(T-49: RunSession 은 TryRead 에 SimLimitsDef 의 한도를 준다). 한 틱에 Step 한 번이라 Step 수의 상한도 틱 상한이다
        public static ReplayLimits RecordLimits(ContentDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            uint maxTicks = db.SimLimits.RunMaxTicks;
            return new ReplayLimits(maxTicks, maxTicks);
        }

        public InputRecordHeader Header { get; }

        public ReplayLimits Limits => _limits;

        public SimClock Clock => _core.Clock;

        public ISimView View => _core.View;

        // 마지막 OnInput 뒤로 세계가 낸 표시 이벤트
        public ReadOnlySpan<SimEvent> Events => _core.Events;

        public uint Tick => _core.Tick;

        public bool IsOver => _core.IsOver;

        public bool IsPaused => _core.IsPaused;

        // 틱이 더는 돌지 않는다(판 끝, 틱 상한, Finish)
        public bool IsHalted => _core.IsHalted;

        public bool IsWindowOpen => _core.IsWindowOpen;

        // 세계에 넣은 키로 셈한 논리 키 상태
        public KeyMask WorldKeys => _core.WorldKeys;

        public int RejectedCommandCount => _core.RejectedCommandCount;

        public int PendingCommandCount => _core.PendingCommandCount;

        public int WithheldKeyCount => _core.WithheldKeyCount;

        public int RetimedLateKeyCount => _core.RetimedLateKeyCount;

        public int DroppedOverflowKeyCount => _core.DroppedOverflowKeyCount;

        public int DroppedLateKeyCount => _core.DroppedLateKeyCount;

        // 버린 키 때문에 세계와 입력원의 키 상태가 어긋나 다음 프레임에 낸 차이 이벤트의 수(SessionCore 머리 주석)
        public int DropCorrectionKeyCount => _core.DropCorrectionKeyCount;

        public int PauseCount => _core.PauseCount;

        public long PauseTotalMs => _core.PauseTotalMs;

        public void SetWindowOpen(bool open) => _core.SetWindowOpen(open);

        public void RequestResume() => _core.RequestResume();

        public void Submit(RunCommandOp op, int a = 0, int b = 0) => _core.Submit(new RunCommandDraft(op, a, b));

        public void OnInput(IInputSource source, double nowSeconds) => _core.OnInput(source, nowSeconds);

        public void Step(uint tick) => _core.Step(tick);

        public double RunFrame(IInputSource source, double nowSeconds) => _core.RunFrame(source, nowSeconds);

        public ulong ComputeStateHash() => _core.ComputeStateHash();

        // 중간 지점(spec 09 3.6): 지금(세계의 Tick, 그 틱의 Step 전)의 상태 해시. 언제 부를지는 웨이브 쪽이 정한다(M6)
        public void MarkCheckpoint(int wave)
        {
            _core.RequireOpen();
            _core.Recorder.MarkCheckpoint(wave, _core.Tick, _core.ComputeStateHash());
        }

        // 마지막 중간 지점까지의 완결된 기록(중간 지점 올리기, 원정 정리). 중간 지점이 없으면 InvalidOperationException
        public ReadOnlyMemory<byte> Snapshot() => _core.Recorder.Snapshot();

        // 판의 기록을 닫는다(.wzrc 전체). 꼬리말 = 지금의 세계 Tick 과 상태 해시
        public ReadOnlyMemory<byte> Finish(double nowSeconds)
        {
            ReadOnlyMemory<byte> file = _core.Finish(nowSeconds);
            if (!InputRecordCodec.TryRead(file.Span, _limits, out _, out _, out RecordError error))
            {
                throw new InvalidOperationException("the finished record does not pass InputRecordCodec.TryRead: " + error);
            }
            return file;
        }
    }
}
