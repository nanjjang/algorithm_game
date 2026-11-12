using Wizard.ReplayCli;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // spec 01 T-C14, PIPELINE 7.3 "두 번 실행": 같은 씨앗과 같은 입력 기록을 두 번 돌리면 모든 틱의 해시가 같다(GDD 16 1단계의 자동 테스트).
    // 기록 L 은 10분(36,000틱)의 봇 입력이고 판을 하는 쪽과 같은 길(InputRecorder·DuelRecordWriter)로 만든다. 기록을 만든 실행(끊지 않은 원래 실행)과도 같아야 한다.
    public sealed class TwoRunTests
    {
        private const ulong SeedS = 0x5EED_0C14_0000_0001UL;

        [Theory]
        [InlineData(WorldKind.Practice, "dense", 0x0C14_0001UL)]
        [InlineData(WorldKind.Endless, "dense", 0x0C14_0002UL)]
        [InlineData(WorldKind.Duel, "dense", 0x0C14_0003UL)]
        public void S01_T_C14__SameSeedAndRecordGiveTheSameHashEveryTick(WorldKind kind, string profile, ulong botSeed)
        {
            var original = new ulong[DetKit.TenMinutes + 1];
            RecordFeed feed = DetKit.Feed(DetKit.Record(kind, SeedS, botSeed, DetKit.TenMinutes, profile, original));
            Assert.Equal(DetKit.TenMinutes, feed.EndTick);

            ulong[] first = DetKit.Hashes(kind, feed);
            ulong[] second = DetKit.Hashes(kind, feed);
            HashAssert.SameEveryTick(first, second, "run 1 vs run 2", kind, feed);
            HashAssert.SameEveryTick(original, first, "recording run vs replay");
            Assert.Equal(feed.FinalStateHash, first[DetKit.TenMinutes]);
            Assert.True(feed.InputCount > 1000, "the record must carry input");
        }

        // 씨앗이 결과에 닿는다(같은 기록, 다른 씨앗이면 해시가 다르다). 씨앗이 상태에 없는데도 위 시험이 통과하는 일을 막는다
        [Fact]
        public void DifferentSeedChangesTheHash()
        {
            RecordFeed a = DetKit.Feed(DetKit.EndlessRecord(SeedS, 0x11UL, 60));
            RecordFeed b = DetKit.Feed(DetKit.EndlessRecord(SeedS + 1, 0x11UL, 60));
            Assert.NotEqual(DetKit.Hashes(WorldKind.Endless, a)[0], DetKit.Hashes(WorldKind.Endless, b)[0]);
        }
    }
}
