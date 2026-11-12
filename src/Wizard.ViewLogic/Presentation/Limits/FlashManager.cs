using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.2 FlashManager [확정 규칙], effects.md 0.3절 "번쩍 관리기".
    // 전체 화면 번쩍과 큰 국소 번쩍(화면 25% 이상)은 이 한 곳을 거쳐서만 낸다. 한도를 넘는 요청은 버리지 않고
    // 테두리 강조(흰 선 8px, 0.15초)로 바꾼다. 한도(1초 창 2회, 간격 0.4초, 불투명도 35%, 빨강 금지, 모양)는 설정과 무관하게 항상 걸린다.
    //  - "1초 구간"은 미끄러지는 창이다: 시각 t 에서 (t − 1초, t] 안의 시작이 2개 이하 [임시: 시작 시각 기준, U-20].
    //  - 테두리 강조는 전체 화면 번쩍으로 세지 않는다 [임시, U-20].
    //  - 낱개 조절은 곱이다: 불투명도 = min(요청, 단계 상한) × flash_scale/100 [임시, U-20].
    //  - 작은 국소 번쩍(25% 미만)은 그대로 통과한다(다른 규칙은 효과 쪽 책임, 의사코드 그대로).
    // 시각은 표시 쪽 단조 시계의 밀리초다. 게임 틱과 무관하다.
    public sealed class FlashManager
    {
        // 창 안에는 많아야 MaxFullFlashesPerWindow 개만 들어가므로 그만큼의 고리 버퍼면 된다(자료구조 크기)
        private readonly long[] _recentStartsMs = new long[PhotosensitivityLimits.MaxFullFlashesPerWindow];
        private int _recentHead;
        private int _recentCount;
        private long _lastStartMs;
        private bool _hasLast;

        public int RecentFullFlashCount => _recentCount;

        public FlashDecision Submit(in FlashRequest r, long nowMs, in MotionSettings settings)
        {
            bool isFull = r.Coverage >= PhotosensitivityLimits.FullFlashCoverage;
            if (!isFull)
            {
                return FlashDecision.Flash(r.Opacity, r.RiseMs, r.FallMs, false);
            }
            if (r.Color.IsDanger)
            {
                return Border();                                            // 빨강 전체 화면 번쩍 금지
            }
            float presetCap = MotionPresets.Of(settings.Preset).FlashCap;
            if (presetCap <= 0f)
            {
                return Border();                                            // 최소: 번쩍 없음
            }
            float requested = float.IsNaN(r.Opacity) ? 0f : r.Opacity;
            float op = Math.Min(Math.Min(requested, presetCap), PhotosensitivityLimits.MaxFullFlashOpacity) * settings.FlashFraction;
            if (op <= 0f)
            {
                return Border();
            }
            Expire(nowMs);
            if (_recentCount >= PhotosensitivityLimits.MaxFullFlashesPerWindow)
            {
                return Border();                                            // 1초에 2회 이하
            }
            if (_hasLast && nowMs - _lastStartMs < PhotosensitivityLimits.MinFullFlashGapMs)
            {
                return Border();                                            // 간격 0.4초 이상
            }
            Push(nowMs);
            return FlashDecision.Flash(op,
                PhotosensitivityLimits.ClampFlashRiseMs(r.RiseMs),
                PhotosensitivityLimits.ClampFlashFallMs(r.FallMs),
                true);
        }

        public void Reset()
        {
            _recentHead = 0;
            _recentCount = 0;
            _hasLast = false;
            _lastStartMs = 0;
        }

        private static FlashDecision Border() =>
            FlashDecision.Border(PhotosensitivityLimits.BorderHighlightWidthPx, PhotosensitivityLimits.BorderHighlightMs);

        // 창 밖으로 나간 시작을 버린다: now − 시작 ≥ 1초
        private void Expire(long nowMs)
        {
            while (_recentCount > 0)
            {
                long oldest = _recentStartsMs[_recentHead];
                if (nowMs - oldest < PhotosensitivityLimits.FullFlashWindowMs)
                {
                    break;
                }
                _recentHead = (_recentHead + 1) % _recentStartsMs.Length;
                _recentCount--;
            }
        }

        private void Push(long nowMs)
        {
            int tail = (_recentHead + _recentCount) % _recentStartsMs.Length;
            _recentStartsMs[tail] = nowMs;
            _recentCount++;
            _lastStartMs = nowMs;
            _hasLast = true;
        }
    }
}
