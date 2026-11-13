using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation
{
    // event_bindings.json 의 한 줄(CONTRACTS 13절). 읽은 뒤 바뀌지 않는다
    public sealed class EventBinding
    {
        public EventBinding(string eventKey, IReadOnlyList<string> fx, IReadOnlyList<string> sfx, BindingShake shake, int hitstopMs, bool confirmedOnly)
        {
            EventKey = eventKey;
            Fx = fx;
            Sfx = sfx;
            Shake = shake;
            HitstopMs = hitstopMs;
            ConfirmedOnly = confirmedOnly;
        }

        public string EventKey { get; }

        public IReadOnlyList<string> Fx { get; }          // effects.md 20절에서 붙인 식별자(fx_catalog)

        public IReadOnlyList<string> Sfx { get; }         // effects.md 19절의 사건 번호(sfx_catalog)

        public BindingShake Shake { get; }

        public int HitstopMs { get; }

        // 참이면 그림도 Confirmed 에서만 낸다. 소리·흔들림·그림 정지는 이 값과 무관하게 Confirmed 에서만 낸다(CONTRACTS 6.4)
        public bool ConfirmedOnly { get; }

        // fx 와 sfx 가 둘 다 비었다: 에셋을 나중에 꽂을 자리
        public bool IsPlaceholder => Fx.Count == 0 && Sfx.Count == 0;
    }
}
