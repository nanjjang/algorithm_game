using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Tests.Player;
using Xunit;
using static Wizard.Sim.Tests.Graze.GrazeKit;

namespace Wizard.Sim.Tests.Graze
{
    // CONTRACTS 3.3 "뷰 훅"(T-54)의 Graze 쪽: PlayerView.GrazeBuffActive(spec 01 17.1). 스침 가속은 인정된 틱부터 90틱(spec 01 8.4).
    public sealed class GrazeViewTests
    {
        [Fact]
        public void GrazeBuffActive_FollowsTheBuffWindow()
        {
            PlayerRig rig = ReadyAt(5, PlayerRig.Endless());
            Assert.False(rig.World.View.Player(P0).GrazeBuffActive);
            uint t = rig.Tick;
            Offer(rig, BulletAt(rig, 30, 6, 700));
            rig.Step();                                                       // 틱 t 에 스침 인정 → t + 90 전까지 가속
            Assert.True(rig.World.View.Player(P0).GrazeBuffActive);
            rig.RunTo(t + 89U);
            Assert.True(rig.World.View.Player(P0).GrazeBuffActive);
            rig.RunTo(t + 90U);
            Assert.False(rig.World.View.Player(P0).GrazeBuffActive);
        }

        [Fact]
        public void ReadingTheViewEveryTick_DoesNotChangeTheHashes()
        {
            PlayerRig read = ReadyAt(5, PlayerRig.Endless()), plain = ReadyAt(5, PlayerRig.Endless());
            for (int i = 0; i < 120; i++)
            {
                read.World.View.Player(P0);
                if (i == 3)
                {
                    Offer(read, BulletAt(read, 30, 6, 700));
                    Offer(plain, BulletAt(plain, 30, 6, 700));
                }
                read.Step();
                plain.Step();
                read.World.View.Player(P0);
                Assert.Equal(plain.World.ComputeStateHash(), read.World.ComputeStateHash());
            }
        }
    }
}
