using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;

namespace Wizard.ReplayCli
{
    // 기록 하나를 틱마다 세계에 넣을 열로 펼친 것(.wzrc 또는 .wzdr). 틱을 마음대로 골라 꺼낼 수 있다(되감기 퍼저가 같은 틱을 다시 돌린다).
    //
    // 읽기: 바이트는 먼저 코어의 유일한 문(InputRecordCodec.TryRead, DuelRecordCodec.TryRead)을 통과해야 한다(CONTRACTS 3.8).
    // 통과한 본문만 여기서 다시 펼친다. 코어의 본문 판독기는 내부라서 이 도구가 CONTRACTS 11.1 의 배치대로 한 번 더 읽는다:
    //   .wzrc  0x01·0x02·0x03 은 앞 레코드의 틱 + Δtick, 0x04 는 절대 틱. 명령·키의 순번은 레코드 순서대로 1부터(같은 공간)
    //   .wzdr  0x05 는 앞 레코드의 틱 + Δtick, 슬롯, 오프셋, 키, 실린 순번
    // 펼친 레코드 수가 코덱이 센 수(InputCount, CommandCount, PauseCount, 중간 지점)와 다르면 이 판독기의 결함이라 거절한다.
    // 재실행 쪽 대조(같은 열이 세계에 들어가는가)는 Wizard.Sim.Tests 의 Determinism/ReplayCliFeedTests 가 ReplayRunner 의 관찰 자리로 한다.
    public sealed class RecordFeed
    {
        private const byte TagInput = 0x01;
        private const byte TagCommand = 0x02;
        private const byte TagPause = 0x03;
        private const byte TagCheckpoint = 0x04;
        private const byte TagDuelInput = 0x05;
        private const byte TagFooter = 0xFF;
        private const int DuelInputDelayBytes = 2;

        private readonly PlayerInput[] _inputs;
        private readonly RunCommand[] _commands;
        private readonly int[] _inputStart;      // 길이 EndTick + 1. 틱 t 의 키 = _inputs[_inputStart[t] .. _inputStart[t + 1])
        private readonly int[] _commandStart;
        private readonly RecordMark[] _checkpoints;

        private RecordFeed(RecordFormat format, EndlessSetup? endless, DuelSetup? duel, uint endTick, ulong finalStateHash,
                           PlayerInput[] inputs, RunCommand[] commands, RecordMark[] checkpoints, int pauseCount)
        {
            Format = format;
            Endless = endless;
            Duel = duel;
            EndTick = endTick;
            FinalStateHash = finalStateHash;
            _inputs = inputs;
            _commands = commands;
            _checkpoints = checkpoints;
            PauseCount = pauseCount;
            _inputStart = Index(endTick, inputs, i => inputs[i].Event.Tick);
            _commandStart = Index(endTick, commands, i => commands[i].Tick);
        }

        public RecordFormat Format { get; }

        // .wzrc 의 머리말(무한 웨이브·연습). 연습 세계는 머리말을 읽지 않는다
        public EndlessSetup? Endless { get; }

        // .wzdr 의 머리말
        public DuelSetup? Duel { get; }

        // 꼬리말: 처리한 틱 수(마지막 Step 뒤의 world.Tick)와 그때의 상태 해시
        public uint EndTick { get; }

        public ulong FinalStateHash { get; }

        // 0x04 중간 지점(파일 순서). 해시는 world.Tick == Tick 일 때(그 틱의 Step 전)의 것이다
        public IReadOnlyList<RecordMark> Checkpoints => _checkpoints;

        public int PauseCount { get; }

        public int InputCount => _inputs.Length;

        public int CommandCount => _commands.Length;

        // 기록의 머리말이 정한 세계의 씨앗(연습은 0)
        public ulong SetupSeed => Duel != null ? Duel.Seed : Endless!.Seed;

        // 틱 t 에 세계에 넣을 키(InputOrder 순)와 명령(순번 순). t >= EndTick 이면 비어 있다
        public ReadOnlySpan<PlayerInput> InputsAt(uint tick) =>
            tick >= EndTick ? ReadOnlySpan<PlayerInput>.Empty
                            : new ReadOnlySpan<PlayerInput>(_inputs, _inputStart[tick], _inputStart[tick + 1] - _inputStart[tick]);

        public ReadOnlySpan<RunCommand> CommandsAt(uint tick) =>
            tick >= EndTick ? ReadOnlySpan<RunCommand>.Empty
                            : new ReadOnlySpan<RunCommand>(_commands, _commandStart[tick], _commandStart[tick + 1] - _commandStart[tick]);

        // 순번만 바꾼 사본(순서는 그대로여야 한다). 순번의 독립 시험(CONTRACTS 3.1: 순번은 상태와 해시에 들어가지 않는다)
        public RecordFeed WithSequences(Func<PlayerInput, uint> inputSeq, Func<RunCommand, uint> commandSeq)
        {
            var inputs = new PlayerInput[_inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                InputEvent e = _inputs[i].Event;
                inputs[i] = new PlayerInput(_inputs[i].Slot, new InputEvent(e.Tick, e.Offset, e.Key, e.Down, inputSeq(_inputs[i])));
            }
            var commands = new RunCommand[_commands.Length];
            for (int i = 0; i < commands.Length; i++)
            {
                RunCommand c = _commands[i];
                commands[i] = new RunCommand(c.Tick, commandSeq(c), c.Slot, c.Op, c.A, c.B);
            }
            return new RecordFeed(Format, Endless, Duel, EndTick, FinalStateHash, inputs, commands, _checkpoints, PauseCount);
        }

        public static RecordFeed Load(ReadOnlySpan<byte> file, ReplayLimits limits)
        {
            if (TryLoad(file, limits, out RecordFeed? feed, out string? error))
            {
                return feed;
            }
            throw new FormatException(error);
        }

        // 머리말의 magic 으로 형식을 가른다
        public static bool TryLoad(ReadOnlySpan<byte> file, ReplayLimits limits, [NotNullWhen(true)] out RecordFeed? feed,
                                   [NotNullWhen(false)] out string? error)
        {
            if (file.Length >= 4 && file[0] == (byte)'W' && file[1] == (byte)'Z' && file[2] == (byte)'D' && file[3] == (byte)'R')
            {
                return TryLoadDuel(file, limits, out feed, out error);
            }
            return TryLoadRun(file, limits, out feed, out error);
        }

        private static bool TryLoadRun(ReadOnlySpan<byte> file, ReplayLimits limits, [NotNullWhen(true)] out RecordFeed? feed,
                                       [NotNullWhen(false)] out string? error)
        {
            feed = null;
            if (!InputRecordCodec.TryRead(file, limits, out InputRecordHeader? header, out InputRecordBody? body, out RecordError codecError))
            {
                error = "not a valid .wzrc (" + codecError + ")";
                return false;
            }
            var inputs = new List<PlayerInput>(body.InputCount);
            var commands = new List<RunCommand>(body.CommandCount);
            var marks = new List<RecordMark>();
            int pauses = 0;
            var slot = new PlayerSlot(0);
            var r = new ByteReader(file.Slice(body.HeaderLength));
            uint tick = 0;
            uint seq = 0;
            while (true)
            {
                if (!r.TryU8(out byte tag))
                {
                    error = "body ended before the footer";
                    return false;
                }
                if (tag == TagFooter)
                {
                    break;
                }
                switch (tag)
                {
                    case TagInput:
                        if (!ReadDelta(ref r, ref tick) || !r.TryU8(out byte offset) || !r.TryU8(out byte packed)
                            || !InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down))
                        {
                            error = "bad 0x01 record";
                            return false;
                        }
                        inputs.Add(new PlayerInput(slot, new InputEvent(tick, offset, key, down, ++seq)));
                        break;
                    case TagCommand:
                        if (!ReadDelta(ref r, ref tick) || !r.TryU8(out byte op) || !r.TryI32(out int a) || !r.TryI32(out int b))
                        {
                            error = "bad 0x02 record";
                            return false;
                        }
                        commands.Add(new RunCommand(tick, ++seq, slot, (RunCommandOp)op, a, b));
                        break;
                    case TagPause:
                        if (!ReadDelta(ref r, ref tick) || !r.TryU32(out _))
                        {
                            error = "bad 0x03 record";
                            return false;
                        }
                        pauses++;
                        break;
                    case TagCheckpoint:
                        if (!r.TryU16(out ushort wave) || !r.TryU32(out uint at) || !r.TryU64(out ulong hash))
                        {
                            error = "bad 0x04 record";
                            return false;
                        }
                        tick = at;
                        marks.Add(new RecordMark(wave, at, hash));
                        break;
                    default:
                        error = "unknown tag 0x" + tag.ToString("x2", System.Globalization.CultureInfo.InvariantCulture);
                        return false;
                }
            }
            if (inputs.Count != body.InputCount || commands.Count != body.CommandCount || pauses != body.PauseCount
                || marks.Count != body.Checkpoints.Length)
            {
                error = "record decoder disagrees with InputRecordCodec (counts differ)";
                return false;
            }
            feed = new RecordFeed(RecordFormat.Wzrc, header.Setup, null, body.EndTick, body.FinalStateHash, inputs.ToArray(), commands.ToArray(),
                                  marks.ToArray(), pauses);
            error = null;
            return true;
        }

        private static bool TryLoadDuel(ReadOnlySpan<byte> file, ReplayLimits limits, [NotNullWhen(true)] out RecordFeed? feed,
                                        [NotNullWhen(false)] out string? error)
        {
            feed = null;
            if (!DuelRecordCodec.TryRead(file, limits, out DuelSetup? setup, out DuelRecordBody? body, out RecordError codecError))
            {
                error = "not a valid .wzdr (" + codecError + ")";
                return false;
            }
            var inputs = new List<PlayerInput>(body.InputCount);
            var r = new ByteReader(file.Slice(body.HeaderLength + DuelInputDelayBytes));
            uint tick = 0;
            while (true)
            {
                if (!r.TryU8(out byte tag))
                {
                    error = "body ended before the footer";
                    return false;
                }
                if (tag == TagFooter)
                {
                    break;
                }
                if (tag != TagDuelInput || !ReadDelta(ref r, ref tick) || !r.TryU8(out byte slot) || !r.TryU8(out byte offset)
                    || !r.TryU8(out byte packed) || !InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down)
                    || !r.TryVarUInt(out ulong seq) || seq > uint.MaxValue)
                {
                    error = "bad 0x05 record";
                    return false;
                }
                inputs.Add(new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, (uint)seq)));
            }
            if (inputs.Count != body.InputCount)
            {
                error = "record decoder disagrees with DuelRecordCodec (counts differ)";
                return false;
            }
            feed = new RecordFeed(RecordFormat.Wzdr, null, setup, body.EndTick, body.FinalStateHash, inputs.ToArray(), Array.Empty<RunCommand>(),
                                  Array.Empty<RecordMark>(), 0);
            error = null;
            return true;
        }

        private static bool ReadDelta(ref ByteReader r, ref uint tick)
        {
            if (!r.TryVarUInt(out ulong delta) || delta > uint.MaxValue || tick + delta > uint.MaxValue)
            {
                return false;
            }
            tick = (uint)(tick + delta);
            return true;
        }

        // 틱마다 시작 색인(코덱이 확인했으므로 틱은 줄지 않고 모두 EndTick 보다 앞이다)
        private static int[] Index<T>(uint endTick, T[] items, Func<int, uint> tickOf)
        {
            var start = new int[checked((int)endTick + 1)];
            int i = 0;
            for (uint t = 0; t <= endTick; t++)
            {
                start[t] = i;
                while (i < items.Length && tickOf(i) == t)
                {
                    i++;
                }
            }
            if (i != items.Length)
            {
                throw new FormatException("record has entries at or after end_tick");
            }
            return start;
        }
    }
}
