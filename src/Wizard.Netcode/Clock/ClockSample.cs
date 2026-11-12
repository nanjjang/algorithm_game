namespace Wizard.Netcode.Clock
{
    // CONTRACTS 6.4·10.4, spec 10 14.4 Sync 의 표본 하나: 클라이언트가 보내고(t0) 서버가 시각을 찍고(ts) 클라이언트가 받았다(t1).
    // 경기 전에는 SyncRequest.t0_us / SyncReply.server_time_us / SyncReply 를 받은 시각,
    // 경기 중에는 ServerTick 의 echo_client_send_time_ms / server_time_ms / 받은 시각이다(ms 칸은 호출자가 µs 로 바꿔 넘긴다).
    // T0Us 와 T1Us 는 같은 클라이언트 단조 시계의 값이다. 벽시계를 쓰지 않는다.
    public readonly struct ClockSample
    {
        /// <summary>보낸 시각(클라이언트 단조 시계, µs)</summary>
        public readonly long T0Us;

        /// <summary>서버가 답에 적은 서버 시각(µs)</summary>
        public readonly long ServerUs;

        /// <summary>답을 받은 시각(클라이언트 단조 시계, µs)</summary>
        public readonly long T1Us;

        public ClockSample(long t0Us, long serverUs, long t1Us)
        {
            T0Us = t0Us;
            ServerUs = serverUs;
            T1Us = t1Us;
        }

        /// <summary>왕복 = t1 − t0(µs). 음수면 쓸 수 없는 표본이다(<see cref="ClockSync.Estimate"/>가 건너뛴다)</summary>
        public long RttUs => T1Us - T0Us;
    }
}
