using System;
using System.Collections.Generic;
using Wizard.Client.Duel;
using Wizard.Client.Net.Crypto;
using Wizard.Content;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Client.Tests.Duel
{
    // 결투 세션 시험의 도구. 시각은 정수 µs 로 들고 다니고 세션에 넘길 때만 초(double)로 바꾼다.
    // 팩은 저장소 data/(RunKit.Db), 암호는 제품 구현(BouncyCastleAeadCipher, RFC 8439).
    internal static class DuelKit
    {
        public const uint Token0 = 0x51A70001U;
        public const uint Token1 = 0x51A70002U;
        public const long UsPerSecond = 1_000_000;
        public const long StartUs = 4_000_000_321;      // 0 이 아닌 기기 시각(기기가 켜진 지 4000초)
        public const long FrameUs = 16_667;             // 시험의 프레임 간격(60Hz 근처)
        public const long RetryUs = 100_000;            // 시험이 주는 인사 재전송 간격
        public const long LeadUs = 200_000;             // 시험이 주는 시작 여유

        public static ContentDb Db => Wizard.Client.Tests.Run.RunKit.Db;

        public static readonly PlayerSlot S0 = new PlayerSlot(0);
        public static readonly PlayerSlot S1 = new PlayerSlot(1);

        public static double Sec(long us) => us / (double)UsPerSecond;

        public static DuelSetup Setup(ulong seed = 0xD0E1_5EED_0007UL) => new DuelSetup
        {
            MatchId = new Guid("6a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d"),
            Seed = seed,
            CoreVersion = SimVersion.CoreVersion,
            DataVersion = Db.DataVersion,
            Options = new DuelRoomOptions
            {
                Rounds = 3,
                RoundSeconds = 90,
                HpMultPermille = 1000,
                EnhanceCap = 13,
                AllowMutations = true,
                FloorColor = TimeOfDay.Day,
            },
            Players = new[]
            {
                new DuelPlayerSetup
                {
                    Slot = S0,
                    AppliedWand = new WandSnapshot { Character = CharacterId.Baseline, Stage = 1, Core = BlockId.MagicBolt },
                    ActualStage = 1,
                    MaxHpPoints = 300,
                },
                new DuelPlayerSetup
                {
                    Slot = S1,
                    AppliedWand = new WandSnapshot { Character = CharacterId.Baseline, Stage = 1, Core = BlockId.MagicBolt },
                    ActualStage = 1,
                    MaxHpPoints = 300,
                },
            },
        };

        public static byte[] Key(int slot)
        {
            var k = new byte[DuelWire.KeyBytes];
            for (int i = 0; i < k.Length; i++)
            {
                k[i] = (byte)(i * 11 + slot * 97 + 5);
            }
            return k;
        }

        public static uint Token(int slot) => slot == 0 ? Token0 : Token1;

        public static DuelHostKeys HostKeys() =>
            new DuelHostKeys(new DuelPeerKey(Token0, 1U, Key(0)), new DuelPeerKey(Token1, 1U, Key(1)));

        public static LoopbackHostOptions HostOptions() => new LoopbackHostOptions { StartLeadUs = LeadUs };

        public static DuelSessionOptions SessionOptions() => new DuelSessionOptions { ClientBuild = 7U, HandshakeRetryUs = RetryUs };

        public static IAeadCipher Cipher() => new BouncyCastleAeadCipher();

        public static LocalDuel NewLocal(ulong seed = 0xD0E1_5EED_0007UL) =>
            new LocalDuel(Db, Setup(seed), HostKeys(), Cipher(), KeyBindings.Default, KeyBindings.Alternate, HostOptions(), SessionOptions());

        // startUs 부터 프레임마다 now 를 FrameUs 씩 올리며 until 까지 돈다. 프레임 직전에 beforeFrame(지난 µs)을 부른다. 마지막 now 를 돌려준다
        public static long Run(LocalDuel duel, long fromUs, long untilUs, Action<long>? beforeFrame = null)
        {
            long t = fromUs;
            for (; t <= untilUs; t += FrameUs)
            {
                beforeFrame?.Invoke(t);
                duel.Frame(Sec(StartUs + t));
            }
            return t;
        }

        // 두 세션이 다 Running 이 될 때까지(연결 절차). 지난 µs 를 돌려준다
        public static long Connect(LocalDuel duel, long fromUs = 0, long limitUs = 3_000_000)
        {
            long t = fromUs;
            while (duel.Session(S0).Phase != DuelSessionPhase.Running || duel.Session(S1).Phase != DuelSessionPhase.Running)
            {
                Assert.True(t < limitUs, "the loopback handshake did not finish");
                duel.Frame(Sec(StartUs + t));
                t += FrameUs;
            }
            return t;
        }

        // 경기 시계의 틱 tick 이 시작한 뒤의 첫 프레임 시각(시작 기준 µs)
        public static long MatchTickUs(LocalDuel duel, long tick) =>
            duel.Host.MatchEpochUs - StartUs + (tick * UsPerSecond + 59) / 60;

        public static DuelPacketHeader Header(ReadOnlySpan<byte> packet)
        {
            Assert.Equal(DuelCodecError.None, DuelPacketFrame.TryPeekHeader(packet, out DuelPacketHeader h));
            return h;
        }

        public static byte[] Open(ReadOnlySpan<byte> packet, int slot, DuelDirection direction)
        {
            var body = new byte[DuelWire.MaxBodyBytes];
            Assert.True(DuelPacketFrame.TryOpen(Cipher(), Key(slot), direction, packet, body, out _, out int n, out DuelCodecError e), e.ToString());
            return body.AsSpan(0, n).ToArray();
        }
    }

    // 전송을 감싸 보낸 것과 받은 것을 그대로 적는다. Ops 는 호출 순서("send:<종류>", "recv:<종류>", "recv:none")
    internal sealed class RecordingTransport : IDuelTransport
    {
        private readonly IDuelTransport _inner;

        public readonly List<byte[]> Sent = new List<byte[]>();
        public readonly List<byte[]> Received = new List<byte[]>();
        public readonly List<string> Ops = new List<string>();

        public RecordingTransport(IDuelTransport inner)
        {
            _inner = inner;
        }

        public DuelTransportKind Kind => _inner.Kind;

        public void Send(ReadOnlySpan<byte> packet)
        {
            Sent.Add(packet.ToArray());
            Ops.Add("send:" + DuelKit.Header(packet).Kind);
            _inner.Send(packet);
        }

        public int Receive(Span<byte> buffer)
        {
            int n = _inner.Receive(buffer);
            if (n > 0)
            {
                Received.Add(buffer.Slice(0, n).ToArray());
                Ops.Add("recv:" + DuelKit.Header(buffer.Slice(0, n)).Kind);
            }
            else
            {
                Ops.Add("recv:none");
            }
            return n;
        }

        public void Dispose() => _inner.Dispose();
    }

    // LoopbackHost 와 DuelHostCore 사이를 그대로 적는다(같은 호출 열을 다른 호스트에 줄 수 있게). 내려간 ServerTick 을 열어
    // 확정 틱의 이벤트를 틱마다 한 번만 모은다
    internal sealed class HostTap : ILoopbackTap
    {
        public readonly List<(int Op, byte[] Packet, long TimeUs, ulong Address, byte Slot)> Calls =
            new List<(int, byte[], long, ulong, byte)>();

        public readonly SortedDictionary<uint, InputEvent[][]> Confirmed = new SortedDictionary<uint, InputEvent[][]>();

        public const int OpPacket = 0, OpAdvance = 1, OpSend = 2;

        public void OnHostPacket(ReadOnlySpan<byte> packet, long receiveMatchUs, ulong fromAddress) =>
            Calls.Add((OpPacket, packet.ToArray(), receiveMatchUs, fromAddress, 0));

        public void OnHostAdvance(long matchUs) => Calls.Add((OpAdvance, Array.Empty<byte>(), matchUs, 0UL, 0));

        public void OnHostSend(PlayerSlot to, ReadOnlySpan<byte> packet, ulong toAddress)
        {
            Calls.Add((OpSend, packet.ToArray(), 0L, toAddress, to.Value));
            byte[] body = DuelKit.Open(packet, to.Value, DuelDirection.ServerToClient);
            var st = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, st.TryRead(body));
            for (int i = 0; i < st.ConfirmedCount; i++)
            {
                uint tick = st.ConfirmedFromTick + (uint)i;
                if (!Confirmed.ContainsKey(tick))
                {
                    Confirmed[tick] = new[] { st.SlotEvents(i, 0).ToArray(), st.SlotEvents(i, 1).ToArray() };
                }
            }
        }

        // 확정 열에서 그 슬롯의 이벤트(틱 오름차순)
        public List<InputEvent> EventsOf(int slot)
        {
            var list = new List<InputEvent>();
            foreach (var kv in Confirmed)
            {
                list.AddRange(kv.Value[slot]);
            }
            return list;
        }
    }
}
