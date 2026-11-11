using System;
using System.Collections.Generic;
using System.Linq;

namespace Wizard.TaskGraph
{
    // deps 그래프: 위상정렬(Kahn, tasks.json 순서로 큐), 전이 닫힘(조상 비트셋). check-tasks.mjs 와 같은 방법이다.
    public sealed class DepGraph
    {
        private readonly Dictionary<string, int> _index;
        private readonly Dictionary<string, ulong[]> _anc;

        private DepGraph(bool acyclic, IReadOnlyList<string> order, IReadOnlyList<string> inCycle, Dictionary<string, int> index, Dictionary<string, ulong[]> anc)
        {
            Acyclic = acyclic;
            Order = order;
            InCycle = inCycle;
            _index = index;
            _anc = anc;
        }

        public bool Acyclic { get; }
        public IReadOnlyList<string> Order { get; }
        public IReadOnlyList<string> InCycle { get; }

        public static DepGraph Build(IReadOnlyList<TaskDef> tasks, IReadOnlyDictionary<string, TaskDef> byId)
        {
            var indeg = new Dictionary<string, int>(StringComparer.Ordinal);
            var outs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (TaskDef t in tasks)
            {
                indeg[t.Id] = 0;
                outs[t.Id] = new List<string>();
            }
            foreach (TaskDef t in tasks)
            {
                foreach (string d in t.Deps)
                {
                    if (byId.ContainsKey(d))
                    {
                        indeg[t.Id] = indeg[t.Id] + 1;
                        outs[d].Add(t.Id);
                    }
                }
            }
            var order = new List<string>();
            var queue = new Queue<string>(tasks.Where(t => indeg[t.Id] == 0).Select(t => t.Id));
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                order.Add(id);
                foreach (string n in outs[id])
                {
                    indeg[n] = indeg[n] - 1;
                    if (indeg[n] == 0)
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            bool acyclic = order.Count == tasks.Count;
            var inCycle = tasks.Where(t => indeg[t.Id] > 0).Select(t => t.Id).ToList();

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < tasks.Count; i++)
            {
                index[tasks[i].Id] = i;
            }
            int words = (tasks.Count + 63) / 64;
            var anc = new Dictionary<string, ulong[]>(StringComparer.Ordinal);
            if (acyclic)
            {
                foreach (string id in order)
                {
                    var bits = new ulong[words];
                    foreach (string d in byId[id].Deps)
                    {
                        if (!anc.TryGetValue(d, out ulong[]? a))
                        {
                            continue;
                        }
                        for (int w = 0; w < words; w++)
                        {
                            bits[w] |= a[w];
                        }
                        int i = index[d];
                        bits[i >> 6] |= 1UL << (i & 63);
                    }
                    anc[id] = bits;
                }
            }
            return new DepGraph(acyclic, order, inCycle, index, anc);
        }

        // a 가 b 에 (전이로) 의존하는가
        public bool DependsOn(string a, string b)
        {
            if (!_anc.TryGetValue(a, out ulong[]? bits) || !_index.TryGetValue(b, out int i))
            {
                return false;
            }
            return (bits[i >> 6] & (1UL << (i & 63))) != 0;
        }

        public bool Ordered(string a, string b) => DependsOn(a, b) || DependsOn(b, a);
    }
}
