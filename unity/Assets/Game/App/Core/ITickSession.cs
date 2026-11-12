#nullable enable
using System;
using Wizard.Client;
using Wizard.Sim.Events;

namespace Wizard.Unity.App.Core
{
    // FixedTickDriver 가 프레임마다 부르는 세션의 모양(ARCHITECTURE 6.1). RunSession·PracticeSession 을 감싼다(뒤에 DuelSession 도).
    // 프레임 하나: OnInput(now) → while (NextTickDue(now)) { Step(Tick); Advance(); } → Alpha(now) → Events·View 를 표시 계층에 넘긴다.
    // 시각은 모두 같은 단조 시계(IMonotonicClock)의 값이다. 세션은 틱 번호만 세계에 넣는다(T-27).
    public interface ITickSession
    {
        // 입력원에서 이번 프레임의 키를 꺼내 양자화한다(Drain → Quantize → 대기 줄, 결투면 곧바로 송신)
        void OnInput(IInputSource source, double nowSeconds);

        // 다음 틱(Tick)의 구간이 다 지났고 틱이 멈춰 있지 않으면 참
        bool NextTickDue(double nowSeconds);

        // 다음에 돌 틱 번호
        uint Tick { get; }

        void Step(uint tick);

        // 틱 하나를 셌다. Step 바로 뒤에 부른다
        void Advance();

        // 표시 보간 비율 0~1
        double Alpha(double nowSeconds);

        // 마지막 OnInput 뒤로 세계가 낸 표시 이벤트
        ReadOnlySpan<SimEvent> Events { get; }

        ISimView View { get; }
    }
}
