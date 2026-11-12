using Wizard.Sim.Events;

namespace Wizard.Sim
{
    // 재실행의 sink. 표시 이벤트는 버리고, 0단계가 받지 않은 명령(DebugCommandRejected)만 센다(CONTRACTS 3.1: 재실행의 무효 명령은 RecordCorrupt)
    internal sealed class ReplaySink : ISimEventSink
    {
        public int Rejected;

        public void Emit(in SimEvent e)
        {
            if (e.Type == SimEventType.DebugCommandRejected)
            {
                Rejected++;
            }
        }
    }
}
