using System.Collections.Generic;
using Wizard.Sim.Events;

namespace Wizard.BalanceLab.Core
{
    // 세계가 낸 표시 이벤트를 모두 모은다(방출 순서 그대로)
    internal sealed class EventLog : ISimEventSink
    {
        public List<SimEvent> Events { get; } = new List<SimEvent>();

        public void Emit(in SimEvent e) => Events.Add(e);
    }
}
