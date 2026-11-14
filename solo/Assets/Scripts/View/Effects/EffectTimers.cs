using System.Collections.Generic;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 연속 스침 음높이(순수, 02 M-15 → D-12 "표시 층에서 센다", 10 요약서 3.5절, Q-22).
    /// 같은 슬롯의 앞 스침과 창(2.0초 = 120틱) 안이면 한 단씩 올라가 최대 5단, 창 밖이면 0단. 정확히 2초 차도 이어진 것으로 본다.
    /// </summary>
    public sealed class GrazePitchCounter
    {
        readonly int _windowTicks;
        readonly int _maxStep;
        readonly int[] _lastTick = new int[SimConst.MaxPlayers];
        readonly int[] _step = new int[SimConst.MaxPlayers];
        readonly bool[] _has = new bool[SimConst.MaxPlayers];

        public GrazePitchCounter(float windowSeconds, int maxStep)
        {
            _windowTicks = SimConst.Ticks(windowSeconds);
            _maxStep = maxStep;
        }

        public int WindowTicks => _windowTicks;

        /// <summary>스침 한 번을 세고 이번 스침의 단(0~max)을 돌려준다.</summary>
        public int Next(int slot, int tick)
        {
            if (slot < 0 || slot >= _step.Length) slot = 0;
            if (_has[slot] && tick - _lastTick[slot] <= _windowTicks)
                _step[slot] = _step[slot] + 1 > _maxStep ? _maxStep : _step[slot] + 1;
            else
                _step[slot] = 0;
            _has[slot] = true;
            _lastTick[slot] = tick;
            return _step[slot];
        }

        public void Reset()
        {
            for (int i = 0; i < _has.Length; i++) { _has[i] = false; _step[i] = 0; _lastTick[i] = 0; }
        }
    }

    /// <summary>
    /// 그림 정지 예산(순수, 10 요약서 3.4절 "그림 정지는 1초에 합계 0.1초까지"). 피격(player.hit)은 줄일 수 없어 이 예산을 거치지 않는다.
    /// 시각은 게임 초(틱 / 60) — 그림이 멈춰도 게임 시간은 간다.
    /// </summary>
    public sealed class HitStopBudget
    {
        struct Grant { public double At; public float Seconds; }
        readonly List<Grant> _grants = new List<Grant>();
        readonly float _budget;
        readonly float _window;

        public HitStopBudget(float budgetSeconds, float windowSeconds) { _budget = budgetSeconds; _window = windowSeconds; }

        public float UsedInWindow(double now)
        {
            float s = 0f;
            for (int i = 0; i < _grants.Count; i++) if (now - _grants[i].At < _window) s += _grants[i].Seconds;
            return s;
        }

        /// <summary>요청 초 → 실제로 줄 초(남은 예산까지 자른다. 0이면 정지 없음).</summary>
        public float Request(float seconds, double now)
        {
            for (int i = _grants.Count - 1; i >= 0; i--) if (now - _grants[i].At >= _window) _grants.RemoveAt(i);
            if (seconds <= 0f) return 0f;
            float left = _budget - UsedInWindow(now);
            if (left <= 0f) return 0f;
            float g = seconds < left ? seconds : left;
            _grants.Add(new Grant { At = now, Seconds = g });
            return g;
        }

        public void Reset() => _grants.Clear();
    }

    /// <summary>
    /// 사건 사이 최소 간격 문(순수). 임계 흔들림 "0.3초에 한 번만"(10 3.4절), 감속 걸림 소리 "같은 적 0.5초 1번"(10 5.1절 C-25)처럼 열쇠별로 센다.
    /// </summary>
    public sealed class GapGate
    {
        readonly Dictionary<int, int> _last = new Dictionary<int, int>();
        readonly int _gapTicks;

        public GapGate(float gapSeconds) { _gapTicks = SimConst.Ticks(gapSeconds); }

        public int GapTicks => _gapTicks;

        /// <summary>열어 주면 true(그리고 이번 틱을 적는다).</summary>
        public bool TryPass(int key, int tick)
        {
            int last;
            if (_last.TryGetValue(key, out last) && tick - last < _gapTicks) return false;
            _last[key] = tick;
            return true;
        }

        public void Reset() => _last.Clear();
    }

    /// <summary>
    /// 한꺼번에 처치(순수, 10 요약서 3.7절 "0.3초 안 5마리 이상 처치 → 처치음 하나로 합침 + 흔들림 소", H-06).
    /// 처치마다 Kill(틱)을 부른다. 창 안 처치가 기준 수에 처음 닿은 처치에서 Merged, 그 묶음이 이어지는 동안의 처치는 Suppressed.
    /// </summary>
    public sealed class KillBurstCounter
    {
        public enum Result { Single, Merged, Suppressed }

        readonly List<int> _ticks = new List<int>();
        readonly int _windowTicks;
        readonly int _count;
        int _mergedUntil = int.MinValue;

        public KillBurstCounter(float windowSeconds, int count) { _windowTicks = SimConst.Ticks(windowSeconds); _count = count; }

        public Result Kill(int tick)
        {
            for (int i = _ticks.Count - 1; i >= 0; i--) if (tick - _ticks[i] >= _windowTicks) _ticks.RemoveAt(i);
            _ticks.Add(tick);
            if (tick < _mergedUntil) { _mergedUntil = tick + _windowTicks; return Result.Suppressed; }
            if (_count > 0 && _ticks.Count >= _count)
            {
                _mergedUntil = tick + _windowTicks;
                return Result.Merged;
            }
            return Result.Single;
        }

        public void Reset() { _ticks.Clear(); _mergedUntil = int.MinValue; }
    }
}
