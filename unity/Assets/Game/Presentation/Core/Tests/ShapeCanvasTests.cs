#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace Wizard.Unity.Presentation.Core.Tests
{
    public sealed class ShapeCanvasTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Arc_UsesSegmentsInProportionToTheSweep()
        {
            using (var c = new ShapeCanvas("test", null))
            {
                c.Begin();
                Assert.That(c.Arc(Vector2.zero, 1f, 0.1f, 90f, -360f, Color.white, 0, 48), Is.EqualTo(48));
                Assert.That(c.Arc(Vector2.zero, 1f, 0.1f, 90f, -90f, Color.white, 0, 48), Is.EqualTo(12));
                Assert.That(c.Arc(Vector2.zero, 1f, 0.1f, 90f, -1f, Color.white, 0, 48), Is.EqualTo(1));
                Assert.That(c.Arc(Vector2.zero, 1f, 0.1f, 90f, 0f, Color.white, 0, 48), Is.EqualTo(0));
                c.End();
                Assert.That(c.UsedCount, Is.EqualTo(61));
            }
        }

        [Test]
        public void Bar_SpansBothPoints()
        {
            using (var c = new ShapeCanvas("test", null))
            {
                c.Begin();
                SpriteRenderer r = c.Bar(new Vector2(1f, 1f), new Vector2(1f, 3f), 0.25f, Color.white, 7);
                c.End();
                Assert.That(r.transform.localPosition.x, Is.EqualTo(1f).Within(Eps));
                Assert.That(r.transform.localPosition.y, Is.EqualTo(2f).Within(Eps));
                Assert.That(r.transform.localScale.x, Is.EqualTo(2f).Within(Eps));
                Assert.That(r.transform.localScale.y, Is.EqualTo(0.25f).Within(Eps));
                Assert.That(r.transform.localEulerAngles.z, Is.EqualTo(90f).Within(Eps));
                Assert.That(r.sortingOrder, Is.EqualTo(7));
            }
        }

        [Test]
        public void Pieces_AreReusedAcrossFrames()
        {
            using (var c = new ShapeCanvas("test", null))
            {
                c.Begin();
                SpriteRenderer a = c.Disk(Vector2.zero, 1f, Color.white, 0);
                c.Disk(Vector2.one, 1f, Color.white, 0);
                c.End();
                c.Begin();
                SpriteRenderer b = c.Disk(Vector2.zero, 0.5f, Color.gray, 0);
                c.End();
                Assert.That(b, Is.SameAs(a));
                Assert.That(c.PieceCount, Is.EqualTo(2));
                Assert.That(c.UsedCount, Is.EqualTo(1));
                Assert.That(b.transform.localScale.x, Is.EqualTo(1f).Within(Eps));
            }
        }

        [Test]
        public void Sprites_AreOneUnitWide()
        {
            using (var c = new ShapeCanvas("test", null))
            {
                Assert.That(c.Sprites.Disk.bounds.size.x, Is.EqualTo(1f).Within(Eps));
                Assert.That(c.Sprites.Square.bounds.size.x, Is.EqualTo(1f).Within(Eps));
                Assert.That(c.Sprites.Triangle.bounds.size.y, Is.EqualTo(1f).Within(Eps));
                // 원판: 가운데는 불투명, 모서리는 투명
                Texture2D t = c.Sprites.Disk.texture;
                Assert.That(t.GetPixel(t.width / 2, t.height / 2).a, Is.EqualTo(1f).Within(Eps));
                Assert.That(t.GetPixel(0, 0).a, Is.EqualTo(0f).Within(Eps));
            }
        }
    }
}
