using System;
using System.Collections.Generic;
using Wizard.Sim.Input;

namespace Wizard.Client
{
    // CONTRACTS 6.5, spec 01 4.2, T-28. 하드웨어 시각 → (틱, 오프셋 0~255). 순번은 매기지 않는다(CONTRACTS 3.1:
    // 순번을 매기는 곳은 InputRecorder와 DuelClientCore 둘뿐이다).
    // 틱과 무관하게 부른다. 프레임마다 Drain 직후에 한 번. 같은 이벤트 열이면 몇 번에 나눠 넣어도 결과가 같다.
    //
    // 틱   = floor((시각 − epoch) / tickSeconds) + inputDelayTicks
    // 오프셋 = 틱 안의 위치 × 256 (내림)
    // 두 값을 한 번의 내림 floor((시각 − epoch) / tickSeconds × 256)에서 같이 꺼낸다. 오프셋이 256으로 올림되는 일이 없다.
    //
    // [임시] (OPEN-QUESTIONS에 올림)
    // - epoch보다 앞선 시각(그리고 NaN)은 epoch(틱 0 + D, 오프셋 0)으로 본다. 판·경기 시작 전에 눌린 키를 버리면 물리 키와 논리 키가 어긋난다.
    // - 하드웨어 시각은 줄지 않는 것으로 본다. 입력원이 앞서 준 시각보다 이른 시각(그리고 NaN)을 주면 그 이벤트는
    //   앞서 받은 하드웨어 시각으로 보고(호출을 건너서도), 계약 식에는 그 호출의 시계를 그대로 쓴다.
    //   입력원이 정한 순서(누름 → 뗌)를 지키고, 결투에서 "한 패킷 안의 역행"을 만들지 않으려는 것이다(CONTRACTS 6.4, 10.6).
    //   올림은 하드웨어 시각에서만 한다. 출력(틱, 오프셋)에서 올리지 않는다. 그래서 시계가 바뀌면(일시정지 재개로 epoch가
    //   밀림, 새 판·새 경기, 입력 시계 보정) 바뀐 시계의 계약 식 값이 그대로 나온다(ARCHITECTURE 6.1, spec 01 3.6).
    // - 입력 시각이 uint 틱을 넘으면(+∞ 포함) 마지막 틱의 오프셋 255에 묶는다. 이런 깨진 시각은 그 이벤트에만 쓰고
    //   기억하지 않는다. 뒤의 이벤트는 계약 식대로 나온다.
    public sealed class InputQuantizer
    {
        public const int OffsetsPerTick = 256;            // 1/256틱(spec 01 4.3). 단위 환산 상수
        private const int OffsetBits = 8;
        private const long MaxTime = ((long)uint.MaxValue << OffsetBits) | (OffsetsPerTick - 1);

        // 지금까지 받은 정상 하드웨어 시각의 최댓값(초). 없으면 −∞. 시계와 무관한 값이라 시계가 바뀌어도 계약 식을 바꾸지 않는다
        private double _lastSeconds = double.NegativeInfinity;

        // 돌려주는 값 = output에 쓴 개수(= raw.Length). 같은 (틱, 오프셋)은 KeyClass 순으로 놓는다(이 호출 안에서)
        public int Quantize(ReadOnlySpan<RawKeyEvent> raw, in InputClock clock, int inputDelayTicks, Span<QuantizedKey> output)
        {
            double tickSeconds = clock.TickSeconds;
            if (!(tickSeconds > 0) || double.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(clock), "TickSeconds must be positive and finite");
            }
            if (double.IsNaN(clock.EpochSeconds) || double.IsInfinity(clock.EpochSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(clock), "EpochSeconds must be finite");
            }
            if (inputDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputDelayTicks));
            }
            if (output.Length < raw.Length)
            {
                throw new ArgumentException("output is shorter than raw", nameof(output));
            }

            long delay = (long)inputDelayTicks << OffsetBits;
            for (int i = 0; i < raw.Length; i++)
            {
                RawKeyEvent e = raw[i];
                int k = (int)e.Key;
                if (k < 0 || k >= KeyState.KeyCount)
                {
                    throw new ArgumentException("unknown LogicalKey", nameof(raw));
                }
                double seconds = e.TimeSeconds;
                if (!(seconds >= _lastSeconds))            // 거꾸로 온 시각과 NaN. 앞이 없으면(−∞) epoch로 묶인다
                {
                    seconds = _lastSeconds;
                }
                long time = Units(seconds, clock.EpochSeconds, tickSeconds, out bool saturated);
                if (!saturated && !double.IsInfinity(seconds))
                {
                    _lastSeconds = seconds;
                }
                time = time > MaxTime - delay ? MaxTime : time + delay;
                output[i] = new QuantizedKey((uint)(time >> OffsetBits), (byte)(time & (OffsetsPerTick - 1)), e.Key, e.Down);
            }
            // 한 호출 안의 시계는 하나이고 하드웨어 시각이 줄지 않으므로, 이 정렬은 같은 (틱, 오프셋) 안에서만 자리를 바꾼다
            // (예외: 범위를 넘은 깨진 시각은 마지막 틱에 묶여 뒤로 간다)
            DetSortQuantized(output.Slice(0, raw.Length));
            return raw.Length;
        }

        // epoch 기준 1/256틱 단위 수(내림). 음수·NaN은 0, 너무 큰 값은 MaxTime(saturated = true)
        private static long Units(double timeSeconds, double epochSeconds, double tickSeconds, out bool saturated)
        {
            saturated = false;
            double u = (timeSeconds - epochSeconds) / tickSeconds * OffsetsPerTick;
            if (!(u > 0))
            {
                return 0;
            }
            if (u >= MaxTime)
            {
                saturated = true;
                return MaxTime;
            }
            return (long)Math.Floor(u);
        }

        private static void DetSortQuantized(Span<QuantizedKey> keys) => Wizard.Core.DetSort.InsertionStable(keys, default(TimeClassOrder));

        // (Tick, Offset, KeyClass). 동률은 안정 정렬이 들어온 순서를 지킨다
        private readonly struct TimeClassOrder : IComparer<QuantizedKey>
        {
            public int Compare(QuantizedKey a, QuantizedKey b)
            {
                long ta = a.Time, tb = b.Time;
                if (ta != tb)
                {
                    return ta < tb ? -1 : 1;
                }
                int ca = InputOrder.KeyClass(a.Key), cb = InputOrder.KeyClass(b.Key);
                return ca == cb ? 0 : (ca < cb ? -1 : 1);
            }
        }
    }
}
