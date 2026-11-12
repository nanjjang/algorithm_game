using System.Collections.Generic;
using Wizard.Sim.Input;

namespace Wizard.BalanceLab.Feel
{
    // 사람이 플레이한 것처럼 틱·오프셋을 정해 쓴 키 각본(자체 점검의 입력). 키 이벤트는 InputRecorder 가 .wzrc 로 적는다
    internal sealed class KeyScript
    {
        private readonly SortedDictionary<uint, List<QuantizedKey>> _byTick = new SortedDictionary<uint, List<QuantizedKey>>();

        public void Down(uint tick, LogicalKey key, byte offset = 0) => Add(new QuantizedKey(tick, offset, key, true));

        public void Up(uint tick, LogicalKey key, byte offset = 0) => Add(new QuantizedKey(tick, offset, key, false));

        public IEnumerable<KeyValuePair<uint, List<QuantizedKey>>> Ticks => _byTick;

        private void Add(QuantizedKey k)
        {
            if (!_byTick.TryGetValue(k.Tick, out List<QuantizedKey>? list))
            {
                list = new List<QuantizedKey>();
                _byTick[k.Tick] = list;
            }
            list.Add(k);
        }
    }
}
