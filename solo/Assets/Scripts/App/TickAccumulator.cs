using System;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 고정 틱 누적기(순수). 프레임 시간을 쌓아 1/60초마다 한 스텝을 내준다(00 문서 4절).
    /// 프레임 하나가 너무 길면(창 끌기, 중단점) 0.25초로 자른다 → 한 프레임 최대 15스텝.
    /// 일시정지 중에는 시간을 쌓지 않는다(틱을 멈추는 것은 일시정지뿐).
    /// 오차가 쌓이지 않게 내부는 double로 센다.
    /// </summary>
    public sealed class TickAccumulator
    {
        public const double StepSeconds = 1.0 / SimConst.TicksPerSecond;
        public const float MaxFrameSeconds = 0.25f;
        // float 프레임 시간(1/60f = 0.016666668)과 double 스텝의 반올림 차이로 한 스텝이 밀리지 않게 둔 여유.
        const double Epsilon = 1e-7;

        double _acc;

        /// <summary>아직 스텝으로 바꾸지 않은 시간(초, 0 이상 1/60 미만).</summary>
        public double Accumulated => _acc;

        /// <summary>지난 틱과 다음 틱 사이 보간 값(0~1).</summary>
        public float Alpha
        {
            get
            {
                double a = _acc / StepSeconds;
                if (a < 0) a = 0;
                if (a > 1) a = 1;
                return (float)a;
            }
        }

        /// <summary>남은 시간을 틱 안의 입력 시각(u, 0~255)으로.</summary>
        public long OffsetU
        {
            get
            {
                long u = (long)Math.Floor(_acc * SimConst.UnitsPerSecond);
                if (u < 0) u = 0;
                if (u > SimConst.UnitsPerTick - 1) u = SimConst.UnitsPerTick - 1;
                return u;
            }
        }

        /// <summary>프레임 시간 dt(초)를 쌓고 이번 프레임에 돌릴 스텝 수를 돌려준다. paused면 쌓지 않고 0.</summary>
        public int Advance(float dt, bool paused = false)
        {
            if (paused) return 0;
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            if (dt > MaxFrameSeconds) dt = MaxFrameSeconds;
            _acc += dt;
            int steps = 0;
            while (_acc + Epsilon >= StepSeconds)
            {
                _acc -= StepSeconds;
                steps++;
            }
            if (_acc < 0) _acc = 0;
            return steps;
        }

        public void Reset() { _acc = 0; }
    }
}
