using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Meta.Enhance
{
    /// <summary>이어서 강화가 멈추는 까닭(07 요약서 5.4절).</summary>
    public enum AutoStopReason
    {
        None,
        /// <summary>시도 전: 골드·강화석·부적·제물이 모자라거나 그 밖의 까닭으로 시도할 수 없다.</summary>
        CannotAttempt,
        SacrificeShort,
        TargetReached, MutationOffered, GreatSuccess, Milestone, Dropped
    }

    /// <summary>이어서 강화의 누계("12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800").</summary>
    public struct EnhanceTally
    {
        public int Attempts, Successes, Failures;
        public long GoldSpent;

        /// <summary>결과 하나를 센다. 실패 수에는 변이·부적이 막음·하락 없음이 다 든다 [임시]. 변이 결정(받음·거절)의 꾸러미는 세지 않는다.</summary>
        public void Add(EnhanceResult r, bool isMutationDecision)
        {
            if (r == null || isMutationDecision) return;
            Attempts++;
            if (r.Outcome == EnhanceOutcome.GreatSuccess || r.Outcome == EnhanceOutcome.Success) Successes++;
            else Failures++;
            GoldSpent += r.Breakdown.CostGold;
        }
    }

    /// <summary>
    /// 건너뛰기·빠른 강화·이어서 강화의 규칙 쪽(07 요약서 5.4절, 12.12절). 화면(G23)이 부른다.
    /// 연출은 결과를 바꾸지 않는다 — 여기 함수들은 "언제 넘어가는가, 언제 멈추는가"만 정한다.
    /// </summary>
    public static class EnhanceFlowRules
    {
        /// <summary>의식 중 J 재입력이 건너뛰기로 먹히는가: 누른 뒤 skip_ignore_ticks가 지났고, 변이 카드가 아니고,
        /// +13 첫 도달의 못 넘기는 구간이 아니다.</summary>
        public static bool CanSkip(EnhanceTuning tuning, int ticksSincePress, bool mutationCardShown, int plus13FirstElapsedTicks)
        {
            if (mutationCardShown) return false;
            if (plus13FirstElapsedTicks >= 0 && plus13FirstElapsedTicks < tuning.Plus13FirstLockTicks) return false;
            return ticksSincePress >= tuning.SkipIgnoreTicks;
        }

        /// <summary>빠른 강화를 켰어도 전체 의식을 하는가(+10 이상 도전. 설정에서 풀 수 있다).</summary>
        public static bool UsesFullRitual(EnhanceTuning tuning, bool fastOn, int stage, bool fastAllowHigh)
        {
            if (!fastOn) return true;
            return stage >= tuning.FastFullRitualMinStage && !fastAllowHigh;
        }

        /// <summary>하락 없는 시도에는 부적을 쓰지 않는다.</summary>
        public static bool AutoUseCharm(bool wantCharm, bool dropPossible) => wantCharm && dropPossible;

        /// <summary>결과를 보고 멈출지 정한다. 한 번에 한 결과만 본다(시도마다 따로 판정한다).</summary>
        public static AutoStopReason StopAfter(EnhanceResult r, int targetStage, bool stopOnDrop)
        {
            if (r == null) return AutoStopReason.CannotAttempt;
            if (r.Outcome == EnhanceOutcome.MutationOffered) return AutoStopReason.MutationOffered;
            if (r.Outcome == EnhanceOutcome.GreatSuccess) return AutoStopReason.GreatSuccess;
            if (r.MilestoneStage > 0) return AutoStopReason.Milestone;
            if (r.StageAfter >= targetStage) return AutoStopReason.TargetReached;
            if (stopOnDrop && r.Outcome == EnhanceOutcome.FailDrop) return AutoStopReason.Dropped;
            return AutoStopReason.None;
        }

        /// <summary>시도 전에 멈출지 정한다. picked = PickSacrifices의 결과(null = 제물 부족).</summary>
        public static AutoStopReason StopBefore(ProbBreakdown preview, IReadOnlyList<string> picked)
        {
            if (picked == null) return AutoStopReason.SacrificeShort;
            return preview.Blocked == ErrorCode.None ? AutoStopReason.None : AutoStopReason.CannotAttempt;
        }

        /// <summary>
        /// 같은 제물 구성을 서고에서 다시 고른다: 같은 block id가 남아 있으면 그것, 없으면 같은 (등급, 속성)의 다른 블록.
        /// 서고의 여분(끼우지 않은 수량)만 쓴다. 하나라도 못 채우면 null(이어서 강화는 멈춘다).
        /// </summary>
        public static List<string> PickSacrifices(IContentDb db, ProfileData profile, IReadOnlyList<string> template)
        {
            var picked = new List<string>();
            if (template == null || template.Count == 0) return picked;
            var used = new Dictionary<string, int>();
            for (int i = 0; i < template.Count; i++)
            {
                string want = template[i];
                string got = null;
                if (Free(profile, want, used) > 0) got = want;
                else
                {
                    BlockDef wantDef = db != null ? db.Block(want) : null;
                    if (wantDef != null)
                    {
                        IReadOnlyList<BlockDef> all = db.Blocks;
                        for (int k = 0; k < all.Count; k++)
                        {
                            BlockDef b = all[k];
                            if (b.Id == want || b.Rarity != wantDef.Rarity || b.Element != wantDef.Element) continue;
                            if (Free(profile, b.Id, used) > 0) { got = b.Id; break; }
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

        static int Free(ProfileData profile, string blockId, Dictionary<string, int> used)
        {
            if (profile == null || blockId == null || !profile.Items.TryGetValue(blockId, out ItemStack s)) return 0;
            used.TryGetValue(blockId, out int n);
            return s.Qty - s.QtySocketed - n;
        }
    }
}
