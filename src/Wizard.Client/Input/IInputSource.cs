using System;
using Wizard.Sim.Input;

namespace Wizard.Client
{
    // CONTRACTS 6.5, T-28. InputQuantizer·TickInputQueue 는 M1-CLI-01 이 만든다
    public interface IInputSource
    {
        int Drain(Span<RawKeyEvent> buffer);              // 논리 키 상태가 바뀔 때만 이벤트가 생긴다
        KeyMask CurrentKeys { get; }                      // 지금의 논리 키 상태(물리 키에서 온 것). 결투의 키 상태 맞추기(6.4절)
        void SetBindings(KeyBindings bindings);
        void SetDuelPolling(bool on);                     // 결투 중에는 입력 폴링을 프레임과 분리한다
    }
}
