using System.Collections.Generic;

namespace Wizard.Solo.View.Effects
{
    public enum FlashKind
    {
        /// <summary>아무것도 그리지 않는다(세기 0).</summary>
        None,
        /// <summary>전체 화면 번쩍(관리기가 센다).</summary>
        Full,
        /// <summary>화면 25% 미만의 국소 번쩍(세지 않는다).</summary>
        Local,
        /// <summary>테두리 강조: 흰 선 8px, 0.15초(세지 않는다).</summary>
        Border
    }

    /// <summary>번쩍 요청 한 건.</summary>
    public struct FlashRequest
    {
        /// <summary>덮는 색 불투명도(0~1).</summary>
        public float Opacity;
        public float OnSeconds;
        public float OffSeconds;
        /// <summary>빨강 계열(위험색) 번쩍인가 — 전체 화면은 금지.</summary>
        public bool Red;
        /// <summary>화면에서 덮는 넓이 비율(0~1). 1 = 전체 화면.</summary>
        public float AreaFraction;
    }

    public struct FlashResult
    {
        public FlashKind Kind;
        public float Opacity;
        public float OnSeconds;
        public float OffSeconds;
        /// <summary>테두리 강조일 때 선 굵기(px)와 길이(초).</summary>
        public float BorderPx;
        public float BorderSeconds;
    }

    /// <summary>
    /// 번쩍 관리기(순수, 08 요약서 14.1절, TV-178~185). 전체 화면 번쩍은 이 한 곳을 거친다.
    /// - 어느 1초 구간에도 2회 이하, 간격 0.4초 이상. 넘으면 버리지 않고 테두리 강조로 바꾼다(테두리 강조는 세지 않는다).
    /// - 불투명도 = min(요청, 단계 상한) × flash_scale. 단계 상한 Full 0.35 / Reduced 0.15 / Minimal = 테두리 강조.
    /// - 켜짐 ≤ 0.05초, 꺼짐 ≥ 0.10초로 강제. 빨강 전체 화면 번쩍은 금지(테두리 강조).
    /// - 화면 25% 이상을 덮는 국소 번쩍은 전체 화면 번쩍으로 센다. 그보다 작으면 세지 않는다.
    /// 시각은 초(게임 틱 / 60). 1초 창은 요청 시각 기준 미끄러지는 창(t − 수락 시각 &lt; 1초).
    /// </summary>
    public sealed class FlashGovernor
    {
        readonly List<double> _accepted = new List<double>();
        public float WindowSeconds;
        public int MaxPerWindow;
        public float MinGapSeconds;
        public float[] LevelCap = new float[3];
        public float MaxOnSeconds;
        public float MinOffSeconds;
        public float BorderPx;
        public float BorderSeconds;
        public float FullscreenAreaFraction;

        public static FlashGovernor FromTuning(EffectsTuning t)
        {
            var g = new FlashGovernor
            {
                WindowSeconds = t.F("flash", "window_seconds"),
                MaxPerWindow = t.I("flash", "max_per_window"),
                MinGapSeconds = t.F("flash", "min_gap_seconds"),
                LevelCap = t.Floats("flash", "level_cap"),
                MaxOnSeconds = t.F("flash", "max_on_seconds"),
                MinOffSeconds = t.F("flash", "min_off_seconds"),
                BorderPx = t.F("flash", "border_px"),
                BorderSeconds = t.F("flash", "border_seconds"),
                FullscreenAreaFraction = t.F("flash", "fullscreen_area_fraction")
            };
            if (g.LevelCap.Length < 3) g.LevelCap = new float[3];
            return g;
        }

        public int AcceptedInWindow(double now)
        {
            int n = 0;
            for (int i = 0; i < _accepted.Count; i++) if (now - _accepted[i] < WindowSeconds) n++;
            return n;
        }

        /// <param name="now">요청 시각(초).</param>
        /// <param name="effectsLevel">0 Full / 1 Reduced / 2 Minimal.</param>
        /// <param name="flashScale">설정 flash_scale / 100.</param>
        public FlashResult Submit(FlashRequest req, double now, int effectsLevel, float flashScale)
        {
            if (flashScale < 0f) flashScale = 0f;
            if (flashScale > 1f) flashScale = 1f;
            float on = req.OnSeconds > MaxOnSeconds ? MaxOnSeconds : req.OnSeconds;
            if (on < 0f) on = 0f;
            float off = req.OffSeconds < MinOffSeconds ? MinOffSeconds : req.OffSeconds;
            bool counts = req.AreaFraction >= FullscreenAreaFraction;
            if (!counts)
            {
                // 국소 번쩍: 세지 않는다. 세기에는 번쩍 배율만(빨강 국소는 그대로 두지 않는다 — 플레이어 쪽 위험색 금지는 부르는 쪽 책임).
                return new FlashResult { Kind = req.Opacity * flashScale > 0f ? FlashKind.Local : FlashKind.None, Opacity = req.Opacity * flashScale, OnSeconds = on, OffSeconds = off };
            }
            int lv = effectsLevel < 0 ? 0 : effectsLevel > 2 ? 2 : effectsLevel;
            if (req.Red || lv == 2 || LevelCap[lv] <= 0f) return Border();
            Prune(now);
            if (_accepted.Count > 0)
            {
                double last = _accepted[_accepted.Count - 1];
                if (now - last < MinGapSeconds) return Border();
            }
            if (AcceptedInWindow(now) >= MaxPerWindow) return Border();
            float op = req.Opacity < LevelCap[lv] ? req.Opacity : LevelCap[lv];
            op *= flashScale;
            _accepted.Add(now);
            return new FlashResult { Kind = op > 0f ? FlashKind.Full : FlashKind.None, Opacity = op, OnSeconds = on, OffSeconds = off };
        }

        FlashResult Border() => new FlashResult { Kind = FlashKind.Border, BorderPx = BorderPx, BorderSeconds = BorderSeconds };

        void Prune(double now)
        {
            // 창과 간격 둘 다 지난 것은 버린다(창이 간격보다 길다).
            for (int i = _accepted.Count - 1; i >= 0; i--)
                if (now - _accepted[i] >= WindowSeconds && now - _accepted[i] >= MinGapSeconds) _accepted.RemoveAt(i);
        }

        public void Reset() => _accepted.Clear();
    }
}
