using System;
using System.Collections.Generic;
using Wizard.Core;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "입자 예산 관리기"(ARCHITECTURE 9절의 ParticleBudget). 한 화면(전투, 결투, 광장, 강화 화면)에 하나.
    // 넘쳤을 때의 처리는 9.3절의 규칙 하나뿐이다:
    //  1. 입자는 전부 여기서 발급받는다. 분류와 수명을 대고 요청하며, 안 주면 그리지 않는다.
    //  2. 반복 분류는 몫이 차면 새로 만들지 않는다. 이미 나온 입자는 제 수명대로 사라진다(지우지 않으므로 깜빡이지 않는다).
    //  3. 일회성 분류는 몫이 차면 그 분류에서 가장 오래된 것부터 0.1초에 걸쳐 흐려지며 자리를 내준다. 새 사건은 항상 보인다.
    //  4. 합계가 넘치면 우선순위 번호가 큰 분류부터 줄인다(전투: 6 → 5 → 4 → 3 → 2, 1은 마지막).
    //     [임시 해석] 반복 분류(6, 5, 4)는 2번 규칙대로 지우지 않으므로 "줄인다" = 합계가 찬 동안 새로 만들지 않는다.
    //     일회성 분류(3, 2)는 요청한 분류보다 번호가 큰 것부터 가장 오래된 것을 흐리게 지운다. 요청한 분류보다 중요한 분류는 건드리지 않는다.
    //     그래도 자리가 없으면 요청한 분류 안의 오래된 것을, 그래도 없으면 발급 수를 줄인다.
    // 몫과 합계는 연출 줄이기 단계를 따른다. 단계를 바꾸면 이미 나온 입자는 그대로 두고 새 요청부터 새 몫을 쓴다.
    // 표시 계층 전용이다. 게임 시간·코어와 무관하고 시각은 표시 쪽 밀리초다.
    public sealed class ParticleBudget
    {
        private readonly ParticleBudgetTable _table;
        private readonly ParticleSceneDef _scene;
        private readonly List<Batch> _batches = new List<Batch>();        // 발급 순(= BatchId 오름차순)
        private readonly List<ParticleEviction> _evictions = new List<ParticleEviction>();
        private readonly int[] _live;
        private readonly ParticlePoolDef[] _byPriorityDesc;
        private MotionPreset _preset;
        private long _nextBatchId = 1;

        public ParticleBudget(ParticleBudgetTable table, ParticleScene scene, MotionPreset preset)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _scene = table.Scene(scene);
            _preset = preset;
            int maxCategory = 0;
            foreach (ParticleCategory c in (ParticleCategory[])Enum.GetValues(typeof(ParticleCategory)))
            {
                maxCategory = Math.Max(maxCategory, (int)c);
            }
            _live = new int[maxCategory + 1];
            _byPriorityDesc = new ParticlePoolDef[_scene.Pools.Count];
            for (int i = 0; i < _byPriorityDesc.Length; i++)
            {
                _byPriorityDesc[i] = _scene.Pools[i];
            }
            // 우선순위 번호가 서로 다르다(표가 검사한다) → 완전 순서
            DetSort.InsertionStable(new Span<ParticlePoolDef>(_byPriorityDesc), new PriorityDescending());
        }

        public ParticleScene Scene => _scene.Scene;

        public MotionPreset Preset => _preset;

        public int Total => _scene.Total(_preset);

        // 합계에 세는 분류의 살아 있는 입자 수
        public int LiveTotal
        {
            get
            {
                int sum = 0;
                foreach (ParticlePoolDef p in _scene.Pools)
                {
                    if (!p.OutsideTotal)
                    {
                        sum += _live[(int)p.Category];
                    }
                }
                return sum;
            }
        }

        public int Live(ParticleCategory category) => _live[(int)category];

        public int Quota(ParticleCategory category) => _scene.Quota(category, _preset);

        public void SetPreset(MotionPreset preset)
        {
            ParticleBudgetTable.PresetIndex(preset);   // 범위 검사
            _preset = preset;
        }

        // 수명이 다한 입자를 지운다
        public void Advance(long nowMs)
        {
            int w = 0;
            for (int i = 0; i < _batches.Count; i++)
            {
                Batch b = _batches[i];
                if (b.ExpireMs <= nowMs || b.Count <= 0)
                {
                    _live[(int)b.Category] -= b.Count;
                    continue;
                }
                _batches[w++] = b;
            }
            _batches.RemoveRange(w, _batches.Count - w);
        }

        public ParticleGrant Request(ParticleCategory category, int count, int lifetimeMs, long nowMs)
        {
            Advance(nowMs);
            ParticlePoolDef? pool = _scene.Pool(category);
            if (pool == null || count <= 0 || lifetimeMs <= 0)
            {
                return new ParticleGrant(0, Math.Max(0, count), 0);
            }
            int quota = pool.Quota(_preset);
            int want = Math.Min(count, quota);
            int c = (int)category;
            int grant;
            if (pool.Recurrence == ParticleRecurrence.Recurring)
            {
                grant = Math.Max(0, Math.Min(want, quota - _live[c]));
            }
            else
            {
                int over = _live[c] + want - quota;
                if (over > 0)
                {
                    EvictOldest(category, over);
                }
                grant = want;
            }
            if (grant > 0 && !pool.OutsideTotal)
            {
                int over = LiveTotal + grant - Total;
                if (over > 0)
                {
                    foreach (ParticlePoolDef other in _byPriorityDesc)
                    {
                        if (over <= 0 || other.Priority <= pool.Priority)
                        {
                            break;
                        }
                        if (other.OutsideTotal || other.Recurrence != ParticleRecurrence.OneShot)
                        {
                            continue;
                        }
                        over -= EvictOldest(other.Category, over);
                    }
                    if (over > 0 && pool.Recurrence == ParticleRecurrence.OneShot)
                    {
                        over -= EvictOldest(category, over);
                    }
                    if (over > 0)
                    {
                        grant = Math.Max(0, grant - over);
                    }
                }
            }
            if (grant <= 0)
            {
                return new ParticleGrant(0, count, 0);
            }
            long id = _nextBatchId++;
            _batches.Add(new Batch(id, category, grant, nowMs + lifetimeMs));
            _live[c] += grant;
            return new ParticleGrant(id, count, grant);
        }

        // 그 묶음을 일찍 끝낸다(효과가 먼저 사라졌을 때)
        public void Release(long batchId)
        {
            for (int i = 0; i < _batches.Count; i++)
            {
                if (_batches[i].Id == batchId)
                {
                    _live[(int)_batches[i].Category] -= _batches[i].Count;
                    _batches.RemoveAt(i);
                    return;
                }
            }
        }

        // 지난 호출 뒤에 생긴 "흐리게 지울 것"을 넘기고 비운다(발급 순)
        public void DrainEvictions(List<ParticleEviction> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            into.AddRange(_evictions);
            _evictions.Clear();
        }

        public int PendingEvictionCount => _evictions.Count;

        // 그 분류에서 가장 오래된 것부터 n 개까지 자리를 비운다. 비운 수를 돌려준다
        private int EvictOldest(ParticleCategory category, int n)
        {
            int freed = 0;
            for (int i = 0; i < _batches.Count && freed < n; i++)
            {
                Batch b = _batches[i];
                if (b.Category != category || b.Count <= 0)
                {
                    continue;
                }
                int take = Math.Min(b.Count, n - freed);
                _batches[i] = new Batch(b.Id, b.Category, b.Count - take, b.ExpireMs);
                _live[(int)category] -= take;
                freed += take;
                _evictions.Add(new ParticleEviction(b.Id, category, take, _table.Shares.OneShotFadeMs));
            }
            return freed;
        }

        private readonly struct PriorityDescending : IComparer<ParticlePoolDef>
        {
            public int Compare(ParticlePoolDef? x, ParticlePoolDef? y) => (y?.Priority ?? 0).CompareTo(x?.Priority ?? 0);
        }

        private readonly struct Batch
        {
            public readonly long Id;
            public readonly ParticleCategory Category;
            public readonly int Count;
            public readonly long ExpireMs;

            public Batch(long id, ParticleCategory category, int count, long expireMs)
            {
                Id = id;
                Category = category;
                Count = count;
                ExpireMs = expireMs;
            }
        }
    }
}
