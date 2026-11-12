#nullable enable
using System;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 화면 틱(spec 13 0.2: 고정 60Hz UiTick, 게임 틱과 따로)을 렌더 프레임의 경과 시간에서 센다. 표시 전용이라 실수를 쓴다.
    // 한 프레임에 너무 많이 밀리면(창을 끌거나 멈춘 뒤) 한 번에 최대 1초 분만 돌린다 [임시].
    public sealed class UiTickClock
    {
        public const int MaxTicksPerAdvance = UiTime.UiTicksPerSecond;

        private double? _last;
        private double _carry;

        public long TotalTicks { get; private set; }

        // nowSeconds: 단조 시계의 지금(초). 처음 부르면 0을 돌려준다
        public int Advance(double nowSeconds)
        {
            if (_last == null || nowSeconds < _last.Value)
            {
                _last = nowSeconds;
                return 0;
            }
            double ticks = (nowSeconds - _last.Value) * UiTime.UiTicksPerSecond + _carry;
            _last = nowSeconds;
            int n = (int)Math.Floor(ticks);
            _carry = ticks - n;
            if (n > MaxTicksPerAdvance)
            {
                n = MaxTicksPerAdvance;
                _carry = 0;
            }
            TotalTicks += n;
            return n;
        }

        public void Reset()
        {
            _last = null;
            _carry = 0;
        }
    }
}
