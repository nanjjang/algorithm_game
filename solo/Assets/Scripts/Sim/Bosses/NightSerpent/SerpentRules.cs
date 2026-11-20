using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    public enum SerpentPart { Head, Segment, Tail }

    /// <summary>밤삼킴의 몸 정의(bosses.json의 body. 05 요약서 9.6절).</summary>
    public sealed class SerpentBodyDef
    {
        public float HeadRadius, SegmentRadius, TailRadius;
        public int SegmentCount;
        public float SegmentSpacing;
        public float SegmentDamageRatio = 1f, TailDamageRatio = 1f;
        public int MaxSegmentHits;

        /// <summary>머리 뒤에 붙는 부위 수 = 마디 + 꼬리 하나(BossState.Segments의 길이. 마지막이 꼬리).</summary>
        public int PartCount => SegmentCount + 1;

        public float RadiusOf(int partIndex) => partIndex >= SegmentCount ? TailRadius : SegmentRadius;

        public static SerpentBodyDef From(BossDef def)
        {
            var b = new SerpentBodyDef();
            JObject j = def != null ? def.Body : null;
            b.HeadRadius = BossTuning.Float(j, "head_radius", def != null ? def.BodyRadius : 0f);
            b.SegmentRadius = BossTuning.Float(j, "segment_radius", 0f);
            b.TailRadius = BossTuning.Float(j, "tail_radius", b.SegmentRadius);
            b.SegmentCount = BossTuning.Int(j, "segment_count", 0);
            b.SegmentSpacing = BossTuning.Float(j, "segment_spacing", 0f);
            b.SegmentDamageRatio = BossTuning.Float(j, "segment_damage_ratio", 1f);
            b.TailDamageRatio = BossTuning.Float(j, "tail_damage_ratio", b.SegmentDamageRatio);
            b.MaxSegmentHits = BossTuning.Int(j, "max_segment_hits", 0);
            return b;
        }
    }

    /// <summary>밤삼킴의 순수 규칙: 경로 따라가기, 맞은 부위, 부위별 피해 배율, 고리 기하.</summary>
    public static class SerpentRules
    {
        /// <summary>
        /// 경로 따라가기: head에서 시작해 trail(가장 최근 자리부터)을 잇는 꺾은선 위에서, 머리로부터 spacing × (k + 1)만큼
        /// 떨어진 점을 k번째 부위의 자리로 한다. 꺾은선이 모자라면 남은 부위는 꺾은선의 끝에 모인다.
        /// </summary>
        public static void Follow(Vector2 head, IReadOnlyList<Vector2> trail, float spacing, int parts, List<Vector2> into)
        {
            into.Clear();
            Vector2 a = head;
            int next = 0;
            float want = spacing;
            float walked = 0f;
            while (into.Count < parts)
            {
                if (next >= trail.Count)
                {
                    into.Add(a);
                    continue;
                }
                Vector2 b = trail[next];
                float len = (b - a).magnitude;
                if (len > 0f && walked + len >= want)
                {
                    a += (b - a) / len * (want - walked);
                    walked = want;
                    into.Add(a);
                    want += spacing;
                    if (spacing <= 0f) next++;
                    continue;
                }
                walked += len;
                a = b;
                next++;
            }
        }

        /// <summary>
        /// 맞은 자리(hitPos)가 어느 부위인가. 머리 원(+ slack) 안이면 머리, 아니면 마디·꼬리 원(+ slack) 안에서 가장 가까운 것,
        /// 어느 원에도 들지 않으면 머리(몸통 원으로 맞은 것으로 본다).
        /// </summary>
        public static SerpentPart PartAt(SerpentBodyDef body, Vector2 head, IReadOnlyList<Vector2> parts, Vector2 hitPos, float slack)
        {
            if ((hitPos - head).magnitude <= body.HeadRadius + slack) return SerpentPart.Head;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < parts.Count; i++)
            {
                float d = (hitPos - parts[i]).magnitude;
                if (d > body.RadiusOf(i) + slack || d >= bestDist) continue;
                best = i;
                bestDist = d;
            }
            if (best < 0) return SerpentPart.Head;
            return best >= body.SegmentCount ? SerpentPart.Tail : SerpentPart.Segment;
        }

        /// <summary>
        /// OnHitSerpent(05 요약서 9.6절): 머리 ×1.0(상한과 무관), 마디·꼬리 ×0.5 — 탄 한 발이 마디를 max_segment_hits(3)번 맞혔으면 0.
        /// segHits = 그 탄이 지금까지 맞힌 마디 수(맞으면 1 늘린다).
        /// </summary>
        public static float HitMul(SerpentBodyDef body, SerpentPart part, ref int segHits)
        {
            if (part == SerpentPart.Head) return 1f;
            if (body.MaxSegmentHits > 0 && segHits >= body.MaxSegmentHits) return 0f;
            segHits++;
            return part == SerpentPart.Tail ? body.TailDamageRatio : body.SegmentDamageRatio;
        }

        /// <summary>닫힌 고리의 접촉 띠(안 반지름~바깥 반지름)에 플레이어 피격 원이 걸치나.</summary>
        public static bool TouchesRing(Vector2 center, float inner, float outer, Vector2 pos, float hitRadius)
        {
            float d = (pos - center).magnitude;
            return d > inner - hitRadius && d < outer + hitRadius;
        }

        /// <summary>원(center, radius) 위나 안의 점 from에서 dir로 나아가 원을 벗어나는 거리. 벗어나는 점이 앞에 없으면 0.</summary>
        public static float ExitDistance(Vector2 from, Vector2 dir, Vector2 center, float radius)
        {
            Vector2 m = from - center;
            float b = Vector2.Dot(m, dir);
            float c = m.sqrMagnitude - radius * radius;
            float disc = b * b - c;
            if (disc < 0f) return 0f;
            float t = -b + Mathf.Sqrt(disc);
            return t > 0f ? t : 0f;
        }

        /// <summary>바깥쪽 법선: tangent에 수직인 두 방향 가운데 away(바깥을 가리키는 벡터)와 같은 쪽.</summary>
        public static Vector2 OutwardNormal(Vector2 tangent, Vector2 away)
        {
            if (tangent.sqrMagnitude <= 0f) return away.sqrMagnitude > 0f ? away.normalized : new Vector2(0f, 1f);
            Vector2 n = new Vector2(-tangent.y, tangent.x).normalized;
            return Vector2.Dot(n, away) < 0f ? -n : n;
        }
    }
}
