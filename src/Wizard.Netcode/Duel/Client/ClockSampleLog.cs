using System;
using Wizard.Netcode.Clock;

namespace Wizard.Netcode.Duel
{
    // 경기 중 시계 표본(T-62 9번, CONTRACTS 10.4 "경기 중에는 ServerTick 의 시각 칸으로 다시 잰다", spec 10 14.4).
    // 보낸 ClientInput 마다 (client_send_time_ms, 보낸 시각 µs)를 기억하고, 받은 ServerTick 의 echo_client_send_time_ms 가
    // 그중 하나와 맞으면 표본 (T0 = 기억한 µs, Server = server_time_ms × 1,000, T1 = 받은 시각)을 하나 만든다.
    // 맞는 것이 없으면(서버가 아직 내 패킷을 받지 못했다, 기억에서 밀려날 만큼 오래됐다) 만들지 않는다.
    // 한 보낸 시각으로는 표본을 한 번만 만든다(서버는 새 패킷을 받을 때까지 같은 값을 되풀이해 돌려준다).
    // 꺼내지 않은 표본이 ClockSyncSamples 개를 넘으면 가장 오래된 것부터 버린다.
    // 배열은 생성자에서 한 번만 잡는다(OnPacket·PollOutgoing 에서 힙에 할당하지 않는다).
    internal sealed class ClockSampleLog
    {
        // 자료구조 크기. 매 틱 한 패킷 + 키 이벤트마다 한 패킷이므로 약 1초 남짓의 보낸 시각을 기억한다.
        // 이보다 오래된 보낸 시각이 돌아오면 왕복이 이미 1초를 넘은 것이라 쓸 만한 표본이 아니다
        private const int SentCapacity = 64;
        private const long UsPerMs = 1000L;     // 단위 환산 상수

        private readonly uint[] _sentMs = new uint[SentCapacity];
        private readonly long[] _sentUs = new long[SentCapacity];
        private readonly bool[] _sentUsed = new bool[SentCapacity];   // 이 보낸 시각으로 이미 표본을 만들었다
        private int _sentHead;                                          // 다음에 쓸 칸
        private int _sentCount;

        private readonly ClockSample[] _samples;
        private int _sampleHead;                                        // 가장 오래된 표본의 칸
        private int _sampleCount;

        public ClockSampleLog(int sampleCapacity)
        {
            if (sampleCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCapacity));
            }
            _samples = new ClockSample[sampleCapacity];
        }

        public int PendingSamples => _sampleCount;

        // 봉인해 내보낸 ClientInput 하나의 보낸 시각. sentMs 는 그 패킷의 client_send_time_ms 칸 값이다.
        // 같은 ms 에 두 패킷을 보냈으면 먼저 보낸 것만 기억한다: 서버가 돌려준 ms 가 어느 쪽인지 가를 수 없고,
        // 먼저 보낸 시각을 쓰면 왕복이 크게 잡힐 뿐 작게 잡히지 않는다(ClockSync 는 왕복이 가장 짧은 표본을 쓴다)
        public void OnSent(uint sentMs, long sentUs)
        {
            if (_sentCount > 0 && _sentMs[Prev(_sentHead)] == sentMs)
            {
                return;
            }
            _sentMs[_sentHead] = sentMs;
            _sentUs[_sentHead] = sentUs;
            _sentUsed[_sentHead] = false;
            _sentHead = Next(_sentHead);
            if (_sentCount < SentCapacity)
            {
                _sentCount++;
            }
        }

        // 연 ServerTick 하나. 최근 보낸 것부터 찾는다(ms 칸은 2^32 ms 마다 돌아오므로 가장 최근의 같은 값이 그 패킷이다)
        public void OnServerTick(uint echoClientSendTimeMs, uint serverTimeMs, long receivedUs)
        {
            int i = _sentHead;
            for (int k = 0; k < _sentCount; k++)
            {
                i = Prev(i);
                if (_sentMs[i] != echoClientSendTimeMs)
                {
                    continue;
                }
                if (_sentUsed[i])
                {
                    return;
                }
                _sentUsed[i] = true;
                Push(new ClockSample(_sentUs[i], serverTimeMs * UsPerMs, receivedUs));
                return;
            }
        }

        // 가장 오래된 것부터 buffer 에 옮기고 옮긴 수를 돌려준다. 다 옮기지 못한 표본은 다음 호출에 남는다
        public int Drain(Span<ClockSample> buffer)
        {
            int n = 0;
            while (n < buffer.Length && _sampleCount > 0)
            {
                buffer[n++] = _samples[_sampleHead];
                _sampleHead = _sampleHead + 1 == _samples.Length ? 0 : _sampleHead + 1;
                _sampleCount--;
            }
            return n;
        }

        private void Push(ClockSample s)
        {
            if (_sampleCount == _samples.Length)
            {
                // 가득 찼다: 가장 오래된 것을 버린다
                _sampleHead = _sampleHead + 1 == _samples.Length ? 0 : _sampleHead + 1;
                _sampleCount--;
            }
            int tail = _sampleHead + _sampleCount;
            if (tail >= _samples.Length)
            {
                tail -= _samples.Length;
            }
            _samples[tail] = s;
            _sampleCount++;
        }

        private static int Next(int i) => i + 1 == SentCapacity ? 0 : i + 1;

        private static int Prev(int i) => i == 0 ? SentCapacity - 1 : i - 1;
    }
}
