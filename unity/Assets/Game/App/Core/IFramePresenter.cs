#nullable enable
using System;
using Wizard.Sim.Events;

namespace Wizard.Unity.App.Core
{
    // 프레임의 끝에서 표시 계층이 받는 것(ARCHITECTURE 6.1, 9절). 표시 계층은 ISimView 와 표시 이벤트만 읽는다. 코어 상태를 바꾸지 않는다.
    // FixedTickDriver 는 등록된 모든 표시기에 OnEvents 를 먼저 다 부르고(PresentationEventRouter.Drain 자리), 그다음에 Render 를 부른다.
    public interface IFramePresenter
    {
        // 이번 프레임에 돈 틱들이 낸 표시 이벤트(틱 순)
        void OnEvents(ReadOnlySpan<SimEvent> events, ISimView view);

        // 마지막으로 돈 틱 뒤의 상태와 보간 비율(0~1. 실수는 여기서부터)
        void Render(ISimView view, double alpha);
    }
}
