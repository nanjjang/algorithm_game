using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.BalanceLab.Core
{
    // 사람이 플레이한 .wzrc 를 틱마다 세계에 넣을 열로 펼치고 연습 세계에서 다시 돌린다.
    //
    // 읽기: 바이트는 먼저 코어의 유일한 문(InputRecordCodec.TryRead)을 통과해야 한다(CONTRACTS 3.8, 신뢰할 수 없는 기록). 통과한 본문만
    // 여기서 CONTRACTS 11.1 의 배치대로 다시 펼친다(코어의 본문 판독기는 내부라 도구가 한 번 더 읽는다):
    //   0x01 Input   Δtick(varuint), offset u8, key_down u8      순번은 레코드 순서대로 1부터(명령과 같은 공간)
    //   0x02 Command Δtick, op u8, a i32, b i32
    //   0x03 Pause   Δtick, wall_ms u32                          시뮬레이션에 영향 없음
    //   0x04 Checkpoint wave u16, tick u32(절대), hash u64       이 도구는 쓰지 않는다
    //   0xFF 꼬리말
    // 넷 모두 기준 틱을 옮긴다. 펼친 레코드 수가 코덱이 센 수와 다르면 이 판독기의 결함이라 거절한다.
    //
    // 세계: .wzrc 는 무한 웨이브와 연습이 같은 머리말을 쓴다. 손맛 시제품은 연습 세계에서만 돌므로 이 도구는 연습 세계로 다시 돌린다
    // (연습 세계는 머리말의 씨앗과 조립을 읽지 않는다). 재실행의 마지막 해시가 꼬리말의 final_state_hash 와 같아야 그 기록이 이 코어·팩의
    // 연습 세계에서 나온 것이다(같지 않으면 아래 계측은 사람이 한 플레이가 아니다).
    internal sealed class WzrcTrace
    {
        private const byte TagInput = 0x01;
        private const byte TagCommand = 0x02;
        private const byte TagPause = 0x03;
        private const byte TagCheckpoint = 0x04;
        private const byte TagFooter = 0xFF;

        private WzrcTrace(InputRecordHeader header, uint endTick, ulong finalStateHash, List<PlayerInput> inputs,
                          List<RunCommand> commands, int pauseCount)
        {
            Header = header;
            EndTick = endTick;
            FinalStateHash = finalStateHash;
            Inputs = inputs;
            Commands = commands;
            PauseCount = pauseCount;
        }

        public InputRecordHeader Header { get; }

        public uint EndTick { get; }

        public ulong FinalStateHash { get; }

        // 틱 오름차순(기록 순서)
        public IReadOnlyList<PlayerInput> Inputs { get; }

        public IReadOnlyList<RunCommand> Commands { get; }

        public int PauseCount { get; }

        public static bool TryLoad(ReadOnlySpan<byte> file, out WzrcTrace? trace, out string error)
        {
            trace = null;
            var limits = new ReplayLimits(uint.MaxValue, long.MaxValue);
            if (!InputRecordCodec.TryRead(file, limits, out InputRecordHeader? header, out InputRecordBody? body, out RecordError recordError))
            {
                error = "not a valid .wzrc (" + recordError + ")";
                return false;
            }
            var inputs = new List<PlayerInput>(body.InputCount);
            var commands = new List<RunCommand>(body.CommandCount);
            int pauses = 0;
            var slot = new PlayerSlot(0);
            var r = new ByteReader(file.Slice(body.HeaderLength));
            uint baseTick = 0;
            uint seq = 0;
            while (true)
            {
                if (!r.TryU8(out byte tag))
                {
                    error = "record ended without a footer";
                    return false;
                }
                if (tag == TagFooter)
                {
                    break;
                }
                if (tag == TagCheckpoint)
                {
                    if (!r.TryU16(out _) || !r.TryU32(out uint at) || !r.TryU64(out _))
                    {
                        error = "truncated checkpoint record";
                        return false;
                    }
                    baseTick = at;
                    continue;
                }
                if (!r.TryVarUInt(out ulong delta) || baseTick + delta > uint.MaxValue)
                {
                    error = "bad tick delta";
                    return false;
                }
                uint tick = (uint)(baseTick + delta);
                baseTick = tick;
                switch (tag)
                {
                    case TagInput:
                        if (!r.TryU8(out byte offset) || !r.TryU8(out byte packed) || !InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down))
                        {
                            error = "bad input record";
                            return false;
                        }
                        inputs.Add(new PlayerInput(slot, new InputEvent(tick, offset, key, down, ++seq)));
                        break;
                    case TagCommand:
                        if (!r.TryU8(out byte op) || !r.TryI32(out int a) || !r.TryI32(out int b))
                        {
                            error = "bad command record";
                            return false;
                        }
                        commands.Add(new RunCommand(tick, ++seq, slot, (RunCommandOp)op, a, b));
                        break;
                    case TagPause:
                        if (!r.TryU32(out _))
                        {
                            error = "bad pause record";
                            return false;
                        }
                        pauses++;
                        break;
                    default:
                        error = "unknown record tag " + tag;
                        return false;
                }
            }
            if (inputs.Count != body.InputCount || commands.Count != body.CommandCount || pauses != body.PauseCount)
            {
                error = "the tool's reader disagrees with the codec about the record counts";
                return false;
            }
            error = string.Empty;
            trace = new WzrcTrace(header, body.EndTick, body.FinalStateHash, inputs, commands, pauses);
            return true;
        }

        // 연습 세계로 다시 돌린다. 틱 0..EndTick-1 을 Step 한다
        public ReplayRun Replay(ContentDb db)
        {
            SimWorld world = PracticeRig.NewWorld(db);
            var log = new EventLog();
            var tickInputs = new List<PlayerInput>();
            var tickCommands = new List<RunCommand>();
            int ni = 0;
            int nc = 0;
            for (uint t = 0; t < EndTick; t++)
            {
                tickInputs.Clear();
                tickCommands.Clear();
                while (ni < Inputs.Count && Inputs[ni].Event.Tick == t)
                {
                    tickInputs.Add(Inputs[ni++]);
                }
                while (nc < Commands.Count && Commands[nc].Tick == t)
                {
                    tickCommands.Add(Commands[nc++]);
                }
                world.Step(tickInputs.ToArray(), tickCommands.ToArray(), log);
                if (world.IsOver)
                {
                    break;
                }
            }
            int rejected = 0;
            foreach (SimEvent e in log.Events)
            {
                if (e.Type == SimEventType.DebugCommandRejected)
                {
                    rejected++;
                }
            }
            return new ReplayRun(log, world.Tick, world.ComputeStateHash(), world.ComputeStateHash() == FinalStateHash, rejected);
        }
    }

    internal sealed class ReplayRun
    {
        public ReplayRun(EventLog log, uint ticks, ulong finalHash, bool hashMatches, int rejectedCommands)
        {
            Log = log;
            Ticks = ticks;
            FinalHash = finalHash;
            HashMatches = hashMatches;
            RejectedCommands = rejectedCommands;
        }

        public EventLog Log { get; }

        public uint Ticks { get; }

        public ulong FinalHash { get; }

        public bool HashMatches { get; }

        public int RejectedCommands { get; }
    }
}
