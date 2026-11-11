using Wizard.Core;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3, T-15. 고정 크기, 참조 없음. 코어는 그림과 소리를 모르고 이것만 낸다
    public struct SimEvent
    {
        public SimEventType Type;
        public PlayerSlot Slot;             // 관련 플레이어. 없으면 None
        public uint Tick;
        public EntityId Source, Target;
        public int A, B, C;                 // 종류별 인자(SimEventType 의 주석과 각 명세의 연결점 절)
        public long X, Y;                   // 위치(Fix64 Raw). 없으면 0
        public long V;                      // 큰 값(피해 Raw 등)
    }
}
