using Wizard.Solo.Data;

namespace Wizard.Solo.Meta.Records
{
    /// <summary>도전 과제 글(이름 표만 쓴다). 이름 = challenge.&lt;id&gt;, 설명 틀 = str.challenge.kind.&lt;kind&gt;[.all|.sum], 보상 = str.challenge.reward.*.</summary>
    public static class ChallengeText
    {
        public const string KindPrefix = "str.challenge.kind.", RewardTitle = "str.challenge.reward.title",
            RewardCosmetic = "str.challenge.reward.cosmetic", RewardLibrary = "str.challenge.reward.library",
            Progress = "str.challenge.progress", Done = "str.challenge.done", Next = "str.challenge.next";

        /// <summary>설명 틀 열쇠: 다섯 제자 모두(min)면 .all, 합(sum)이면 .sum 꼬리.</summary>
        public static string KindKey(ChallengeDef d)
        {
            if (d == null) return KindPrefix;
            string k = KindPrefix + d.Kind;
            if (d.Character == 0 && d.Aggregate == "min") return k + ".all";
            if (d.Character == 0 && d.Aggregate == "sum") return k + ".sum";
            return k;
        }

        public static string Describe(INameTable n, ChallengeDef d) =>
            n == null || d == null ? KindKey(d) : n.Format(KindKey(d), "n", n.Number(d.Target));

        public static string Reward(INameTable n, ChallengeDef d)
        {
            if (d == null) return string.Empty;
            switch (d.Reward)
            {
                case ChallengeRewardKind.Title: return n != null ? n.Format(RewardTitle, "name", n.Get(d.RewardId)) : RewardTitle;
                case ChallengeRewardKind.Cosmetic: return n != null ? n.Format(RewardCosmetic, "name", n.Get(d.RewardId)) : RewardCosmetic;
                case ChallengeRewardKind.LibrarySlot: return n != null ? n.Format(RewardLibrary, "n", d.RewardCount) : RewardLibrary;
                default: return string.Empty;
            }
        }

        public static string ProgressText(INameTable n, ChallengeProgress p) =>
            n == null ? Progress : p.Done ? n.Get(Done) : n.Format(Progress, "n", n.Number(p.Value), "max", n.Number(p.Target));
    }
}
