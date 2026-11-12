using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 진짜 연습 세계(손맛 시제품이 도는 세계)를 돌리며 표시 변환을 본다. 저장소 data/ 로 만든 팩을 쓴다.
    // 기대값은 세계의 읽기 전용 값(ISimView, 표시 이벤트)과 FixConvert·명세의 단위 정의에서 온다. 표시 코드의 출력을 베끼지 않는다.
    public sealed class WorldConvertTests
    {
        [Fact]
        public void Players_AreSampledFromTheWorldViewInSlotOrder()
        {
            ConvertRig rig = ConvertRig.Practice();
            rig.Step();
            ISimView view = rig.World.View;
            var frame = new ShapeFrame(4, 0);
            frame.Reset(view.Tick);
            new PlayerShapeSampler(PlayerShapeSampler.BodyRadiusFrom(ConvertRig.Db)).Fill(view, frame);
            Assert.Equal(view.PlayerCount, frame.PlayerCount);
            Assert.Equal(1, frame.PlayerCount);
            PlayerView p = view.Player(ConvertRig.P0);
            ShapeSample s = frame.Player(0);
            Assert.Equal(FixConvert.ToWorld(p.Pos), s.Pos);
            Assert.Equal(FixConvert.ToFloat(ConvertRig.Db.Character(p.Character).BodyRadius), s.RadiusCells);
            Assert.Equal(PlayerShapeSampler.FacingVector(p.Facing), s.Dir);
            Assert.Equal(0, s.Id);                                             // 도형의 Id = 슬롯
            Assert.Equal(new System.Numerics.Vector2(12f, -7f), s.Pos);        // 시작 위치 (12, 7)(data/sim/arena.json)
        }

        // CONTRACTS 3.3(T-54): 시작 상태가 서기 전(첫 Step 전)의 PlayerView 는 MaxHpPoints 가 0 이고 표시 계층은 그리지 않는다
        [Fact]
        public void BeforeTheFirstStep_NoPlayerIsSampled()
        {
            ConvertRig rig = ConvertRig.Practice();
            ISimView view = rig.World.View;
            Assert.False(PlayerShapeSampler.IsPresent(view.Player(ConvertRig.P0)));
            var frame = new ShapeFrame(4, 0);
            frame.Reset(view.Tick);
            new PlayerShapeSampler(PlayerShapeSampler.BodyRadiusFrom(ConvertRig.Db)).Fill(view, frame);
            Assert.Equal(0, frame.PlayerCount);
            rig.Step();
            Assert.True(PlayerShapeSampler.IsPresent(view.Player(ConvertRig.P0)));
        }

        // 열 링·시야 화살표·잠금이 진짜 세계의 ISimView 값으로 나온다(수락 조건 2). J 를 누르고 있으면 늦어도 26발째(틱 225)에 잠긴다
        // (spec 01 7.8). 잠금에 들어간 틱의 다음 뷰에서 남은 잠금은 전체(간격 9 + 180틱)에서 1틱 줄어 있다
        [Fact]
        public void HeatRingAndFacing_ComeFromTheRealWorldView()
        {
            ConvertRig rig = ConvertRig.Practice();
            rig.Step(ConvertRig.Press(LogicalKey.Fire));
            PlayerView p = rig.World.View.Player(ConvertRig.P0);
            HeatRingSample ring = HeatRingSample.From(p);
            Assert.Equal(HeatRingState.Rising, ring.State);
            Assert.Equal(4f / 100f, ring.HeatRatio, 5);                        // 마력탄 한 발의 열 4
            Assert.Equal(85f / 100f, ring.CritMarkRatio, 5);                   // 기준값의 임계 시작 85
            while (rig.World.View.Player(ConvertRig.P0).HeatRing != HeatRingState.Locked)
            {
                Assert.True(rig.Tick <= 225);                                  // 늦어도 26발째(포탑 탄을 스치면 더 이르다)
                rig.Step();
            }
            p = rig.World.View.Player(ConvertRig.P0);
            ring = HeatRingSample.From(p);
            Assert.True(ring.Locked);
            Assert.Equal(188f / 189f, ring.LockRemainRatio, 5);                // 잠금 189틱(간격 9 + 180) 중 188 남음
            FacingArrowSample arrow = FacingArrowSample.From(p);
            Assert.Equal(FacingArrowSample.DegreesOf(p.Facing), arrow.AngleDegrees);
            rig.Step(ConvertRig.Press(LogicalKey.Stance));
            Assert.True(FacingArrowSample.From(rig.World.View.Player(ConvertRig.P0)).Opaque);
        }

        [Fact]
        public void GapReport_FromTheRealWorld_ReadsInMilliseconds()
        {
            // spec 03 21절 끝: 자세에서 마지막 방향 입력부터 K 까지. 방향 1×256, K 2×256+128 → 384 단위 = 25ms(spec 01 2.1)
            ConvertRig rig = ConvertRig.Practice();
            var readout = new GapReadout();
            rig.Step(ConvertRig.Press(LogicalKey.Stance));
            readout.Observe(rig.Events.ToArray());
            rig.Step(ConvertRig.Press(LogicalKey.Down, 0));
            readout.Observe(rig.Events.ToArray());
            Assert.False(readout.TryGet(0, out _));
            rig.Step(ConvertRig.Release(LogicalKey.Down), ConvertRig.Press(LogicalKey.Skill, 128));
            readout.Observe(rig.Events.ToArray());
            Assert.True(readout.TryGet(0, out GapReport g));
            Assert.True(g.HasGap);
            Assert.Equal(384L, g.GapUnits);
            Assert.Equal(25.0, g.Milliseconds);
            Assert.Equal(25L, g.RoundedMilliseconds);
            Assert.True(g.Decided);
            SimEvent cast = rig.Events.Single(e => e.Type == SimEventType.ArtCast);
            Assert.Equal((ArtId)cast.A, g.Art);
            Assert.Equal(cast.C != 0, g.Just);                         // 같은 틱의 ArtCast 가 섬 여부를 붙인다
        }

        [Fact]
        public void GapReport_OutsideStance_HasNoGap()
        {
            ConvertRig rig = ConvertRig.Practice();
            var readout = new GapReadout();
            rig.Step();
            rig.Step(ConvertRig.Press(LogicalKey.Skill));
            readout.Observe(rig.Events.ToArray());
            Assert.True(readout.TryGet(0, out GapReport g));
            Assert.False(g.HasGap);
            Assert.Equal(0L, g.RoundedMilliseconds);
        }

        [Fact]
        public void GapReadout_IgnoresOtherEventsAndSlotsOutOfRange()
        {
            var readout = new GapReadout();
            int v = readout.Version;
            readout.Observe(new SimEvent { Type = SimEventType.ShotFired, Slot = new PlayerSlot(0), Tick = 3 });
            readout.Observe(new SimEvent { Type = SimEventType.PracticeGapReport, Slot = PlayerSlot.None, Tick = 3, V = 10 });
            Assert.Equal(v, readout.Version);
            Assert.False(readout.TryGet(0, out _));
            Assert.False(readout.TryGet(-1, out _));
            readout.Observe(new SimEvent { Type = SimEventType.PracticeGapReport, Slot = new PlayerSlot(1), Tick = 3, V = 1075, A = 1075, B = 1, C = 1 });
            Assert.True(readout.TryGet(1, out GapReport g));
            Assert.Equal(70L, g.RoundedMilliseconds);                 // 섬 창 0.07초 = 1,075 단위(spec 03 1.2)
            Assert.False(g.Just);
            readout.Observe(new SimEvent { Type = SimEventType.ArtCast, Slot = new PlayerSlot(1), Tick = 4, A = 1, C = 1 });   // 다른 틱
            Assert.True(readout.TryGet(1, out g));
            Assert.False(g.Just);
            readout.Observe(new SimEvent { Type = SimEventType.ArtCast, Slot = new PlayerSlot(1), Tick = 3, A = 2, C = 1 });   // 다른 기술
            Assert.True(readout.TryGet(1, out g));
            Assert.False(g.Just);
            readout.Observe(new SimEvent { Type = SimEventType.ArtCast, Slot = new PlayerSlot(1), Tick = 3, A = 1, C = 1 });
            Assert.True(readout.TryGet(1, out g));
            Assert.True(g.Just);
            readout.Clear();
            Assert.False(readout.TryGet(1, out _));
        }

        // 수락 조건 3: 표시 계층은 코어 상태를 바꾸지 않는다. 같은 입력의 두 세계 가운데 하나만 매 틱 표시 변환으로 읽어도 틱마다 해시가 같다
        [Fact]
        public void ReadingForDisplay_DoesNotChangeTheWorld()
        {
            ConvertRig shown = ConvertRig.Practice();
            ConvertRig plain = ConvertRig.Practice();
            var interp = new ShapeInterpolator(4, 512);
            int entities = 0;
            var players = new PlayerShapeSampler(PlayerShapeSampler.BodyRadiusFrom(ConvertRig.Db));
            var readout = new GapReadout();
            var script = new Dictionary<int, (LogicalKey, bool, byte)[]>
            {
                [0] = new[] { ConvertRig.Press(LogicalKey.Fire) },
                [30] = new[] { ConvertRig.Press(LogicalKey.Stance) },
                [31] = new[] { ConvertRig.Press(LogicalKey.Down, 40) },
                [32] = new[] { ConvertRig.Release(LogicalKey.Down), ConvertRig.Press(LogicalKey.Skill, 90) },
                [33] = new[] { ConvertRig.Release(LogicalKey.Skill) },
                [60] = new[] { ConvertRig.Release(LogicalKey.Stance), ConvertRig.Press(LogicalKey.Right) },
                [120] = new[] { ConvertRig.Release(LogicalKey.Right), ConvertRig.Press(LogicalKey.Up) },
            };
            int reports = 0;
            for (int t = 0; t < 600; t++)
            {
                (LogicalKey, bool, byte)[] keys = script.TryGetValue(t, out var k) ? k : Array.Empty<(LogicalKey, bool, byte)>();
                shown.Step(keys);
                plain.Step(keys);
                ISimView view = shown.World.View;
                if (!interp.Has(view.Tick))
                {
                    ShapeFrame f = interp.Begin(view.Tick);
                    players.Fill(view, f);
                    EntityShapeSampler.Fill(view, f);
                    interp.End();
                }
                readout.Observe(shown.Events.ToArray());
                for (int i = 0; i < interp.Current.EntityCount; i++)
                {
                    ShapeSample e = interp.Entity(i, 0.5);
                    e.CastFill(view.Tick, 0.5);
                    entities = Math.Max(entities, i + 1);
                }
                for (int i = 0; i < interp.Current.PlayerCount; i++)
                {
                    interp.Player(i, 0.5);
                    PlayerView p = view.Player(new PlayerSlot((byte)i));
                    HeatRingSample.From(p);
                    FacingArrowSample.From(p);
                }
                if (readout.TryGet(0, out _))
                {
                    reports++;
                }
                Assert.Equal(plain.World.ComputeStateHash(), shown.World.ComputeStateHash());
            }
            Assert.True(reports > 0);
            Assert.True(entities > 4);                                         // 포탑, 슬라임, 탄을 실제로 읽었다
            Assert.Equal(1, interp.Current.PlayerCount);
        }
    }
}
