using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 분류 2: "같은 적에게 0.05초 안에 3번 넘게 맞으면 큰 불꽃 하나(입자 8)로 합친다".
    // [임시 해석] 그 적이 맞은 시각들 중 (now − 0.05초, now] 안의 것이 3개를 넘는 첫 적중이 큰 불꽃 하나가 되고,
    // 큰 불꽃을 낸 뒤 그 창 안에 이어지는 적중은 거기에 합쳐진다(새 입자 없음). 앞의 3번은 이미 그려졌으므로 그대로 둔다.
    public sealed class HitSparkMerger
    {
        private readonly int _windowMs;
        private readonly int _overHits;
        private readonly Dictionary<long, Track> _byTarget = new Dictionary<long, Track>();
        private readonly List<long> _stale = new List<long>();

        public HitSparkMerger(ParticleBudgetTable table)
        {
            _windowMs = table.Hit.MergeWindowMs;
            _overHits = table.Hit.MergeOverHits;
        }

        public HitSparkKind OnHit(long targetKey, long nowMs)
        {
            if (!_byTarget.TryGetValue(targetKey, out Track t))
            {
                t = new Track(new Queue<long>());
                _byTarget[targetKey] = t;
            }
            while (t.Times.Count > 0 && nowMs - t.Times.Peek() >= _windowMs)
            {
                t.Times.Dequeue();
            }
            t.Times.Enqueue(nowMs);
            HitSparkKind kind;
            if (t.Times.Count <= _overHits)
            {
                kind = HitSparkKind.Normal;
                t.MergedUntilMs = long.MinValue;
            }
            else if (t.MergedUntilMs != long.MinValue && nowMs < t.MergedUntilMs)
            {
                kind = HitSparkKind.Absorbed;
                t.MergedUntilMs = nowMs + _windowMs;
            }
            else
            {
                kind = HitSparkKind.MergedBig;
                t.MergedUntilMs = nowMs + _windowMs;
            }
            _byTarget[targetKey] = t;
            return kind;
        }

        // 오래 맞지 않은 적의 기록을 지운다(프레임마다 한 번)
        public void Prune(long nowMs)
        {
            _stale.Clear();
            foreach (KeyValuePair<long, Track> kv in _byTarget)
            {
                Queue<long> q = kv.Value.Times;
                if (q.Count == 0 || nowMs - LastOf(q) >= _windowMs)
                {
                    _stale.Add(kv.Key);
                }
            }
            foreach (long k in _stale)
            {
                _byTarget.Remove(k);
            }
        }

        public int TrackedTargets => _byTarget.Count;

        private static long LastOf(Queue<long> q)
        {
            long last = long.MinValue;
            foreach (long v in q)
            {
                last = v;
            }
            return last;
        }

        private struct Track
        {
            public readonly Queue<long> Times;
            public long MergedUntilMs;

            public Track(Queue<long> times)
            {
                Times = times;
                MergedUntilMs = long.MinValue;
            }
        }
    }
}
