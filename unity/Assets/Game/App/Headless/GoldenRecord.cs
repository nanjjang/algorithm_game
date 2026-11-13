#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;

namespace Wizard.Unity.App.Headless
{
    // 골든의 입력 기록 하나를 틱마다 세계에 넣을 열로 펼친 것(.wzrc 또는 .wzdr. CONTRACTS 11.1).
    // 바이트는 먼저 코어의 유일한 문(InputRecordCodec.TryRead, DuelRecordCodec.TryRead. CONTRACTS 3.8)을 통과해야 한다.
    // 코어의 본문 판독기는 내부라서 통과한 본문만 여기서 11.1절의 배치대로 한 번 더 읽는다:
    //   .wzrc  0x01·0x02·0x03 은 앞 레코드의 틱 + Δtick, 0x04 는 절대 틱. 명령·키의 순번은 레코드 순서대로 1부터(같은 공간)
    //   .wzdr  머리말 뒤 input_delay 두 바이트, 그다음 0x05(Δtick, 슬롯, 오프셋, 키, 실린 순번)
    // 펼친 레코드 수가 코덱이 센 수와 다르면 이 판독기의 결함이라 거절한다.
    // dotnet 쪽의 같은 일은 tools/Wizard.ReplayCli/RecordFeed.cs 가 한다. 두 판독기는 따로 쓴 것이고, 같은 기대 해시(expected.json)로 서로를 확인한다.
    public sealed class GoldenRecord
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

        private GoldenRecord(EndlessSetup? endless, DuelSetup? duel, uint endTick, PlayerInput[] inputs, RunCommand[] commands)
        {
            Endless = endless;
            Duel = duel;
            EndTick = endTick;
            _inputs = inputs;
            _commands = commands;
            _inputStart = new int[(int)endTick + 1];
            _commandStart = new int[(int)endTick + 1];
            IndexInputs();
            IndexCommands();
        }

        // .wzrc 의 머리말(무한 웨이브·연습). 결투면 null
        public EndlessSetup? Endless { get; }

        // .wzdr 의 머리말. 무한 웨이브·연습이면 null
        public DuelSetup? Duel { get; }

        public bool IsDuel => Duel != null;

        // 꼬리말의 end_tick: 처리한 틱 수(마지막 Step 뒤의 world.Tick)
        public uint EndTick { get; }

        public int InputCount => _inputs.Length;

        public int CommandCount => _commands.Length;

        // 기록의 머리말이 정한 세계의 씨앗
        public ulong SetupSeed => Duel != null ? Duel.Seed : Endless!.Seed;

        // 틱 t 에 세계에 넣을 키(기록 순서 = InputOrder 순)와 명령(순번 순). t >= EndTick 이면 비어 있다
        public ReadOnlySpan<PlayerInput> InputsAt(uint tick) =>
            tick >= EndTick ? ReadOnlySpan<PlayerInput>.Empty
                            : new ReadOnlySpan<PlayerInput>(_inputs, _inputStart[tick], _inputStart[tick + 1] - _inputStart[tick]);

        public ReadOnlySpan<RunCommand> CommandsAt(uint tick) =>
            tick >= EndTick ? ReadOnlySpan<RunCommand>.Empty
                            : new ReadOnlySpan<RunCommand>(_commands, _commandStart[tick], _commandStart[tick + 1] - _commandStart[tick]);

        // 머리말의 magic 으로 형식을 가른다. 실패면 FormatException(까닭을 담는다)
        public static GoldenRecord Load(byte[] file, ReplayLimits limits)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }
            bool duel = file.Length >= 4 && file[0] == (byte)'W' && file[1] == (byte)'Z' && file[2] == (byte)'D' && file[3] == (byte)'R';
            return duel ? LoadDuel(file, limits) : LoadRun(file, limits);
        }

        private static GoldenRecord LoadRun(byte[] file, ReplayLimits limits)
        {
            if (!InputRecordCodec.TryRead(file, limits, out InputRecordHeader? header, out InputRecordBody? body, out RecordError codecError)
                || header == null || body == null)
            {
                throw new FormatException("not a valid .wzrc (" + codecError + ")");
            }
            var inputs = new List<PlayerInput>(body.InputCount);
            var commands = new List<RunCommand>(body.CommandCount);
            int pauses = 0;
            int checkpoints = 0;
            var slot = new PlayerSlot(0);
            var r = new ByteReader(new ReadOnlySpan<byte>(file, body.HeaderLength, file.Length - body.HeaderLength));
            uint tick = 0;
            uint seq = 0;
            while (true)
            {
                if (!r.TryU8(out byte tag))
                {
                    throw new FormatException("body ended before the footer");
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
                            throw new FormatException("bad 0x01 record");
                        }
                        seq++;
                        inputs.Add(new PlayerInput(slot, new InputEvent(tick, offset, key, down, seq)));
                        break;
                    case TagCommand:
                        if (!ReadDelta(ref r, ref tick) || !r.TryU8(out byte op) || !r.TryI32(out int a) || !r.TryI32(out int b))
                        {
                            throw new FormatException("bad 0x02 record");
                        }
                        seq++;
                        commands.Add(new RunCommand(tick, seq, slot, (RunCommandOp)op, a, b));
                        break;
                    case TagPause:
                        if (!ReadDelta(ref r, ref tick) || !r.TryU32(out _))
                        {
                            throw new FormatException("bad 0x03 record");
                        }
                        pauses++;
                        break;
                    case TagCheckpoint:
                        if (!r.TryU16(out _) || !r.TryU32(out uint at) || !r.TryU64(out _))
                        {
                            throw new FormatException("bad 0x04 record");
                        }
                        tick = at;
                        checkpoints++;
                        break;
                    default:
                        throw new FormatException("unknown tag " + tag);
                }
            }
            if (inputs.Count != body.InputCount || commands.Count != body.CommandCount || pauses != body.PauseCount
                || checkpoints != body.Checkpoints.Length)
            {
                throw new FormatException("record decoder disagrees with InputRecordCodec (counts differ)");
            }
            return new GoldenRecord(header.Setup, null, body.EndTick, inputs.ToArray(), commands.ToArray());
        }

        private static GoldenRecord LoadDuel(byte[] file, ReplayLimits limits)
        {
            if (!DuelRecordCodec.TryRead(file, limits, out DuelSetup? setup, out DuelRecordBody? body, out RecordError codecError)
                || setup == null || body == null)
            {
                throw new FormatException("not a valid .wzdr (" + codecError + ")");
            }
            var inputs = new List<PlayerInput>(body.InputCount);
            int start = body.HeaderLength + DuelInputDelayBytes;
            var r = new ByteReader(new ReadOnlySpan<byte>(file, start, file.Length - start));
            uint tick = 0;
            while (true)
            {
                if (!r.TryU8(out byte tag))
                {
                    throw new FormatException("body ended before the footer");
                }
                if (tag == TagFooter)
                {
                    break;
                }
                if (tag != TagDuelInput || !ReadDelta(ref r, ref tick) || !r.TryU8(out byte slot) || !r.TryU8(out byte offset)
                    || !r.TryU8(out byte packed) || !InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down)
                    || !r.TryVarUInt(out ulong seq) || seq > uint.MaxValue)
                {
                    throw new FormatException("bad 0x05 record");
                }
                inputs.Add(new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, (uint)seq)));
            }
            if (inputs.Count != body.InputCount)
            {
                throw new FormatException("record decoder disagrees with DuelRecordCodec (counts differ)");
            }
            return new GoldenRecord(null, setup, body.EndTick, inputs.ToArray(), Array.Empty<RunCommand>());
        }

        private static bool ReadDelta(ref ByteReader r, ref uint tick)
        {
            if (!r.TryVarUInt(out ulong delta) || delta > uint.MaxValue || (ulong)tick + delta > uint.MaxValue)
            {
                return false;
            }
            tick = (uint)(tick + delta);
            return true;
        }

        // 코덱이 확인했으므로 틱은 줄지 않고 모두 EndTick 보다 앞이다. 그래도 어긋나면 거절한다
        private void IndexInputs()
        {
            int i = 0;
            for (uint t = 0; t <= EndTick; t++)
            {
                _inputStart[t] = i;
                while (i < _inputs.Length && _inputs[i].Event.Tick == t)
                {
                    i++;
                }
            }
            if (i != _inputs.Length)
            {
                throw new FormatException("record has inputs at or after end_tick, or out of tick order");
            }
        }

        private void IndexCommands()
        {
            int i = 0;
            for (uint t = 0; t <= EndTick; t++)
            {
                _commandStart[t] = i;
                while (i < _commands.Length && _commands[i].Tick == t)
                {
                    i++;
                }
            }
            if (i != _commands.Length)
            {
                throw new FormatException("record has commands at or after end_tick, or out of tick order");
            }
        }
    }
}
