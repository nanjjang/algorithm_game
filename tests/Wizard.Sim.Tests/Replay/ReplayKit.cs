using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Tests.Runtime;

namespace Wizard.Sim.Tests.Replay
{
    // 기록·재실행 시험의 도우미
    internal static class ReplayKit
    {
        public static readonly ContentDb Db = WorldKit.EmptyDb();

        public const string CoreVersionHex = "0123456789abcdeffedcba9876543210";

        public static readonly ReplayLimits Generous = new ReplayLimits(1_000_000U, 1_000_000L);

        // end_tick 의 상한만 정한 한도(TryRead 는 MaxTicks 만 쓴다)
        public static ReplayLimits MaxTicks(uint maxTicks) => new ReplayLimits(maxTicks, long.MaxValue);

        public static EndlessSetup Setup(ulong seed = 0x5EED_0000_0000_0001UL) => new EndlessSetup
        {
            RunId = new Guid("00112233-4455-6677-8899-aabbccddeeff"),
            Seed = seed,
            Kind = RunKind.Normal,
            StartWave = 1,
            PhantomFirst = true,
            SeasonTagOn = false,
            Wand = new WandSnapshot
            {
                Character = CharacterId.Char2,
                Stage = 3,
                SlotsExtra = 1,
                Mutations = new[] { (MutationId)1, (MutationId)4 },
                Core = (BlockId)2,
                Links = new[] { (BlockId)1, (BlockId)3, (BlockId)5 },
            },
            UnlockedArts = (ArtMask)0x0005,
            CoreVersion = CoreVersionHex,
            DataVersion = Db.DataVersion,
        };

        public static InputRecordHeader Header(ulong seed = 0x5EED_0000_0000_0001UL) =>
            new InputRecordHeader { FormatVersion = EndlessSetup.CanonicalFormatVersion, Setup = Setup(seed) };

        public static byte[] HeaderBytes(InputRecordHeader header)
        {
            var w = new ByteWriter();
            InputRecordCodec.WriteHeader(w, header);
            return w.ToArray();
        }

        // 파일의 머리말을 떼어 낸 본문 ‖ 꼬리말(ReplayRunner.Run 의 recordBody)
        public static byte[] BodyOf(byte[] file)
        {
            Assert(EndlessSetup.TryReadCanonicalPrefix(file, out _, out int length));
            return file.AsSpan(length).ToArray();
        }

        public static QuantizedKey Key(uint tick, byte offset, LogicalKey key, bool down) => new QuantizedKey(tick, offset, key, down);

        // 대조용 글자: 넣은 Step 의 틱과 칸 전부(슬롯, 틱, 오프셋, 키, 눌림, 순번)
        public static string Describe(uint stepTick, PlayerInput p) =>
            stepTick + "|" + p.Slot.Value + ":" + p.Event.Tick + ":" + p.Event.Offset + ":" + (int)p.Event.Key + ":" + p.Event.Down + ":" + p.Event.Seq;

        public static string Describe(uint stepTick, RunCommand c) =>
            stepTick + "|" + c.Slot.Value + ":" + c.Tick + ":" + c.Seq + ":" + (int)c.Op + ":" + c.A + ":" + c.B;

        public static List<string> DescribeInputs(List<uint> ticks, List<PlayerInput> fed)
        {
            var list = new List<string>(fed.Count);
            for (int i = 0; i < fed.Count; i++)
            {
                list.Add(Describe(ticks[i], fed[i]));
            }
            return list;
        }

        public static List<string> DescribeCommands(List<uint> ticks, List<RunCommand> fed)
        {
            var list = new List<string>(fed.Count);
            for (int i = 0; i < fed.Count; i++)
            {
                list.Add(Describe(ticks[i], fed[i]));
            }
            return list;
        }

        private static void Assert(bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException("ReplayKit precondition failed");
            }
        }
    }

    // 판을 하는 쪽의 흐름: 틱마다 Append → Step. 틱마다의 해시를 남긴다(원래 실행)
    internal sealed class EndlessSession
    {
        private readonly RunCommand[] _commands = new RunCommand[16];
        private readonly PlayerInput[] _inputs = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
        private readonly RecordingSink _sink = new RecordingSink();

        public EndlessSession(InputRecordHeader header)
        {
            Header = header;
            World = SimWorld.CreateEndless(ReplayKit.Db, SimConfig.ForEndless(ReplayKit.Db), header.Setup);
            Recorder = new InputRecorder(header);
        }

        public InputRecordHeader Header { get; }

        public SimWorld World { get; }

        public InputRecorder Recorder { get; }

        public List<ulong> TickHashes { get; } = new List<ulong>();      // [i] = i 번째 Step 뒤의 해시

        public List<PlayerInput> Fed { get; } = new List<PlayerInput>();  // 세계에 넣은 키(순번 포함)

        public List<RunCommand> FedCommands { get; } = new List<RunCommand>();

        public List<uint> FedAtTick { get; } = new List<uint>();          // [i] = Fed[i] 를 넘긴 Step 의 world.Tick

        public List<uint> FedCommandsAtTick { get; } = new List<uint>();

        public int Rejected { get; private set; }

        public void Tick(QuantizedKey[]? keys = null, RunCommandDraft[]? commands = null)
        {
            keys ??= Array.Empty<QuantizedKey>();
            commands ??= Array.Empty<RunCommandDraft>();
            Recorder.Append(World.Tick, new PlayerSlot(0), commands, keys, _commands, out int nc, _inputs, out int ni);
            for (int i = 0; i < ni; i++)
            {
                Fed.Add(_inputs[i]);
                FedAtTick.Add(World.Tick);
            }
            for (int i = 0; i < nc; i++)
            {
                FedCommands.Add(_commands[i]);
                FedCommandsAtTick.Add(World.Tick);
            }
            _sink.Clear();
            World.Step(_inputs.AsSpan(0, ni), _commands.AsSpan(0, nc), _sink);
            for (int i = 0; i < _sink.Count && i < 1024; i++)
            {
                if (_sink[i].Type == SimEventType.DebugCommandRejected)
                {
                    Rejected++;
                }
            }
            TickHashes.Add(World.ComputeStateHash());
        }

        public void Checkpoint(int wave) => Recorder.MarkCheckpoint(wave, World.Tick, World.ComputeStateHash());

        // 판 끝(세계의 판 끝 규칙은 아직 모듈이 없으므로 시험이 MarkOver 로 끝낸다)
        public byte[] EndAndFinish()
        {
            World.MarkOver();
            return Recorder.Finish(World.Tick, World.GetOutcome().FinalStateHash).ToArray();
        }

        public byte[] FinishWithoutEnding() => Recorder.Finish(World.Tick, World.ComputeStateHash()).ToArray();
    }

    // 재실행의 틱마다 해시를 남기고, 원래 실행과 같은 틱에 판을 끝낸다.
    // 재실행이 Step 에 넘긴 키·명령 열과 그때의 world.Tick 도 남긴다(원래 실행의 Fed·FedCommands 와 대조한다)
    internal sealed class HashObserver : IReplayObserver
    {
        private readonly uint _endAt;

        public HashObserver(uint endAt = uint.MaxValue)
        {
            _endAt = endAt;
        }

        public List<ulong> TickHashes { get; } = new List<ulong>();

        public List<PlayerInput> Fed { get; } = new List<PlayerInput>();

        public List<RunCommand> FedCommands { get; } = new List<RunCommand>();

        public List<uint> FedAtTick { get; } = new List<uint>();          // [i] = Fed[i] 를 넘긴 Step 의 world.Tick

        public List<uint> FedCommandsAtTick { get; } = new List<uint>();

        public void BeforeStep(SimWorld world, ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands)
        {
            foreach (PlayerInput p in inputs)
            {
                Fed.Add(p);
                FedAtTick.Add(world.Tick);
            }
            foreach (RunCommand c in commands)
            {
                FedCommands.Add(c);
                FedCommandsAtTick.Add(world.Tick);
            }
        }

        public void AfterStep(SimWorld world)
        {
            TickHashes.Add(world.ComputeStateHash());
            if (world.Tick == _endAt)
            {
                world.MarkOver();
            }
        }
    }

    // 손으로 짠 본문(잘못된 기록을 만드는 데 쓴다). Seal 은 계약 11.1 대로 그 앞의 모든 바이트의 SHA-256 을 붙인다(구현의 봉인 함수를 쓰지 않는다)
    internal sealed class RawBody
    {
        private readonly ByteWriter _w = new ByteWriter();

        public RawBody Input(ulong delta, byte offset, LogicalKey key, bool down)
        {
            _w.U8(0x01);
            _w.VarUInt(delta);
            _w.U8(offset);
            _w.U8((byte)((int)key | (down ? 0x10 : 0)));
            return this;
        }

        public RawBody InputPacked(ulong delta, byte offset, byte packed)
        {
            _w.U8(0x01);
            _w.VarUInt(delta);
            _w.U8(offset);
            _w.U8(packed);
            return this;
        }

        public RawBody Command(ulong delta, byte op, int a = 0, int b = 0)
        {
            _w.U8(0x02);
            _w.VarUInt(delta);
            _w.U8(op);
            _w.I32(a);
            _w.I32(b);
            return this;
        }

        public RawBody Pause(ulong delta, uint wallMs)
        {
            _w.U8(0x03);
            _w.VarUInt(delta);
            _w.U32(wallMs);
            return this;
        }

        public RawBody Checkpoint(ushort wave, uint tick, ulong hash)
        {
            _w.U8(0x04);
            _w.U16(wave);
            _w.U32(tick);
            _w.U64(hash);
            return this;
        }

        public RawBody Bytes(params byte[] bytes)
        {
            _w.Bytes(bytes);
            return this;
        }

        public byte[] Records => _w.ToArray();

        public static byte[] Seal(byte[] header, byte[] records, uint endTick, ulong finalHash = 0, byte flags = 0)
        {
            var all = new ByteWriter();
            all.Bytes(header);
            all.Bytes(records);
            all.U8(0xFF);
            all.U32(endTick);
            all.U64(finalHash);
            all.U8(flags);
            byte[] sha = SHA256.HashData(all.Written.ToArray());
            all.Bytes(sha);
            return all.ToArray();
        }

        public byte[] File(byte[] header, uint endTick, ulong finalHash = 0, byte flags = 0) => Seal(header, Records, endTick, finalHash, flags);
    }
}
