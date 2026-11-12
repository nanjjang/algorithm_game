using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Xunit;
using static Wizard.Sim.Tests.Player.PlayerRig;

namespace Wizard.Sim.Tests.Player
{
    // CONTRACTS 3.3 "뷰 훅"(T-54)의 Player 쪽: PlayerView 의 캐릭터·위치·시야·자세·체력·무적·키 칸이 Player 상태와 같다.
    // 기대값의 출처: 시작 위치 (12, 7)(data/sim/arena.json), 기준값 캐릭터의 체력 6칸·피격 후 무적 60틱(spec 01 6.1·6.2, 벡터 T-P01),
    // 자세·시야 상태 기계(spec 01 5.3~5.5). 읽기는 상태를 바꾸지 않는다.
    public sealed class PlayerViewTests
    {
        private static readonly PlayerSlot P0 = new PlayerSlot(0);

        [Fact]
        public void BeforeTheFirstStep_ThePlayerIsNotThereYet()
        {
            PlayerRig rig = Endless();
            ulong before = rig.World.ComputeStateHash();
            PlayerView v = rig.World.View.Player(P0);
            Assert.Equal(0, v.MaxHpPoints);                                   // 계약: 시작 상태가 서기 전에는 0
            Assert.False(rig.World.PlayersSpawned);                           // 뷰를 읽어도 시작 상태를 세우지 않는다
            Assert.Equal(before, rig.World.ComputeStateHash());
        }

        [Fact]
        public void AfterTheFirstStep_TheViewShowsTheStartState()
        {
            PlayerRig rig = Endless();
            rig.Step();
            PlayerView v = rig.World.View.Player(P0);
            Assert.Equal(CharacterId.Baseline, v.Character);
            Assert.Equal(new FixVec2(Fix64.FromInt(12), Fix64.FromInt(7)), v.Pos);
            Assert.Equal(StanceMode.Normal, v.Stance);
            Assert.Equal((600, 600), (v.HpPoints, v.MaxHpPoints));
            Assert.False(v.Invulnerable);
            Assert.Equal(KeyMask.None, v.Keys);
            Assert.True(v.Id.IsNone);                                         // 플레이어는 개체 id 를 받지 않는다(T-54)
        }

        [Fact]
        public void MovingAndFacing_FollowTheState()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Right));
            rig.RunTo(10);
            PlayerView v = rig.World.View.Player(P0);
            Assert.Equal(Dir8.E, v.Facing);
            Assert.Equal(rig.P.Pos, v.Pos);
            Assert.True(v.Pos.X > Fix64.FromInt(12));
            Assert.Equal(Fix64.FromInt(7), v.Pos.Y);
            Assert.Equal(KeyState.Bit(LogicalKey.Right), v.Keys);
        }

        [Fact]
        public void StanceAndReverse_FollowTheState()
        {
            PlayerRig rig = Endless();
            rig.Step(Press(LogicalKey.Up));
            rig.Step(Release(LogicalKey.Up));
            rig.Step(Press(LogicalKey.Stance));
            PlayerView v = rig.World.View.Player(P0);
            Assert.Equal(StanceMode.Stance, v.Stance);
            Assert.Equal(Dir8.N, v.Facing);                                   // 자세는 마지막 시야의 축에 잠긴다(spec 01 5.4)
            Assert.Equal(rig.P.Stance, v.Stance);
            rig.Step(Release(LogicalKey.Stance));
            Assert.Equal(StanceMode.Normal, rig.World.View.Player(P0).Stance);
        }

        [Fact]
        public void HitAndInvulnerability_FollowTheState()
        {
            PlayerRig rig = Endless();
            rig.RunTo(100);
            rig.World.QueuePlayerHit(P0, new EntityId(1), 1);
            rig.Step();                                                       // 틱 100 에 피격 확정, 무적은 160 전까지(T-P01)
            PlayerView v = rig.World.View.Player(P0);
            Assert.Equal((500, 600), (v.HpPoints, v.MaxHpPoints));
            Assert.True(v.Invulnerable);
            rig.RunTo(159);
            Assert.True(rig.World.View.Player(P0).Invulnerable);
            rig.RunTo(160);
            Assert.False(rig.World.View.Player(P0).Invulnerable);             // t < InvulnUntil 이 아니다
        }

        [Fact]
        public void Duel_EachSlotHasItsOwnView()
        {
            PlayerRig rig = PlayerRig.Duel();
            rig.StepSlots((Press(LogicalKey.Left), (byte)1));
            rig.StepSlots();
            ISimView view = rig.World.View;
            PlayerView a = view.Player(P0), b = view.Player(new PlayerSlot(1));
            Assert.Equal(rig.World.PlayerAt(0).Pos, a.Pos);
            Assert.Equal(rig.World.PlayerAt(1).Pos, b.Pos);
            Assert.NotEqual(a.Pos, b.Pos);
            Assert.Equal(Dir8.W, b.Facing);
            Assert.Equal(KeyState.Bit(LogicalKey.Left), b.Keys);
            Assert.Equal(KeyMask.None, a.Keys);
        }

        [Fact]
        public void ReadingTheViewEveryTick_DoesNotChangeTheHashes()
        {
            PlayerRig read = Endless(), plain = Endless();
            for (uint t = 0; t < 200; t++)
            {
                Key[] keys = t == 3 ? new[] { Press(LogicalKey.Right) }
                    : t == 50 ? new[] { Release(LogicalKey.Right), Press(LogicalKey.Stance) }
                    : t == 90 ? new[] { Press(LogicalKey.Up, 30) }
                    : new Key[0];
                read.World.View.Player(P0);
                if (t == 120)
                {
                    read.World.QueuePlayerHit(P0, new EntityId(9), 1);
                    plain.World.QueuePlayerHit(P0, new EntityId(9), 1);
                }
                read.Step(keys);
                plain.Step(keys);
                read.World.View.Player(P0);
                Assert.Equal(plain.World.ComputeStateHash(), read.World.ComputeStateHash());
            }
        }
    }
}
