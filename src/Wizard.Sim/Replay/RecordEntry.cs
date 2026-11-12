using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // 검사를 통과한 본문 레코드 하나. Seq 는 읽는 쪽이 매긴 순번이다(.wzrc: 명령과 키가 같은 공간에서 레코드 순서대로 1부터,
    // WZDR: 레코드에 실린 값). 종류에 맞지 않는 칸은 0이다
    internal struct RecordEntry
    {
        public RecordKind Kind;
        public uint Tick;
        public uint Seq;
        public byte Slot;
        public byte Offset;
        public LogicalKey Key;
        public bool Down;
        public RunCommandOp Op;
        public int A, B;
        public uint WallMs;
        public ushort Wave;
        public ulong StateHash;
    }
}
