using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 14.2 "번쩍 한도와의 관계": 적 예고의 흰 번쩍(구역 안, 2프레임)도 번쩍 관리기가 본다.
    //  - 1초 동안 번쩍인 예고 구역의 넓이 합이 화면의 25%를 넘으면 그 뒤의 번쩍은 테두리 강조(3겹 테두리의 흰 선이 8px, 2프레임)로 바꾼다.
    //  - 연출 줄이기가 줄임·최소면 항상 테두리 강조다(spec 13 14.5 TelegraphFlash.ThickBorder).
    //  - 테두리 강조로 바뀐 번쩍은 넓이 합에 넣지 않는다 [임시: FlashManager 의 U-20 과 같은 해석].
    //  - 창은 FlashManager 와 같은 미끄러지는 1초다(시작 시각 기준).
    // 적 예고 자체(테두리, 채움, 앞선)는 줄일 수 없는 것이라 여기서 바꾸지 않는다. 바뀌는 것은 흰 번쩍의 빛뿐이다.
    public sealed class TelegraphFlashLimiter
    {
        public const int BorderFrames = 2;   // effects.md 14.2 "2프레임"

        private readonly Queue<Entry> _recent = new Queue<Entry>();
        private float _areaSum;

        public float RecentAreaSum => _areaSum;

        // areaFraction = 이번에 번쩍일 구역의 화면 비율(0..1)
        public FlashOutcome Submit(float areaFraction, long nowMs, in MotionSettings settings)
        {
            if (MotionPresets.Of(settings.Preset).Telegraph == TelegraphFlash.ThickBorder)
            {
                return FlashOutcome.BorderHighlight;
            }
            Expire(nowMs);
            if (_areaSum > PhotosensitivityLimits.FullFlashCoverage)
            {
                return FlashOutcome.BorderHighlight;
            }
            float area = float.IsNaN(areaFraction) ? 0f : Math.Max(0f, Math.Min(1f, areaFraction));
            _recent.Enqueue(new Entry(nowMs, area));
            _areaSum += area;
            return FlashOutcome.Flash;
        }

        public void Reset()
        {
            _recent.Clear();
            _areaSum = 0f;
        }

        private void Expire(long nowMs)
        {
            bool removed = false;
            while (_recent.Count > 0 && nowMs - _recent.Peek().StartMs >= PhotosensitivityLimits.FullFlashWindowMs)
            {
                _recent.Dequeue();
                removed = true;
            }
            if (removed)
            {
                // 빼기를 되풀이하면 오차가 쌓이므로 남은 것으로 다시 더한다
                float sum = 0f;
                foreach (Entry e in _recent)
                {
                    sum += e.Area;
                }
                _areaSum = sum;
            }
        }

        private readonly struct Entry
        {
            public readonly long StartMs;
            public readonly float Area;

            public Entry(long startMs, float area)
            {
                StartMs = startMs;
                Area = area;
            }
        }
    }
}
