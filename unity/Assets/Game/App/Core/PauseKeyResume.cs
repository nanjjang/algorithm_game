#nullable enable
using System;
using Wizard.Client;
using Wizard.Sim.Input;

namespace Wizard.Unity.App.Core
{
    // [임시] 일시정지 화면(M9-TUT-02)이 생기기 전의 자리(OPEN-QUESTIONS). 일시정지를 푸는 것은 원래 일시정지 메뉴다(SessionCore 머리 주석).
    // 멈춰 있는 동안 Pause 키를 눌렀다 떼면 풀기를 청한다. 멈추게 한 그 누름의 뗌으로는 풀지 않는다(멈춘 뒤에 새로 누른 것만 센다).
    // 세션의 OnInput 이 키를 꺼낼 때 그 키를 엿보기만 한다. 키는 바꾸지 않고 그대로 세션에 넘긴다. 일시정지 화면이 생기면 이것을 끈다.
    public sealed class PauseKeyResume : IInputSource
    {
        private readonly IPausableSession _session;
        private IInputSource? _inner;
        private bool _pressedWhilePaused;

        public PauseKeyResume(IPausableSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        // 이번 프레임의 입력원을 감싼다. 돌려주는 값을 세션의 OnInput 에 준다
        public IInputSource Wrap(IInputSource inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            return this;
        }

        public KeyMask CurrentKeys => Inner.CurrentKeys;

        public int Drain(Span<RawKeyEvent> buffer)
        {
            int n = Inner.Drain(buffer);
            for (int i = 0; i < n; i++)
            {
                if (!_session.IsPaused)
                {
                    _pressedWhilePaused = false;
                    continue;
                }
                RawKeyEvent e = buffer[i];
                if (e.Key != LogicalKey.Pause)
                {
                    continue;
                }
                if (e.Down)
                {
                    _pressedWhilePaused = true;
                }
                else if (_pressedWhilePaused)
                {
                    _pressedWhilePaused = false;
                    _session.RequestResume();
                }
            }
            if (!_session.IsPaused)
            {
                _pressedWhilePaused = false;
            }
            return n;
        }

        public void SetBindings(KeyBindings bindings) => Inner.SetBindings(bindings);

        public void SetDuelPolling(bool on) => Inner.SetDuelPolling(on);

        private IInputSource Inner => _inner ?? throw new InvalidOperationException("PauseKeyResume.Wrap was not called");
    }
}
