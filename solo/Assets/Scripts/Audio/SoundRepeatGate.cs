using System.Collections.Generic;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 같은 클립 연타 제한(솔로 임시, Audio.json mix): 앞 재생과 MinInterval 안이면 버린다(같은 틱의 겹침이 한 소리로 뭉개져 커지는 것을 막는다).
    /// Window 안의 되풀이면 Repeated = 참 — 재생기가 음높이를 조금 흩는다(기계적인 같은 소리 반복을 덜 들리게).
    /// 섞는 규칙의 초당 횟수(적중 8, 처치 6)와는 따로다 — 그것은 SoundMixPolicy가 센다.
    /// </summary>
    public sealed class SoundRepeatGate
    {
        readonly Dictionary<string, double> _last = new Dictionary<string, double>();
        public double MinInterval;
        public double Window;

        public SoundRepeatGate(double minInterval, double window)
        {
            MinInterval = minInterval;
            Window = window;
        }

        /// <summary>낼 수 있으면 참(그리고 시각을 기록). repeated = 앞 재생과 Window 안.</summary>
        public bool TryPass(string key, double now, out bool repeated)
        {
            repeated = false;
            if (string.IsNullOrEmpty(key)) return true;
            double t;
            if (_last.TryGetValue(key, out t))
            {
                double gap = now - t;
                if (gap >= 0 && gap < MinInterval) return false;
                repeated = gap >= 0 && gap < Window;
            }
            _last[key] = now;
            return true;
        }

        public void Reset() => _last.Clear();
    }
}
