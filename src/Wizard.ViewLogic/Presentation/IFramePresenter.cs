using System;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13(T-54). 프레임의 끝에서 표시 계층이 받는 것(ARCHITECTURE 6.1, 9절). 엔진 타입이 없다.
    // 고정 틱 구동기(Wizard.Unity.App 의 FixedTickDriver)는 등록된 모든 표시기에 OnEvents 를 먼저 다 부르고
    // (PresentationEventRouter.Drain 자리), 그다음에 Render 를 부른다.
    // 표시기는 ISimView 와 표시 이벤트만 읽는다. 세계를 쥐지 않고 코어 상태를 바꾸지 않는다.
    // 구현은 Wizard.Unity.Presentation(세계 그림)과 Wizard.Unity.UI(HUD)에 둔다. 이 계약이 그 둘보다 아래에 있어야
    // 조립 루트(App)가 얇은 이음 없이 꽂을 수 있다.
    public interface IFramePresenter
    {
        // 이번 프레임에 돈 틱들이 낸 표시 이벤트(틱 순)
        void OnEvents(ReadOnlySpan<SimEvent> events, ISimView view);

        // 마지막으로 돈 틱 뒤의 상태와 보간 비율(0~1. 실수는 여기서부터)
        void Render(ISimView view, double alpha);
    }
}
