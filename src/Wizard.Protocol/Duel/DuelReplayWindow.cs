namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.1 재전송 차단. 방향마다(받는 쪽이 키마다) 하나. 가장 큰 번호와 64칸 비트맵 창.
    // 창보다 오래됐거나 이미 받은 번호는 버린다. 번호 0은 없다.
    // 태그 검증 전에는 IsFresh 만 부르고, 통과한 뒤에 Mark 를 부른다(위조 패킷이 창을 움직이지 못한다).
    public struct DuelReplayWindow
    {
        public const int WindowSize = 64;

        private ulong _highest;     // 받은 가장 큰 번호. 0 이면 아직 없음
        private ulong _seen;        // 비트 i = (_highest − i) 를 받았다

        public ulong Highest => _highest;

        public bool IsFresh(ulong packetNumber)
        {
            if (packetNumber == 0UL)
            {
                return false;
            }
            if (packetNumber > _highest)
            {
                return true;
            }
            ulong age = _highest - packetNumber;
            if (age >= WindowSize)
            {
                return false;
            }
            return ((_seen >> (int)age) & 1UL) == 0UL;
        }

        // 받아 들였으면 true. 이미 받았거나 창 밖이면 false 이고 상태는 그대로다
        public bool Mark(ulong packetNumber)
        {
            if (!IsFresh(packetNumber))
            {
                return false;
            }
            if (packetNumber > _highest)
            {
                ulong shift = packetNumber - _highest;
                _seen = shift >= WindowSize ? 0UL : _seen << (int)shift;
                _seen |= 1UL;
                _highest = packetNumber;
            }
            else
            {
                _seen |= 1UL << (int)(_highest - packetNumber);
            }
            return true;
        }

        public void Reset()
        {
            _highest = 0UL;
            _seen = 0UL;
        }
    }
}
