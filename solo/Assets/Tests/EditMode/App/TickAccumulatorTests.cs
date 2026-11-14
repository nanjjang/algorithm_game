using NUnit.Framework;
using Wizard.Solo.App;

namespace Wizard.Solo.Tests.App
{
    /// <summary>누적기 방식 60Hz(00 문서 4절): 프레임 시간 → 스텝 수, 보간 값, 0.25초 자르기, 일시정지 중 0스텝.</summary>
    public class TickAccumulatorTests
    {
        [Test]
        public void OneFrameAt60Hz_GivesOneStep()
        {
            var a = new TickAccumulator();
            for (int i = 0; i < 600; i++) Assert.AreEqual(1, a.Advance(1f / 60f), "frame " + i);
        }

        [Test]
        public void At120Hz_StepsEveryOtherFrame_AlphaHalf()
        {
            var a = new TickAccumulator();
            Assert.AreEqual(0, a.Advance(1f / 120f));
            Assert.AreEqual(0.5f, a.Alpha, 1e-4f);
            Assert.AreEqual(1, a.Advance(1f / 120f));
            Assert.AreEqual(0f, a.Alpha, 1e-4f);
        }

        [Test]
        public void At30Hz_TwoStepsPerFrame()
        {
            var a = new TickAccumulator();
            int total = 0;
            for (int i = 0; i < 30; i++) total += a.Advance(1f / 30f);
            Assert.AreEqual(60, total);
        }

        [Test]
        public void LongFrame_ClampedToQuarterSecond()
        {
            var a = new TickAccumulator();
            Assert.AreEqual(15, a.Advance(5f));
            Assert.AreEqual(15, a.Advance(0.25f));
        }

        [Test]
        public void Paused_NoStepsAndNoAccumulation()
        {
            var a = new TickAccumulator();
            a.Advance(1f / 120f);
            Assert.AreEqual(0, a.Advance(1f, paused: true));
            Assert.AreEqual(0.5f, a.Alpha, 1e-4f);
        }

        [Test]
        public void NegativeOrNaN_TreatedAsZero()
        {
            var a = new TickAccumulator();
            Assert.AreEqual(0, a.Advance(-1f));
            Assert.AreEqual(0, a.Advance(float.NaN));
            Assert.AreEqual(0f, a.Alpha);
        }

        [Test]
        public void OffsetU_InsideTick()
        {
            var a = new TickAccumulator();
            a.Advance(1f / 120f);
            // 반 틱 = 128u(1틱 = 256u)
            Assert.That(a.OffsetU, Is.InRange(127, 128));
            a.Reset();
            Assert.AreEqual(0, a.OffsetU);
            Assert.AreEqual(0f, a.Alpha);
        }
    }
}
