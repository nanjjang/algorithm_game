namespace Wizard.Sim
{
    // CONTRACTS 3.8, 11.1, DECISIONS-TECH T-48. DuelRecordCodec.TryRead 가 검사를 통과한 WZDR 에서 뽑아 주는 것
    public sealed class DuelRecordBody
    {
        public int HeaderLength;                          // 머리말(DuelSetup 의 정규 바이트)의 길이
        public int InputDelayTicks0;                      // 머리말 뒤의 input_delay 두 칸(슬롯 0, 1). 시뮬레이션 밖의 값이다
        public int InputDelayTicks1;
        public uint EndTick;                              // 꼬리말
        public ulong FinalStateHash;                      // 꼬리말
        public int InputCount;                            // 0x05 레코드 수
    }
}
