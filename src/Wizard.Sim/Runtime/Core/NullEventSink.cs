using Wizard.Sim.Events;

namespace Wizard.Sim
{
    // CanApply 의 확인 단계에 넘기는 sink. 확인 단계의 훅은 이벤트를 내지 않지만(SimWorld.Hooks.cs), 낸다면 버린다.
    // 세계마다 하나를 만들어 둔다(정적 인스턴스를 두지 않는다)
    internal sealed class NullEventSink : ISimEventSink
    {
        public void Emit(in SimEvent e)
        {
        }
    }
}
