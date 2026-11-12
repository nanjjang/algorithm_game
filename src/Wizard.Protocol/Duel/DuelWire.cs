using Wizard.Sim.Input;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10절의 형식 상수. 모든 수는 리틀 엔디언이다.
    public static class DuelWire
    {
        public const int ProtocolVersion = 1;
        public const int MaxPacketBytes = 1200;
        public const int HeaderBytes = 13;                          // ver_type 1 + token_id 4 + packet_number 8. AAD 와 같다
        public const int TagBytes = 16;                             // Poly1305
        public const int NonceBytes = 12;
        public const int KeyBytes = 32;
        public const int OverheadBytes = HeaderBytes + TagBytes;    // 29
        public const int MaxBodyBytes = MaxPacketBytes - OverheadBytes;

        public const int MaxClientEvents = 64;                      // ClientInput.event_count 0..64
        public const int MaxConfirmedTicks = 32;                    // ServerTick.confirmed_count 0..32
        public const int MaxEventsPerTickPerSlot = InputOrder.MaxEventsPerTickPerPlayer;   // 8
        public const int DuelSlots = 2;
        public const int OverlapTicks = 8;                          // ClientInput 이 겹쳐 싣는 틱 수(유효 틱 ≥ sealed_through_tick − 7)

        public const byte KeyDownBit = 0x10;
        public const byte ServerTickFlagHash = 1;
        public const byte ServerTickFlagDropped = 2;

        // 결투 키는 Up~Reverse(0~7)다. Pause(8)는 결투에 없고(CONTRACTS 3.1 "결투에는 일시정지가 없다"),
        // 9~15 와 윗 3비트(0xE0)는 정의되지 않았다. 셋 다 범위 밖으로 거절한다.
        public static bool TryUnpackDuelKey(byte packed, out LogicalKey key, out bool down)
        {
            if (!InputEvent.TryUnpackKey(packed, out key, out down) || key > LogicalKey.Reverse)
            {
                key = default;
                down = false;
                return false;
            }
            return true;
        }

        public static bool IsDuelKey(LogicalKey key) => key <= LogicalKey.Reverse;
    }
}
