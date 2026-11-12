using System;
using Wizard.Sim;
using Wizard.Sim.Events;

namespace Wizard.ReplayCli
{
    // 기록을 세계에 넣어 틱을 돌린다. 틱 t 의 Step 에는 기록의 틱 t 열만 넣는다(ReplayRunner 와 같은 꼴).
    // 해시 목록의 칸 t = world.Tick == t 일 때의 상태 해시(칸 0 = 첫 Step 전, 칸 EndTick = 판 끝). .wzrc 의 0x04 와 꼬리말이 같은 규약이다.
    public static class Replayer
    {
        public static void StepOnce(SimWorld world, RecordFeed feed, ISimEventSink sink)
        {
            uint t = world.Tick;
            world.Step(feed.InputsAt(t), feed.CommandsAt(t), sink);
        }

        // world 를 기록의 끝(또는 판이 끝날 때)까지 돌리며 틱마다 해시. 길이 = 돌린 틱 수 + 1
        public static ulong[] TickHashes(SimWorld world, RecordFeed feed, ISimEventSink? sink = null)
        {
            if (world == null || feed == null)
            {
                throw new ArgumentNullException(world == null ? nameof(world) : nameof(feed));
            }
            if (world.Tick != 0)
            {
                throw new ArgumentException("the world must be at tick 0", nameof(world));
            }
            ISimEventSink s = sink ?? new CountingSink();
            var hashes = new ulong[feed.EndTick + 1];
            hashes[0] = world.ComputeStateHash();
            while (world.Tick < feed.EndTick && !world.IsOver)
            {
                StepOnce(world, feed, s);
                hashes[world.Tick] = world.ComputeStateHash();
            }
            if (world.Tick < feed.EndTick)
            {
                Array.Resize(ref hashes, (int)world.Tick + 1);
            }
            return hashes;
        }

        // world.Tick 이 tick 이 될 때까지(기록의 끝을 넘지 않는다)
        public static void RunTo(SimWorld world, RecordFeed feed, uint tick, ISimEventSink sink)
        {
            while (world.Tick < tick && world.Tick < feed.EndTick && !world.IsOver)
            {
                StepOnce(world, feed, sink);
            }
        }
    }
}
