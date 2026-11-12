using System.Numerics;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 보간 규칙(ShapeInterpolator 의 머리 주석): 그리는 시각 = 지금 틱 − 1 + alpha, 짝은 id, 새 것·순간 이동은 지금 자리
    public sealed class ShapeInterpolatorTests
    {
        private static ShapeSample S(int id, ShapeKind kind, float x, float y) =>
            new ShapeSample(id, kind, new Vector2(x, y), 0.25f, Vector2.Zero, 1f, 0U, 0, 255, 0);

        private static void Frame(ShapeInterpolator interp, uint tick, params ShapeSample[] entities)
        {
            ShapeFrame f = interp.Begin(tick);
            f.AddPlayer(S(0, ShapeKind.Player, entities.Length > 0 ? entities[0].Pos.X : 0f, 0f));
            foreach (ShapeSample s in entities)
            {
                f.AddEntity(s);
            }
            interp.End();
        }

        [Fact]
        public void OneTickApart_LerpsByAlpha()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.Slime, 0f, 0f));
            Frame(interp, 11, S(7, ShapeKind.Slime, 0.5f, -0.25f));
            Assert.Equal(1, interp.TickSpan);
            Assert.Equal(new Vector2(0f, 0f), interp.Entity(0, 0.0).Pos);
            Assert.Equal(new Vector2(0.25f, -0.125f), interp.Entity(0, 0.5).Pos);
            Assert.Equal(new Vector2(0.5f, -0.25f), interp.Entity(0, 1.0).Pos);
            Assert.Equal(new Vector2(0.25f, 0f), interp.Player(0, 0.5).Pos);
        }

        [Fact]
        public void SeveralTicksInOneFrame_DrawsOneTickBehindAlongThePath()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.PlayerBullet, 0f, 0f));
            Frame(interp, 14, S(7, ShapeKind.PlayerBullet, 2f, 0f));      // 4틱에 2칸(틱당 0.5칸)
            Assert.Equal(4, interp.TickSpan);
            Assert.Equal(0.75f, interp.Progress(0.0));                      // (4 − 1 + 0) ÷ 4
            Assert.Equal(1.5f, interp.Entity(0, 0.0).Pos.X);               // 13틱의 자리
            Assert.Equal(1.75f, interp.Entity(0, 0.5).Pos.X);
            Assert.Equal(2f, interp.Entity(0, 1.0).Pos.X);
        }

        [Fact]
        public void NewEntity_IsDrawnWhereItIsNow()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.Slime, 0f, 0f));
            Frame(interp, 11, S(7, ShapeKind.Slime, 0.1f, 0f), S(9, ShapeKind.EnemyShot, 3f, 3f));
            Assert.Equal(new Vector2(3f, 3f), interp.Entity(1, 0.0).Pos);
        }

        [Fact]
        public void PairsById_NotByIndex()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(5, ShapeKind.Slime, 0f, 0f), S(6, ShapeKind.Slime, 10f, 0f));
            Frame(interp, 11, S(6, ShapeKind.Slime, 10.5f, 0f));             // 5가 사라져 6이 0번 자리로 왔다
            Assert.Equal(10.25f, interp.Entity(0, 0.5).Pos.X);
        }

        [Fact]
        public void JumpLongerThanTheSnapLimit_IsNotInterpolated()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.Slime, 0f, 0f));
            Frame(interp, 11, S(7, ShapeKind.Slime, 2f, 0f));                // 넉백 2칸(ShapeInterpolator.SnapCellsPerTick 초과)
            Assert.Equal(2f, interp.Entity(0, 0.0).Pos.X);
        }

        [Fact]
        public void KindChange_IsNotInterpolated()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.Slime, 0f, 0f));
            Frame(interp, 11, S(7, ShapeKind.EnemyShot, 0.5f, 0f));
            Assert.Equal(0.5f, interp.Entity(0, 0.0).Pos.X);
        }

        [Fact]
        public void TickGoingBackwards_DropsThePreviousFrame()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.Slime, 0f, 0f));
            Frame(interp, 3, S(7, ShapeKind.Slime, 0.5f, 0f));               // 새 세계
            Assert.Equal(0, interp.TickSpan);
            Assert.Equal(1f, interp.Progress(0.0));
            Assert.Equal(0.5f, interp.Entity(0, 0.0).Pos.X);
        }

        [Fact]
        public void FirstFrame_HasNothingToInterpolateFrom()
        {
            var interp = new ShapeInterpolator(4, 8);
            Assert.False(interp.Has(10));
            Frame(interp, 10, S(7, ShapeKind.Slime, 1f, 2f));
            Assert.True(interp.Has(10));
            Assert.Equal(new Vector2(1f, 2f), interp.Entity(0, 0.0).Pos);
        }

        [Fact]
        public void AlphaOutsideZeroToOne_IsClamped()
        {
            var interp = new ShapeInterpolator(4, 8);
            Frame(interp, 10, S(7, ShapeKind.Slime, 0f, 0f));
            Frame(interp, 11, S(7, ShapeKind.Slime, 0.5f, 0f));
            Assert.Equal(0f, interp.Entity(0, -1.0).Pos.X);
            Assert.Equal(0.5f, interp.Entity(0, 2.0).Pos.X);
        }

        [Fact]
        public void FullFrame_CountsDroppedEntities()
        {
            var f = new ShapeFrame(1, 2);
            f.Reset(1);
            Assert.True(f.AddEntity(S(1, ShapeKind.Slime, 0f, 0f)));
            Assert.True(f.AddEntity(S(2, ShapeKind.Slime, 0f, 0f)));
            Assert.False(f.AddEntity(S(3, ShapeKind.Slime, 0f, 0f)));
            Assert.Equal(2, f.EntityCount);
            Assert.Equal(1, f.DroppedCount);
        }
    }
}
