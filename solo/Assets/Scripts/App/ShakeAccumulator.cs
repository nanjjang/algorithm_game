using System;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 화면 흔들림 누적기(순수, 08 요약서 14.1절 "흔들림 누적기"). 전장 카메라만 흔든다(HUD는 안 흔들림).
    /// 요청 세기(px)는 소 4 / 중 8 / 대 12 / 특대 16px로 오고, add = √(px / 16)(= 소 0.5 / 중 0.7071 / 대 0.8660 / 특대 1.0)로 바꿔
    /// acc = min(1, acc + add), 초당 2.5씩 줄인다. 폭 = acc² × 16px × 단계 배율(Full 1, Reduced 0.3, Minimal 0) × shake_scale.
    /// 한도: 폭 16px, 길이 0.6초 이하(요청 길이도 0.6초로 자르고, 그 시간이 지나면 0). 회전·확대 떨림 없음.
    /// </summary>
    public sealed class ShakeAccumulator
    {
        public const float MaxAmplitudePx = 16f;
        public const float MaxDurationSeconds = 0.6f;
        public const float DecayPerSecond = 2.5f;
        public const float ReducedLevelScale = 0.3f;

        float _acc;
        float _remaining;

        public float Accumulated => _acc;
        public float RemainingSeconds => _remaining;

        /// <summary>흔들림 요청. seconds ≤ 0이면 한도 길이(0.6초).</summary>
        public void Add(float intensityPx, float seconds)
        {
            if (float.IsNaN(intensityPx) || intensityPx <= 0f) return;
            float px = intensityPx > MaxAmplitudePx ? MaxAmplitudePx : intensityPx;
            float add = (float)Math.Sqrt(px / MaxAmplitudePx);
            _acc = Math.Min(1f, _acc + add);
            float dur = seconds <= 0f || float.IsNaN(seconds) ? MaxDurationSeconds : Math.Min(seconds, MaxDurationSeconds);
            if (dur > _remaining) _remaining = dur;
        }

        public void Update(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt)) return;
            _acc -= DecayPerSecond * dt;
            _remaining -= dt;
            if (_acc < 0f) _acc = 0f;
            if (_remaining <= 0f) { _remaining = 0f; _acc = 0f; }
        }

        /// <summary>단계 배율. effectsLevel 0 전체 / 1 줄임 / 2 최소.</summary>
        public static float LevelScale(int effectsLevel) => effectsLevel <= 0 ? 1f : effectsLevel == 1 ? ReducedLevelScale : 0f;

        /// <summary>지금 폭(기준 화면 px). shakeScale = 설정 shake_scale / 100(0~1).</summary>
        public float AmplitudePx(int effectsLevel, float shakeScale)
        {
            if (shakeScale < 0f) shakeScale = 0f;
            if (shakeScale > 1f) shakeScale = 1f;
            float a = _acc * _acc * MaxAmplitudePx * LevelScale(effectsLevel) * shakeScale;
            return a > MaxAmplitudePx ? MaxAmplitudePx : a;
        }

        public void Reset() { _acc = 0f; _remaining = 0f; }
    }

    /// <summary>
    /// 그림 정지 시계(순수, 08 요약서 14.1절 "그림 정지"). 게임 틱은 멈추지 않고 표시 dt만 0으로 만든다.
    /// 배율 Full 1 / Reduced 0.5 / Minimal 0, hitstop_enabled 끔이면 0.
    /// </summary>
    public sealed class HitStopTimer
    {
        public const float ReducedLevelScale = 0.5f;
        float _remaining;

        public bool Active => _remaining > 0f;
        public float RemainingSeconds => _remaining;

        public static float LevelScale(int effectsLevel) => effectsLevel <= 0 ? 1f : effectsLevel == 1 ? ReducedLevelScale : 0f;

        public void Request(float seconds, int effectsLevel, bool enabled)
        {
            if (!enabled || float.IsNaN(seconds) || seconds <= 0f) return;
            float s = seconds * LevelScale(effectsLevel);
            if (s > _remaining) _remaining = s;
        }

        /// <summary>프레임 시간을 받아 표시 층에 넘길 dt를 돌려준다(정지 중이면 0).</summary>
        public float Filter(float dt)
        {
            if (_remaining <= 0f) return dt;
            _remaining -= dt;
            if (_remaining < 0f) _remaining = 0f;
            return 0f;
        }

        public void Reset() { _remaining = 0f; }
    }
}
