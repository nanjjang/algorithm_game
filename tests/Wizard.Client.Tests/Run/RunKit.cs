using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.Client.Run;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Sim;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Client.Tests.Run
{
    // 판 세션 시험의 도우미. 시각은 정수 마이크로초로 들고 다니고 넘길 때만 초(double)로 바꾼다.
    // 기대값은 구현과 따로 정수로 셈한다: tickSeconds = 1/60 이면 1/256틱 단위 = floor(µs × 15,360 / 1,000,000).
    internal static class RunKit
    {
        public const long UsPerSecond = 1_000_000;
        public const long TicksPerSecond = 60;
        public const long UnitsPerSecond = TicksPerSecond * 256;      // 15,360
        public const long StartUs = 4_000_000_321;                    // 0이 아닌 시작 시각(기기가 켜진 지 4000초)

        private static readonly Lazy<ContentDb> RepoDbLazy = new Lazy<ContentDb>(BuildRepoDb);

        // 저장소 data/ 로 만든 팩(기준값 캐릭터, 전장). 키가 세계를 움직이게(이동) 하려고 빈 팩이 아닌 것을 쓴다
        public static ContentDb Db => RepoDbLazy.Value;

        public static readonly ReplayLimits Generous = new ReplayLimits(10_000_000U, 10_000_000L);

        public static double Sec(long us) => us / (double)UsPerSecond;

        // 시작에서 us 만큼 지난 시각의 (틱, 오프셋)
        public static (uint Tick, byte Offset) TickOf(long sinceStartUs)
        {
            long units = sinceStartUs * UnitsPerSecond / UsPerSecond;
            return ((uint)(units / 256), (byte)(units % 256));
        }

        // 그 시각까지 다 지난 틱의 수
        public static long TicksAt(long sinceStartUs) => sinceStartUs * TicksPerSecond / UsPerSecond;

        public static EndlessSetup Setup(ulong seed = 0xC11E_0000_0000_0002UL) => new EndlessSetup
        {
            RunId = new Guid("8d0c1f2e-3a4b-4c5d-9e6f-0a1b2c3d4e5f"),
            Seed = seed,
            Kind = RunKind.Normal,
            StartWave = 1,
            PhantomFirst = true,
            SeasonTagOn = false,
            Wand = new WandSnapshot
            {
                Character = CharacterId.Baseline,
                Stage = 0,
                SlotsExtra = 0,
                Mutations = Array.Empty<MutationId>(),
                Core = (BlockId)1,
                Links = new[] { (BlockId)2 },
            },
            UnlockedArts = (ArtMask)0,
            CoreVersion = SimVersion.CoreVersion,
            DataVersion = Db.DataVersion,
        };

        public static InputRecordHeader Header(ulong seed = 0xC11E_0000_0000_0002UL) =>
            new InputRecordHeader { FormatVersion = EndlessSetup.CanonicalFormatVersion, Setup = Setup(seed) };

        public static RunSession NewSession(ReplayLimits? limits = null, ulong seed = 0xC11E_0000_0000_0002UL) =>
            new RunSession(Db, Header(seed), limits ?? Generous, Sec(StartUs));

        // 파일에서 머리말을 뗀 본문 ‖ 꼬리말(ReplayRunner.Run 의 recordBody)
        public static byte[] BodyOf(ReadOnlySpan<byte> file)
        {
            Assert.True(EndlessSetup.TryReadCanonicalPrefix(file, out _, out int length));
            return file.Slice(length).ToArray();
        }

        // 일정한 간격의 프레임 시각(시작 기준 µs). phaseUs 만큼 밀린다
        public static long[] Regular(double intervalUs, long phaseUs, long endUs)
        {
            var list = new List<long>();
            for (int i = 0; ; i++)
            {
                long t = phaseUs + (long)(i * intervalUs);
                if (t > endUs)
                {
                    break;
                }
                list.Add(t);
            }
            list.Add(endUs);
            return list.ToArray();
        }

        // 흔들리는 프레임: 1~40ms, 가끔 250ms 걸림
        public static long[] Irregular(ulong seed, long endUs)
        {
            var rng = new Pcg32(seed, 77UL);
            var list = new List<long>();
            long t = 0;
            while (t < endUs)
            {
                t += rng.NextInt(100) < 3 ? 250_000 : rng.NextRange(1_000, 40_000);
                list.Add(t < endUs ? t : endUs);
            }
            return list.ToArray();
        }

        private static ContentDb BuildRepoDb()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(),
                                             PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return ContentDb.Build(ContentPack.Load(o.Pack!));
        }
    }

    // 정해 둔 논리 키 이벤트를 하드웨어 시각과 넘겨줄 시각에 맞춰 내주는 입력원(유니티 쪽 IInputSource 의 흉내).
    // CurrentKeys 는 넘겨줄 수 있게 된 이벤트(꺼냈든 아니든)까지의 논리 키 상태다.
    internal sealed class ScriptedSource : IInputSource
    {
        private readonly List<(long TimeUs, long DeliverUs, LogicalKey Key, bool Down)> _events = new List<(long, long, LogicalKey, bool)>();
        private int _available;
        private int _drained;
        private int _mask;

        public int Count => _events.Count;

        public KeyMask CurrentKeys => (KeyMask)_mask;

        // 시작 기준 µs. 시각이 줄지 않게 넣는다. deliverUs 를 주면 그때까지 넘겨주지 않는다(늦게 오는 이벤트)
        public ScriptedSource Add(long timeUs, LogicalKey key, bool down, long deliverUs = -1)
        {
            if (_events.Count > 0)
            {
                Assert.True(_events[_events.Count - 1].TimeUs <= timeUs);
            }
            _events.Add((timeUs, deliverUs < 0 ? timeUs : deliverUs, key, down));
            return this;
        }

        public void DeliverUntil(long nowUs)
        {
            while (_available < _events.Count && _events[_available].DeliverUs <= nowUs)
            {
                var e = _events[_available];
                int bit = 1 << (int)e.Key;
                _mask = e.Down ? _mask | bit : _mask & ~bit;
                _available++;
            }
        }

        public int Drain(Span<RawKeyEvent> buffer)
        {
            int n = 0;
            while (n < buffer.Length && _drained < _available)
            {
                var e = _events[_drained++];
                buffer[n++] = new RawKeyEvent(e.Key, e.Down, RunKit.Sec(RunKit.StartUs + e.TimeUs));
            }
            return n;
        }

        public void SetBindings(KeyBindings bindings)
        {
        }

        public void SetDuelPolling(bool on)
        {
        }

        // 판 내내 고르게 섞인 이동·발사·자세 키(일시정지 키는 뺀다). 논리 키 상태가 바뀔 때만 이벤트가 생긴다
        public static ScriptedSource Random(ulong seed, long endUs, int count)
        {
            var rng = new Pcg32(seed, 91UL);
            var keys = new[] { LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right, LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Stance, LogicalKey.Reverse };
            var s = new ScriptedSource();
            int state = 0;
            long t = 5_000;
            for (int i = 0; i < count && t < endUs - 100_000; i++)   // 끝 틱 뒤의 키가 남지 않게 끝 0.1초 전까지
            {
                int r = rng.NextInt(100);
                if (r >= 10)
                {
                    t += r < 40 ? rng.NextRange(1, 3_000) : rng.NextRange(10_000, 200_000);
                }
                if ((t * RunKit.UnitsPerSecond) % RunKit.UsPerSecond == 0)
                {
                    t++;     // 1/256틱 경계는 피한다(정수 기대값과 실수 셈이 다툴 수 있다)
                }
                LogicalKey key = keys[rng.NextInt(keys.Length)];
                bool down = (state & (1 << (int)key)) == 0;
                state ^= 1 << (int)key;
                s.Add(t, key, down);
            }
            return s;
        }
    }

    // ARCHITECTURE 6.1 의 프레임을 시험이 돌린다: 입력원이 그 시각까지의 이벤트를 넘기고 → 세션의 프레임
    internal static class Frames
    {
        public static void Drive(RunSession session, ScriptedSource source, long[] framesUs, Action<long>? beforeFrame = null)
        {
            foreach (long f in framesUs)
            {
                beforeFrame?.Invoke(f);
                source.DeliverUntil(f);
                session.RunFrame(source, RunKit.Sec(RunKit.StartUs + f));
            }
        }

        public static void Drive(PracticeSession session, ScriptedSource source, long[] framesUs)
        {
            foreach (long f in framesUs)
            {
                source.DeliverUntil(f);
                session.RunFrame(source, RunKit.Sec(RunKit.StartUs + f));
            }
        }
    }

    // .wzrc 를 CONTRACTS 11.1 대로 읽는 시험 쪽 판독기(구현의 판독기를 쓰지 않는다). 순번은 레코드 순서대로 1부터 매긴다
    internal sealed class WzrcRecords
    {
        public enum Kind { Input, Command, Pause, Checkpoint }

        public readonly struct Rec
        {
            public readonly Kind K;
            public readonly uint Tick;
            public readonly uint Seq;
            public readonly byte Offset;
            public readonly LogicalKey Key;
            public readonly bool Down;
            public readonly byte Op;
            public readonly uint WallMs;
            public readonly ulong Hash;

            public Rec(Kind k, uint tick, uint seq, byte offset, LogicalKey key, bool down, byte op, uint wallMs, ulong hash)
            {
                K = k;
                Tick = tick;
                Seq = seq;
                Offset = offset;
                Key = key;
                Down = down;
                Op = op;
                WallMs = wallMs;
                Hash = hash;
            }

            public override string ToString() => K switch
            {
                Kind.Input => $"in {Tick}:{Offset} {Key} {(Down ? "down" : "up")} #{Seq}",
                Kind.Command => $"cmd {Tick} op{Op} #{Seq}",
                Kind.Pause => $"pause {Tick} {WallMs}ms",
                _ => $"cp {Tick} {Hash:x16}",
            };
        }

        public List<Rec> Records { get; } = new List<Rec>();

        public uint EndTick { get; private set; }

        public ulong FinalHash { get; private set; }

        public List<Rec> Inputs => Records.FindAll(r => r.K == Kind.Input);

        // 기록된 키를 코어의 KeyState 규칙(눌린 키의 누름, 안 눌린 키의 뗌은 무시)으로 차례로 넣은 뒤의 논리 키 상태 = 세계의 키 상태
        public KeyMask KeysAfterInputs()
        {
            var state = new KeyState();
            foreach (Rec r in Inputs)
            {
                state.Apply(new InputEvent(r.Tick, r.Offset, r.Key, r.Down, r.Seq));
            }
            return state.Mask;
        }

        public static WzrcRecords Read(ReadOnlySpan<byte> file)
        {
            Assert.True(EndlessSetup.TryReadCanonicalPrefix(file, out _, out int pos));
            var result = new WzrcRecords();
            uint tick = 0, seq = 0;
            while (true)
            {
                byte tag = file[pos++];
                switch (tag)
                {
                    case 0x01:
                        tick += (uint)VarUInt(file, ref pos);
                        byte offset = file[pos++];
                        byte kd = file[pos++];
                        result.Records.Add(new Rec(Kind.Input, tick, ++seq, offset, (LogicalKey)(kd & 0x0F), (kd & 0x10) != 0, 0, 0, 0));
                        break;
                    case 0x02:
                        tick += (uint)VarUInt(file, ref pos);
                        byte op = file[pos];
                        pos += 1 + 4 + 4;
                        result.Records.Add(new Rec(Kind.Command, tick, ++seq, 0, default, false, op, 0, 0));
                        break;
                    case 0x03:
                        tick += (uint)VarUInt(file, ref pos);
                        uint ms = U32(file, pos);
                        pos += 4;
                        result.Records.Add(new Rec(Kind.Pause, tick, 0, 0, default, false, 0, ms, 0));
                        break;
                    case 0x04:
                        pos += 2;
                        tick = U32(file, pos);
                        pos += 4;
                        ulong h = U32(file, pos) | ((ulong)U32(file, pos + 4) << 32);
                        pos += 8;
                        result.Records.Add(new Rec(Kind.Checkpoint, tick, 0, 0, default, false, 0, 0, h));
                        break;
                    case 0xFF:
                        result.EndTick = U32(file, pos);
                        result.FinalHash = U32(file, pos + 4) | ((ulong)U32(file, pos + 8) << 32);
                        Assert.Equal(file.Length, pos + 4 + 8 + 1 + 32);
                        return result;
                    default:
                        throw new InvalidOperationException("unknown tag " + tag);
                }
            }
        }

        private static ulong VarUInt(ReadOnlySpan<byte> b, ref int pos)
        {
            ulong v = 0;
            int shift = 0;
            while (true)
            {
                byte x = b[pos++];
                v |= (ulong)(x & 0x7F) << shift;
                if ((x & 0x80) == 0)
                {
                    return v;
                }
                shift += 7;
            }
        }

        private static uint U32(ReadOnlySpan<byte> b, int pos) =>
            b[pos] | ((uint)b[pos + 1] << 8) | ((uint)b[pos + 2] << 16) | ((uint)b[pos + 3] << 24);
    }
}
