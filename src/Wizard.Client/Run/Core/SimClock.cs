using System;
using Wizard.Sim;

namespace Wizard.Client.Run
{
    // ARCHITECTURE 6.1, CONTRACTS 6.5("무한 웨이브는 SimClock"). 무한 웨이브·연습의 틱 시계. FixedTickDriver 가 프레임마다 부른다:
    //
    //   while (clock.NextTickDue(now)) { session.Step(clock.Tick); clock.Advance(); }
    //   alpha = clock.Alpha(now);
    //
    // - 받는 시각은 단조 시계 값(초)뿐이다. 앞서 받은 값보다 이른 값은 앞서 받은 값으로 본다(시각이 거꾸로 가도 틱이 줄거나 늘지 않는다).
    //   NaN·무한대는 시계가 깨진 것이라 ArgumentOutOfRangeException 이다(무한대를 받으면 틱 루프가 끝나지 않는다).
    //   RawKeyEvent.TimeSeconds 와 같은 시간 축이어야 한다(InputClock 의 epoch 를 같이 쓴다).
    // - 틱 k 는 epoch + k·T 에서 시작해 epoch + (k+1)·T 에 끝난다. 틱 k 는 그 구간이 다 지난 뒤에 돈다(그 구간의 키를 다 모은 뒤).
    //   마감 시각을 매번 epoch + (k+1)·T 로 새로 계산하므로 프레임 간격이 흔들려도 오차가 쌓이지 않는다:
    //   루프가 끝난 뒤의 Tick 은 floor((now − epoch) / T) 다(부동소수 경계에서 1틱 안).
    // - 따라잡는 틱 수에 상한을 두지 않는다. 상한을 두면 틱 시간이 실제 시간보다 느려지고 그 차이는 기록에 남지 않는다
    //   (spec 09의 est_end = started_at + end_tick/60 + pause_total, 느리게 돌리기 탐지). [임시] OPEN-QUESTIONS.
    // - 틱을 멈추는 것은 일시정지뿐이다(T-13). Pause 뒤에는 NextTickDue 가 거짓이고, Resume 이 epoch 를 멈춰 있던 만큼 민다.
    //   Halt(판 끝, 틱 상한)는 되돌릴 수 없다. Advance 는 멈춘 뒤에도 부를 수 있다(멈추게 한 틱 하나를 센다).
    // 실수는 이 계층(표시 쪽 시계)까지만 있다. 세계에는 틱 번호만 들어간다(T-27).
    public sealed class SimClock
    {
        // 1/60초(SimVersion.TickRate). 단위 환산
        public static readonly double DefaultTickSeconds = 1.0 / SimVersion.TickRate;

        private readonly double _tickSeconds;
        private double _epochSeconds;                     // 틱 0이 시작하는 시각
        private double _lastNow;                          // 지금까지 받은 가장 늦은 시각
        private uint _tick;                               // 다음에 돌 틱
        private bool _paused;
        private bool _halted;
        private double _stoppedAt;                        // 멈춘(Pause·Halt) 순간의 시각. 표시 보간 비율을 그 값에 묶는다

        // startSeconds 에 틱 startTick 이 시작한다
        public SimClock(double startSeconds, uint startTick = 0)
            : this(startSeconds, startTick, DefaultTickSeconds)
        {
        }

        public SimClock(double startSeconds, uint startTick, double tickSeconds)
        {
            RequireFinite(startSeconds, nameof(startSeconds));
            if (!(tickSeconds > 0) || double.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds));
            }
            _tickSeconds = tickSeconds;
            _epochSeconds = startSeconds - (double)startTick * tickSeconds;
            _lastNow = startSeconds;
            _tick = startTick;
        }

        // 다음에 돌 틱 번호(= 세계의 Tick)
        public uint Tick => _tick;

        public double TickSeconds => _tickSeconds;

        public double EpochSeconds => _epochSeconds;

        public bool IsPaused => _paused;

        public bool IsHalted => _halted;

        // 키 양자화의 시계(CONTRACTS 6.5). 일시정지가 풀리면 epoch 가 밀리므로 프레임마다 새로 읽는다
        public InputClock InputClock => new InputClock(_epochSeconds, _tickSeconds);

        // 틱 Tick 의 구간이 다 지났고 멈춰 있지 않으면 참
        public bool NextTickDue(double nowSeconds)
        {
            double now = Sample(nowSeconds);
            if (_paused || _halted || _tick == uint.MaxValue)
            {
                return false;
            }
            return now >= DueAt(_tick);
        }

        // 틱 하나를 셌다. session.Step(Tick) 바로 뒤에 부른다
        public void Advance()
        {
            if (_tick == uint.MaxValue)
            {
                throw new InvalidOperationException("SimClock tick space is exhausted");
            }
            _tick++;
        }

        // 표시 보간 비율 0~1: 마지막으로 돈 틱 뒤로 지난 시간 / T. 멈춰 있으면 멈춘 순간의 값에 묶인다
        public double Alpha(double nowSeconds)
        {
            double now = Sample(nowSeconds);
            return Fraction(_paused || _halted ? _stoppedAt : now);
        }

        // 일시정지 시작. 마지막으로 받은 시각을 멈춘 순간으로 본다(세션이 Step 안에서 부른다)
        public void Pause()
        {
            if (_halted)
            {
                throw new InvalidOperationException("SimClock is halted");
            }
            if (_paused)
            {
                return;
            }
            _paused = true;
            _stoppedAt = _lastNow;
        }

        // 일시정지를 푼다. 멈춘 순간의 보간 비율에서 이어지도록 epoch 를 민다. 돌려주는 값 = 민 시간(초) = 틱이 돌지 않은 실제 시간
        public double Resume(double nowSeconds)
        {
            double now = Sample(nowSeconds);
            if (!_paused)
            {
                throw new InvalidOperationException("SimClock is not paused");
            }
            double shifted = ShiftedEpoch(now);
            double paused = shifted - _epochSeconds;
            _epochSeconds = shifted;
            _paused = false;
            return paused;
        }

        // 지금 풀면 민 시간이 얼마일지(일시정지 중에 판을 끝낼 때 PauseMark 에 쓴다). 상태를 바꾸지 않는다
        public double PausedSeconds(double nowSeconds)
        {
            double now = Sample(nowSeconds);
            return _paused ? ShiftedEpoch(now) - _epochSeconds : 0;
        }

        // 다시는 틱을 돌리지 않는다(판 끝, 틱 상한, 기록 마감)
        public void Halt()
        {
            if (_halted)
            {
                return;
            }
            if (!_paused)
            {
                _stoppedAt = _lastNow;
            }
            _halted = true;
        }

        // 단조로 묶은 시각. 세션도 같은 값을 쓴다
        internal double Sample(double nowSeconds)
        {
            RequireFinite(nowSeconds, nameof(nowSeconds));
            if (nowSeconds > _lastNow)
            {
                _lastNow = nowSeconds;
            }
            return _lastNow;
        }

        private double DueAt(uint tick) => _epochSeconds + ((double)tick + 1) * _tickSeconds;

        private double Fraction(double at)
        {
            double a = (at - (_epochSeconds + (double)_tick * _tickSeconds)) / _tickSeconds;
            if (!(a > 0))
            {
                return 0;
            }
            return a < 1 ? a : 1;
        }

        // 지금 풀 때의 epoch: 지금 = epoch' + (Tick + 멈춘 순간의 비율)·T. 앞으로만 민다
        private double ShiftedEpoch(double now)
        {
            double shifted = now - ((double)_tick + Fraction(_stoppedAt)) * _tickSeconds;
            return shifted > _epochSeconds ? shifted : _epochSeconds;
        }

        private static void RequireFinite(double seconds, string name)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(name, "monotonic clock value must be finite");
            }
        }
    }
}
