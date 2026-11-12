using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Perf.Tests
{
    // 벤치의 입력 각본: 같은 씨앗이면 같은 각본, 플레이어당 틱마다 8개 이하(CONTRACTS 3.1), Step 의 전제를 지킨다
    public sealed class KeyScriptTests
    {
        private const int Ticks = 3000;

        [Fact]
        public void SameSeed_SameScript()
        {
            KeyScript a = KeyScript.Generate(1, Ticks, 2, true);
            KeyScript b = KeyScript.Generate(1, Ticks, 2, true);
            Assert.Equal(a.KeyCountTotal, b.KeyCountTotal);
            for (int t = 0; t < Ticks; t++)
            {
                Assert.True(Same(a, b, t), "tick " + t);
                Assert.Equal(a.DraftsAt(t).Length, b.DraftsAt(t).Length);
            }
        }

        private static bool Same(KeyScript a, KeyScript b, int t)
        {
            var x = a.KeysAt(t);
            var y = b.KeysAt(t);
            if (x.Length != y.Length)
            {
                return false;
            }
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i].Tick != y[i].Tick || x[i].Slot != y[i].Slot || x[i].Offset != y[i].Offset || x[i].Key != y[i].Key || x[i].Down != y[i].Down)
                {
                    return false;
                }
            }
            return true;
        }

        [Fact]
        public void AtMostEightEventsPerPlayerPerTick_NoPause()
        {
            KeyScript s = KeyScript.Generate(ScenarioInfo.ScriptSeed(PerfScenario.Duel2p), Ticks, 2, false);
            for (int t = 0; t < Ticks; t++)
            {
                var perSlot = new int[2];
                foreach (ScriptKey k in s.KeysAt(t))
                {
                    Assert.Equal((uint)t, k.Tick);
                    Assert.NotEqual(LogicalKey.Pause, k.Key);
                    perSlot[k.Slot]++;
                }
                Assert.True(perSlot[0] <= InputOrder.MaxEventsPerTickPerPlayer && perSlot[1] <= InputOrder.MaxEventsPerTickPerPlayer);
            }
        }

        // Step 은 입력 순서·개수·명령 순번의 전제를 모든 빌드에서 검사한다(CONTRACTS 3.2). 각본 전체가 예외 없이 돈다
        [Fact]
        public void EveryScenario_RunsTheWholeScriptThroughStep()
        {
            foreach (PerfScenario sc in new[] { PerfScenario.Duel2p, PerfScenario.PracticeProto, PerfScenario.Endless1p })
            {
                StepFeed feed = PerfFixtures.Feed(sc, Ticks);
                SimWorld world = ScenarioInfo.Create(sc);
                var sink = new CountingSink();
                while (world.Tick < (uint)feed.Ticks)
                {
                    feed.StepAt(world, sink);
                }
                Assert.Equal((uint)Ticks, world.Tick);
            }
        }
    }
}
