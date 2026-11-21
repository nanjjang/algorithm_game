using System;
using System.Collections.Generic;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>번쩍 요청을 한도에 맞춘 결과.</summary>
    public struct FlashDecision
    {
        /// <summary>실제로 덮는 불투명도(0이면 번쩍 없음).</summary>
        public double Alpha;
        /// <summary>번쩍 대신 테두리 강조(흰 선 8px, 0.15초)로 바꿨다.</summary>
        public bool Border;
    }

    /// <summary>
    /// 강화 화면의 전체 화면 번쩍·흔들림 관리(08 14.1절, 10 요약서 4.3절). 화면 틱 단위의 순수 클래스.
    /// - 번쩍: 불투명도 = min(요청, 단계 상한 Full 0.35 / Reduced 0.15 / Minimal = 테두리) × flash_scale/100, 빠른 강화는 0.15 이하.
    ///   시도 1회에 최대 1회, 앞 번쩍과 0.4초 이상, 어느 1초 창에서도 2회 이하. 넘치는 요청은 버리지 않고 테두리 강조로 바꾼다.
    /// - 흔들림 누적기: acc = min(1, acc + add), 초당 2.5 감소, 폭 = acc² × 16px × 단계 배율 × shake_scale/100.
    /// </summary>
    public sealed class EnhanceFlashGuard
    {
        readonly EnhanceScreenTuning _t;
        readonly List<int> _flashTicks = new List<int>();
        int _now;
        int _attemptFlashes;
        double _shakeAcc;

        public EnhanceFlashGuard(EnhanceScreenTuning t) { _t = t; }

        public int Now => _now;
        public double ShakeAccumulator => _shakeAcc;

        public void Tick()
        {
            _now++;
            _shakeAcc = Math.Max(0, _shakeAcc - _t.ShakeDecayPerSecond / EnhanceScreenTuning.TicksPerSecond);
        }

        /// <summary>새 시도가 시작됐다(시도당 번쩍 1회 셈을 비운다).</summary>
        public void NewAttempt() { _attemptFlashes = 0; }

        /// <summary>
        /// 강화 화면이 쓰는 번쩍 배율(%): 설정 flash_scale에 "섬광 줄이기"(reduce_flash)의 상한(ViewOptions.ReduceFlashCap = 30%)을 건다.
        /// 섬광 줄이기가 켜지면 전체 화면 번쩍은 아예 쓰지 않는다(화면이 테두리 강조로 바꾼다) — 이 배율은 국소 빛에 곱한다.
        /// </summary>
        public static int EffectiveFlashScalePct(int flashScalePct, bool reduceFlash)
        {
            int cap = (int)Math.Round(Wizard.Solo.View.ViewOptions.ReduceFlashCap * 100f);
            return reduceFlash && flashScalePct > cap ? cap : flashScalePct;
        }

        public FlashDecision RequestFlash(double requested, int effectsLevel, int flashScalePct, bool fast)
        {
            var d = new FlashDecision();
            double cap = EnhanceScreenTuning.At(_t.FlashCap, effectsLevel);
            if (fast) cap = Math.Min(cap, _t.FlashFastCap);
            if (cap <= 0) { d.Border = true; return d; }
            int minGap = EnhanceScreenTuning.Ticks(_t.FlashMinIntervalSeconds);
            int window = EnhanceScreenTuning.Ticks(_t.FlashWindowSeconds);
            int inWindow = 0;
            for (int i = 0; i < _flashTicks.Count; i++) if (_now - _flashTicks[i] < window) inWindow++;
            bool tooSoon = _flashTicks.Count > 0 && _now - _flashTicks[_flashTicks.Count - 1] < minGap;
            if (_attemptFlashes >= _t.FlashPerAttemptMax || tooSoon || inWindow >= _t.FlashWindowMax)
            {
                d.Border = true;
                return d;
            }
            d.Alpha = Math.Min(requested, cap) * Math.Max(0, Math.Min(100, flashScalePct)) / 100.0;
            if (d.Alpha <= 0) return d;
            _flashTicks.Add(_now);
            if (_flashTicks.Count > 8) _flashTicks.RemoveAt(0);
            _attemptFlashes++;
            return d;
        }

        /// <summary>흔들림 사건(small/medium/large/xlarge)을 더한다.</summary>
        public void AddShake(string cls) { _shakeAcc = Math.Min(1, _shakeAcc + _t.ShakeAmount(cls)); }

        /// <summary>지금 흔들림 폭(px).</summary>
        public double ShakePx(int effectsLevel, int shakeScalePct) =>
            _shakeAcc * _shakeAcc * _t.ShakeMaxPx * EnhanceScreenTuning.At(_t.ShakeLevelScale, effectsLevel) * Math.Max(0, shakeScalePct) / 100.0;

        /// <summary>번쩍 모양: 켜짐 0.05초 안, 꺼짐 0.10초 이상. 시작 뒤 tick의 배율 0..1.</summary>
        public double Envelope(int ticksSinceStart)
        {
            int rise = Math.Max(1, EnhanceScreenTuning.Ticks(_t.FlashRiseSeconds));
            int fall = Math.Max(1, EnhanceScreenTuning.Ticks(_t.FlashFallSeconds));
            if (ticksSinceStart < 0) return 0;
            if (ticksSinceStart < rise) return (ticksSinceStart + 1) / (double)rise;
            int f = ticksSinceStart - rise;
            return f >= fall ? 0 : 1 - f / (double)fall;
        }
    }
}
