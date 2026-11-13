using System;
using Wizard.Client.Duel;
using Wizard.Netcode.Duel;
using Wizard.Protocol;

namespace Wizard.Client.Tests.Duel
{
    // LocalDuel 과 같은 짜임을 시험이 손으로 엮은 것: 전송을 RecordingTransport 로 감싸고, 호스트(서버) 시계를 기기 시계와 다르게 줄 수 있다.
    // Frame 의 순서는 LocalDuel.Frame 과 같다(호스트 Pump → 세션 0 → 세션 1 → 호스트 Pump → 전체 상태)
    internal sealed class DuelRig
    {
        public readonly LoopbackHost Host;
        public readonly DuelSession[] Sessions = new DuelSession[2];
        public readonly RecordingTransport[] Transports = new RecordingTransport[2];
        public readonly DuelClientKeys[] Keys = new DuelClientKeys[2];
        public readonly SplitKeyboardInput Keyboard = new SplitKeyboardInput(KeyBindings.Default, KeyBindings.Alternate);
        public readonly HostTap Tap = new HostTap();

        // 서버 시계의 빠르기(ppm). 서버 시각 = 시작 + (기기 시각 − 시작) × (1 + ppm/10^6)
        public long ServerDriftPpm;

        private readonly Wizard.Core.ByteWriter _state = new Wizard.Core.ByteWriter();

        public DuelRig(ulong seed = 0xD0E1_5EED_0007UL, IAeadCipher? cipher = null)
        {
            cipher ??= DuelKit.Cipher();
            Host = new LoopbackHost(DuelKit.Db, DuelKit.Setup(seed), DuelKit.HostKeys(), cipher, DuelKit.HostOptions());
            Host.SetTap(Tap);
            for (int s = 0; s < 2; s++)
            {
                Keys[s] = new DuelClientKeys(DuelKit.Token(s), 1U, DuelKit.Key(s));
                Transports[s] = new RecordingTransport(Host.Connect(new Wizard.Core.PlayerSlot((byte)s)));
                Sessions[s] = new DuelSession(DuelKit.Db, DuelKit.Setup(seed), new Wizard.Core.PlayerSlot((byte)s), Keys[s], cipher, Transports[s],
                                              DuelKit.SessionOptions());
            }
        }

        public long ServerUs(long sinceStartUs) => DuelKit.StartUs + sinceStartUs + sinceStartUs * ServerDriftPpm / 1_000_000;

        public void Frame(long sinceStartUs)
        {
            double now = DuelKit.Sec(DuelKit.StartUs + sinceStartUs);
            long server = ServerUs(sinceStartUs);
            Host.Pump(server);
            Sessions[0].Step(Keyboard.Player(0), now);
            Sessions[1].Step(Keyboard.Player(1), now);
            Host.Pump(server);
            for (int s = 0; s < 2; s++)
            {
                if (Sessions[s].NeedsFullState)
                {
                    _state.Reset();
                    Host.Core.TryWriteFullState(_state, out uint tick);
                    Sessions[s].OnFullState(tick, _state.Written);
                }
            }
        }

        public long Run(long fromUs, long untilUs, Action<long>? beforeFrame = null)
        {
            long t = fromUs;
            for (; t <= untilUs; t += DuelKit.FrameUs)
            {
                beforeFrame?.Invoke(t);
                Frame(t);
            }
            return t;
        }

        public long Connect(long limitUs = 3_000_000)
        {
            long t = 0;
            while (Sessions[0].Phase != DuelSessionPhase.Running || Sessions[1].Phase != DuelSessionPhase.Running)
            {
                Xunit.Assert.True(t < limitUs, "the loopback handshake did not finish");
                Frame(t);
                t += DuelKit.FrameUs;
            }
            return t;
        }

        // 경기 시계 틱 tick 이 (서버 시계로) 시작한 뒤의 시각을 기기 시각(시작 기준 µs)으로 어림한다(drift 0 일 때 정확하다)
        public long MatchTickUs(long tick) => Host.MatchEpochUs - DuelKit.StartUs + (tick * DuelKit.UsPerSecond + 59) / 60;

        public void Press(long sinceStartUs, PhysicalKey key, bool down) =>
            Keyboard.OnPhysicalKey(key, down, DuelKit.Sec(DuelKit.StartUs + sinceStartUs));
    }
}
