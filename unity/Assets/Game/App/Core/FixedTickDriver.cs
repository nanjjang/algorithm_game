#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Unity.Input;

namespace Wizard.Unity.App.Core
{
    // ARCHITECTURE 6.1 의 프레임 루프. 렌더 프레임마다 한 번 RunFrame 을 부른다(CompositionRoot.Update). 엔진 타입을 쓰지 않는다.
    //
    //   now = 단조 시계(IMonotonicClock: Input System 의 시간 축. 키 이벤트의 시각과 같은 축)
    //   session.OnInput(입력원, now)                       // Drain → Quantize → 대기 줄(결투면 곧바로 송신). 틱 진행과 무관
    //   while (session.NextTickDue(now)) { session.Step(session.Tick); session.Advance(); }
    //   alpha = session.Alpha(now)                         // 실수는 여기서부터
    //   표시기마다 OnEvents(session.Events, view)          // PresentationEventRouter.Drain 자리
    //   표시기마다 Render(view, alpha)
    //
    // - 틱 수는 프레임 수가 아니라 단조 시계가 정한다(SimClock: 따라잡는 틱 수에 상한이 없다). Unity 의 FixedUpdate·Time.deltaTime·
    //   Time.timeScale 을 쓰지 않고 바꾸지도 않는다(ARCHITECTURE 4.1, spec 01 D12). 히트스톱은 표시 계층이 그림만 멈춘다.
    // - 세션이 없는 동안(메뉴 등)에도 입력원을 비운다. 그 키는 버린다(판 밖의 조작이 판의 첫 틱으로 몰려 들어가지 않게).
    // - [임시] 세션을 꽂는 순간(SetSession) 그때까지 쌓인 키를 버리고, 그 순간 눌려 있는 논리 키(CurrentKeys)를 그 시각의 누름으로 만들어 둔다.
    //   세션의 첫 OnInput 은 이 누름들을 먼저 받고, 그 뒤로 꽂은 다음에 생긴 키를 그대로 받는다. 세션은 꽂기 직전에 같은 시계로 만든다.
    //   물리 키와 세계의 키 상태가 처음부터 맞게 하려는 것이다(InputQuantizer 의 "판 시작 전에 눌린 키" 규칙과 같은 뜻). OPEN-QUESTIONS.
    // - 세션이 예외를 던지면 그대로 올린다. 부르는 쪽(CompositionRoot)이 기록하고 세션을 뺀다.
    public sealed class FixedTickDriver
    {
        private const int DiscardChunk = 32;              // 자료구조 크기

        private readonly IMonotonicClock _clock;
        private readonly IInputSource _input;
        private readonly SessionStartInput _start;
        private readonly List<IFramePresenter> _presenters = new List<IFramePresenter>();
        private readonly RawKeyEvent[] _discard = new RawKeyEvent[DiscardChunk];
        private ITickSession? _session;

        public FixedTickDriver(IMonotonicClock clock, IInputSource input)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _start = new SessionStartInput(input);
        }

        public ITickSession? Session => _session;

        public IInputSource Input => _input;

        public long FrameCount { get; private set; }

        // 이 구동기가 돌린 틱 수(모든 세션의 합)
        public long TickCount { get; private set; }

        // 세션 밖이라 버린 키 수(세션이 없는 동안, 그리고 세션을 꽂는 순간 쌓여 있던 것)
        public long DiscardedKeyCount { get; private set; }

        // 세션을 바꾼다(null 이면 뺀다). 다음 RunFrame 부터 돈다. 쌓인 키는 지금 버리고 지금 눌린 키를 지금 시각의 누름으로 둔다
        public void SetSession(ITickSession? session)
        {
            _session = session;
            _start.Disarm();
            if (session != null)
            {
                DiscardInput();
                _start.Arm(_clock.NowSeconds);
            }
        }

        public void AddPresenter(IFramePresenter presenter)
        {
            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }
            if (!_presenters.Contains(presenter))
            {
                _presenters.Add(presenter);
            }
        }

        public bool RemovePresenter(IFramePresenter presenter) => _presenters.Remove(presenter);

        // 프레임 하나. 돌려주는 값 = 표시 보간 비율(세션이 없으면 0)
        public double RunFrame()
        {
            FrameCount++;
            double now = _clock.NowSeconds;
            ITickSession? session = _session;
            if (session == null)
            {
                DiscardInput();
                return 0;
            }

            session.OnInput(_start.IsArmed ? _start : _input, now);
            while (session.NextTickDue(now))
            {
                session.Step(session.Tick);
                session.Advance();
                TickCount++;
            }
            double alpha = session.Alpha(now);

            ISimView view = session.View;
            ReadOnlySpan<SimEvent> events = session.Events;
            for (int i = 0; i < _presenters.Count; i++)
            {
                _presenters[i].OnEvents(events, view);
            }
            for (int i = 0; i < _presenters.Count; i++)
            {
                _presenters[i].Render(view, alpha);
            }
            return alpha;
        }

        private void DiscardInput()
        {
            while (true)
            {
                int n = _input.Drain(_discard);
                DiscardedKeyCount += n;
                if (n < _discard.Length)
                {
                    return;
                }
            }
        }

        // 세션을 꽂은 순간 눌려 있던 논리 키의 누름(번호 오름차순)을 먼저 내고, 이어서 입력원을 그대로 잇는다
        private sealed class SessionStartInput : IInputSource
        {
            private readonly IInputSource _inner;
            private readonly RawKeyEvent[] _held = new RawKeyEvent[KeyState.KeyCount];
            private int _heldCount;
            private int _heldNext;

            public SessionStartInput(IInputSource inner)
            {
                _inner = inner;
            }

            public bool IsArmed => _heldNext < _heldCount;

            public KeyMask CurrentKeys => _inner.CurrentKeys;

            public void Arm(double nowSeconds)
            {
                Disarm();
                int mask = (int)_inner.CurrentKeys;
                for (int k = 0; k < KeyState.KeyCount; k++)
                {
                    if ((mask & (1 << k)) != 0)
                    {
                        _held[_heldCount++] = new RawKeyEvent((LogicalKey)k, true, nowSeconds);
                    }
                }
            }

            public void Disarm()
            {
                _heldCount = 0;
                _heldNext = 0;
            }

            public int Drain(Span<RawKeyEvent> buffer)
            {
                int n = 0;
                while (n < buffer.Length && _heldNext < _heldCount)
                {
                    buffer[n++] = _held[_heldNext++];
                }
                if (n < buffer.Length)
                {
                    n += _inner.Drain(buffer.Slice(n));
                }
                return n;
            }

            public void SetBindings(KeyBindings bindings) => _inner.SetBindings(bindings);

            public void SetDuelPolling(bool on) => _inner.SetDuelPolling(on);
        }
    }
}
