using System;
using Wizard.Core;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Replay
{
    // 기록기의 틱마다 호출과 재실행의 틱 고리가 관리 힙에 할당하지 않는다(틱 수에 비례하는 할당이 없다).
    //
    // GetAllocatedBytesForCurrentThread 는 병렬 xunit 프로세스 안에서 가끔 측정 구간과 무관한 수백 바이트를 더 센다
    // (실행기·JIT 쪽. 저장소 밖에서 같은 호출만 되풀이하면 매번 같은 값이다). 그래서 측정을 몇 번 되풀이해 가장 작은 값을 쓰고,
    // 재실행 쪽은 "틱에 비례하는 할당"만 잡는다: 틱마다 1바이트라도 할당하면 차이가 늘어난 틱 수 이상이 된다(객체 하나는 24바이트 이상).
    public sealed class ReplayAllocationTests
    {
        private const int Rounds = 5;

        [Fact]
        public void Recorder_AppendDoesNotAllocateInSteadyState()
        {
            var cmds = new RunCommand[1];
            var ins = new PlayerInput[8];
            var keys = new QuantizedKey[2];
            var drafts = new[] { new RunCommandDraft(RunCommandOp.Heal, 0, 0) };
            long least = long.MaxValue;
            for (int round = 0; round < Rounds; round++)
            {
                var rec = new InputRecorder(ReplayKit.Header());
                keys[0] = new QuantizedKey(0, 9, LogicalKey.Skill, true);
                keys[1] = new QuantizedKey(0, 3, LogicalKey.Up, true);
                // 데우기(JIT, 정렬 칸)
                rec.Append(0, new PlayerSlot(0), drafts, keys, cmds, out _, ins, out _);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (uint t = 1; t <= 100; t++)
                {
                    keys[0] = new QuantizedKey(t, 9, LogicalKey.Skill, (t & 1) == 0);
                    keys[1] = new QuantizedKey(t, 3, LogicalKey.Up, (t & 1) == 0);
                    rec.Append(t, new PlayerSlot(0), drafts, keys, cmds, out _, ins, out _);   // 100틱 × 약 30바이트 < 본문의 첫 칸 4096
                }
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            Assert.Equal(0, least);
        }

        [Fact]
        public void Replay_AllocationDoesNotGrowWithTicks()
        {
            const uint ShortTicks = 200;
            const uint LongTicks = 20000;
            byte[] shortBody = ReplayKit.BodyOf(new InputRecorder(ReplayKit.Header()).Finish(ShortTicks, 0).ToArray());
            byte[] longBody = ReplayKit.BodyOf(new InputRecorder(ReplayKit.Header()).Finish(LongTicks, 0).ToArray());
            InputRecordHeader header = ReplayKit.Header();
            ReplayRunner.Run(ReplayKit.Db, header, shortBody, ReplayKit.Generous);   // 데우기
            ReplayRunner.Run(ReplayKit.Db, header, longBody, ReplayKit.Generous);

            long leastShort = long.MaxValue, leastLong = long.MaxValue;
            for (int round = 0; round < Rounds; round++)
            {
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                ReplayResult s = ReplayRunner.Run(ReplayKit.Db, header, shortBody, ReplayKit.Generous);
                long a1 = GC.GetAllocatedBytesForCurrentThread();
                ReplayResult l = ReplayRunner.Run(ReplayKit.Db, header, longBody, ReplayKit.Generous);
                long a2 = GC.GetAllocatedBytesForCurrentThread();

                Assert.Equal(ReplayStatus.EndedLate, s.Status);
                Assert.Equal(ReplayStatus.EndedLate, l.Status);
                Assert.Equal(ShortTicks, s.TicksSimulated);
                Assert.Equal(LongTicks, l.TicksSimulated);
                leastShort = Math.Min(leastShort, a1 - a0);
                leastLong = Math.Min(leastLong, a2 - a1);
            }
            // 19,800틱을 더 돌려도 늘어난 할당이 틱 수보다 작다(틱마다의 할당이 없다)
            long grown = leastLong - leastShort;
            Assert.True(grown < LongTicks - ShortTicks,
                "replay allocated " + leastShort + " bytes for " + ShortTicks + " ticks and " + leastLong + " bytes for " + LongTicks + " ticks");
        }
    }
}
