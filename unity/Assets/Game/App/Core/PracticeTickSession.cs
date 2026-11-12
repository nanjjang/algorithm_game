#nullable enable
using System;
using Wizard.Client;
using Wizard.Client.Run;
using Wizard.Sim.Events;

namespace Wizard.Unity.App.Core
{
    // PracticeSession(Wizard.Client.Run)을 FixedTickDriver 에 꽂는 얇은 이음(ARCHITECTURE 6.1). 틱 시계는 세션의 SimClock 이다.
    // resumeOnPauseKey: [임시] 일시정지 화면이 없는 동안 Pause 키로 풀기(PauseKeyResume)
    public sealed class PracticeTickSession : ITickSession, IPausableSession
    {
        private readonly PracticeSession _session;
        private readonly PauseKeyResume? _pauseKey;

        public PracticeTickSession(PracticeSession session, bool resumeOnPauseKey)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _pauseKey = resumeOnPauseKey ? new PauseKeyResume(this) : null;
        }

        public PracticeSession Session => _session;

        public uint Tick => _session.Clock.Tick;

        public ReadOnlySpan<SimEvent> Events => _session.Events;

        public ISimView View => _session.View;

        public bool IsPaused => _session.IsPaused;

        public void OnInput(IInputSource source, double nowSeconds) =>
            _session.OnInput(_pauseKey != null ? _pauseKey.Wrap(source) : source, nowSeconds);

        public bool NextTickDue(double nowSeconds) => _session.Clock.NextTickDue(nowSeconds);

        public void Step(uint tick) => _session.Step(tick);

        public void Advance() => _session.Clock.Advance();

        public double Alpha(double nowSeconds) => _session.Clock.Alpha(nowSeconds);

        public void RequestResume() => _session.RequestResume();
    }
}
