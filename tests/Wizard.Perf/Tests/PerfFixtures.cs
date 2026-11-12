using System;
using System.IO;
using Wizard.Sim;
using Wizard.Sim.Events;

namespace Wizard.Perf.Tests
{
    // 시험이 같이 쓰는 것: 짧은 각본, 할당을 재는 Step 고리, 일부러 할당하는 받는 쪽, 임시 파일
    internal static class PerfFixtures
    {
        public const int WarmupTicks = 600;
        public const int MeasureTicks = 2400;

        public static StepFeed Feed(PerfScenario s, int ticks) =>
            StepFeed.From(KeyScript.Generate(ScenarioInfo.ScriptSeed(s), ticks, ScenarioInfo.Players(s), ScenarioInfo.UsesCommands(s)));

        // 벤치 코드(TickBench)와 따로, GC 없는 구간(NoGcWindow) 안에서 틱마다 Step 앞뒤의 할당 계수기를 읽어 더한다. 0단계 명령이 있는 틱은 commandBytes 로 따로 센다
        // (0단계의 주문 컴파일이 Step 할당 0의 유일한 예외다, CONTRACTS 3.2)
        public static void MeasureStepAllocation(SimWorld world, StepFeed feed, ISimEventSink sink, int warmupTicks, int measureTicks,
                                                 out long stepBytes, out long commandBytes, out int commandTicks)
        {
            while (world.Tick < (uint)warmupTicks)
            {
                feed.StepAt(world, sink);
            }
            stepBytes = 0;
            commandBytes = 0;
            commandTicks = 0;
            uint end = (uint)(warmupTicks + measureTicks);
            using NoGcWindow window = NoGcWindow.Open();
            while (world.Tick < end)
            {
                bool commands = feed.HasCommandsAt(world.Tick);
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                feed.StepAt(world, sink);
                long a1 = GC.GetAllocatedBytesForCurrentThread();
                if (commands)
                {
                    commandBytes += a1 - a0;
                    commandTicks++;
                }
                else
                {
                    stepBytes += a1 - a0;
                }
            }
            // GC 가 돌았으면 계수기의 값이 실제 할당이 아니다(NoGcWindow 머리말). 그 경우는 시험의 실패다(다시 돌려 확인한다)
            if (!window.Valid)
            {
                throw new Xunit.Sdk.XunitException("a GC ran inside the allocation window; the count is not trustworthy");
            }
        }

        public static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "wizard-perf-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // 이벤트마다 관리 힙에 작은 배열을 만든다. Step 안에서 불리므로 할당 계수기가 이것을 Step 의 할당으로 잡아야 한다(결함 주입)
    internal sealed class AllocatingSink : ISimEventSink
    {
        public object? Last;
        public long Count;

        public void Emit(in SimEvent e)
        {
            Last = new int[1];
            Count++;
        }
    }
}
