using System;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // 재실행의 틱마다 부르는 시험용 자리(내부). 공개 Run 은 넘기지 않는다.
    // BeforeStep: 그 틱의 Step 에 넘기는 키·명령 열 그대로(재실행이 기록을 세계에 넣는지 시험이 원래 실행의 열과 대조한다).
    // AfterStep: Step 이 끝난 뒤, 판 끝 검사 전.
    internal interface IReplayObserver
    {
        void BeforeStep(SimWorld world, ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands);

        void AfterStep(SimWorld world);
    }
}
