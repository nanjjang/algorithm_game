using System;
using System.Collections.Generic;
using Wizard.Protocol.Duel;

namespace Wizard.Client.Duel
{
    // LoopbackHost 의 한 방향 한 사람 몫의 줄. 패킷을 복사해 두고 닿는 시각(deliverAtUs)이 된 것부터 (닿는 시각, 넣은 순서) 순으로 내준다.
    // 인위 지연(디버그 값)을 바꾸면 늦게 넣은 패킷이 먼저 닿을 수 있다(실제 망의 순서 바뀜과 같다). 버퍼는 이 줄이 가진 것을 다시 쓴다.
    internal sealed class LoopbackPacketQueue
    {
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Stack<byte[]> _free = new Stack<byte[]>();
        private long _order;

        public int Count => _entries.Count;

        public void Enqueue(ReadOnlySpan<byte> packet, long deliverAtUs)
        {
            if (packet.Length == 0 || packet.Length > DuelWire.MaxPacketBytes)
            {
                return;   // 10.1: 한 패킷은 1,200바이트를 넘지 않는다. 망이 실을 수 없는 것은 버린다(UDP 와 같다)
            }
            byte[] data = _free.Count > 0 ? _free.Pop() : new byte[DuelWire.MaxPacketBytes];
            packet.CopyTo(data);
            var e = new Entry(data, packet.Length, deliverAtUs, _order++);
            int i = _entries.Count;
            while (i > 0 && Later(_entries[i - 1], e))
            {
                i--;
            }
            _entries.Insert(i, e);
        }

        // 닿는 시각이 nowUs 이하인 첫 패킷을 buffer 에 옮긴다. 없으면 0. buffer 가 모자라면 그 패킷은 버린다(UDP 의 잘림과 같이 받지 못한다)
        public int TryDequeue(long nowUs, Span<byte> buffer, out long deliverAtUs)
        {
            deliverAtUs = 0;
            if (_entries.Count == 0 || _entries[0].DeliverAtUs > nowUs)
            {
                return 0;
            }
            Entry e = _entries[0];
            _entries.RemoveAt(0);
            _free.Push(e.Data);
            deliverAtUs = e.DeliverAtUs;
            if (buffer.Length < e.Length)
            {
                return -1;
            }
            new ReadOnlySpan<byte>(e.Data, 0, e.Length).CopyTo(buffer);
            return e.Length;
        }

        private static bool Later(in Entry a, in Entry b) =>
            a.DeliverAtUs > b.DeliverAtUs || (a.DeliverAtUs == b.DeliverAtUs && a.Order > b.Order);

        private readonly struct Entry
        {
            public readonly byte[] Data;
            public readonly int Length;
            public readonly long DeliverAtUs;
            public readonly long Order;

            public Entry(byte[] data, int length, long deliverAtUs, long order)
            {
                Data = data;
                Length = length;
                DeliverAtUs = deliverAtUs;
                Order = order;
            }
        }
    }
}
