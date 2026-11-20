using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>보스 공통 규칙의 순수 함수(05 요약서 2·3.2·8절). 상태를 갖지 않는다.</summary>
    public static class BossRules
    {
        // ------------------------------------------------------------ 순환·체력 (8.1, 3.2절)

        /// <summary>그 웨이브의 보스 id. k = w / 10, 첫 만남이면 k == 1에서 허상. 보스 웨이브가 아니면 null.</summary>
        public static string SelectBoss(IContentDb db, int wave, bool phantomFirst)
        {
            if (db == null || !DayCycle.IsBossWave(wave)) return null;
            var bosses = db.Bosses;
            if (bosses == null || bosses.Count == 0) return null;
            if (wave / DayCycle.DayLength == 1 && phantomFirst && db.Boss(Ids.Boss.MirrorPhantom) != null)
                return Ids.Boss.MirrorPhantom;
            int index = DayCycle.BossRotationIndex(wave, bosses.Count);
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i].RotationIndex == index) return bosses[i].Id;
            return bosses[index].Id;
        }

        /// <summary>체력 배율 H(w). w ≤ 12: 1 + 0.12 × (w − 1) / 그 뒤: 2.32 × 1.06^(w − 12). waves.json 값으로 계산한다.</summary>
        public static double HpMul(WavesDef waves, int wave)
        {
            if (waves == null) return 1.0;
            if (wave < 1) wave = 1;
            if (wave <= waves.HpLinearUntil) return 1.0 + (double)waves.HpLinearStep * (wave - 1);
            return (double)waves.HpExpBase * Math.Pow(waves.HpExpRate, wave - waves.HpLinearUntil);
        }

        /// <summary>보스 최대 체력 = base × H(w) / H(anchor). 첫 만남은 배율 1(체력 바닥은 EnemyState.HpFloor).</summary>
        public static float MaxHp(IContentDb db, BossDef def, int wave, bool firstEncounter)
        {
            if (def == null) return 0f;
            if (firstEncounter || db == null) return def.BaseHp;
            var waves = db.Waves;
            int anchor = def.HpScaleAnchorWave > 0 ? def.HpScaleAnchorWave : (waves != null ? waves.BossHpRefWave : wave);
            double h = HpMul(waves, wave), ha = HpMul(waves, anchor);
            if (ha <= 0.0) return def.BaseHp;
            return (float)(def.BaseHp * h / ha);
        }

        /// <summary>복수 추적기의 흠집 % = floor((hpMax − hpMin) × 100 / hpMax). 05 요약서 10.4절 T280.</summary>
        public static int DentPercent(float hpMax, float hpMin)
        {
            if (hpMax <= 0f) return 0;
            if (hpMin < 0f) hpMin = 0f;
            if (hpMin > hpMax) hpMin = hpMax;
            return (int)Math.Floor((double)(hpMax - hpMin) * 100.0 / hpMax);
        }

        // ------------------------------------------------------------ 격노 (8.2·8.3절)

        /// <summary>격노 가산식의 분모(rage_step_ratio 0.1 → 10). 실수 곱을 피해 정수로 내린다.</summary>
        public static int RageDenominator(BossCommonDef common)
        {
            if (common == null || common.RageStepRatio <= 0f) return 0;
            return (int)Math.Round(1.0 / common.RageStepRatio, MidpointRounding.AwayFromZero);
        }

        /// <summary>틱 수에 격노를 반영한다: base × (den − n) / den 내림.</summary>
        public static int ScaleByRage(BossCommonDef common, int baseTicks, int rageStacks)
        {
            int den = RageDenominator(common);
            if (den <= 0 || rageStacks <= 0) return baseTicks;
            int n = Math.Min(rageStacks, den);
            return baseTicks * (den - n) / den;
        }

        /// <summary>패턴 사이 대기 틱: 90 × (10 − rage) / 10 → 90, 81, 72, 63, 54, 45.</summary>
        public static int WaitTicks(BossCommonDef common, int rageStacks)
        {
            if (common == null) return 0;
            return ScaleByRage(common, SimConst.Ticks(common.WaitSeconds), rageStacks);
        }

        /// <summary>연사형 간격: base × (10 − n) / 10 내림, 하한 min_interval_ticks(R3).</summary>
        public static int RapidIntervalTicks(BossCommonDef common, int baseTicks, int rageStacks, int minIntervalTicks)
        {
            return Math.Max(minIntervalTicks, ScaleByRage(common, baseTicks, rageStacks));
        }

        /// <summary>격노 스택 = fightTicks / 1200(최대 5).</summary>
        public static int RageStacks(BossCommonDef common, int fightTicks)
        {
            if (common == null) return 0;
            int period = SimConst.Ticks(common.RageStackSeconds);
            if (period <= 0 || fightTicks <= 0) return 0;
            return Math.Min(common.RageMaxStacks, fightTicks / period);
        }

        /// <summary>최종 격노(fightTicks ≥ 10800).</summary>
        public static bool FinalRage(BossCommonDef common, int fightTicks)
        {
            if (common == null || common.FinalRageSeconds <= 0f) return false;
            return fightTicks >= SimConst.Ticks(common.FinalRageSeconds);
        }

        // ------------------------------------------------------------ 단계 (8.3절)

        /// <summary>
        /// 지금 체력에서 있어야 할 단계 색인(Def.Phases의 첨자). hp × 100 ≤ hpMax × enter_at_hp_percent 인 가장 뒤 단계.
        /// 되돌아가지 않는다(current보다 작아지지 않는다). 한 틱에 두 경계를 넘으면 마지막 단계가 한 번에 나온다.
        /// </summary>
        public static int PhaseFor(BossDef def, float hp, float hpMax, int currentPhase)
        {
            if (def == null || def.Phases == null) return currentPhase;
            int best = currentPhase;
            for (int i = currentPhase + 1; i < def.Phases.Count; i++)
            {
                if ((double)hp * 100.0 <= (double)hpMax * def.Phases[i].EnterAtHpPercent) best = i;
            }
            return best;
        }

        // ------------------------------------------------------------ 패턴 고르기 (8.3절)

        /// <summary>
        /// 고를 수 있는 패턴: 지금 단계의 patterns에서 직전 패턴을 빼고 Eligible로 거른다. 비면 직전 패턴을 다시 넣어 본다.
        /// 그래도 비면 빈 목록(부른 쪽이 no_candidate_wait_seconds만큼 더 기다린다).
        /// </summary>
        public static void Candidates(IReadOnlyList<string> phasePatterns, string lastPattern, Func<string, bool> eligible, List<string> into)
        {
            into.Clear();
            if (phasePatterns == null) return;
            for (int i = 0; i < phasePatterns.Count; i++)
            {
                string key = phasePatterns[i];
                if (key == lastPattern) continue;
                if (eligible != null && !eligible(key)) continue;
                into.Add(key);
            }
            if (into.Count > 0 || lastPattern == null) return;
            for (int i = 0; i < phasePatterns.Count; i++)
            {
                string key = phasePatterns[i];
                if (key != lastPattern) continue;
                if (eligible != null && !eligible(key)) continue;
                into.Add(key);
                break;
            }
        }

        // ------------------------------------------------------------ R1 (2절, 9.7절)

        /// <summary>기준 속도 V = 캐릭터 자세 속도의 최솟값(3.76).</summary>
        public static float BaselineSpeed(IContentDb db)
        {
            float v = float.MaxValue;
            var chars = db != null ? db.Characters : null;
            if (chars != null)
                for (int i = 0; i < chars.Count; i++)
                    if (chars[i].StanceSpeed > 0f && chars[i].StanceSpeed < v) v = chars[i].StanceSpeed;
            if (v == float.MaxValue && db != null && db.BaselineCharacter != null) v = db.BaselineCharacter.StanceSpeed;
            return v == float.MaxValue ? 0f : v;
        }

        /// <summary>플레이어 피격 반지름(기준 캐릭터 값 0.15).</summary>
        public static float BaselineHitRadius(IContentDb db)
        {
            return db != null && db.BaselineCharacter != null ? db.BaselineCharacter.HitRadius : 0f;
        }

        /// <summary>
        /// 벗어나는 거리(05 요약서 2절): Circle r + hit / Band·AimLine w/2 + hit / Fan R·sinα/(1 + sinα) + hit /
        /// Ring 띠 폭의 절반 + hit. escape_override_milli가 있으면 그 값.
        /// </summary>
        public static float EscapeDistance(AttackDef attack, float hitRadius)
        {
            if (attack == null) return 0f;
            if (attack.EscapeOverrideMilli > 0) return attack.EscapeOverrideMilli / 1000f;
            switch (ParseShape(attack.Shape))
            {
                case ZoneShape.Band:
                case ZoneShape.AimLine:
                    return attack.Width / 2f + hitRadius;
                case ZoneShape.Fan:
                {
                    float s = Mathf.Sin(attack.FanDegrees / 2f * Mathf.Deg2Rad);
                    return attack.Radius * s / (1f + s) + hitRadius;
                }
                case ZoneShape.Ring:
                    return (attack.Radius - attack.InnerRadius) / 2f + hitRadius;
                default:
                    return attack.Radius + hitRadius;
            }
        }

        /// <summary>R1의 여유(초) = 예고 − 반응 시간 − 벗어나는 거리 / V. R1 통과 = 여유 ≥ r1_margin_seconds(0.05).</summary>
        public static float R1SlackSeconds(float telegraphSeconds, float escapeDistance, float speed, float reactionSeconds)
        {
            if (speed <= 0f) return float.NegativeInfinity;
            return telegraphSeconds - reactionSeconds - escapeDistance / speed;
        }

        /// <summary>보스의 모든 예고 공격을 R1로 검사해 어긴 것의 "패턴.공격" 열쇠를 모은다(데이터 검사용).</summary>
        public static void CheckR1(IContentDb db, BossDef def, BossTuning tuning, List<string> violations)
        {
            if (def == null || def.Patterns == null) return;
            float v = BaselineSpeed(db), hit = BaselineHitRadius(db);
            for (int i = 0; i < def.Patterns.Count; i++)
            {
                var pattern = def.Patterns[i];
                for (int s = 0; s < pattern.Steps.Count; s++)
                {
                    var attack = pattern.Steps[s].Attack;
                    if (attack == null || string.IsNullOrEmpty(attack.Shape) || attack.TelegraphSeconds <= 0f) continue;
                    // 조준선의 벗어나는 시간은 방향이 고정된 구간(lock_seconds)으로 잰다.
                    float seconds = attack.Lock == LockLastTicks && attack.LockSeconds > 0f ? attack.LockSeconds : attack.TelegraphSeconds;
                    float slack = R1SlackSeconds(seconds, EscapeDistance(attack, hit), v, tuning.R1ReactionSeconds);
                    if (slack < tuning.R1MarginSeconds) violations.Add(pattern.Key + "." + attack.Key);
                }
            }
        }

        // ------------------------------------------------------------ 데이터 글자 → 열거형

        public const string LockLastTicks = "last_n_ticks";
        public const string AnchorSelf = "self";
        public const string AnchorPlayerPos = "player_pos_at_start";
        public const string KindDash = "dash", KindZone = "zone", KindLob = "lob", KindRingShot = "ring_shot",
            KindSpread = "spread", KindAimedShot = "aimed_shot", KindContact = "contact";

        public static ZoneShape ParseShape(string shape)
        {
            switch (shape)
            {
                case "band": return ZoneShape.Band;
                case "fan": return ZoneShape.Fan;
                case "aim_line": return ZoneShape.AimLine;
                case "ring": return ZoneShape.Ring;
                default: return ZoneShape.Circle;
            }
        }

        public static PlayerDamageSource ParseDamageSource(string source)
        {
            switch (source)
            {
                case "contact": return PlayerDamageSource.Contact;
                case "projectile": return PlayerDamageSource.Projectile;
                case "lingering": return PlayerDamageSource.Lingering;
                default: return PlayerDamageSource.Telegraph;
            }
        }

        public static HazardKind ParseHazardKind(string kind)
        {
            switch (kind)
            {
                case "ember_strip": return HazardKind.EmberStrip;
                case "spore_cloud": return HazardKind.SporeCloud;
                case "frost_field": return HazardKind.FrostField;
                default: return HazardKind.ThornField;
            }
        }

        // ------------------------------------------------------------ 기하

        /// <summary>시뮬레이션 좌표에서 벡터를 도(°)만큼 돌린다.</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>점에서 선분까지의 거리.</summary>
        public static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 0f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>두 선분 사이의 가장 가까운 거리(교차하면 0).</summary>
        public static float SegmentSegmentDistance(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            if (SegmentsCross(a1, a2, b1, b2)) return 0f;
            float d = PointSegmentDistance(a1, b1, b2);
            d = Mathf.Min(d, PointSegmentDistance(a2, b1, b2));
            d = Mathf.Min(d, PointSegmentDistance(b1, a1, a2));
            return Mathf.Min(d, PointSegmentDistance(b2, a1, a2));
        }

        static bool SegmentsCross(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float d1 = Cross(b2 - b1, a1 - b1), d2 = Cross(b2 - b1, a2 - b1);
            float d3 = Cross(a2 - a1, b1 - a1), d4 = Cross(a2 - a1, b2 - a1);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>몸 반지름만큼 벽 안쪽인 전장에서 pos에서 dir로 갈 수 있는 거리(벽까지).</summary>
        public static float DistanceToWall(SimWorld w, Vector2 pos, Vector2 dir, float radius)
        {
            float best = float.MaxValue;
            if (dir.x > 0f) best = Mathf.Min(best, (w.ArenaWidth - radius - pos.x) / dir.x);
            else if (dir.x < 0f) best = Mathf.Min(best, (radius - pos.x) / dir.x);
            if (dir.y > 0f) best = Mathf.Min(best, (w.ArenaHeight - radius - pos.y) / dir.y);
            else if (dir.y < 0f) best = Mathf.Min(best, (radius - pos.y) / dir.y);
            if (best == float.MaxValue) return 0f;
            return Mathf.Max(0f, best);
        }
    }
}
