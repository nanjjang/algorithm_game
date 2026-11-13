using System;
using Wizard.Protocol.Duel;

namespace Wizard.Netcode.Duel
{
    // 한 사람에게 알릴 dropped 항목(CONTRACTS 10.3: seq, reason, run)의 대기열. 이어진 순번이 같은 이유면 한 항목으로 합친다.
    // 알림은 이유를 알려 줄 뿐이고 정본은 확정 열이다(6.4절). 그래서 한 번 실어 보낸 항목은 다시 보내지 않고,
    // 대기열이 차면 새 순번은 알리지 않고 그 수만 센다(Overflowed). 할당하지 않는다.
    internal sealed class DropNoticeQueue
    {
        private readonly DroppedRun[] _runs;
        private int _start;
        private int _count;

        public DropNoticeQueue(int capacity)
        {
            _runs = new DroppedRun[capacity];
        }

        public int Count => _count;

        public long Overflowed { get; private set; }

        public DroppedRun this[int index] => _runs[(_start + index) % _runs.Length];

        // first..last(포함)를 같은 이유로 더한다. first 는 1 이상, first ≤ last
        public void AddRange(uint first, uint last, DuelDropReason reason)
        {
            long s = first;
            long end = last;
            const long maxRun = ushort.MaxValue;
            while (s <= end)
            {
                if (_count > 0)
                {
                    int tail = (_start + _count - 1) % _runs.Length;
                    DroppedRun t = _runs[tail];
                    if (t.Reason == reason && (long)t.Seq + t.Run + 1 == s && t.Run < maxRun)
                    {
                        long take = Math.Min(maxRun - t.Run, end - s + 1);
                        _runs[tail] = new DroppedRun(t.Seq, reason, (ushort)(t.Run + take));
                        s += take;
                        continue;
                    }
                }
                if (_count == _runs.Length)
                {
                    Overflowed += end - s + 1;
                    return;
                }
                long len = Math.Min(maxRun + 1, end - s + 1);
                _runs[(_start + _count) % _runs.Length] = new DroppedRun((uint)s, reason, (ushort)(len - 1));
                _count++;
                s += len;
            }
        }

        public void RemoveFirst(int n)
        {
            _start = (_start + n) % _runs.Length;
            _count -= n;
        }
    }
}
