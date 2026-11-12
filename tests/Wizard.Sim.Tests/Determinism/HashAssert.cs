using System;
using Wizard.ReplayCli;
using Xunit.Sdk;

namespace Wizard.Sim.Tests.Determinism
{
    // 틱별 해시 열의 비교. 처음 갈라진 틱을 알리고, 기록이 주어지면 그 틱에서 다른 상태 절도 찾는다(ReplayCli 의 Divergence)
    internal static class HashAssert
    {
        public static void SameEveryTick(ulong[] expected, ulong[] actual, string what, WorldKind? kind = null, RecordFeed? feed = null)
        {
            int n = Math.Min(expected.Length, actual.Length);
            for (int t = 0; t < n; t++)
            {
                if (expected[t] != actual[t])
                {
                    string where = "";
                    if (kind.HasValue && feed != null)
                    {
                        where = "\nsections differing from a fresh replay at that tick: " + SectionsAt(kind.Value, feed, (uint)t);
                    }
                    throw new XunitException(what + ": hashes differ first at world.Tick " + t + " (" + expected[t].ToString("x16") + " vs " +
                                             actual[t].ToString("x16") + ")" + where);
                }
            }
            if (expected.Length != actual.Length)
            {
                throw new XunitException(what + ": lengths differ (" + expected.Length + " vs " + actual.Length + " tick boundaries)");
            }
        }

        // 기록을 처음부터 끊지 않고 돌린 세계의 그 틱 상태(실패 보고용)
        public static StateImage FreshImageAt(WorldKind kind, RecordFeed feed, uint tick)
        {
            SimWorld w = DetKit.World(kind, feed);
            Replayer.RunTo(w, feed, tick, new CountingSink());
            return StateSections.Capture(w);
        }

        private static string SectionsAt(WorldKind kind, RecordFeed feed, uint tick)
        {
            // 두 번 돌려 다른 절을 찾는다(같으면 이 실패는 재실행이 아니라 비교한 쪽의 문제다)
            return StateImage.Describe(StateImage.Diff(FreshImageAt(kind, feed, tick), FreshImageAt(kind, feed, tick)));
        }
    }
}
