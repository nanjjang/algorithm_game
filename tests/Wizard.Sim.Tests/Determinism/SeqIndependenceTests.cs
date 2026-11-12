using System;
using Wizard.ReplayCli;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // CONTRACTS 3.1 "Seq 는 정렬의 동률 해소와 결투의 확정 대조에만 쓴다. 세계 상태와 해시에 넣지 않는다", ARCHITECTURE 11 "상태의 범위".
    // 순번을 전부 다른 값으로 바꿔 넣어도(순서는 같게: 사람마다 늘어나는 함수) 틱별 해시가 같다. 명령과 키가 섞인 판(조립 바꾸기)과 두 사람 판으로 본다.
    public sealed class SeqIndependenceTests
    {
        // 늘어나는 함수(이웃한 값의 차가 3 이상)이고 모든 값을 바꾼다. 건너뜀이 고르지 않다
        private static uint Remap(uint seq) => 1_000_003U + seq * 7U + (seq * seq) % 5U;

        [Theory]
        [InlineData(WorldKind.Practice, "loadout")]
        [InlineData(WorldKind.Practice, "dense")]
        [InlineData(WorldKind.Duel, "dense")]
        [InlineData(WorldKind.Endless, "dense")]
        public void RenumberedSequencesGiveTheSameHashEveryTick(WorldKind kind, string profile)
        {
            RecordFeed feed = DetKit.Feed(DetKit.Record(kind, 0x5E9_0001UL, 0x5E9_0002UL, DetKit.Minute, profile));
            RecordFeed renumbered = feed.WithSequences(p => Remap(p.Event.Seq), c => Remap(c.Seq));

            int changed = 0;
            for (uint t = 0; t < feed.EndTick; t++)
            {
                ReadOnlySpan<PlayerInput> a = feed.InputsAt(t), b = renumbered.InputsAt(t);
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.NotEqual(a[i].Event.Seq, b[i].Event.Seq);
                    Assert.Equal(Math.Sign(InputOrder.Compare(a[0], a[i])), Math.Sign(InputOrder.Compare(b[0], b[i])));
                    changed++;
                }
                ReadOnlySpan<RunCommand> ca = feed.CommandsAt(t), cb = renumbered.CommandsAt(t);
                for (int i = 0; i < ca.Length; i++)
                {
                    Assert.NotEqual(ca[i].Seq, cb[i].Seq);
                    changed++;
                }
            }
            Assert.True(changed > 500, "the record must carry enough numbered input, got " + changed);
            if (profile == "loadout")
            {
                Assert.True(feed.CommandCount > 0, "the loadout record must carry commands");
            }

            HashAssert.SameEveryTick(DetKit.Hashes(kind, feed), DetKit.Hashes(kind, renumbered), "original vs renumbered sequences");
        }
    }
}
