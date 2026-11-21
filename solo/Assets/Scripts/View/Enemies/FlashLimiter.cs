using System.Collections.Generic;

namespace Wizard.Solo.View.Enemies
{
    /// <summary>
    /// 예고 번쩍 한도(05 요약서 1.2절, S5 9.4): 최근 1초(60틱) 번쩍 넓이 합이 화면 25%(1920×1080 기준 518,400px)를 넘으면
    /// 그 번쩍은 구역 전체 흰색 대신 흰 선 8px 테두리 강조로 바꾼다.
    /// 해석(G15): 이번 구역을 더했을 때 넘으면 강조로 바꾼다(한 장만으로 화면 25%를 넘는 번쩍도 막는다). 강조로 바뀐 번쩍은 합에 넣지 않는다.
    /// 예고 자체(테두리·채움·앞선)는 줄일 수 없는 것이라 여기서 바꾸지 않는다 — 바뀌는 것은 흰 번쩍의 빛뿐이다.
    /// </summary>
    public sealed class FlashLimiter
    {
        struct Entry { public int Tick; public float Area; }

        readonly Queue<Entry> _recent = new Queue<Entry>();
        readonly float _limitPx2;
        readonly int _windowTicks;
        float _sum;

        public FlashLimiter(float limitPx2, int windowTicks)
        {
            _limitPx2 = limitPx2;
            _windowTicks = windowTicks < 1 ? 1 : windowTicks;
        }

        public float RecentSumPx2 => _sum;
        public float LimitPx2 => _limitPx2;

        /// <summary>번쩍 한 번을 청한다. true = 구역 전체 흰 번쩍, false = 흰 선 테두리 강조.</summary>
        public bool TryFlash(float areaPx2, int tick)
        {
            Expire(tick);
            if (areaPx2 < 0f) areaPx2 = 0f;
            if (_sum + areaPx2 > _limitPx2) return false;
            _recent.Enqueue(new Entry { Tick = tick, Area = areaPx2 });
            _sum += areaPx2;
            return true;
        }

        public void Reset()
        {
            _recent.Clear();
            _sum = 0f;
        }

        void Expire(int tick)
        {
            bool removed = false;
            while (_recent.Count > 0 && tick - _recent.Peek().Tick >= _windowTicks) { _recent.Dequeue(); removed = true; }
            if (!removed) return;
            float s = 0f;
            foreach (var e in _recent) s += e.Area;
            _sum = s;
        }
    }
}
