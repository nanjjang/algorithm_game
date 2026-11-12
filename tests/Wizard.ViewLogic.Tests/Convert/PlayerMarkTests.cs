using System;
using System.Numerics;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 열 링(effects 10, spec 01 17.1)과 시야 화살표(GDD 3)가 PlayerView 의 값으로만 정해지는지
    public sealed class PlayerMarkTests
    {
        internal static PlayerView View(FixVec2 pos = default, Dir8 facing = Dir8.E, StanceMode stance = StanceMode.Normal,
                                        int heatHu = 0, int critStartHu = 10200, HeatRingState ring = HeatRingState.Cold,
                                        bool exempt = false, int lockRemain = 0, int lockTotal = 0, int hp = 300, int maxHp = 300,
                                        CharacterId character = CharacterId.Baseline) =>
            new PlayerView(new EntityId(1), character, pos, facing, stance, hp, maxHp, false, heatHu, critStartHu, ring, exempt,
                           lockRemain, lockTotal, 0, 0, false, 0, 0, 0, 0, false, default, RecentInputs.Empty, KeyMask.None);

        [Fact]
        public void Cold_HidesTheRing()
        {
            HeatRingSample s = HeatRingSample.From(View());
            Assert.False(s.RingVisible);
            Assert.False(s.Locked);
            Assert.Equal(0f, s.FillRatio);
        }

        [Fact]
        public void Rising_FillsByHeatOverOneHundred_AndMarksTheCritStart()
        {
            // 열 40 = 4,800 HU, 임계 시작 85 = 10,200 HU(기준값 캐릭터, spec 01 7절)
            HeatRingSample s = HeatRingSample.From(View(heatHu: 4800, ring: HeatRingState.Rising));
            Assert.True(s.RingVisible);
            Assert.Equal(0.4f, s.FillRatio);
            Assert.Equal(0.85f, s.CritMarkRatio);
            Assert.False(s.CriticalMark);
        }

        [Theory]
        [InlineData(HeatRingState.Critical, true)]
        [InlineData(HeatRingState.Brink, true)]
        [InlineData(HeatRingState.Imminent, false)]
        public void CriticalMark_OnlyInCriticalAndBrink(HeatRingState ring, bool mark)
        {
            Assert.Equal(mark, HeatRingSample.From(View(heatHu: 10800, ring: ring)).CriticalMark);
        }

        [Fact]
        public void OverOneHundred_StopsAtFull()
        {
            Assert.Equal(1f, HeatRingSample.From(View(heatHu: 12600, ring: HeatRingState.Brink)).HeatRatio);
        }

        [Fact]
        public void Locked_ShowsRemainingLockTime()
        {
            // 잠금 180틱 중 45틱 남음(spec 01 7절 잠금 3초)
            HeatRingSample s = HeatRingSample.From(View(heatHu: 12000, ring: HeatRingState.Locked, lockRemain: 45, lockTotal: 180));
            Assert.True(s.Locked);
            Assert.True(s.RingVisible);
            Assert.Equal(0.25f, s.LockRemainRatio);
            Assert.Equal(0.25f, s.FillRatio);
        }

        [Fact]
        public void LockRemainWithoutLockedState_IsIgnored()
        {
            Assert.Equal(0f, HeatRingSample.From(View(ring: HeatRingState.Rising, heatHu: 1200, lockRemain: 45, lockTotal: 180)).LockRemainRatio);
            Assert.Equal(0f, HeatRingSample.From(View(ring: HeatRingState.Locked, lockRemain: 45, lockTotal: 0)).LockRemainRatio);
        }

        [Fact]
        public void ExemptArmed_IsCarried()
        {
            Assert.True(HeatRingSample.From(View(ring: HeatRingState.Critical, heatHu: 10200, exempt: true)).ExemptArmed);
        }

        [Theory]
        [InlineData(Dir8.E, 0f)]
        [InlineData(Dir8.NE, 45f)]
        [InlineData(Dir8.N, 90f)]
        [InlineData(Dir8.W, 180f)]
        [InlineData(Dir8.S, 270f)]
        [InlineData(Dir8.SE, 315f)]
        public void FacingArrow_AngleIsCounterClockwiseFromEast(Dir8 facing, float degrees)
        {
            Assert.Equal(degrees, FacingArrowSample.From(View(facing: facing)).AngleDegrees);
        }

        [Theory]
        [InlineData(StanceMode.Normal, false, false)]
        [InlineData(StanceMode.Stance, true, false)]
        [InlineData(StanceMode.Reverse, true, true)]
        public void FacingArrow_TranslucentNormallyOpaqueInStance_OtherColorReversed(StanceMode mode, bool opaque, bool reversed)
        {
            FacingArrowSample a = FacingArrowSample.From(View(stance: mode));
            Assert.Equal(opaque, a.Opaque);
            Assert.Equal(reversed, a.Reversed);
        }

        [Fact]
        public void FacingVector_PointsUpForNorth()
        {
            Vector2 n = PlayerShapeSampler.FacingVector(Dir8.N);
            Assert.Equal(0f, n.X, 6);
            Assert.Equal(1f, n.Y, 6);
            Vector2 w = PlayerShapeSampler.FacingVector(Dir8.W);
            Assert.Equal(-1f, w.X, 6);
            Assert.Equal(0f, w.Y, 6);
        }

        [Fact]
        public void PlayerSampler_ReadsEveryPlayerInSlotOrderFromTheView()
        {
            var view = new FakeView(
                View(pos: new FixVec2(Fix64.FromInt(12), Fix64.FromInt(7)), facing: Dir8.N, hp: 150),
                View(pos: new FixVec2(Fix64.FromRatio(1, 2), Fix64.FromInt(3)), facing: Dir8.W, character: CharacterId.Char2));
            var sampler = new PlayerShapeSampler(id => id == CharacterId.Char2 ? 0.5f : 0.25f);
            var frame = new ShapeFrame(4, 0);
            frame.Reset(view.Tick);
            sampler.Fill(view, frame);
            Assert.Equal(2, frame.PlayerCount);
            ShapeSample a = frame.Player(0);
            Assert.Equal(new Vector2(12f, -7f), a.Pos);
            Assert.Equal(0.25f, a.RadiusCells);
            Assert.Equal(0.5f, a.HpRatio);
            Assert.Equal(ShapeKind.Player, a.Kind);
            ShapeSample b = frame.Player(1);
            Assert.Equal(new Vector2(0.5f, -3f), b.Pos);
            Assert.Equal(0.5f, b.RadiusCells);
            Assert.Equal(1, b.Id);
        }

        [Fact]
        public void BodyRadiusFrom_ReadsTheCharacterDefinition()
        {
            Func<CharacterId, float> r = PlayerShapeSampler.BodyRadiusFrom(ConvertRig.Db);
            Assert.Equal(FixConvert.ToFloat(ConvertRig.Db.Character(CharacterId.Baseline).BodyRadius), r(CharacterId.Baseline));
            Assert.True(r(CharacterId.Baseline) > 0f);
        }

        internal sealed class FakeView : ISimView
        {
            private readonly PlayerView[] _players;

            public FakeView(params PlayerView[] players)
            {
                _players = players;
            }

            public uint Tick { get; set; } = 1;

            public int PlayerCount => _players.Length;

            public PlayerView Player(PlayerSlot slot) => _players[slot.Value];

            public ReadOnlySpan<EnemyView> Enemies => ReadOnlySpan<EnemyView>.Empty;

            public ReadOnlySpan<ProjectileView> Projectiles => ReadOnlySpan<ProjectileView>.Empty;

            public ReadOnlySpan<TelegraphView> Telegraphs => ReadOnlySpan<TelegraphView>.Empty;

            public ReadOnlySpan<HazardView> Hazards => ReadOnlySpan<HazardView>.Empty;

            public WaveView Wave => default;

            public CampView Camp => default;

            public DuelView Duel => default;
        }
    }
}
