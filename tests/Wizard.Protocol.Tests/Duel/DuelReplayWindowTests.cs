using Wizard.Protocol.Duel;
using Xunit;

namespace Wizard.Protocol.Tests.Duel
{
    // CONTRACTS 10.1 재전송 차단: 가장 큰 번호와 64칸 비트맵. 창보다 오래됐거나 이미 받은 번호는 버린다.
    public sealed class DuelReplayWindowTests
    {
        [Fact]
        public void DuplicateIsRejected()
        {
            var w = new DuelReplayWindow();
            Assert.True(w.Mark(1));
            Assert.False(w.IsFresh(1));
            Assert.False(w.Mark(1));
            Assert.True(w.Mark(2));
            Assert.False(w.Mark(2));
            Assert.False(w.Mark(1));
        }

        [Fact]
        public void ZeroIsNeverFresh()
        {
            var w = new DuelReplayWindow();
            Assert.False(w.IsFresh(0));
            Assert.False(w.Mark(0));
        }

        [Fact]
        public void OutOfOrderInsideWindowIsAcceptedOnce()
        {
            var w = new DuelReplayWindow();
            Assert.True(w.Mark(10));
            Assert.True(w.Mark(5));
            Assert.True(w.Mark(7));
            Assert.False(w.Mark(5));
            Assert.True(w.Mark(6));
            Assert.Equal(10UL, w.Highest);
        }

        [Fact]
        public void WindowEdgeIs64()
        {
            var w = new DuelReplayWindow();
            Assert.True(w.Mark(100));
            Assert.True(w.IsFresh(37));     // 100 − 37 = 63: 창 안
            Assert.False(w.IsFresh(36));    // 64: 창 밖
            Assert.True(w.Mark(37));
            Assert.False(w.Mark(37));
        }

        [Fact]
        public void BigJumpClearsHistory()
        {
            var w = new DuelReplayWindow();
            Assert.True(w.Mark(1));
            Assert.True(w.Mark(3));
            Assert.True(w.Mark(1000));
            Assert.False(w.IsFresh(3));     // 창 밖으로 밀려남
            Assert.True(w.IsFresh(999));
            Assert.True(w.Mark(1064));      // 64 칸 이동: 1000 은 창 밖
            Assert.False(w.IsFresh(1000));
            Assert.True(w.IsFresh(1063));
            Assert.True(w.Mark(1063));
        }

        // 위조 패킷은 IsFresh 만 보고 Mark 하지 않으므로 창을 움직이지 못한다(태그 검증 뒤에만 Mark)
        [Fact]
        public void IsFreshDoesNotChangeState()
        {
            var w = new DuelReplayWindow();
            Assert.True(w.Mark(5));
            Assert.True(w.IsFresh(ulong.MaxValue));
            Assert.Equal(5UL, w.Highest);
            Assert.True(w.IsFresh(4));
            Assert.True(w.Mark(ulong.MaxValue));
            Assert.False(w.Mark(ulong.MaxValue));
            w.Reset();
            Assert.Equal(0UL, w.Highest);
            Assert.True(w.Mark(1));
        }
    }
}
