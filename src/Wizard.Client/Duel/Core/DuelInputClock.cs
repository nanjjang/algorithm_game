using System;
using Wizard.Content.Rules;
using Wizard.Netcode.Clock;
using Wizard.Sim;

namespace Wizard.Client.Duel
{
    // 결투의 입력 시계(CONTRACTS 6.4 "시계 둘", spec 10 14.4). 서버와 맞춘 경기 시계이고 멈추지 않는다. 키 이벤트의 유효 틱(누른 틱 + D)과
    // 패킷의 sealed_through_tick 의 기준이다. 실수 시각은 Wizard.Client 까지만 쓴다(InputClock 과 같은 초 단위).
    //
    // 시작: 연결 절차의 오프셋(서버 시각 = 클라이언트 시각 + offset)과 HelloAck 의 match_epoch_server_us 로 틱 0 의 클라이언트 시각을 정한다.
    // 경기 중: DuelClientCore.DrainClockSamples 의 표본(ServerUs 는 경기 시계의 µs, T-62 9번)에 match_epoch_server_us 를 더해 서버 시계로 옮기고,
    //   ClockResyncTicks(데이터 값)마다 최근 표본으로 오프셋을 다시 재어 "서버가 보는 경기 시각 − 내 입력 시계의 경기 시각"을 오차로 본다.
    //   틱 길이를 ClockSync.TickLengthAdjustPpm(±ClockSlewMaxPpm, 데이터 값)으로만 바꾼다. 틱을 건너뛰지 않는다: 바꾸는 순간의 틱 위치를
    //   그대로 두고 그 뒤의 길이만 바뀐다(TV-N31). 예측의 따라잡기는 이 보정으로 하지 않는다(DuelClientCore 의 CatchupTicksPerAdvance).
    public sealed class DuelInputClock
    {
        private const double MicrosPerSecond = 1000000.0;   // 단위 환산
        private const double PpmPerUnit = 1000000.0;        // 단위 환산(1 = 1,000,000ppm)

        private readonly NetRuleSet _rules;
        private readonly double _baseTickSeconds = 1.0 / SimVersion.TickRate;
        private readonly ClockSample[] _window;
        private int _windowCount;
        private int _windowNext;

        private double _epochSeconds;
        private double _tickSeconds;
        private long _matchEpochServerUs;
        private int _adjustPpm;

        public DuelInputClock(NetRuleSet rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (rules.ClockSyncSamples < 1)
            {
                throw new ArgumentException("NetRuleSet.ClockSyncSamples is not positive", nameof(rules));
            }
            _window = new ClockSample[rules.ClockSyncSamples];
            _tickSeconds = _baseTickSeconds;
        }

        public bool IsStarted { get; private set; }

        // 틱 0 이 시작하는 클라이언트 시각(초)
        public double EpochSeconds => _epochSeconds;

        // 지금의 틱 길이(초). 늘 기본 길이 × (1 ± ClockSlewMaxPpm / 10^6) 안이다
        public double TickSeconds => _tickSeconds;

        // 마지막 보정의 ppm(틱 길이 = 기본 × (10^6 + 값) / 10^6)
        public int AdjustPpm => _adjustPpm;

        public long MatchEpochServerUs => _matchEpochServerUs;

        // InputQuantizer 에 주는 시계
        public InputClock InputClock => new InputClock(_epochSeconds, _tickSeconds);

        public void Start(long matchEpochServerUs, in ClockOffset offset)
        {
            if (!offset.IsValid)
            {
                throw new ArgumentException("the clock offset is not measured", nameof(offset));
            }
            _matchEpochServerUs = matchEpochServerUs;
            _epochSeconds = (matchEpochServerUs - offset.OffsetUs) / MicrosPerSecond;
            _tickSeconds = _baseTickSeconds;
            _adjustPpm = 0;
            _windowCount = 0;
            _windowNext = 0;
            IsStarted = true;
        }

        // 지금 시각의 틱(내림). 틱 0 전(카운트다운 시작 전)이면 음수
        public long TickAt(double nowSeconds) => (long)Math.Floor((nowSeconds - _epochSeconds) / _tickSeconds);

        // 경기 중 표본 하나(DuelClientCore.DrainClockSamples 의 값 그대로. ServerUs 는 경기 시계)
        public void AddMatchSample(in ClockSample matchSample)
        {
            _window[_windowNext] = new ClockSample(matchSample.T0Us, matchSample.ServerUs + _matchEpochServerUs, matchSample.T1Us);
            _windowNext = (_windowNext + 1) % _window.Length;
            if (_windowCount < _window.Length)
            {
                _windowCount++;
            }
        }

        public int SampleCount => _windowCount;

        // 최근 표본으로 오차를 재고 틱 길이를 맞춘다. 표본이 없으면 아무것도 하지 않고 false. 돌려준 errorUs = 서버가 보는 경기 시각 − 내 경기 시각
        public bool Resync(double nowSeconds, out long errorUs)
        {
            errorUs = 0;
            if (!IsStarted || _windowCount == 0)
            {
                return false;
            }
            ClockOffset offset = ClockSync.Estimate(new ReadOnlySpan<ClockSample>(_window, 0, _windowCount));
            if (!offset.IsValid)
            {
                return false;
            }
            double nowUs = nowSeconds * MicrosPerSecond;
            double serverMatchUs = nowUs + offset.OffsetUs - _matchEpochServerUs;
            double position = (nowSeconds - _epochSeconds) / _tickSeconds;              // 내 시계의 틱 위치
            double mineMatchUs = position * _baseTickSeconds * MicrosPerSecond;
            errorUs = (long)Math.Round(serverMatchUs - mineMatchUs);
            int ppm = ClockSync.TickLengthAdjustPpm(errorUs, _rules);
            double next = _baseTickSeconds * (PpmPerUnit + ppm) / PpmPerUnit;
            _epochSeconds = nowSeconds - position * next;                               // 틱 위치를 그대로 둔다(건너뛰지 않는다)
            _tickSeconds = next;
            _adjustPpm = ppm;
            return true;
        }
    }
}
