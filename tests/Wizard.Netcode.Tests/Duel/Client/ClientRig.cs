using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Duel;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // DuelClientCore 시험의 틀. DuelHostCore(M2-NET-04)가 아직 없으므로 서버 쪽은 시험이 손으로 만든다:
    // 정본 세계(Server)를 확정 열로 돌리고, 그 열을 CONTRACTS 10.3의 ServerTick 으로 봉인해 클라이언트에 준다.
    // 무엇을 확정할지(받음, 버림)는 각 시험이 정한다. 팩은 저장소 data/ 로 만든다(넷 절의 되감기 한도·따라잡기·공백 한도).
    internal sealed class ClientRig
    {
        public const uint Token = 0x0A0B0C0DU;
        public const uint Epoch = 1U;

        private static readonly Lazy<ContentDb> DbLazy = new Lazy<ContentDb>(BuildDb);

        public readonly DuelClientCore Client;
        public readonly SimWorld Server;
        public readonly PlayerSlot Me;
        public readonly FakeAeadCipher Cipher = new FakeAeadCipher();
        public readonly byte[] Key = new byte[DuelWire.KeyBytes];
        public readonly DuelClientKeys Keys;

        // 시험이 쥔 클라이언트 단조 시계(µs). OnPacket 과 PollOutgoing 에 이 값을 준다(코어는 시계를 모른다)
        public long NowUs;
        public readonly List<PresentationEvent> Events = new List<PresentationEvent>();

        private readonly PlayerInput[] _serverInputs = new PlayerInput[16];
        private ulong _serverPacketNumber = 1UL;
        private DuelReplayWindow _serverReplay;

        // db 가 없으면 저장소 data/ 의 팩. 한도 하나만 바꾼 팩으로 시험할 때 DbWithNet 으로 만든 것을 준다
        public ClientRig(byte me = 0, int inputDelay = 2, DuelResume? resume = null, ulong seed = 0x5EED_0001UL, ContentDb? db = null)
        {
            db ??= Db;
            for (int i = 0; i < Key.Length; i++)
            {
                Key[i] = (byte)(0x40 + i);
            }
            Me = new PlayerSlot(me);
            Keys = new DuelClientKeys(Token, Epoch, Key);
            Client = new DuelClientCore(db, Setup(seed), Me, Keys, Cipher, resume);
            Client.SetInputDelay(inputDelay);
            Server = NewWorld(seed, db);
        }

        public static ContentDb Db => DbLazy.Value;

        public static NetRules Net => new NetRules(Db);

        public static DuelSetup Setup(ulong seed) => new DuelSetup
        {
            Seed = seed,
            Options = new DuelRoomOptions(),
            Players = new[]
            {
                new DuelPlayerSetup { Slot = new PlayerSlot(0) },
                new DuelPlayerSetup { Slot = new PlayerSlot(1) },
            },
        };

        public static SimWorld NewWorld(ulong seed = 0x5EED_0001UL, ContentDb? db = null)
        {
            db ??= Db;
            DuelSetup s = Setup(seed);
            return SimWorld.CreateDuel(db, SimConfig.ForDuel(db, s.Options), s);
        }

        // 저장소 data/ 를 임시 폴더에 복사하고 data/rules/net.json 의 한 줄만 바꿔 만든 팩(손으로 만든 NetRuleSet 이 아니다).
        // from 은 파일에 꼭 한 번 있어야 한다
        public static ContentDb DbWithNet(string from, string to)
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wizard-netcode-client-tests", Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(TestRepo.Path("data"), System.IO.Path.Combine(root, "data"));
                File.WriteAllText(System.IO.Path.Combine(root, "Wizard.sln"), "");
                string netPath = System.IO.Path.Combine(root, "data", "rules", "net.json");
                string net = File.ReadAllText(netPath);
                int at = net.IndexOf(from, StringComparison.Ordinal);
                Assert.True(at >= 0 && net.IndexOf(from, at + 1, StringComparison.Ordinal) < 0, "net.json must contain '" + from + "' exactly once");
                File.WriteAllBytes(netPath, new UTF8Encoding(false).GetBytes(net.Substring(0, at) + to + net.Substring(at + from.Length)));
                var d = new Diagnostics();
                Pipeline.Output o = Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
                Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
                return ContentDb.Build(ContentPack.Load(o.Pack!));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from))
            {
                File.Copy(f, System.IO.Path.Combine(to, System.IO.Path.GetFileName(f)));
            }
            foreach (string sub in Directory.GetDirectories(from))
            {
                CopyDirectory(sub, System.IO.Path.Combine(to, System.IO.Path.GetFileName(sub)));
            }
        }

        public static InputEvent Ev(uint tick, LogicalKey key, bool down, uint seq, byte offset = 0) =>
            new InputEvent(tick, offset, key, down, seq);

        public static QuantizedKey Q(uint tick, LogicalKey key, bool down, byte offset = 0) => new QuantizedKey(tick, offset, key, down);

        // ── 서버 쪽

        // 정본 세계를 tick 하나 진행하고 그 확정 열을 돌려준다(시험이 패킷으로 묶는다)
        public ConfirmedTick StepServer(InputEvent[]? slot0 = null, InputEvent[]? slot1 = null)
        {
            slot0 ??= Array.Empty<InputEvent>();
            slot1 ??= Array.Empty<InputEvent>();
            uint tick = Server.Tick;
            int n = 0;
            foreach (InputEvent e in slot0)
            {
                Assert.Equal(tick, e.Tick);
                _serverInputs[n++] = new PlayerInput(new PlayerSlot(0), e);
            }
            foreach (InputEvent e in slot1)
            {
                Assert.Equal(tick, e.Tick);
                _serverInputs[n++] = new PlayerInput(new PlayerSlot(1), e);
            }
            var span = new Span<PlayerInput>(_serverInputs, 0, n);
            InputOrder.Sort(span);
            Server.Step(span, ReadOnlySpan<RunCommand>.Empty, NullSink.Instance);
            return new ConfirmedTick(tick, slot0, slot1, Server.ComputeStateHash());
        }

        // 확정 틱들을 ServerTick 하나로 봉인한다. hashTick 이 있으면 그 틱의 해시(hashValue 가 있으면 그 값)를 싣는다
        public byte[] ServerTick(IReadOnlyList<ConfirmedTick> ticks, ConfirmedTick? hashOf = null, ulong? hashValue = null,
                                 uint? fromTick = null, uint serverTimeMs = 0U, short marginMs = 0, uint echoClientSendTimeMs = 0U)
        {
            var p = new ServerTickPacket();
            p.ServerTimeMs = serverTimeMs;
            p.EchoClientSendTimeMs = echoClientSendTimeMs;
            p.MarginMsEstimate = marginMs;
            p.ConfirmedFromTick = fromTick ?? (ticks.Count > 0 ? ticks[0].Tick : 0U);
            foreach (ConfirmedTick t in ticks)
            {
                Assert.Equal(DuelCodecError.None, p.TryAddConfirmedTick(t.Slot0, t.Slot1));
            }
            if (hashOf != null)
            {
                p.HasHash = true;
                p.HashTick = hashOf.Tick;
                p.StateHash = hashValue ?? hashOf.Hash;
            }
            return Seal(p, DuelPacketKind.ServerTick);
        }

        public byte[] Seal(ServerTickPacket p, DuelPacketKind kind)
        {
            var w = new ByteWriter();
            Assert.Equal(DuelCodecError.None, p.TryWrite(w));
            var header = new DuelPacketHeader(kind, Token, _serverPacketNumber++);
            byte[] buffer = new byte[DuelWire.MaxPacketBytes];
            int n = DuelPacketFrame.Seal(Cipher, Key, DuelDirection.ServerToClient, header, w.Written, buffer, out DuelCodecError e);
            Assert.Equal(DuelCodecError.None, e);
            return buffer.AsSpan(0, n).ToArray();
        }

        // 확정 틱들을 정본 세계로 돌리고 한 패킷으로 클라이언트에 준다
        public List<ConfirmedTick> ConfirmEmptyThrough(uint lastTick)
        {
            var list = new List<ConfirmedTick>();
            while (Server.Tick <= lastTick)
            {
                list.Add(StepServer());
            }
            if (list.Count > 0)
            {
                DeliverInChunks(list);
            }
            return list;
        }

        public void DeliverInChunks(List<ConfirmedTick> ticks)
        {
            for (int i = 0; i < ticks.Count; i += DuelWire.MaxConfirmedTicks)
            {
                int n = Math.Min(DuelWire.MaxConfirmedTicks, ticks.Count - i);
                Deliver(ServerTick(ticks.GetRange(i, n)));
            }
        }

        public void Deliver(byte[] packet) => Client.OnPacket(packet, NowUs);

        // ── 클라이언트 쪽

        // 클라이언트가 보낼 패킷 하나를 열어 읽는다. 없으면 null
        public ClientInputPacket? Outgoing()
        {
            byte[] buffer = new byte[DuelWire.MaxPacketBytes];
            int n = Client.PollOutgoing(buffer, NowUs);
            if (n == 0)
            {
                return null;
            }
            return Open(buffer.AsSpan(0, n).ToArray(), out _);
        }

        public ClientInputPacket Open(byte[] packet, out DuelPacketHeader header)
        {
            byte[] body = new byte[DuelWire.MaxBodyBytes];
            Assert.True(DuelPacketFrame.TryOpen(Cipher, Key, DuelDirection.ClientToServer, packet, body, out header, out int len, out DuelCodecError e), e.ToString());
            Assert.Equal(DuelPacketKind.ClientInput, header.Kind);
            Assert.Equal(Token, header.TokenId);
            Assert.True(_serverReplay.Mark(header.PacketNumber), "client reused a packet number");
            var p = new ClientInputPacket();
            Assert.Equal(DuelCodecError.None, p.TryRead(body.AsSpan(0, len)));
            return p;
        }

        public void Drain()
        {
            var buffer = new PresentationEvent[64];
            int n;
            while ((n = Client.DrainEvents(buffer)) > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    Events.Add(buffer[i]);
                }
            }
        }

        private static ContentDb BuildDb()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return ContentDb.Build(ContentPack.Load(o.Pack!));
        }
    }

    internal sealed class ConfirmedTick
    {
        public readonly uint Tick;
        public readonly InputEvent[] Slot0;
        public readonly InputEvent[] Slot1;
        public readonly ulong Hash;

        public ConfirmedTick(uint tick, InputEvent[] slot0, InputEvent[] slot1, ulong hash)
        {
            Tick = tick;
            Slot0 = slot0;
            Slot1 = slot1;
            Hash = hash;
        }
    }

    // 데이터의 넷 값(코드에 8, 30 을 박지 않고 시험도 팩에서 읽는다)
    internal readonly struct NetRules
    {
        public readonly int RollbackLimit;
        public readonly int Catchup;
        public readonly int GapLimit;

        public NetRules(ContentDb db)
        {
            RollbackLimit = db.Net.RollbackLimitTicks;
            Catchup = db.Net.CatchupTicksPerAdvance;
            GapLimit = db.Net.FullStateGapTicks;
        }
    }

    internal sealed class NullSink : ISimEventSink
    {
        public static readonly NullSink Instance = new NullSink();

        public void Emit(in SimEvent e)
        {
        }
    }

    internal sealed class ListSink : ISimEventSink
    {
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public void Emit(in SimEvent e) => Events.Add(e);
    }
}
