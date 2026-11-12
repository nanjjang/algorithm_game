using System;
using Wizard.Content;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Client.Run
{
    // ARCHITECTURE 6.1: "PracticeSession: RunSession과 같되 기록·제출 없음". 허수아비 연습 세계(SimWorld.CreatePractice) 하나.
    // 봇 결투 연습은 Loopback + 기기 안의 DuelHostCore 라 여기가 아니다(Client.Duel).
    //
    // 순번은 연습에서도 InputRecorder 가 매긴다(CONTRACTS 3.1: 순번을 매기는 곳은 InputRecorder(무한 웨이브·연습)와 DuelClientCore 뿐).
    // 그래서 안에 기록기를 두지만 기록을 밖으로 내지 않는다. 기록기의 머리말은 담기만 하면 되는 자리 값이다(세계는 머리말을 읽지 않는다):
    // 코어·팩 버전은 지금의 것, 나머지는 0·빈 값, 시작 웨이브 1. [임시] OPEN-QUESTIONS(기록 없는 순번 매기기에 머리말이 필요한 것).
    // 일시정지·창·명령의 규칙은 RunSession 과 같다(SessionCore). 틱 상한은 없다(uint 의 끝에서 멈춘다).
    public sealed class PracticeSession
    {
        private readonly SessionCore _core;

        public PracticeSession(ContentDb db, PracticeSetup setup, double startSeconds)
            : this(db, setup, startSeconds, null)
        {
        }

        // basedOn: 연습의 바탕 설정(spec 01 12.1). 없으면 무한 웨이브 설정이다. 모드와 디버그 op 은 SimConfig.ForPractice 가 정한다
        public PracticeSession(ContentDb db, PracticeSetup setup, double startSeconds, SimConfig? basedOn)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            SimConfig config = SimConfig.ForPractice(db, basedOn ?? SimConfig.ForEndless(db));
            SimWorld world = SimWorld.CreatePractice(db, config, setup);
            _core = new SessionCore(world, new InputRecorder(PlaceholderHeader(db)), startSeconds, uint.MaxValue);
        }

        public SimClock Clock => _core.Clock;

        public ISimView View => _core.View;

        public ReadOnlySpan<SimEvent> Events => _core.Events;

        public uint Tick => _core.Tick;

        public bool IsPaused => _core.IsPaused;

        public bool IsHalted => _core.IsHalted;

        public bool IsWindowOpen => _core.IsWindowOpen;

        public KeyMask WorldKeys => _core.WorldKeys;

        public int RejectedCommandCount => _core.RejectedCommandCount;

        public int WithheldKeyCount => _core.WithheldKeyCount;

        public void SetWindowOpen(bool open) => _core.SetWindowOpen(open);

        public void RequestResume() => _core.RequestResume();

        public void Submit(RunCommandOp op, int a = 0, int b = 0) => _core.Submit(new RunCommandDraft(op, a, b));

        public void OnInput(IInputSource source, double nowSeconds) => _core.OnInput(source, nowSeconds);

        public void Step(uint tick) => _core.Step(tick);

        public double RunFrame(IInputSource source, double nowSeconds) => _core.RunFrame(source, nowSeconds);

        public ulong ComputeStateHash() => _core.ComputeStateHash();

        // 연습을 끝낸다. 기록은 버린다
        public void End(double nowSeconds) => _core.Finish(nowSeconds);

        private static InputRecordHeader PlaceholderHeader(ContentDb db) => new InputRecordHeader
        {
            FormatVersion = EndlessSetup.CanonicalFormatVersion,
            Setup = new EndlessSetup
            {
                Seed = 0,
                Kind = RunKind.Normal,
                StartWave = 1,
                Wand = new WandSnapshot
                {
                    Character = CharacterId.Baseline,
                    Mutations = Array.Empty<MutationId>(),
                    Links = Array.Empty<BlockId>(),
                },
                CoreVersion = SimVersion.CoreVersion,
                DataVersion = db.DataVersion,
            },
        };
    }
}
