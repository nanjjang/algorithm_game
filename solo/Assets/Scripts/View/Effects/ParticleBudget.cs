using System.Collections.Generic;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 입자 예산 관리기(순수, 10 요약서 3.3절 "모든 입자는 입자 예산 관리기 한 곳에서 발급"). 분류·수명을 대고 하나씩 청한다.
    /// - 반복(recurring: shot, wand_idle, environment): 몫이 차면 새로 안 만든다.
    /// - 일회(one_shot: art, hit, kill): 몫이 차면 같은 분류의 가장 오래된 것을 내보내고(흐려지며 사라짐 0.1초) 새 것을 준다 — 새 사건은 항상 보인다.
    /// - 합계가 넘치면 우선순위 번호가 큰 분류부터(6 → 5 → 4 → 3 → 2, 1은 마지막) 가장 오래된 것을 내보낸다.
    ///   반복 분류는 자기보다 번호가 큰 분류만 밀어낼 수 있고, 못 밀면 거절된다.
    /// 내보낸 입자의 표는 <see cref="DrainEvicted"/>로 그리는 쪽이 가져가 흐리게 사라지게 한다(이미 몫에서는 빠졌다).
    /// </summary>
    public sealed class ParticleBudget
    {
        struct Entry { public int Token; public float Expire; }

        readonly ParticleBudgetConfig _cfg;
        readonly Dictionary<string, List<Entry>> _live = new Dictionary<string, List<Entry>>();
        readonly Dictionary<int, string> _categoryOf = new Dictionary<int, string>();
        readonly List<int> _evicted = new List<int>();
        int _nextToken = 1;
        int _total;

        /// <summary>연출 감도 0 전체 / 1 줄임 / 2 최소.</summary>
        public int Level;

        public ParticleBudget(ParticleBudgetConfig cfg)
        {
            _cfg = cfg ?? new ParticleBudgetConfig();
            for (int i = 0; i < _cfg.Pools.Count; i++) _live[_cfg.Pools[i].Category] = new List<Entry>();
        }

        public ParticleBudgetConfig Config => _cfg;
        public int Total => _total;
        public int TotalCap => _cfg.Total[ParticleBudgetConfig.Level(Level)];

        public int Quota(string category)
        {
            var p = _cfg.Pool(category);
            return p == null ? 0 : p.Quota[ParticleBudgetConfig.Level(Level)];
        }

        public int Count(string category)
        {
            List<Entry> l;
            return _live.TryGetValue(category, out l) ? l.Count : 0;
        }

        /// <summary>수명이 다한 것을 뺀다(now = 표시 초).</summary>
        public void Prune(float now)
        {
            foreach (var kv in _live)
            {
                var l = kv.Value;
                for (int i = l.Count - 1; i >= 0; i--)
                {
                    if (l[i].Expire > now) continue;
                    _categoryOf.Remove(l[i].Token);
                    l.RemoveAt(i);
                    _total--;
                }
            }
        }

        /// <summary>입자 하나를 청한다. 주면 true와 표(token), 아니면 false(그리지 않는다).</summary>
        public bool TryAcquire(string category, float now, float lifeSeconds, out int token)
        {
            token = 0;
            var pool = _cfg.Pool(category);
            if (pool == null) return false;
            int quota = pool.Quota[ParticleBudgetConfig.Level(Level)];
            if (quota <= 0) return false;
            Prune(now);
            var mine = _live[category];
            if (mine.Count >= quota)
            {
                if (!pool.OneShot) return false;
                EvictOldest(category);
            }
            int cap = TotalCap;
            if (cap <= 0) return false;
            while (_total >= cap)
            {
                string victim = PickVictim(pool);
                if (victim == null) return false;
                EvictOldest(victim);
            }
            token = _nextToken++;
            mine.Add(new Entry { Token = token, Expire = now + lifeSeconds });
            _categoryOf[token] = category;
            _total++;
            return true;
        }

        /// <summary>n개를 청해 받은 표를 into에 더한다. 받은 수를 돌려준다.</summary>
        public int Acquire(string category, int n, float now, float lifeSeconds, List<int> into)
        {
            int got = 0;
            for (int i = 0; i < n; i++)
            {
                int t;
                if (!TryAcquire(category, now, lifeSeconds, out t)) break;
                if (into != null) into.Add(t);
                got++;
            }
            return got;
        }

        /// <summary>그리는 쪽이 입자를 일찍 거둘 때.</summary>
        public void Release(int token)
        {
            string cat;
            if (!_categoryOf.TryGetValue(token, out cat)) return;
            _categoryOf.Remove(token);
            var l = _live[cat];
            for (int i = 0; i < l.Count; i++)
                if (l[i].Token == token) { l.RemoveAt(i); _total--; break; }
        }

        public bool IsLive(int token) => _categoryOf.ContainsKey(token);

        public void DrainEvicted(List<int> into)
        {
            into.AddRange(_evicted);
            _evicted.Clear();
        }

        public void Clear()
        {
            foreach (var kv in _live) kv.Value.Clear();
            _categoryOf.Clear();
            _evicted.Clear();
            _total = 0;
        }

        string PickVictim(ParticlePool requester)
        {
            ParticlePool best = null;
            for (int i = 0; i < _cfg.Pools.Count; i++)
            {
                var p = _cfg.Pools[i];
                if (Count(p.Category) == 0) continue;
                // 반복 분류는 자기보다 뒤(번호가 큰) 분류만 밀어낸다. 일회 분류는 6 → … → 1 순서로 무엇이든.
                if (!requester.OneShot && p.Priority <= requester.Priority) continue;
                if (best == null || p.Priority > best.Priority) best = p;
            }
            return best != null ? best.Category : null;
        }

        void EvictOldest(string category)
        {
            var l = _live[category];
            if (l.Count == 0) return;
            int token = l[0].Token;
            l.RemoveAt(0);
            _categoryOf.Remove(token);
            _total--;
            _evicted.Add(token);
        }
    }
}
