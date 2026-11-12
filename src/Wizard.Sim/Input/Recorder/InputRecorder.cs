using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 3.8, 3.1, 11.1. 무한 웨이브·연습의 판을 하는 쪽이 쓴다. 순번은 여기서만 매긴다(결투는 DuelClientCore).
    //
    // 한 틱에 Append 를 한 번 부른다(틱 번호가 늘어나는 순서로). 그 틱의 명령에 먼저 순번을 매기고, 그다음 키를 (Offset, KeyClass)
    // 순으로 안정 정렬해 매긴다. 아홉 번째 키부터는 번호를 매기기 전에 버린다(spec 01 4.3, spec 09 3.2 [임시]). 그래서
    // 레코드 순서 = 순번 순서이고, 읽는 쪽(InputRecordCodec)이 레코드 순서대로 1부터 다시 매긴 번호가 여기서 매긴 번호와 같다.
    // .wzrc 의 0x01 레코드에는 슬롯이 없다: 이 기록기는 슬롯 0 하나만 받는다(무한 웨이브와 연습은 플레이어가 하나다).
    //
    // 잘못 부르면(틱 역행, 슬롯 0 이 아님, 다른 틱의 키, 범위 밖 키·op, 출력 칸 부족, Finish 뒤의 호출) 아무것도 기록하지 않고
    // ArgumentException·InvalidOperationException 을 던진다. 호출한 코드의 결함이다(틱 밖에서 부른다).
    public sealed class InputRecorder
    {
        private readonly byte[] _header;                  // = header.Setup.WriteCanonical 의 바이트
        private readonly ByteWriter _body = new ByteWriter(4096);
        private QuantizedKey[] _keyScratch = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer * 2];

        private SeqCounter _seq;
        private uint _baseTick;                           // 마지막 레코드의 틱(Δtick 의 기준)
        private bool _anyAppend;
        private uint _lastAppendTick;
        private bool _debugUsed;
        private int _droppedKeys;

        private bool _hasCheckpoint;                      // Snapshot 이 자를 자리(마지막 중간 지점)
        private int _checkpointBodyLength;
        private uint _checkpointTick;
        private ulong _checkpointHash;
        private bool _checkpointDebugUsed;

        private bool _finished;

        public InputRecorder(InputRecordHeader header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            var w = new ByteWriter();
            InputRecordCodec.WriteHeader(w, header);
            _header = w.ToArray();
        }

        // 버린 키의 수(spec 01 4.3 "버리고 진단 표시를 남긴다"). 상태가 아니고 기록에도 없다
        internal int DroppedKeyCount => _droppedKeys;

        // 이 틱의 명령과 키에 순번을 매기고, 기록하고, 세계에 넣을 열을 돌려준다.
        // commandsOut 은 commands.Length 칸 이상, inputsOut 은 min(keys.Length, 8) 칸 이상이어야 한다.
        // 돌려준 열은 그대로 SimWorld.Step 에 넣을 수 있다(명령은 순번 오름차순, 키는 InputOrder 순).
        public void Append(uint tick, PlayerSlot slot, ReadOnlySpan<RunCommandDraft> commands, ReadOnlySpan<QuantizedKey> keys,
                           Span<RunCommand> commandsOut, out int commandCount, Span<PlayerInput> inputsOut, out int inputCount)
        {
            RequireOpen();
            if (_anyAppend && tick <= _lastAppendTick)
            {
                throw new InvalidOperationException("Append: ticks must increase (one call per tick)");
            }
            if (tick < _baseTick)
            {
                throw new InvalidOperationException("Append: tick is before the last pause or checkpoint mark");
            }
            if (slot.Value != 0)
            {
                throw new ArgumentException("Append: .wzrc records slot 0 only", nameof(slot));
            }
            int keep = keys.Length < InputOrder.MaxEventsPerTickPerPlayer ? keys.Length : InputOrder.MaxEventsPerTickPerPlayer;
            if (commandsOut.Length < commands.Length)
            {
                throw new ArgumentException("Append: commandsOut is too short", nameof(commandsOut));
            }
            if (inputsOut.Length < keep)
            {
                throw new ArgumentException("Append: inputsOut is too short", nameof(inputsOut));
            }
            for (int i = 0; i < commands.Length; i++)
            {
                if (!InputRecordFormat.IsKnownOp((byte)commands[i].Op))
                {
                    throw new ArgumentException("Append: unknown RunCommandOp", nameof(commands));
                }
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].Tick != tick)
                {
                    throw new ArgumentException("Append: key of another tick", nameof(keys));
                }
                if ((int)keys[i].Key > (int)LogicalKey.Pause)
                {
                    throw new ArgumentException("Append: unknown LogicalKey", nameof(keys));
                }
            }

            // 확인이 끝났다. 여기부터 기록한다
            _anyAppend = true;
            _lastAppendTick = tick;

            for (int i = 0; i < commands.Length; i++)
            {
                RunCommandDraft d = commands[i];
                uint seq = _seq.Next();
                _body.U8(InputRecordFormat.TagCommand);
                WriteDelta(tick);
                _body.U8((byte)d.Op);
                _body.I32(d.A);
                _body.I32(d.B);
                commandsOut[i] = new RunCommand(tick, seq, slot, d.Op, d.A, d.B);
                if ((byte)d.Op >= InputRecordFormat.FirstDebugOp)
                {
                    _debugUsed = true;
                }
            }
            commandCount = commands.Length;

            Span<QuantizedKey> sorted = SortKeys(keys);
            for (int i = 0; i < keep; i++)
            {
                QuantizedKey k = sorted[i];
                var e = new InputEvent(tick, k.Offset, k.Key, k.Down, _seq.Next());
                _body.U8(InputRecordFormat.TagInput);
                WriteDelta(tick);
                _body.U8(k.Offset);
                _body.U8(e.PackKey());
                inputsOut[i] = new PlayerInput(slot, e);
            }
            _droppedKeys += keys.Length - keep;
            inputCount = keep;
        }

        // 일시정지 시간(spec 09 3.4). 시뮬레이션에는 영향이 없다
        public void MarkPause(uint tick, uint wallMs)
        {
            RequireOpen();
            if (tick < _baseTick)
            {
                throw new InvalidOperationException("MarkPause: tick goes backwards");
            }
            _body.U8(InputRecordFormat.TagPause);
            WriteDelta(tick);
            _body.U32(wallMs);
        }

        // 중간 지점(spec 09 3.6): world.Tick == tick 일 때(그 틱을 Step 하기 전)의 상태 해시. 이미 기록한 틱보다 뒤여야 한다
        public void MarkCheckpoint(int wave, uint tick, ulong stateHash)
        {
            RequireOpen();
            if (wave < 0 || wave > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(wave));
            }
            if (tick < _baseTick || (_anyAppend && tick <= _lastAppendTick))
            {
                throw new InvalidOperationException("MarkCheckpoint: tick must be after every appended tick");
            }
            _body.U8(InputRecordFormat.TagCheckpoint);
            _body.U16((ushort)wave);
            _body.U32(tick);
            _body.U64(stateHash);
            _baseTick = tick;

            _hasCheckpoint = true;
            _checkpointBodyLength = _body.Length;
            _checkpointTick = tick;
            _checkpointHash = stateHash;
            _checkpointDebugUsed = _debugUsed;
        }

        // 마지막 중간 지점까지의 파일(중간 지점 올리기, 원정 정리의 재실행 입력). 꼬리말의 end_tick·final_state_hash 는 그 지점의 값이다.
        // 중간 지점이 아직 없으면 InvalidOperationException
        public ReadOnlyMemory<byte> Snapshot()
        {
            if (!_hasCheckpoint)
            {
                throw new InvalidOperationException("Snapshot before any checkpoint");
            }
            ReadOnlySpan<byte> body = _body.Written.Slice(0, _checkpointBodyLength);
            return InputRecordFormat.Seal(_header, body, _checkpointTick, _checkpointHash, Flags(_checkpointDebugUsed));
        }

        // 판 끝. endTick = 끝난 뒤의 world.Tick, finalStateHash = 그때의 상태 해시. 이 뒤로는 아무것도 부르지 못한다
        public ReadOnlyMemory<byte> Finish(uint endTick, ulong finalStateHash)
        {
            RequireOpen();
            if (endTick < _baseTick || (_anyAppend && endTick <= _lastAppendTick))
            {
                throw new InvalidOperationException("Finish: endTick must be after every appended tick");
            }
            _finished = true;
            return InputRecordFormat.Seal(_header, _body.Written, endTick, finalStateHash, Flags(_debugUsed));
        }

        private static byte Flags(bool debugUsed) => debugUsed ? InputRecordFormat.FlagDebugUsed : (byte)0;

        private void WriteDelta(uint tick)
        {
            _body.VarUInt(tick - _baseTick);
            _baseTick = tick;
        }

        private void RequireOpen()
        {
            if (_finished)
            {
                throw new InvalidOperationException("InputRecorder is finished");
            }
        }

        // (Offset, KeyClass) 순 안정 정렬. 같은 칸끼리는 받은 순서를 지킨다
        private Span<QuantizedKey> SortKeys(ReadOnlySpan<QuantizedKey> keys)
        {
            if (_keyScratch.Length < keys.Length)
            {
                _keyScratch = new QuantizedKey[keys.Length];
            }
            Span<QuantizedKey> s = new Span<QuantizedKey>(_keyScratch, 0, keys.Length);
            keys.CopyTo(s);
            DetSort.InsertionStable(s, default(KeyComparer));
            return s;
        }

        private readonly struct KeyComparer : IComparer<QuantizedKey>
        {
            public int Compare(QuantizedKey a, QuantizedKey b)
            {
                if (a.Offset != b.Offset)
                {
                    return a.Offset < b.Offset ? -1 : 1;
                }
                int ca = InputOrder.KeyClass(a.Key), cb = InputOrder.KeyClass(b.Key);
                if (ca != cb)
                {
                    return ca < cb ? -1 : 1;
                }
                return 0;
            }
        }
    }
}
