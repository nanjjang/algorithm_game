using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Duel;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // DuelHostCore 시험의 공용 도구: 저장소 data 의 팩, 결투 설정, 키, 시험용 AEAD, 가짜 클라이언트, ServerTick 읽기
    internal static class HostKit
    {
        public const uint Token0 = 0x0A0B0C01U;
        public const uint Token1 = 0x0A0B0C02U;
        public const ulong Addr0 = 0x1111UL;
        public const ulong Addr1 = 0x2222UL;

        private const long MicrosPerSecond = 1000000;
        private const long TicksPerSecond = 60;

        private static readonly Lazy<ContentDb> DbLazy = new Lazy<ContentDb>(BuildDb);

        public static ContentDb Db => DbLazy.Value;

        public static readonly PlayerSlot S0 = new PlayerSlot(0);
        public static readonly PlayerSlot S1 = new PlayerSlot(1);

        private static ContentDb BuildDb()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return ContentDb.Build(ContentPack.Load(o.Pack!));
        }

        // 캐릭터는 기준값(Baseline): 저장소 data 에는 아직 char.1~char.5 의 줄이 없다(M5-DAT-01)
        public static DuelSetup Setup(ulong seed = 0xD0E1_5EEDUL) => new DuelSetup
        {
            MatchId = new Guid("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0"),
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
                k[i] = (byte)(i * 7 + slot * 101 + 3);
            }
            return k;
        }

        public static DuelPeerKey PeerKey(int slot) => new DuelPeerKey(slot == 0 ? Token0 : Token1, 1U, Key(slot));

        public static DuelHostKeys Keys() => new DuelHostKeys(PeerKey(0), PeerKey(1));

        public static DuelHostCore NewHost(IAeadCipher? cipher = null, ulong seed = 0xD0E1_5EEDUL) =>
            new DuelHostCore(Db, Setup(seed), Keys(), cipher ?? new TestCipher());

        // 틱 tick 의 시작 시각(µs, 올림 = 그 틱이 이미 시작한 첫 µs)
        public static long TickStartUs(long tick) => CeilDiv(tick * MicrosPerSecond, TicksPerSecond);

        // tick + num/den 틱 시각(µs, 내림)
        public static long AtTicks(long tick, long num, long den) =>
            FloorDiv((tick * den + num) * MicrosPerSecond, den * TicksPerSecond);

        private static long CeilDiv(long a, long b) => -FloorDiv(-a, b);

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if (a % b != 0 && (a < 0) != (b < 0))
            {
                q--;
            }
            return q;
        }

        public static InputEvent Ev(uint tick, byte offset, LogicalKey key, bool down, uint seq) => new InputEvent(tick, offset, key, down, seq);
    }

    // 시험용 IAeadCipher: .NET 의 ChaCha20Poly1305(RFC 8439). 제품 구현은 M2-NET-02 의 것이고 Netcode 시험 프로젝트는 그 어셈블리를 참조하지 않는다.
    // 이 기기에서 지원되지 않으면 쓰는 시험이 실패한다(건너뛰지 않는다)
    internal sealed class TestCipher : IAeadCipher
    {
        private const int TagBytes = 16;

        public int OpenCalls;

        public void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> plaintext,
                         Span<byte> ciphertextAndTag)
        {
            using var c = new ChaCha20Poly1305(key);
            c.Encrypt(nonce12, plaintext, ciphertextAndTag.Slice(0, plaintext.Length), ciphertextAndTag.Slice(plaintext.Length, TagBytes), aad);
        }

        public bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertextAndTag,
                         Span<byte> plaintext)
        {
            OpenCalls++;
            if (ciphertextAndTag.Length < TagBytes)
            {
                return false;
            }
            int n = ciphertextAndTag.Length - TagBytes;
            byte[] tmp = new byte[n];
            using var c = new ChaCha20Poly1305(key);
            try
            {
                c.Decrypt(nonce12, ciphertextAndTag.Slice(0, n), ciphertextAndTag.Slice(n, TagBytes), tmp, aad);
            }
            catch (AuthenticationTagMismatchException)
            {
                return false;
            }
            tmp.CopyTo(plaintext);
            return true;
        }
    }

    // 한 사람의 클라이언트 흉내: ClientInput 을 짜서 봉인하고 호스트에 넣는다. 순번 매기기·창 채우기는 시험이 직접 정한다
    internal sealed class FakeClient
    {
        private readonly int _slot;
        private readonly uint _token;
        private readonly byte[] _key;
        private readonly IAeadCipher _cipher = new TestCipher();
        private readonly ClientInputPacket _packet = new ClientInputPacket();
        private ulong _packetNumber;

        public FakeClient(int slot, byte[]? key = null, uint? token = null)
        {
            _slot = slot;
            _key = key ?? HostKit.Key(slot);
            _token = token ?? (slot == 0 ? HostKit.Token0 : HostKit.Token1);
        }

        public ulong Address => _slot == 0 ? HostKit.Addr0 : HostKit.Addr1;

        // 다음 패킷 번호를 n 만큼 건너뛴다(받아 놓고 쓰지 못한 번호, DuelSendCounter. 빈 번호는 허용이다)
        public void SkipPacketNumbers(ulong n) => _packetNumber += n;

        // baseSeq 를 주지 않으면 10.2 의 규칙(첫 이벤트 바로 앞 순번, 이벤트가 없으면 lastSeq)
        public byte[] Build(uint sealedThrough, InputEvent[] events, uint? baseSeq = null, uint lastSeq = 0, uint ack = 0,
                            uint echoMs = 0, ushort holdMs = 0, uint sendMs = 0)
        {
            _packet.Clear();
            _packet.SealedThroughTick = sealedThrough;
            _packet.AckConfirmedTick = ack;
            _packet.ClientSendTimeMs = sendMs;
            _packet.EchoServerTimeMs = echoMs;
            _packet.EchoHoldMs = holdMs;
            foreach (InputEvent e in events)
            {
                Assert.True(_packet.TryAddEvent(e));
            }
            _packet.BaseSeq = baseSeq ?? (events.Length > 0 ? events[0].Seq - 1U : lastSeq);
            var w = new ByteWriter();
            Assert.Equal(DuelCodecError.None, _packet.TryWrite(w));
            var header = new DuelPacketHeader(DuelPacketKind.ClientInput, _token, ++_packetNumber);
            var buf = new byte[DuelWire.MaxPacketBytes];
            int n = DuelPacketFrame.Seal(_cipher, _key, DuelDirection.ClientToServer, in header, w.Written, buf, out DuelCodecError e2);
            Assert.Equal(DuelCodecError.None, e2);
            return buf.AsSpan(0, n).ToArray();
        }

        // 부호기를 거치지 않은 본문을 그대로 봉인한다(형식으로 적을 수 없는 열을 시험할 때)
        public byte[] SealRaw(ReadOnlySpan<byte> body, DuelPacketKind kind = DuelPacketKind.ClientInput)
        {
            var header = new DuelPacketHeader(kind, _token, ++_packetNumber);
            var buf = new byte[DuelWire.MaxPacketBytes];
            int n = DuelPacketFrame.Seal(_cipher, _key, DuelDirection.ClientToServer, in header, body, buf, out DuelCodecError e);
            Assert.Equal(DuelCodecError.None, e);
            return buf.AsSpan(0, n).ToArray();
        }

        public byte[] Send(DuelHostCore host, long receiveUs, uint sealedThrough, InputEvent[] events, uint? baseSeq = null, uint lastSeq = 0,
                           uint ack = 0, uint echoMs = 0, ushort holdMs = 0, ulong? fromAddress = null)
        {
            byte[] p = Build(sealedThrough, events, baseSeq, lastSeq, ack, echoMs, holdMs);
            host.OnPacket(p, receiveUs, fromAddress ?? Address);
            return p;
        }
    }

    // 호스트가 보낸 ServerTick 하나를 연 것
    internal sealed class ServerView
    {
        public ulong ToAddress;
        public ulong PacketNumber;
        public uint ServerTimeMs;
        public short MarginMs;
        public uint ConfirmedFrom;
        public readonly List<InputEvent[][]> Ticks = new List<InputEvent[][]>();   // [틱 index][슬롯] 이벤트
        public bool HasHash;
        public uint HashTick;
        public ulong Hash;
        public readonly List<DroppedRun> Dropped = new List<DroppedRun>();

        public bool HasTick(uint tick) => tick >= ConfirmedFrom && tick < ConfirmedFrom + Ticks.Count;

        public InputEvent[] Events(uint tick, int slot) => Ticks[(int)(tick - ConfirmedFrom)][slot];

        // 보낼 것이 없으면 null
        public static ServerView? Poll(DuelHostCore host, int slot)
        {
            var buf = new byte[DuelWire.MaxPacketBytes];
            int n = host.PollOutgoing(new PlayerSlot((byte)slot), buf, out ulong to);
            if (n == 0)
            {
                return null;
            }
            var body = new byte[DuelWire.MaxBodyBytes];
            Assert.True(DuelPacketFrame.TryOpen(new TestCipher(), HostKit.Key(slot), DuelDirection.ServerToClient, buf.AsSpan(0, n), body,
                                                out DuelPacketHeader header, out int len, out DuelCodecError e));
            Assert.Equal(DuelCodecError.None, e);
            Assert.Equal(DuelPacketKind.ServerTick, header.Kind);
            Assert.Equal(slot == 0 ? HostKit.Token0 : HostKit.Token1, header.TokenId);
            var st = new ServerTickPacket();
            Assert.Equal(DuelCodecError.None, st.TryRead(body.AsSpan(0, len)));
            var v = new ServerView
            {
                ToAddress = to,
                PacketNumber = header.PacketNumber,
                ServerTimeMs = st.ServerTimeMs,
                MarginMs = st.MarginMsEstimate,
                ConfirmedFrom = st.ConfirmedFromTick,
                HasHash = st.HasHash,
                HashTick = st.HashTick,
                Hash = st.StateHash,
            };
            for (int i = 0; i < st.ConfirmedCount; i++)
            {
                v.Ticks.Add(new[] { st.SlotEvents(i, 0).ToArray(), st.SlotEvents(i, 1).ToArray() });
            }
            v.Dropped.AddRange(st.Dropped.ToArray());
            return v;
        }

        // 남은 것을 다 꺼낸다(Advance 한 번에 많아야 하나지만 확인용)
        public static List<ServerView> Drain(DuelHostCore host, int slot)
        {
            var list = new List<ServerView>();
            ServerView? v;
            while ((v = Poll(host, slot)) != null)
            {
                list.Add(v);
            }
            return list;
        }
    }
}
