using System;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 3.8, 11.1, DECISIONS-TECH T-48·T-64. WZDR 을 쓰는 쪽(호스트의 확정 열 DuelHostCore.ConfirmedInputRecord, 다시 보기).
    // 확정된 입력을 확정 순서(InputOrder 순)대로 받는다. 순번은 받은 값을 그대로 싣는다
    // (결투의 순번은 DuelClientCore 가 매기고, 서버가 만든 뗌 이벤트는 그 사람의 순번 공간을 소비한다. CONTRACTS 10.1).
    //
    // 순번은 1 이상이기만 하면 된다(T-64). 한 슬롯의 순번이 레코드 순서로 늘어난다는 규칙은 없다: 확정 열은 InputOrder 순이고
    // 순번은 그 정렬 키의 맨 끝이라, 같은 (틱, 오프셋)의 K(순번 10)·앞(순번 11)은 앞 → K 로 실리고(spec 10 TV-N20),
    // 조작한 클라이언트가 패킷 사이에서 시각을 되돌리면 뒤 틱에 더 작은 순번이 온다(CONTRACTS 10.6 은 한 패킷 안의 역행만 거절한다).
    //
    // 읽는 쪽(DuelRecordCodec)이 받지 않을 열은 쓰지 않는다: 잘못 부르면(틱·순서 역행, 슬롯이 0·1 이 아님, 순번 0,
    // 한 사람이 한 틱에 9개째, 범위 밖 키, Finish 뒤의 호출) 아무것도 기록하지 않고 InvalidOperationException·ArgumentException 을 던진다.
    // 호출한 코드의 결함이다(틱 밖에서 부른다).
    public sealed class DuelRecordWriter
    {
        private readonly byte[] _header;                  // = setup.WriteCanonical 의 바이트
        private readonly ByteWriter _body = new ByteWriter(4096);
        private readonly byte[] _inputDelay = new byte[InputRecordFormat.DuelInputDelayCount];
        private readonly int[] _keysThisTick = new int[InputRecordFormat.DuelSlots];
        private PlayerInput _last;
        private bool _any;
        private bool _finished;

        // 머리말은 setup 의 정규 바이트다. 형식에 담기지 않는 설정이면 InvalidOperationException(DuelSetup.WriteCanonical)
        public DuelRecordWriter(DuelSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            var w = new ByteWriter();
            setup.WriteCanonical(w);
            _header = w.ToArray();
        }

        // 머리말 뒤의 input_delay 칸(시뮬레이션 밖의 값). Finish 전에 마지막으로 준 값이 실린다. 주지 않은 칸은 0이다
        public void SetInputDelay(PlayerSlot slot, int delayTicks)
        {
            RequireOpen();
            if (slot.Value >= InputRecordFormat.DuelSlots)
            {
                throw new ArgumentException("SetInputDelay: duel slots are 0 and 1", nameof(slot));
            }
            if (delayTicks < 0 || delayTicks > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(delayTicks));
            }
            _inputDelay[slot.Value] = (byte)delayTicks;
        }

        public void Append(in PlayerInput input)
        {
            RequireOpen();
            InputEvent e = input.Event;
            byte slot = input.Slot.Value;
            if (slot >= InputRecordFormat.DuelSlots)
            {
                throw new ArgumentException("Append: duel slots are 0 and 1", nameof(input));
            }
            if ((int)e.Key > (int)LogicalKey.Pause)
            {
                throw new ArgumentException("Append: unknown LogicalKey", nameof(input));
            }
            if (e.Seq == 0)
            {
                throw new InvalidOperationException("Append: seq must be 1 or above");
            }
            bool sameTick = _any && e.Tick == _last.Event.Tick;
            if (_any && InputOrder.Compare(in _last, in input) >= 0)
            {
                throw new InvalidOperationException("Append: inputs must come in InputOrder");
            }
            int keys = sameTick ? _keysThisTick[slot] : 0;
            if (keys >= InputOrder.MaxEventsPerTickPerPlayer)
            {
                throw new InvalidOperationException("Append: more than 8 events for one player in one tick");
            }

            // 확인이 끝났다. 여기부터 기록한다
            if (!sameTick)
            {
                Array.Clear(_keysThisTick, 0, _keysThisTick.Length);
            }
            _keysThisTick[slot] = keys + 1;
            _body.U8(InputRecordFormat.TagDuelInput);
            _body.VarUInt(e.Tick - (_any ? _last.Event.Tick : 0U));
            _body.U8(slot);
            _body.U8(e.Offset);
            _body.U8(e.PackKey());
            _body.VarUInt(e.Seq);
            _last = input;
            _any = true;
        }

        // 경기 끝. endTick = 끝난 뒤의 world.Tick, finalStateHash = 그때의 상태 해시. 이 뒤로는 아무것도 부르지 못한다
        public ReadOnlyMemory<byte> Finish(uint endTick, ulong finalStateHash)
        {
            RequireOpen();
            if (_any && endTick <= _last.Event.Tick)
            {
                throw new InvalidOperationException("Finish: endTick must be after every appended tick");
            }
            _finished = true;
            var prefix = new ByteWriter(_header.Length + InputRecordFormat.DuelInputDelayCount);
            prefix.Bytes(_header);
            prefix.Bytes(_inputDelay);
            return InputRecordFormat.Seal(prefix.Written, _body.Written, endTick, finalStateHash, 0);
        }

        private void RequireOpen()
        {
            if (_finished)
            {
                throw new InvalidOperationException("DuelRecordWriter is finished");
            }
        }
    }
}
