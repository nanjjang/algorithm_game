using System;
using Wizard.Core;
using Wizard.ReplayCli;
using Wizard.Sim.Events;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // spec 01 T-C17, 3.3 "표시 계층(입자, 흔들림)은 코어 생성기를 쓰지 않는다": 표시 계층이 표시용 난수를 1,000번 써도 같은 기록의 재실행 해시가 그대로다.
    // 표시 계층의 흉내: 틱마다 읽기 전용 뷰(ISimView)를 읽고, 받은 표시 이벤트마다 자기 난수기(코어 세계 밖의 Pcg32)를 쓴다.
    // 세계는 판정기·탄·적이 도는 연습 세계라 표시 이벤트가 많다(입자 하나 = 난수 한 번).
    public sealed class DisplayRngTests
    {
        private const int DisplayDraws = 1000;

        private sealed class DisplayLayer : ISimEventSink
        {
            private Pcg32 _rng = new Pcg32(0xD15_9_1A7UL, 7UL);   // 표시용(코어의 흐름과 무관)
            private readonly SimWorld _world;

            public DisplayLayer(SimWorld world)
            {
                _world = world;
            }

            public int Draws { get; private set; }

            public long Sum { get; private set; }

            public void Emit(in SimEvent e)
            {
                if (Draws < DisplayDraws)
                {
                    Draw();                                         // 입자 하나의 흩어짐
                }
            }

            public void Frame()
            {
                ISimView v = _world.View;
                for (int i = 0; i < v.PlayerCount; i++)
                {
                    PlayerView p = v.Player(new PlayerSlot((byte)i));
                    Sum += p.Pos.X.Raw;
                }
                Sum += v.Enemies.Length + v.Projectiles.Length + v.Telegraphs.Length + v.Hazards.Length;
                if (Draws < DisplayDraws && (_world.Tick % 7U) == 0U)
                {
                    Draw();                                         // 화면 흔들림
                }
            }

            private void Draw()
            {
                Sum += _rng.NextInt(1000);
                Draws++;
            }
        }

        private static ulong[] RunWithDisplay(RecordFeed feed, Action<SimWorld, DisplayLayer> perTick, out int draws)
        {
            SimWorld world = DetKit.World(WorldKind.Practice, feed);
            var display = new DisplayLayer(world);
            var hashes = new ulong[feed.EndTick + 1];
            hashes[0] = world.ComputeStateHash();
            while (world.Tick < feed.EndTick)
            {
                Replayer.StepOnce(world, feed, display);
                perTick(world, display);
                hashes[world.Tick] = world.ComputeStateHash();
            }
            draws = display.Draws;
            return hashes;
        }

        [Fact]
        public void S01_T_C17__DisplayRandomnessDoesNotTouchTheHash()
        {
            RecordFeed feed = DetKit.Feed(DetKit.PracticeRecord(0x0C17_0001UL, DetKit.Minute, "dense"));
            ulong[] plain = DetKit.Hashes(WorldKind.Practice, feed);
            ulong[] shown = RunWithDisplay(feed, (w, d) => d.Frame(), out int draws);
            Assert.Equal(DisplayDraws, draws);
            HashAssert.SameEveryTick(plain, shown, "with display randomness", WorldKind.Practice, feed);
        }

        // 이 시험이 잡는 결함: 표시가 코어의 난수 흐름을 쓰면 해시가 바뀐다(그래서 위 시험의 "같다"는 우연이 아니다)
        [Fact]
        public void UsingACoreStreamForDisplayWouldChangeTheHash()
        {
            RecordFeed feed = DetKit.Feed(DetKit.PracticeRecord(0x0C17_0001UL, 120, "dense"));
            ulong[] plain = DetKit.Hashes(WorldKind.Practice, feed);
            ulong[] wrong = RunWithDisplay(feed, (w, d) => w.HeldRng(RngStream.Weather).NextU32(), out _);
            Assert.NotEqual(plain[feed.EndTick], wrong[feed.EndTick]);
        }
    }
}
