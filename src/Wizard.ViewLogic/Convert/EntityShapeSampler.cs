using System;
using System.Numerics;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Spell;

namespace Wizard.ViewLogic.Convert
{
    // ISimView 의 적과 투사체를 도형으로 옮긴다(CONTRACTS 3.3 EnemyView·ProjectileView, T-54). 세계를 모르고 뷰만 읽는다.
    // 목록은 한 번 읽어 지역 변수에 둔다(뷰는 읽을 때마다 다시 채운다). 순서는 뷰가 준 순서(적 → 투사체)이고 짝은 id 로 찾는다.
    public static class EntityShapeSampler
    {
        private const byte NoOwner = 255;

        public static void Fill(ISimView view, ShapeFrame frame)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }
            ReadOnlySpan<EnemyView> enemies = view.Enemies;
            for (int i = 0; i < enemies.Length; i++)
            {
                frame.AddEntity(FromEnemy(in enemies[i]));
            }
            ReadOnlySpan<ProjectileView> projectiles = view.Projectiles;
            for (int i = 0; i < projectiles.Length; i++)
            {
                frame.AddEntity(FromProjectile(in projectiles[i]));
            }
        }

        public static ShapeSample FromEnemy(in EnemyView e) =>
            new ShapeSample(e.Id.Value, KindOf(e.Type), FixConvert.ToWorld(e.Pos), FixConvert.ToFloat(e.BodyRadius), Vector2.Zero,
                            HpRatio(e.Hp, e.MaxHp), e.WindupStartTick, e.WindupTicks, NoOwner, (int)e.Type);

        public static ShapeSample FromProjectile(in ProjectileView p)
        {
            bool enemy = p.Faction == Faction.Enemy;
            // 방향도 위치와 같은 변환을 거친다(y 뒤집기). 단위 벡터는 그대로 단위 벡터다
            return new ShapeSample(p.Id.Value, enemy ? ShapeKind.EnemyShot : ShapeKind.PlayerBullet, FixConvert.ToWorld(p.Pos),
                                   FixConvert.ToFloat(p.Radius), FixConvert.ToWorld(p.Dir), 1f, 0U, 0,
                                   enemy ? NoOwner : p.Owner.Value, (int)p.Core);
        }

        // 종 → 회색 도형의 종류. 손맛 시제품의 두 종만 따로 그리고 나머지는 한 가지 도형이다(ShapeKind 의 머리말)
        public static ShapeKind KindOf(EnemyTypeId type)
        {
            switch (type)
            {
                case EnemyTypeId.SlimeMoss:
                    return ShapeKind.Slime;
                case EnemyTypeId.ShamanCap:
                    return ShapeKind.Turret;
                default:
                    return ShapeKind.Enemy;
            }
        }

        private static float HpRatio(Fix64 hp, Fix64 max)
        {
            float m = FixConvert.ToFloat(max);
            if (m <= 0f)
            {
                return 1f;
            }
            float r = FixConvert.ToFloat(hp) / m;
            return r < 0f ? 0f : (r > 1f ? 1f : r);
        }
    }
}
