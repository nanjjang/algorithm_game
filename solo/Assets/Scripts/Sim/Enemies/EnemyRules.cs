using System;
using UnityEngine;

namespace Wizard.Solo.Sim.Enemies
{
    /// <summary>군중 제어 판정 결과. 05 요약서 5절 표, 02 요약서 8.5절 ResolveCc.</summary>
    public enum CcVerdict
    {
        /// <summary>아무 일도 없다(면역).</summary>
        None,
        /// <summary>amount만큼 그대로 건다.</summary>
        Apply,
        /// <summary>빙결 대신 감속으로 바꿔 건다(중량·보스. amount = 틱).</summary>
        ConvertToSlow,
        /// <summary>보스 동작 멈춤(뇌광창).</summary>
        BossHalt
    }

    /// <summary>적 쪽 규칙의 순수 함수(상태를 건드리지 않는다). 05 요약서 3~7절, 06 요약서 1.4절.</summary>
    public static class EnemyRules
    {
        /// <summary>실수 올림·내림에서 2진 오차를 지우는 여유.</summary>
        const float Eps = 1e-4f;

        public static Vector2 Norm(Vector2 v)
        {
            float m = v.magnitude;
            return m > Eps ? v / m : Vector2.zero;
        }

        /// <summary>화면 각도(도, 반시계 +)만큼 시뮬레이션 벡터를 돌린다.</summary>
        public static Vector2 Rotate(Vector2 simDir, float degrees)
        {
            if (degrees == 0f) return simDir;
            return Dir8Util.DegreesToSimVector(Dir8Util.SimVectorToDegrees(simDir) + degrees);
        }

        /// <summary>진행 방향의 왼쪽 수직(화면 기준 반시계 90도).</summary>
        public static Vector2 LeftOf(Vector2 simDir) => new Vector2(simDir.y, -simDir.x);

        /// <summary>북두꺼비 버프 속도: s ≥ cap이면 그대로, 아니면 min(s × ratio, cap). 05 요약서 6.12절.</summary>
        public static float BuffedSpeed(float speed, float ratio, float cap) => speed >= cap ? speed : Mathf.Min(speed * ratio, cap);

        /// <summary>북두꺼비 버프 빈틈: ceil(t × ratio). 72 → 51, 90 → 63, 120 → 84.</summary>
        public static int BuffedRecovery(int ticks, float ratio) => Mathf.CeilToInt(ticks * ratio - Eps);

        /// <summary>갓버섯 술사 엄폐 자리 = 엄폐물 + normalize(엄폐물 − 플레이어) × offset. 05 요약서 6.4절.</summary>
        public static Vector2 CoverPoint(Vector2 cover, Vector2 player, float offset) => cover + Norm(cover - player) * offset;

        /// <summary>덤불 늑대 회피 조건식(05 요약서 6.2절). 맞을 탄이면 true와 피할 방향(탄 진행의 수직 중 탄에서 멀어지는 쪽, 0이면 왼쪽).</summary>
        public static bool WolfShouldDodge(Vector2 wolfPos, Vector2 wolfVel, float bodyRadius, Vector2 shotPos, Vector2 shotVel,
            float shotRadius, float windowSeconds, out Vector2 dodgeDir)
        {
            dodgeDir = Vector2.zero;
            Vector2 r = wolfPos - shotPos;
            Vector2 v = shotVel - wolfVel;
            float vv = Vector2.Dot(v, v);
            if (vv <= Eps) return false;
            float t = Mathf.Max(0f, Vector2.Dot(r, v) / vv);
            // r은 탄 → 늑대, v는 탄의 상대 속도라 가장 가까워지는 때는 +dot(r, v)/dot(v, v)다(원문 식의 r = pos − b.pos, 부호는 탄 기준으로 푼 것).
            if (t > windowSeconds) return false;
            float dMin = (r - v * t).magnitude;
            if (!(dMin < bodyRadius + shotRadius)) return false;
            Vector2 d = Norm(shotVel);
            if (d == Vector2.zero) return false;
            Vector2 left = LeftOf(d);
            float side = Vector2.Dot(r, left);
            dodgeDir = side < 0f ? -left : left;
            return true;
        }

        /// <summary>느린 탄막의 탄 수 = floor(n × 배율). 배율이 1 이하면 그대로. 06 요약서 1.4절.</summary>
        public static int BarrageCount(int count, float mul) => mul > 1f ? Mathf.FloorToInt(count * mul + Eps) : count;

        /// <summary>같은 방향으로 이어 쏘는 탄의 발사 간격(틱) = ceil(minGap ÷ (속도 × 배율 ÷ 60)). 00 문서 D-64.</summary>
        public static int BarrageGapTicks(float minGap, float speed, float speedMul)
        {
            float perTick = speed * (speedMul > 0f ? speedMul : 1f) * SimConst.TickSeconds;
            if (perTick <= Eps) return 1;
            return Mathf.Max(1, Mathf.CeilToInt(minGap / perTick - Eps));
        }

        /// <summary>엘리트 절반(틱은 가장 가까운 정수).</summary>
        public static float Half(float amount) => amount / 2f;
        public static int RoundTicks(float ticks) => (int)Math.Round(ticks, MidpointRounding.AwayFromZero);

        /// <summary>
        /// 꼬리표별 군중 제어(05 요약서 5절 표). 땅속은 부르기 전에 걸러낸다(전부 면역).
        /// 해석 순서: 보스 → 중량 → 뿌리 → (엘리트면 절반). 엘리트 + 중량은 중량 줄.
        /// </summary>
        public static CcVerdict ResolveCc(CcKind kind, EnemyTags tags, CcSource source, float baseAmount, out float amount)
        {
            bool boss = (tags & EnemyTags.Boss) != 0;
            bool heavy = (tags & EnemyTags.Heavy) != 0;
            bool rooted = (tags & EnemyTags.Rooted) != 0;
            bool elite = (tags & EnemyTags.Elite) != 0;
            amount = 0f;
            switch (kind)
            {
                case CcKind.Knockback:
                case CcKind.Pull:
                    if (boss || heavy || rooted) return CcVerdict.None;
                    amount = elite ? Half(baseAmount) : baseAmount;
                    return CcVerdict.Apply;
                case CcKind.Freeze:
                    if (boss || heavy) { amount = baseAmount; return CcVerdict.ConvertToSlow; }
                    amount = elite ? Half(baseAmount) : baseAmount;
                    return CcVerdict.Apply;
                case CcKind.Stagger:
                    if (boss || heavy) return CcVerdict.None;
                    amount = elite ? Half(baseAmount) : baseAmount;
                    return CcVerdict.Apply;
                case CcKind.Stun:
                    if (boss) return source == CcSource.ThunderSpear ? CcVerdict.BossHalt : CcVerdict.None;
                    if (heavy) return CcVerdict.None;
                    amount = elite ? Half(baseAmount) : baseAmount;
                    return CcVerdict.Apply;
                default:
                    // 감속: 뿌리 적에게 불가마 감속은 해당 없음. 보스 절반은 ApplySlow가 한다.
                    if (rooted && source == CcSource.Kiln) return CcVerdict.None;
                    amount = baseAmount;
                    return CcVerdict.Apply;
            }
        }

        /// <summary>빙결·기절이 예고를 취소하는 적인가: 엘리트·중량·보스가 아닌 꼬리표 없는 일반 적과 뿌리 적(05 요약서 1.3절).</summary>
        public static bool CcCancelsTelegraph(EnemyTags tags) =>
            (tags & (EnemyTags.Elite | EnemyTags.Heavy | EnemyTags.Boss | EnemyTags.Flying)) == 0;

        /// <summary>정면 철갑이 드는가: 맞은 쪽(fromPos)이 시선에서 반각 안.</summary>
        public static bool InFrontArc(Vector2 pos, Vector2 facing, Vector2 fromPos, float halfAngleDegrees)
        {
            Vector2 to = Norm(fromPos - pos);
            Vector2 f = Norm(facing);
            if (to == Vector2.zero || f == Vector2.zero) return true;
            float cos = Mathf.Clamp(Vector2.Dot(to, f), -1f, 1f);
            return Mathf.Acos(cos) * Mathf.Rad2Deg <= halfAngleDegrees + Eps;
        }

        /// <summary>점 p에서 선분 a→b까지의 거리.</summary>
        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float ll = Vector2.Dot(ab, ab);
            if (ll <= Eps * Eps) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ll);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>판정 겹침: 거리 &lt; 반지름 합(같으면 안 맞음). 05 요약서 0절 U-10.</summary>
        public static bool Overlap(Vector2 a, float ra, Vector2 b, float rb) => (a - b).magnitude < ra + rb;
    }
}
