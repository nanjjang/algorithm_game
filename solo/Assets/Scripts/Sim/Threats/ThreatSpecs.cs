using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Threats
{
    /// <summary>공격 정의(AttackDef, 05 요약서 8.5절)를 예고 구역 명세로 옮기는 순수 함수. 데이터의 글자 값을 계약의 형으로 바꾼다.</summary>
    public static class ThreatSpecs
    {
        public const string LockLastNTicks = "last_n_ticks";
        public const string AnchorPlayerPosAtStart = "player_pos_at_start";

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

        /// <summary>
        /// 공격 정의 하나를 구역 명세로. 위치: anchor = player_pos_at_start면 겨눈 플레이어 자리, 아니면 주인 자리.
        /// 방향이 있는 모양(띠, 부채꼴, 조준선)은 플레이어 쪽을 겨눈다. length null = 벽까지(0).
        /// 예고 뒤에 탄이 나가는 공격(damage_source = projectile)의 구역은 피해 없이 그리기만 한다(피해는 탄이 준다).
        /// </summary>
        public static ZoneSpec FromAttack(AttackDef a, int targetSlot = 0)
        {
            var shape = ParseShape(a.Shape);
            var source = ParseDamageSource(a.DamageSource);
            bool shotFollows = source == PlayerDamageSource.Projectile;
            return new ZoneSpec
            {
                Shape = shape,
                Anchor = a.Anchor == AnchorPlayerPosAtStart ? ZoneAnchor.TargetPlayer : ZoneAnchor.Owner,
                Pos = Vector2.zero,
                Dir = new Vector2(1, 0),
                AimAtPlayer = shape == ZoneShape.Band || shape == ZoneShape.Fan || shape == ZoneShape.AimLine,
                Radius = a.Radius,
                InnerRadius = a.InnerRadius,
                Width = a.Width,
                Length = a.Length ?? 0f,
                FanDegrees = a.FanDegrees,
                TelegraphTicks = SimConst.Ticks(a.TelegraphSeconds),
                LockLastTicks = a.Lock == LockLastNTicks ? SimConst.Ticks(a.LockSeconds) : 0,
                BaseDamage = shotFollows ? 0 : a.Damage,
                DamageSource = shotFollows ? PlayerDamageSource.Telegraph : source,
                Grazeable = a.Grazeable && !shotFollows,
                Harmless = false,
                LeavesHazard = a.LeavesHazard,
                TargetSlot = targetSlot,
            };
        }
    }
}
