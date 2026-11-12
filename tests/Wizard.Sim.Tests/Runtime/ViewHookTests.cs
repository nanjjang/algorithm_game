using System;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Runtime
{
    // CONTRACTS 3.3 "뷰 훅"(T-54)의 뼈대 쪽: 모듈이 하나도 붙지 않은 세계(빈 팩)의 뷰는 기본값과 빈 범위이고, 읽어도 세계가 바뀌지 않는다.
    // 모듈이 채우는 값은 그 모듈의 시험이 본다(Player·Heat·Graze·Proto 의 …ViewTests).
    public sealed class ViewHookTests
    {
        private static readonly PlayerSlot P0 = new PlayerSlot(0);

        [Fact]
        public void EmptyPackWorld_GivesDefaultsAndEmptyLists()
        {
            SimWorld world = WorldKit.NewEndless(WorldKit.EmptyDb());
            ISimView view = world.View;
            PlayerView p = view.Player(P0);
            Assert.Equal(0, p.MaxHpPoints);                     // 시작 상태가 서지 않은 플레이어(계약: MaxHpPoints == 0)
            Assert.True(p.Id.IsNone);
            Assert.Equal(HeatRingState.Cold, p.HeatRing);
            Assert.False(p.GrazeBuffActive);
            Assert.Equal(0, view.Enemies.Length);
            Assert.Equal(0, view.Projectiles.Length);
            Assert.Equal(0, view.Telegraphs.Length);
            Assert.Equal(0, view.Hazards.Length);
            Assert.Equal(CampSub.None, view.Camp.Sub);
        }

        [Fact]
        public void View_FollowsTheWorldTickAndPlayerCount()
        {
            SimWorld duel = WorldKit.NewDuel(WorldKit.EmptyDb());
            ISimView view = duel.View;
            Assert.Same(view, duel.View);
            Assert.Equal(2, view.PlayerCount);
            duel.Step(ReadOnlySpan<PlayerInput>.Empty, ReadOnlySpan<RunCommand>.Empty, new RecordingSink());
            Assert.Equal(1U, view.Tick);
            Assert.Throws<ArgumentOutOfRangeException>(() => view.Player(new PlayerSlot(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => view.Player(PlayerSlot.None));
        }

        [Fact]
        public void ReadingEveryView_DoesNotChangeTheState()
        {
            SimWorld read = WorldKit.NewDuel(WorldKit.EmptyDb());
            SimWorld plain = WorldKit.NewDuel(WorldKit.EmptyDb());
            var sink = new RecordingSink();
            var inputs = new PlayerInput[8];
            uint seqA = 0, seqB = 0;
            for (int t = 0; t < 40; t++)
            {
                ReadAll(read.View);
                int n = WorldKit.FillInputs(inputs, read.Tick, read.PlayerCount, ref seqA);
                read.Step(inputs.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
                n = WorldKit.FillInputs(inputs, plain.Tick, plain.PlayerCount, ref seqB);
                plain.Step(inputs.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
                ReadAll(read.View);
                Assert.Equal(plain.ComputeStateHash(), read.ComputeStateHash());
            }
        }

        [Fact]
        public void ViewBuffer_GrowsAndKeepsOrder()
        {
            var b = new ViewBuffer<TelegraphView>();
            Assert.Equal(0, b.AsSpan().Length);
            for (int i = 0; i < 200; i++)
            {
                b.Add(new TelegraphView((uint)i, i + 1));
            }
            ReadOnlySpan<TelegraphView> all = b.AsSpan();
            Assert.Equal(200, all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Assert.Equal(((uint)i, i + 1), (all[i].StartTick, all[i].LengthTicks));
            }
            int capacity = b.Capacity;
            b.Clear();
            Assert.Equal(0, b.Count);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                b.Add(new TelegraphView(1U, 1));
            }
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());     // 한 번 자란 버퍼는 다시 채울 때 할당하지 않는다
            Assert.Equal(capacity, b.Capacity);
        }

        internal static void ReadAll(ISimView view)
        {
            for (int i = 0; i < view.PlayerCount; i++)
            {
                view.Player(new PlayerSlot((byte)i));
            }
            _ = view.Enemies.Length + view.Projectiles.Length + view.Telegraphs.Length + view.Hazards.Length;
            _ = view.Wave;
            _ = view.Camp;
            _ = view.Duel;
        }
    }
}
