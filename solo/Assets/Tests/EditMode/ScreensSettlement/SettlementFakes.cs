using System.Collections.Generic;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensSettlement
{
    /// <summary>
    /// 정산 서비스 가짜(화면 시험용). 칸수·영웅 한도는 요약서 식(08 9.2절: min(12, passed / 3), passed / 10)을 그대로 낸다 —
    /// 이 테스트가 보는 것은 화면이 서비스의 값을 어떻게 그리고 검증하느냐이고, 서비스 자체의 시험은 Tests/EditMode/Settlement에 있다.
    /// </summary>
    public sealed class FakeSettlementService : ISettlementService
    {
        public int PrepareCalls, CommitCalls, AutoPickCalls;
        public List<int> LastCommitUids;
        public RunSummary LastCommitSummary;
        /// <summary>Prepare가 돌려줄 서고 남은 자리.</summary>
        public int LibraryRoom = 100;
        public bool NewBestScore, NewBestWave;
        /// <summary>Prepare가 돌려줄 점수 내역(null이면 요약 합).</summary>
        public ScoreBreakdown? Score;
        /// <summary>Commit이 돌려줄 결과(null이면 빈 결과).</summary>
        public SettlementResult CommitResult;
        public ErrorCode CommitError = ErrorCode.None;
        /// <summary>AutoPick이 돌려줄 순서(null이면 후보 순서 그대로).</summary>
        public List<int> AutoPickOrder;
        /// <summary>Commit이 불리기 직전에 부를 것(프로필을 바꾸는 흉내).</summary>
        public System.Action<RunSummary> OnCommit;

        public BagLimits BagLimitsFor(int clearedDirect)
        {
            int c = clearedDirect < 0 ? 0 : clearedDirect;
            int slots = c / 3;
            if (slots > 12) slots = 12;
            return new BagLimits { Slots = slots, HeroCap = c / 10 };
        }

        public bool IsValidStartSkip(int bestWave, int skip) => StartSkipOptions(bestWave).Contains(skip);
        public List<int> StartSkipOptions(int bestWave) => new List<int> { 0 };

        public SettlementView Prepare(RunSummary summary)
        {
            PrepareCalls++;
            var v = new SettlementView { Summary = summary, Bag = BagLimitsFor(summary.ClearedDirect), LibraryRoom = LibraryRoom };
            v.NewBestScore = NewBestScore;
            v.NewBestWave = NewBestWave;
            v.Score = Score ?? Wizard.Solo.Screens.Settlement.SettlementRules.SumScore(summary);
            if (summary.WildBlocks != null)
                foreach (var b in summary.WildBlocks)
                    if (b != null && !b.Borrowed && !b.FromLibrary) v.BagCandidates.Add(b);
            return v;
        }

        public Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids)
        {
            CommitCalls++;
            LastCommitSummary = summary;
            LastCommitUids = new List<int>(bagUids);
            OnCommit?.Invoke(summary);
            if (CommitError != ErrorCode.None) return Result<SettlementResult>.Fail(CommitError);
            return Result<SettlementResult>.Success(CommitResult ?? new SettlementResult());
        }

        public List<int> AutoPick(SettlementView view)
        {
            AutoPickCalls++;
            if (AutoPickOrder != null) return new List<int>(AutoPickOrder);
            var list = new List<int>();
            foreach (var b in view.BagCandidates) list.Add(b.Uid);
            return list;
        }

        public void SaveCheckpoint(RunSummary summary) { }
        public SettlementResult SettlePendingCheckpoint() => null;
        public void RecordArtUnlock(int character, string artId) { }
    }

    /// <summary>시험용 요약·프로필 만들기.</summary>
    public static class SettlementFixtures
    {
        public static RunSummary Summary(int cleared, int startWave = 1, int reached = 0)
        {
            return new RunSummary
            {
                RunId = "run.test",
                CharId = Ids.Char.C3,
                Mode = RunMode.Endless,
                StartWave = startWave,
                ClearedDirect = cleared,
                ReachedWave = reached > 0 ? reached : startWave + cleared,
                LastClearedWave = cleared > 0 ? startWave + cleared - 1 : 0,
                WandStage = 2
            };
        }

        public static WildBlock Block(int uid, string rarity, bool borrowed = false, string id = "block.fireball") =>
            new WildBlock { Uid = uid, BlockId = id, Rarity = rarity, Borrowed = borrowed };

        public static ProfileData Profile(bool phantomMet = false, int bestWave = 0)
        {
            var p = new ProfileData();
            for (int i = 1; i <= Ids.Char.Count; i++) p.Characters.Add(new CharacterRecord { Id = i });
            p.Characters[0].BestWave = bestWave;
            p.Flags.PhantomMet = phantomMet;
            return p;
        }
    }
}
