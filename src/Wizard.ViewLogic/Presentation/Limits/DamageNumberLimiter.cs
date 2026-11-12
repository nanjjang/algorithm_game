using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "넘쳤을 때" 6번: 피해 숫자는 입자가 아니고 화면에 40개까지다. 같은 적에게 0.2초 안에 들어간 피해는 합쳐서 한 숫자로.
    //  - 0.2초는 그 숫자를 처음 띄운 시각부터 센다 [임시].
    //  - 40개가 차 있으면 가장 오래된 숫자를 지우고 새 숫자를 띄운다 [임시: 새 피해가 항상 보이게, 일회성 규칙과 같은 방향].
    // 숫자의 표시 여부(피해 숫자 표시: 전부 / 임계·약점만 / 끔)는 설정 쪽이 거른 뒤 여기에 넣는다.
    public sealed class DamageNumberLimiter
    {
        private readonly int _max;
        private readonly int _mergeWindowMs;
        private readonly List<Entry> _live = new List<Entry>();    // 띄운 순
        private long _nextId = 1;

        public DamageNumberLimiter(ParticleBudgetTable table)
        {
            _max = table.Shares.DamageNumbersMax;
            _mergeWindowMs = table.Shares.DamageMergeWindowMs;
        }

        public int LiveCount => _live.Count;

        public DamageNumberOp Submit(long targetKey, long amount, long nowMs, int lifetimeMs)
        {
            Expire(nowMs);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Entry e = _live[i];
                if (e.Target == targetKey && nowMs - e.SpawnMs < _mergeWindowMs)
                {
                    long sum = unchecked(e.Amount + amount);
                    _live[i] = new Entry(e.Id, e.Target, sum, e.SpawnMs, Math.Max(e.ExpireMs, nowMs + Math.Max(0, lifetimeMs)));
                    return new DamageNumberOp(DamageNumberOpKind.Merged, e.Id, sum, 0);
                }
            }
            long evicted = 0;
            if (_live.Count >= _max)
            {
                if (_max <= 0)
                {
                    return new DamageNumberOp(DamageNumberOpKind.New, 0, amount, 0);
                }
                evicted = _live[0].Id;
                _live.RemoveAt(0);
            }
            long id = _nextId++;
            _live.Add(new Entry(id, targetKey, amount, nowMs, nowMs + Math.Max(0, lifetimeMs)));
            return new DamageNumberOp(DamageNumberOpKind.New, id, amount, evicted);
        }

        public void Expire(long nowMs) => _live.RemoveAll(e => e.ExpireMs <= nowMs);

        private readonly struct Entry
        {
            public readonly long Id;
            public readonly long Target;
            public readonly long Amount;
            public readonly long SpawnMs;
            public readonly long ExpireMs;

            public Entry(long id, long target, long amount, long spawnMs, long expireMs)
            {
                Id = id;
                Target = target;
                Amount = amount;
                SpawnMs = spawnMs;
                ExpireMs = expireMs;
            }
        }
    }
}
