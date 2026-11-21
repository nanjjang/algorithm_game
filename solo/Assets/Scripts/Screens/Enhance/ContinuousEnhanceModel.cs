using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>이어서 강화가 멈춘 까닭(08 요약서 10.9절, 07 5.4절). 이름 표 str.enhance.auto_stop.&lt;이름&gt;.</summary>
    public enum ContinueStop
    {
        None, TargetReached, MutationOffered, GreatSuccess, Milestone, Dropped, SacrificeShort, CannotAttempt, Input
    }

    /// <summary>
    /// 이어서 강화(08 10.9절). 같은 제물 구성(같은 block id, 없으면 같은 등급·속성의 다른 블록 [임시 U-21]) + 같은 부적 사용 여부로
    /// 시도마다 따로 결과를 받는다(한 번에 여러 결과를 미리 뽑지 않는다, TV-Q-10). 누계 띠 "12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800".
    /// </summary>
    public sealed class ContinuousEnhanceModel
    {
        public bool Armed;
        public bool Running { get; private set; }
        public bool StopOnDrop;
        public int TargetStage;
        public int Attempts { get; private set; }
        public int Successes { get; private set; }
        public int Failures { get; private set; }
        public long GoldSpent { get; private set; }
        public ContinueStop LastStop { get; private set; }
        readonly List<string> _template = new List<string>();
        public bool UseCharm;

        public IReadOnlyList<string> Template => _template;

        public ContinuousEnhanceModel(bool stopOnDropDefault) { StopOnDrop = stopOnDropDefault; }

        /// <summary>누름으로 시작한다: 제물 구성과 부적 여부를 기억하고 누계를 비운다.</summary>
        public void Start(IReadOnlyList<string> sacrifices, bool useCharm, int targetStage)
        {
            _template.Clear();
            if (sacrifices != null) _template.AddRange(sacrifices);
            UseCharm = useCharm;
            TargetStage = targetStage;
            Attempts = Successes = Failures = 0;
            GoldSpent = 0;
            LastStop = ContinueStop.None;
            Running = true;
        }

        public void Stop(ContinueStop why)
        {
            if (!Running) return;
            Running = false;
            LastStop = why;
        }

        /// <summary>결과 하나를 센다. 실패 수에는 변이·부적이 막음·하락 없음이 든다 [임시]. 변이 결정 꾸러미는 세지 않는다.</summary>
        public void Count(EnhanceResult r)
        {
            if (r == null) return;
            Attempts++;
            if (r.Outcome == EnhanceOutcome.Success || r.Outcome == EnhanceOutcome.GreatSuccess) Successes++;
            else Failures++;
            GoldSpent += r.Breakdown.CostGold;
        }

        /// <summary>결과를 보고 멈출지(TV-127·128, TV-Q-06·07·09). 마일스톤에는 +13이 든다(07 13절 M-11).</summary>
        public static ContinueStop StopAfter(EnhanceResult r, int targetStage, bool stopOnDrop)
        {
            if (r == null) return ContinueStop.CannotAttempt;
            if (r.Outcome == EnhanceOutcome.MutationOffered) return ContinueStop.MutationOffered;
            if (r.Outcome == EnhanceOutcome.GreatSuccess) return ContinueStop.GreatSuccess;
            if (r.MilestoneStage > 0) return ContinueStop.Milestone;
            if (r.StageAfter >= targetStage) return ContinueStop.TargetReached;
            if (stopOnDrop && r.Outcome == EnhanceOutcome.FailDrop) return ContinueStop.Dropped;
            return ContinueStop.None;
        }

        /// <summary>시도 전에 멈출지(TV-129, TV-Q-08): 제물을 다시 못 채우면 제물 부족, 규칙상 시도할 수 없으면(골드·강화석·부적) 재료 부족.</summary>
        public static ContinueStop StopBefore(IReadOnlyList<string> picked, ErrorCode blocked)
        {
            if (picked == null) return ContinueStop.SacrificeShort;
            return blocked == ErrorCode.None ? ContinueStop.None : ContinueStop.CannotAttempt;
        }

        /// <summary>하락 없는 시도로 바뀌면 그 시도엔 부적을 쓰지 않는다(07 5.4절).</summary>
        public static bool CharmFor(bool wantCharm, bool dropPossible) => wantCharm && dropPossible;

        /// <summary>
        /// 같은 제물 구성을 서고 여분(끼우지 않은 수량)에서 다시 고른다. 같은 id가 없으면 같은 (등급, 속성)의 다른 블록. 하나라도 못 채우면 null.
        /// </summary>
        public static List<string> PickSacrifices(IContentDb db, ProfileData profile, IReadOnlyList<string> template)
        {
            var picked = new List<string>();
            if (template == null || template.Count == 0) return picked;
            var used = new Dictionary<string, int>();
            for (int i = 0; i < template.Count; i++)
            {
                string want = template[i];
                string got = Free(profile, want, used) > 0 ? want : null;
                if (got == null && db != null)
                {
                    BlockDef wd = db.Block(want);
                    if (wd != null)
                    {
                        var all = db.Blocks;
                        for (int k = 0; k < all.Count && got == null; k++)
                        {
                            BlockDef b = all[k];
                            if (b.Id != want && b.Rarity == wd.Rarity && b.Element == wd.Element && Free(profile, b.Id, used) > 0) got = b.Id;
                        }
                    }
                }
                if (got == null) return null;
                used.TryGetValue(got, out int n);
                used[got] = n + 1;
                picked.Add(got);
            }
            return picked;
        }

        static int Free(ProfileData p, string id, Dictionary<string, int> used)
        {
            if (p == null || id == null || !p.Items.TryGetValue(id, out ItemStack s) || s == null) return 0;
            used.TryGetValue(id, out int n);
            return s.Qty - s.QtySocketed - n;
        }

        /// <summary>누계 띠 글(TV-130).</summary>
        public string TallyText(INameTable names) =>
            names == null ? EnhanceNameKeys.Tally
            : names.Format(EnhanceNameKeys.Tally, "n", Attempts, "success", Successes, "fail", Failures, "gold", names.Number(GoldSpent));

        public static string StopKey(ContinueStop s)
        {
            switch (s)
            {
                case ContinueStop.TargetReached: return EnhanceNameKeys.AutoStopPrefix + "target";
                case ContinueStop.MutationOffered: return EnhanceNameKeys.AutoStopPrefix + "mutation";
                case ContinueStop.GreatSuccess: return EnhanceNameKeys.AutoStopPrefix + "great";
                case ContinueStop.Milestone: return EnhanceNameKeys.AutoStopPrefix + "milestone";
                case ContinueStop.Dropped: return EnhanceNameKeys.AutoStopPrefix + "dropped";
                case ContinueStop.SacrificeShort: return EnhanceNameKeys.AutoStopPrefix + "sacrifice_short";
                case ContinueStop.CannotAttempt: return EnhanceNameKeys.AutoStopPrefix + "cannot";
                default: return EnhanceNameKeys.AutoStopPrefix + "input";
            }
        }
    }
}
