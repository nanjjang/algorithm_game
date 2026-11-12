using System;
using Wizard.Content.Rules;

namespace Wizard.Netcode.Clock
{
    // CONTRACTS 6.4 ClockSync, spec 10 14.4(시계 맞추기)·14.5(입력 지연 D)·2.8(clock_slew_max). 결정: DECISIONS-TECH T-60.
    // 소켓도 시계도 없다. 시각은 전부 인자로 받는다(마이크로초 정수). 값(표, 한도)은 전부 NetRuleSet 에서 읽는다.
    public static class ClockSync
    {
        /// <summary>
        /// spec 10 14.4 Sync: s = argmin(t1 − t0), rtt = s.t1 − s.t0, offset = s.ts − (s.t0 + rtt / 2).
        /// 왕복이 음수인 표본(단조 시계의 값이 아니다)은 건너뛴다. 왕복이 같으면 앞의 표본을 고른다.
        /// 쓸 표본이 없으면 <c>default</c>(<see cref="ClockOffset.IsValid"/> 거짓)를 돌려준다. 예외를 던지지 않는다.
        /// 표본 수는 호출자가 정한다(경기 전에는 <see cref="NetRuleSet.ClockSyncSamples"/>개를 모아서 부른다).
        /// </summary>
        public static ClockOffset Estimate(ReadOnlySpan<ClockSample> samples)
        {
            int best = -1;
            long bestRtt = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                long rtt = samples[i].RttUs;
                if (rtt < 0)
                {
                    continue;
                }
                if (best < 0 || rtt < bestRtt)
                {
                    best = i;
                    bestRtt = rtt;
                }
            }
            if (best < 0)
            {
                return default;
            }
            ClockSample s = samples[best];
            // bestRtt 는 0 이상이라 / 2 는 내림이다
            return new ClockOffset(s.ServerUs - (s.T0Us + bestRtt / 2), bestRtt, best);
        }

        /// <summary>
        /// spec 10 14.5 ChooseD. 경계와 D 는 데이터(<see cref="NetRuleSet.InputDelayTable"/>)에서만 읽는다.
        /// 첫 줄부터 <c>rttMs ≤ RttUpToMs</c> 인 줄의 D, 어느 줄에도 들지 않으면(마지막 경계 초과) 마지막 줄의 D
        /// ("120ms 초과 4틱 고정"). 왕복을 모르면(음수. 표본이 없을 때의 −1) 마지막 줄의 D 를 준다(T-60: 모르는 회선에는
        /// 마감 여유가 가장 큰 D). 그 경기를 시작할지는 호출자가 정한다(CONTRACTS 10.5).
        /// </summary>
        public static int ChooseInputDelay(int rttMs, NetRuleSet rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            InputDelayStep[] table = rules.InputDelayTable;
            if (table == null || table.Length == 0)
            {
                // DataBuild 가 빈 표를 막는다(NetConverter). 여기에 닿으면 팩이 아니라 호출자가 만든 규칙이 깨진 것이다
                throw new ArgumentException("NetRuleSet.InputDelayTable is empty", nameof(rules));
            }
            int last = table.Length - 1;
            if (rttMs < 0)
            {
                return table[last].DelayTicks;
            }
            for (int i = 0; i < table.Length; i++)
            {
                if (rttMs <= table[i].RttUpToMs)
                {
                    return table[i].DelayTicks;
                }
            }
            return table[last].DelayTicks;
        }

        /// <summary>
        /// spec 10 14.4 TickAdjust: 틱 길이 = 기본 길이 × (1 − clamp(err / 1s, −한도, +한도)).
        /// <paramref name="errorUs"/> = 서버 기준 목표 시각 − 내 입력 시계 시각(µs). 양수면 내 시계가 뒤처졌다.
        /// 돌려주는 값은 틱 길이의 변화(ppm): 틱 길이 = 기본 길이 × (1,000,000 + 값) / 1,000,000. 뒤처졌으면 음수(틱을 짧게)다.
        /// 늘 ±<see cref="NetRuleSet.ClockSlewMaxPpm"/> 안이다(처음 값 5,000 = ±0.5%, 틱 길이 16.583~16.750ms).
        /// "err / 1s" 를 ppm 으로 적으면 err(µs) 그대로다(1초 = 1,000,000µs = 1,000,000ppm). 그래서 환산 상수가 없다.
        /// 입력 시계의 드리프트 보정에만 쓴다. 틱을 건너뛰지 않는다(길이만 바꾼다). 예측의 따라잡기는 이 보정으로 하지 않는다
        /// (CONTRACTS 6.4 "시계 둘": CatchupTicksPerAdvance).
        /// </summary>
        public static int TickLengthAdjustPpm(long errorUs, NetRuleSet rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            int max = rules.ClockSlewMaxPpm;
            if (max < 1)
            {
                // DataBuild 와 팩 읽기가 1 미만을 막는다. 여기에 닿으면 호출자가 만든 규칙이 깨진 것이다
                throw new ArgumentException("NetRuleSet.ClockSlewMaxPpm is not positive", nameof(rules));
            }
            if (errorUs >= max)
            {
                return -max;
            }
            if (errorUs <= -max)
            {
                return max;
            }
            return (int)-errorUs;
        }
    }
}
