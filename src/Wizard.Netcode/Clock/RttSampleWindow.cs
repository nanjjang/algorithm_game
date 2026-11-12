using System;
using Wizard.Content.Rules;

namespace Wizard.Netcode.Clock
{
    // CONTRACTS 6.4·10.5 "서버의 표본", DuelHostCore.MeasuredRttMs 의 재료. 왕복은 서버가 직접 잰다(클라이언트가 알려 준 왕복을 믿지 않는다, T-45).
    //   왕복 = 도착 시각 − 내가 보낸 시각 − hold. hold 가 RttHoldCapMs(데이터 값)를 넘는 표본은 버린다.
    //   최근 ClockSyncSamples(데이터 값, 처음 값 16)개의 최솟값이 그 사람의 왕복이고, 그 절반이 이른 시각 검사의 편도 추정이다(spec 10 14.8).
    // 표본의 출처는 둘이다: SyncRequest 의 (echo_server_seq → 보낸 시각, echo_hold_us)와
    // ClientInput 의 (echo_server_time_ms, echo_hold_ms). ms 칸은 호출자가 ×1000 해서 넘긴다(잘린 ms 는 왕복을 늘리는 쪽으로만 틀린다).
    // 소켓도 시계도 없다. 사람 한 명에 하나(호스트가 슬롯마다, 탐침이 토큰마다 가진다). 버퍼는 이 인스턴스가 가진다(정적 버퍼 없음).
    // Offer 와 조회는 할당하지 않는다.
    public sealed class RttSampleWindow
    {
        // 단위 환산
        private const long MicrosPerMilli = 1000;

        private readonly long[] _rttUs;
        private readonly long _holdCapUs;
        private int _count;
        private int _next;

        public RttSampleWindow(NetRuleSet rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (rules.ClockSyncSamples < 1)
            {
                // DataBuild 와 팩 읽기가 1 미만을 막는다. 여기에 닿으면 호출자가 만든 규칙이 깨진 것이다
                throw new ArgumentException("NetRuleSet.ClockSyncSamples is not positive", nameof(rules));
            }
            _rttUs = new long[rules.ClockSyncSamples];
            _holdCapUs = rules.RttHoldCapMs * MicrosPerMilli;
        }

        /// <summary>창의 크기(<see cref="NetRuleSet.ClockSyncSamples"/>)</summary>
        public int Capacity => _rttUs.Length;

        /// <summary>창에 든 표본 수(0..<see cref="Capacity"/>)</summary>
        public int Count => _count;

        /// <summary>
        /// 순수 함수. 왕복 = 도착 − 보낸 시각 − hold(µs). hold 가 음수이거나 holdCapMs 를 넘거나(같으면 받는다)
        /// 왕복이 음수이면(거짓 에코, 붙든 시간이 실제 간격보다 큼) false 이고 rttUs 는 0 이다.
        /// </summary>
        public static bool TryRoundTripUs(long arrivalUs, long sentUs, long holdUs, int holdCapMs, out long rttUs)
        {
            return TryRoundTripCore(arrivalUs, sentUs, holdUs, holdCapMs * MicrosPerMilli, out rttUs);
        }

        /// <summary>표본 하나를 계산해 받을 만하면 창에 넣는다(가장 오래된 것을 밀어낸다). 버렸으면 false</summary>
        public bool Offer(long arrivalUs, long sentUs, long holdUs)
        {
            if (!TryRoundTripCore(arrivalUs, sentUs, holdUs, _holdCapUs, out long rtt))
            {
                return false;
            }
            _rttUs[_next] = rtt;
            _next = _next + 1 == _rttUs.Length ? 0 : _next + 1;
            if (_count < _rttUs.Length)
            {
                _count++;
            }
            return true;
        }

        /// <summary>창에 든 표본의 최솟값(µs). 표본이 없으면 −1</summary>
        public long MinRttUs
        {
            get
            {
                if (_count == 0)
                {
                    return -1;
                }
                long min = long.MaxValue;
                for (int i = 0; i < _count; i++)
                {
                    if (_rttUs[i] < min)
                    {
                        min = _rttUs[i];
                    }
                }
                return min;
            }
        }

        /// <summary>
        /// MeasuredRttMs 의 값. 최솟값을 ms 로 <b>올림</b>한 것. 표본이 없으면 −1.
        /// 올림이라야 "60ms 이하"가 µs 로 본 것과 같다(60,001µs 는 61 이 되어 60 경계를 넘는다, T-60).
        /// </summary>
        public int MinRttMs
        {
            get
            {
                long us = MinRttUs;
                return us < 0 ? -1 : CeilMillis(us);
            }
        }

        /// <summary>편도 추정(µs) = 최소 왕복의 절반(내림). 표본이 없으면 −1. spec 10 14.8 의 이른 시각 검사가 쓴다</summary>
        public long OneWayUs
        {
            get
            {
                long us = MinRttUs;
                return us < 0 ? -1 : us / 2;
            }
        }

        /// <summary>표본을 모두 지운다(새 경기, 재접속으로 경로가 바뀐 때)</summary>
        public void Clear()
        {
            Array.Clear(_rttUs, 0, _rttUs.Length);
            _count = 0;
            _next = 0;
        }

        private static bool TryRoundTripCore(long arrivalUs, long sentUs, long holdUs, long holdCapUs, out long rttUs)
        {
            rttUs = 0;
            if (holdUs < 0 || holdUs > holdCapUs)
            {
                return false;
            }
            long rtt = arrivalUs - sentUs - holdUs;
            if (rtt < 0)
            {
                return false;
            }
            rttUs = rtt;
            return true;
        }

        // µs → ms 올림(음수 아님)
        private static int CeilMillis(long micros)
        {
            long ms = micros / MicrosPerMilli;
            if (micros % MicrosPerMilli != 0)
            {
                ms++;
            }
            return ms > int.MaxValue ? int.MaxValue : (int)ms;
        }
    }
}
