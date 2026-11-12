using Wizard.Sim.Events;

namespace Wizard.ReplayCli
{
    // 재실행의 sink. 표시 이벤트는 버리고 수만 센다. 0단계가 받지 않은 명령(DebugCommandRejected)은 따로 센다(CONTRACTS 3.1)
    public sealed class CountingSink : ISimEventSink
    {
        public long Events { get; private set; }

        public int RejectedCommands { get; private set; }

        public void Emit(in SimEvent e)
        {
            Events++;
            if (e.Type == SimEventType.DebugCommandRejected)
            {
                RejectedCommands++;
            }
        }
    }
}
