namespace Wizard.Netcode.Duel
{
    // 최근 확정 틱의 상태 해시(틱 T 를 처리한 뒤의 ComputeStateHash). 서버의 해시가 그 틱을 지난 뒤에 와도 견줄 수 있게 둔다.
    // 해시 간격(spec 10 2.8의 30틱)은 서버가 정하고 이 코어는 모른다: 확정 틱마다 남기고 서버가 보낸 틱과 견준다.
    internal sealed class ConfirmedHashRing
    {
        private const int Capacity = 64;     // 자료구조 크기. 서버 해시가 이만큼 늦게 오면 견주지 못하고 건너뛴다

        private readonly uint[] _ticks = new uint[Capacity];
        private readonly ulong[] _hashes = new ulong[Capacity];
        private readonly bool[] _used = new bool[Capacity];

        public void Put(uint tick, ulong hash)
        {
            int i = (int)(tick % Capacity);
            _ticks[i] = tick;
            _hashes[i] = hash;
            _used[i] = true;
        }

        public bool TryGet(uint tick, out ulong hash)
        {
            int i = (int)(tick % Capacity);
            if (_used[i] && _ticks[i] == tick)
            {
                hash = _hashes[i];
                return true;
            }
            hash = 0UL;
            return false;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _used[i] = false;
            }
        }
    }
}
