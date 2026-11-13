using Wizard.Core;

namespace Wizard.ViewLogic.Presentation
{
    // 연속 스침의 음높이 단(chainIndex 0~MaxStep)을 표시 계층에서 센다(CONTRACTS 0.3 T-50: 코어는 GrazeChain 을 두지 않고 이벤트의 C 는 0이다).
    // spec 01 17.2·effects.md 11.1: 2초 안에 연달아 스치면 "챙"이 한 번에 1반음씩, 5반음까지. 첫 스침과 창 밖의 스침은 0.
    // Confirmed 인 GrazeAwarded 만 넣는다(예측·번복은 세지 않는다). 슬롯마다 따로 센다.
    // 창의 경계는 포함이다(틱 차 == 창이면 이어진 것) [임시]. 틱이 거꾸로 오면(새 판, 되감기 뒤 확정 세계의 재시작) 0부터 다시 센다.
    public sealed class GrazeChainCounter
    {
        private const int SlotCapacity = 256;                 // PlayerSlot 은 byte 다(자료구조 크기)

        private readonly GrazeChainSettings _settings;
        private readonly uint[] _lastTick = new uint[SlotCapacity];
        private readonly int[] _step = new int[SlotCapacity];
        private readonly bool[] _has = new bool[SlotCapacity];

        public GrazeChainCounter(GrazeChainSettings settings)
        {
            _settings = settings;
        }

        // 이 스침의 단을 돌려주고 기록한다
        public int Next(PlayerSlot slot, uint tick)
        {
            int s = slot.Value;
            int step = 0;
            if (_has[s] && tick >= _lastTick[s] && tick - _lastTick[s] <= (uint)_settings.WindowTicks)
            {
                step = _step[s] + 1;
                if (step > _settings.MaxStep)
                {
                    step = _settings.MaxStep;
                }
            }
            _has[s] = true;
            _lastTick[s] = tick;
            _step[s] = step;
            return step;
        }

        // 지금 이 슬롯의 마지막 단(HUD·디버그 표시용). 기록이 없으면 0
        public int Current(PlayerSlot slot) => _has[slot.Value] ? _step[slot.Value] : 0;

        public void Reset()
        {
            for (int i = 0; i < SlotCapacity; i++)
            {
                _has[i] = false;
                _lastTick[i] = 0;
                _step[i] = 0;
            }
        }
    }
}
