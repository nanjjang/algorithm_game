using System;
using Wizard.Sim.Events;

namespace Wizard.Netcode.Duel
{
    // 표시 이벤트의 장부(CONTRACTS 6.4 "표시 이벤트의 단계").
    //
    // 열쇠는 (Tick, Type, Slot, Ordinal)이다. Tick 은 그 이벤트를 낸 Step 의 틱, Ordinal 은 그 틱에서 같은 (Type, Slot)의 몇 번째인가.
    // EntityId(Source, Target)는 열쇠에 넣지 않는다: 되감기 뒤에 생성 순서가 바뀌면 개체 번호가 달라져서, 예측 하나가 틀렸을 때
    // 같은 틱 뒤의 무관한 이벤트가 전부 번복·재예측으로 깜박이기 때문이다.
    //
    // 틱마다의 기록은 "Predicted 로 내보냈고 아직 끝이 나지 않은 열쇠"의 모음이다(DECISIONS-TECH T-66, 6.4절 개정문).
    // - 예측 세계가 처음 돈 틱: 그 틱의 이벤트를 전부 Predicted 로 내고 기록한다.
    // - 예측 세계가 그 틱을 다시 돌림(되감기): 기록에 없는 열쇠만 Predicted 로 내고 기록에 더한다. 기록에 있는데 새 열에 없는 열쇠는
    //   **아무것도 내지 않고 기록에 남긴다**. 다시 돌린 것도 예측이라 다음 되감기나 확정에서 그 열쇠가 돌아올 수 있고,
    //   여기서 Retracted 를 내면 같은 열쇠가 Retracted 뒤에 Confirmed 로 나온다(NetSim 27 조합 가운데 11 조합에서 났다).
    // - 확정 세계가 그 틱을 지남: 기록 가운데 확정 열에 없는 열쇠는 Retracted, 확정 열은 전부 Confirmed. 기록을 지운다.
    //   Retracted 는 여기서만 나온다. 그래서 한 열쇠의 끝은 Confirmed 와 Retracted 가운데 하나이고 한 번뿐이다.
    //   Confirmed 는 확정 세계에서만 나오고, 한 틱은 확정 세계에서 한 번만 지나므로 두 번 나오지 않는다.
    internal sealed class PresentationLedger
    {
        private readonly TickRecord[] _records;
        private PresentationEvent[] _queue = new PresentationEvent[256];
        private int _head;
        private int _count;

        public PresentationLedger(int slots)
        {
            if (slots < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slots));
            }
            _records = new TickRecord[slots];
            for (int i = 0; i < slots; i++)
            {
                _records[i] = new TickRecord();
            }
        }

        public int PendingCount => _count;

        public void OnPredictedTick(uint tick, ReadOnlySpan<SimEvent> events)
        {
            TickRecord? old = Find(tick);
            if (old == null)
            {
                TickRecord fresh = Allocate(tick);
                fresh.Set(events);
                for (int i = 0; i < fresh.Count; i++)
                {
                    Enqueue(fresh.Events[i], EventPhase.Predicted, fresh.Ordinals[i]);
                }
                return;
            }
            TickRecord scratch = Scratch();
            scratch.Set(events);
            for (int i = 0; i < scratch.Count; i++)
            {
                int at = old.IndexOf(scratch.Events[i], scratch.Ordinals[i]);
                if (at >= 0)
                {
                    old.Events[at] = scratch.Events[i];   // 표시용 인자(개체 번호 등)는 가장 최근 예측의 것으로 둔다. 내지는 않는다
                    continue;
                }
                Enqueue(scratch.Events[i], EventPhase.Predicted, scratch.Ordinals[i]);
                old.Add(scratch.Events[i], scratch.Ordinals[i]);
            }
        }

        // emit 이 거짓이면(전체 상태로 이미 지난 틱을 다시 지나는 경우) 기록만 지운다
        public void OnConfirmedTick(uint tick, ReadOnlySpan<SimEvent> events, bool emit)
        {
            TickRecord? old = Find(tick);
            if (emit)
            {
                TickRecord confirmed = Scratch();
                confirmed.Set(events);
                if (old != null)
                {
                    for (int i = 0; i < old.Count; i++)
                    {
                        if (!confirmed.Contains(old.Events[i], old.Ordinals[i]))
                        {
                            Enqueue(old.Events[i], EventPhase.Retracted, old.Ordinals[i]);
                        }
                    }
                }
                for (int i = 0; i < confirmed.Count; i++)
                {
                    Enqueue(confirmed.Events[i], EventPhase.Confirmed, confirmed.Ordinals[i]);
                }
            }
            if (old != null)
            {
                old.Used = false;
            }
        }

        // 전체 상태로 확정 세계를 덮어쓴 뒤: 그 앞 틱의 예측 기록은 확정 열을 받을 수 없으므로 그냥 지운다(번복도 확정도 내지 않는다)
        public void ForgetBefore(uint tick)
        {
            foreach (TickRecord r in _records)
            {
                if (r.Used && r.Tick < tick)
                {
                    r.Used = false;
                }
            }
        }

        public int Drain(Span<PresentationEvent> buffer)
        {
            int n = buffer.Length < _count ? buffer.Length : _count;
            for (int i = 0; i < n; i++)
            {
                buffer[i] = _queue[_head];
                _head = (_head + 1) % _queue.Length;
            }
            _count -= n;
            if (_count == 0)
            {
                _head = 0;
            }
            return n;
        }

        private void Enqueue(in SimEvent e, EventPhase phase, ushort ordinal)
        {
            if (_count == _queue.Length)
            {
                var bigger = new PresentationEvent[_queue.Length * 2];
                for (int i = 0; i < _count; i++)
                {
                    bigger[i] = _queue[(_head + i) % _queue.Length];
                }
                _queue = bigger;
                _head = 0;
            }
            _queue[(_head + _count) % _queue.Length] = new PresentationEvent { Event = e, Phase = phase, Ordinal = ordinal };
            _count++;
        }

        private TickRecord? Find(uint tick)
        {
            foreach (TickRecord r in _records)
            {
                if (r.Used && !r.IsScratch && r.Tick == tick)
                {
                    return r;
                }
            }
            return null;
        }

        // 빈 기록. 없으면 가장 이른 틱의 기록을 다시 쓴다(되감기 한도 안에서는 일어나지 않는다)
        private TickRecord Allocate(uint tick)
        {
            TickRecord? pick = null;
            foreach (TickRecord r in _records)
            {
                if (r.IsScratch)
                {
                    continue;
                }
                if (!r.Used)
                {
                    pick = r;
                    break;
                }
                if (pick == null || r.Tick < pick.Tick)
                {
                    pick = r;
                }
            }
            pick!.Used = true;
            pick.Tick = tick;
            return pick;
        }

        private TickRecord Scratch()
        {
            if (_scratch == null)
            {
                _scratch = new TickRecord { IsScratch = true };
            }
            return _scratch;
        }

        private TickRecord? _scratch;

        private sealed class TickRecord
        {
            public uint Tick;
            public bool Used;
            public bool IsScratch;
            public SimEvent[] Events = new SimEvent[16];
            public ushort[] Ordinals = new ushort[16];
            public int Count;

            public void Set(ReadOnlySpan<SimEvent> events)
            {
                if (Events.Length < events.Length)
                {
                    int size = Events.Length;
                    while (size < events.Length)
                    {
                        size *= 2;
                    }
                    Events = new SimEvent[size];
                    Ordinals = new ushort[size];
                }
                Count = events.Length;
                for (int i = 0; i < events.Length; i++)
                {
                    Events[i] = events[i];
                    ushort ordinal = 0;
                    for (int j = 0; j < i; j++)
                    {
                        if (events[j].Type == events[i].Type && events[j].Slot == events[i].Slot)
                        {
                            ordinal++;
                        }
                    }
                    Ordinals[i] = ordinal;
                }
            }

            public bool Contains(in SimEvent e, ushort ordinal) => IndexOf(e, ordinal) >= 0;

            public int IndexOf(in SimEvent e, ushort ordinal)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Ordinals[i] == ordinal && Events[i].Type == e.Type && Events[i].Slot == e.Slot)
                    {
                        return i;
                    }
                }
                return -1;
            }

            // 다시 돌린 틱에서 처음 나온 열쇠를 기록 끝에 더한다(서수는 그 열에서 센 값 그대로)
            public void Add(in SimEvent e, ushort ordinal)
            {
                if (Count == Events.Length)
                {
                    var events = new SimEvent[Events.Length * 2];
                    var ordinals = new ushort[Events.Length * 2];
                    for (int i = 0; i < Count; i++)
                    {
                        events[i] = Events[i];
                        ordinals[i] = Ordinals[i];
                    }
                    Events = events;
                    Ordinals = ordinals;
                }
                Events[Count] = e;
                Ordinals[Count] = ordinal;
                Count++;
            }
        }
    }
}
