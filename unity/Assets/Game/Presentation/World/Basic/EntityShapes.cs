#nullable enable
using UnityEngine;
using Wizard.Content;
using Wizard.Unity.Presentation.Core;
using Wizard.ViewLogic.Convert;

namespace Wizard.Unity.Presentation.World.Basic
{
    // 플레이어 밖 개체의 회색 도형: 슬라임(원판), 포탑(사각)과 시전 예고 링, 그 밖의 적(원판), 플레이어 탄(흰 원판 + 검정 테),
    // 적 탄(위험색 원판). 위험색은 적의 공격(적 탄)과 예고(공격 준비의 링)에만 쓴다. 크기는 세계가 준 반지름이다.
    //
    // 예고의 채움은 세계가 준 시작 틱과 길이로만 센다(ShapeSample.CastFill, CONTRACTS 3.3). 표시 쪽에 기억을 두지 않는다.
    // 그리는 시각은 보간과 같이 한 틱 늦은 (틱 − 1 + alpha)다.
    public static class EntityShapes
    {
        private const float TopDeg = 90f;
        private const float TurnDeg = 360f;

        public static void Draw(ShapeCanvas canvas, in ShapeSample s, uint tick, double alpha)
        {
            Vector2 c = UnityConvert.ToUnity(s.Pos);
            switch (s.Kind)
            {
                case ShapeKind.Slime:
                    canvas.Disk(c, s.RadiusCells, ShapePalette.Slime, ShapeStyle.OrderEnemy);
                    DrawHp(canvas, c, s);
                    DrawCast(canvas, c, s, s.RadiusCells, tick, alpha);
                    break;
                case ShapeKind.Turret:
                    canvas.Box(c, new Vector2(s.RadiusCells * 2f, s.RadiusCells * 2f), 0f, ShapePalette.Turret, ShapeStyle.OrderEnemy);
                    DrawHp(canvas, c, s);
                    DrawCast(canvas, c, s, s.RadiusCells * Mathf.Sqrt(2f), tick, alpha);   // 사각의 꼭짓점 바깥
                    break;
                case ShapeKind.Enemy:
                    canvas.Disk(c, s.RadiusCells, ShapePalette.Enemy, ShapeStyle.OrderEnemy);
                    DrawHp(canvas, c, s);
                    DrawCast(canvas, c, s, s.RadiusCells, tick, alpha);
                    break;
                case ShapeKind.PlayerBullet:
                    canvas.Disk(c, s.RadiusCells + ShapeStyle.Cells(ShapeStyle.BulletOutlinePx), ShapePalette.Outline, ShapeStyle.OrderPlayerBullet);
                    canvas.Disk(c, s.RadiusCells, s.Variant == (int)BlockId.MagicBolt ? ShapePalette.PlayerBullet : ShapePalette.PlayerBulletAlt,
                                ShapeStyle.OrderPlayerBullet + 1);
                    break;
                case ShapeKind.EnemyShot:
                    canvas.Disk(c, s.RadiusCells, ShapePalette.EnemyShot, ShapeStyle.OrderEnemyShot);
                    break;
            }
        }

        // 공격 준비(시전)의 예고 링: 몸 바깥에서 맨 위부터 시계 방향으로 찬다
        private static void DrawCast(ShapeCanvas canvas, Vector2 c, in ShapeSample s, float bodyEdge, uint tick, double alpha)
        {
            if (!s.Casting)
            {
                return;
            }
            float fill = s.CastFill(tick, alpha);
            float r = bodyEdge + ShapeStyle.Cells(ShapeStyle.TelegraphRingGapPx);
            canvas.Arc(c, r, ShapeStyle.Cells(ShapeStyle.TelegraphRingPx), TopDeg, -TurnDeg * fill, ShapePalette.EnemyTelegraph,
                       ShapeStyle.OrderTelegraph, ShapeStyle.RingSegments);
        }

        private static void DrawHp(ShapeCanvas canvas, Vector2 c, in ShapeSample s)
        {
            if (s.HpRatio >= 1f)
            {
                return;
            }
            float w = ShapeStyle.Cells(ShapeStyle.EnemyHpBarWidthPx);
            float h = ShapeStyle.Cells(ShapeStyle.EnemyHpBarHeightPx);
            var at = new Vector2(c.x, c.y + s.RadiusCells + ShapeStyle.Cells(ShapeStyle.EnemyHpBarGapPx));
            canvas.Box(at, new Vector2(w, h), 0f, ShapePalette.EnemyHpBack, ShapeStyle.OrderEnemyHp);
            float fw = w * Mathf.Clamp01(s.HpRatio);
            canvas.Box(new Vector2(at.x - (w - fw) / 2f, at.y), new Vector2(fw, h), 0f, ShapePalette.EnemyHpFill, ShapeStyle.OrderEnemyHp + 1);
        }
    }
}
