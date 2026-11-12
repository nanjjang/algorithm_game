using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.Client.Run;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.ViewLogic.Convert;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // 돌아가는 게임의 길을 엔진 없이 그대로 밟는다(ARCHITECTURE 6.1): 입력원 → PracticeSession.RunFrame(프레임마다) → 세션의 ISimView 와
    // 표시 이벤트 → 표시 변환(플레이어·적·투사체의 도형, 열 링, 섬 간격). 조립 루트가 꽂는 것과 같은 세션이다(CompositionRoot.StartPractice).
    // 표시 계층은 세션의 세계를 쥐지 않는다. 뷰만으로 시제품의 전부가 보인다(M1-UE-02 수락 조건 2·3, T-54).
    public sealed class PracticeSessionDisplayTests
    {
        private const double TickSeconds = 1.0 / 60;

        private sealed class ScriptedInput : IInputSource
        {
            public readonly Queue<RawKeyEvent> Pending = new Queue<RawKeyEvent>();
            public KeyMask CurrentKeys => KeyMask.None;

            public int Drain(Span<RawKeyEvent> buffer)
            {
                int n = 0;
                while (n < buffer.Length && Pending.Count > 0)
                {
                    buffer[n++] = Pending.Dequeue();
                }
                return n;
            }

            public void SetBindings(KeyBindings bindings)
            {
            }

            public void SetDuelPolling(bool on)
            {
            }
        }

        [Fact]
        public void TheRunningPracticeSession_ShowsPlayerTurretSlimesBulletsHeatAndGap_ThroughItsViewAlone()
        {
            var session = new PracticeSession(ConvertRig.Db, new PracticeSetup(), 0.0);
            var input = new ScriptedInput();
            var interp = new ShapeInterpolator(4, 512);
            var players = new PlayerShapeSampler(PlayerShapeSampler.BodyRadiusFrom(ConvertRig.Db));
            var gaps = new GapReadout();
            var seen = new HashSet<ShapeKind>();
            bool sawRing = false, sawTelegraph = false;
            int frames = 0;

            // 프레임 = 1/120초(틱의 절반): 틱이 돌지 않는 프레임에도 그린다. 0.2초에 J 를 누르고, 3초 뒤 자세 → 방향 → K
            for (double now = 0.0; now < 5.0; now += TickSeconds / 2)
            {
                frames++;
                if (frames == 24)
                {
                    input.Pending.Enqueue(new RawKeyEvent(LogicalKey.Fire, true, now));
                }
                if (frames == 360)
                {
                    input.Pending.Enqueue(new RawKeyEvent(LogicalKey.Stance, true, now));
                }
                if (frames == 380)
                {
                    input.Pending.Enqueue(new RawKeyEvent(LogicalKey.Left, true, now));
                }
                if (frames == 384)
                {
                    input.Pending.Enqueue(new RawKeyEvent(LogicalKey.Skill, true, now));
                }
                double alpha = session.RunFrame(input, now);
                ISimView view = session.View;
                gaps.Observe(session.Events);
                if (!interp.Has(view.Tick))
                {
                    ShapeFrame f = interp.Begin(view.Tick);
                    players.Fill(view, f);
                    EntityShapeSampler.Fill(view, f);
                    interp.End();
                }
                Assert.Equal(0, interp.Current.DroppedCount);
                for (int i = 0; i < interp.Current.PlayerCount; i++)
                {
                    ShapeSample p = interp.Player(i, alpha);
                    seen.Add(p.Kind);
                    sawRing |= HeatRingSample.From(view.Player(new PlayerSlot((byte)p.Id))).RingVisible;
                }
                for (int i = 0; i < interp.Current.EntityCount; i++)
                {
                    ShapeSample e = interp.Entity(i, alpha);
                    seen.Add(e.Kind);
                    sawTelegraph |= e.Casting && e.CastFill(view.Tick, alpha) > 0f;
                    Assert.InRange(e.Pos.X, -1f, 25f);                         // 전장(24×14) 언저리 안
                    Assert.InRange(e.Pos.Y, -15f, 1f);
                }
            }

            Assert.True(session.Tick >= 295);                                  // 5초 가까이 틱이 돌았다
            Assert.Contains(ShapeKind.Player, seen);
            Assert.Contains(ShapeKind.Turret, seen);
            Assert.Contains(ShapeKind.Slime, seen);
            Assert.Contains(ShapeKind.PlayerBullet, seen);
            Assert.Contains(ShapeKind.EnemyShot, seen);
            Assert.True(sawRing);                                              // 발사로 열이 올랐다
            Assert.True(sawTelegraph);                                         // 포탑의 시전 예고가 찼다
            Assert.True(gaps.TryGet(0, out GapReport gap));                    // 자세에서 방향 뒤의 K: 섬 간격이 밀리초로 나온다
            Assert.True(gap.HasGap);
            Assert.Equal(33L, gap.RoundedMilliseconds);                        // 4 frames = 1/30 s = 512 input units (spec 01 2.1)
        }
    }
}
