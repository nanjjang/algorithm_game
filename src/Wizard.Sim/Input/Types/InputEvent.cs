namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1, 0.3. 무한 웨이브 기록, 결투 패킷, 다시 보기, 관전이 같은 구조체와 같은 정렬 규칙을 쓴다(ARCHITECTURE P4)
    public readonly struct InputEvent
    {
        public readonly uint Tick;          // 유효 틱(결투는 누른 틱 + D)
        public readonly byte Offset;        // 0..255, 1/256틱
        public readonly LogicalKey Key;
        public readonly bool Down;
        public readonly uint Seq;           // 플레이어별 순번. 1부터 1씩. 정렬의 동률 해소에만 쓴다. 세계 상태와 해시에 넣지 않는다

        public InputEvent(uint tick, byte offset, LogicalKey key, bool down, uint seq)
        {
            Tick = tick;
            Offset = offset;
            Key = key;
            Down = down;
            Seq = seq;
        }

        public long Time => (long)Tick * 256 + Offset;                    // 입력 시각(1단위 = 1/15,360초)

        public byte PackKey() => (byte)((int)Key | (Down ? 0x10 : 0));    // 패킷·파일의 1바이트

        public static bool TryUnpackKey(byte packed, out LogicalKey key, out bool down)
        {
            int k = packed & 0x0F;
            key = (LogicalKey)k;
            down = (packed & 0x10) != 0;
            return k <= (int)LogicalKey.Pause && (packed & 0xE0) == 0;
        }
    }
}
