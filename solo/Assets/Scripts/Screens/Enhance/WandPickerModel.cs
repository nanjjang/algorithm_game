using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>강화 화면 고르기의 거르기 넷(08 10.2절). 여러 개면 AND [임시].</summary>
    [System.Flags]
    public enum WandFilter { None = 0, HasMutation = 1, HasExtSlot = 2, TradeLocked = 4, PlusZeroOnly = 8 }

    /// <summary>떠나보내기 판정(08 10.2절 CanRelease, 07 3.5절).</summary>
    public enum ReleaseBlock { None, LastWand, OnExpedition, MutationPending }

    /// <summary>
    /// WandPicker의 목록 규칙(08 10.2절, 5.3절, 07 3.4·3.5절). 줄 = IWandService.List(정렬은 서비스: 든 지팡이 맨 위 고정)에 거르기를 건 것.
    /// 거르기는 든 지팡이에도 같이 건다. 솔로에는 거래가 없어 "거래 잠금" 거르기를 켜면 줄이 비지 않게 하지 않고 그대로 아무것도 없다(늘 거짓).
    /// </summary>
    public sealed class WandPickerModel
    {
        public int Character = 1;
        public WandSort Sort = WandSort.StageDesc;
        public WandFilter Filters;
        public bool MultiMode;
        public readonly List<long> Checked = new List<long>();
        public readonly List<WandListEntry> Rows = new List<WandListEntry>();
        readonly EnhanceScreenTuning _t;

        public WandPickerModel(EnhanceScreenTuning t) { _t = t; }

        public static readonly WandSort[] SortOrder = { WandSort.StageDesc, WandSort.StageAsc, WandSort.Recent };

        public static string SortKey(WandSort s) =>
            s == WandSort.StageAsc ? EnhanceNameKeys.SortStageAsc : s == WandSort.Recent ? EnhanceNameKeys.SortRecent : EnhanceNameKeys.SortStageDesc;

        public static bool Passes(WandData w, WandFilter f)
        {
            if (w == null) return false;
            if ((f & WandFilter.HasMutation) != 0 && (w.Mutations == null || w.Mutations.Count == 0)) return false;
            if ((f & WandFilter.HasExtSlot) != 0 && w.SlotsExtra <= 0) return false;
            if ((f & WandFilter.TradeLocked) != 0) return false;   // 솔로: 거래 잠금은 늘 거짓
            if ((f & WandFilter.PlusZeroOnly) != 0 && w.Stage != 0) return false;
            return true;
        }

        /// <summary>목록을 다시 만든다.</summary>
        public void Refresh(IWandService wands)
        {
            Rows.Clear();
            if (wands == null) return;
            foreach (var e in wands.List(Character, Sort))
                if (e != null && Passes(e.Wand, Filters)) Rows.Add(e);
            Checked.RemoveAll(id => !Rows.Exists(r => r.Wand.Id == id));
        }

        /// <summary>그 캐릭터의 지팡이 수(탭의 "n자루", 거르기 전).</summary>
        public static int CountOf(IWandService wands, int character) => wands != null ? wands.List(character, WandSort.StageDesc).Count : 0;

        /// <summary>CanRelease(TV-146): 마지막 한 자루 / 원정 중 / 변이 결정 대기.</summary>
        public static ReleaseBlock CanRelease(WandListEntry e, int handCount)
        {
            if (e == null) return ReleaseBlock.LastWand;
            if (handCount <= 1) return ReleaseBlock.LastWand;
            if (e.Locked) return ReleaseBlock.OnExpedition;
            if (e.Wand.PendingOffer != null) return ReleaseBlock.MutationPending;
            return ReleaseBlock.None;
        }

        public static string ReleaseReasonKey(ReleaseBlock b) =>
            b == ReleaseBlock.LastWand ? EnhanceNameKeys.PickerReasonLastWand
            : b == ReleaseBlock.OnExpedition ? EnhanceNameKeys.PickerReasonOnExpedition
            : b == ReleaseBlock.MutationPending ? EnhanceNameKeys.ErrorKey(ErrorCode.MutationPending) : null;

        /// <summary>여러 자루 체크(TV-150): CanRelease && +3 이하 && 변이 0 && 확장 0.</summary>
        public bool Checkable(WandListEntry e, int handCount)
        {
            if (CanRelease(e, handCount) != ReleaseBlock.None) return false;
            var w = e.Wand;
            return w.Stage <= _t.ReleaseMultiMaxStage && (w.Mutations != null ? w.Mutations.Count : 0) <= _t.ReleaseMultiMaxMutations
                && w.SlotsExtra <= _t.ReleaseMultiMaxSlotsExtra;
        }

        /// <summary>체크를 켜거나 끈다. 고른 묶음이 그 캐릭터의 손에 있는 지팡이를 모두 포함하게 되는 마지막 체크는 막는다.</summary>
        public bool ToggleCheck(WandListEntry e, int handCount)
        {
            if (e == null) return false;
            long id = e.Wand.Id;
            if (Checked.Contains(id)) { Checked.Remove(id); return true; }
            if (!Checkable(e, handCount)) return false;
            int sameChar = 0;
            foreach (var r in Rows) if (Checked.Contains(r.Wand.Id) && r.Wand.Character == e.Wand.Character) sameChar++;
            if (sameChar + 1 >= handCount) return false;
            Checked.Add(id);
            return true;
        }

        /// <summary>떠나보내면 돌려받는 골드(07 7.4절 표: 감정가 × 10% 내림, +0은 0 — TV-148).</summary>
        public static long RefundOf(EconomyConfig econ, WandData w)
        {
            if (econ == null || w == null || econ.ReleaseRefundByStage == null || econ.ReleaseRefundByStage.Count == 0) return 0;
            int i = w.Stage < 0 ? 0 : (w.Stage >= econ.ReleaseRefundByStage.Count ? econ.ReleaseRefundByStage.Count - 1 : w.Stage);
            return econ.ReleaseRefundByStage[i];
        }

        public long CheckedRefund(EconomyConfig econ)
        {
            long sum = 0;
            foreach (var r in Rows) if (Checked.Contains(r.Wand.Id)) sum += RefundOf(econ, r.Wand);
            return sum;
        }
    }

    /// <summary>떠나보내기 확인 창의 규칙(07 3.5절, TV-149): +7 이상 한 자루는 이름을 그대로 입력해야 [떠나보내기]가 켜진다.</summary>
    public static class WandReleaseRules
    {
        public static bool NeedsName(EnhanceScreenTuning t, IReadOnlyList<WandData> wands) =>
            wands != null && wands.Count == 1 && wands[0] != null && wands[0].Stage >= t.ReleaseNameConfirmMinStage;

        /// <summary>앞뒤 공백을 지운 뒤 이름 표 글자와 정확히 같다 [임시].</summary>
        public static bool NameMatches(string typed, string expected) =>
            typed != null && expected != null && string.Equals(typed.Trim(), expected.Trim(), System.StringComparison.Ordinal);

        public static bool CanConfirm(EnhanceScreenTuning t, IReadOnlyList<WandData> wands, string typed, INameTable names)
        {
            if (wands == null || wands.Count == 0) return false;
            if (!NeedsName(t, wands)) return true;
            return NameMatches(typed, EnhanceNameKeys.WandName(names, wands[0].Character));
        }
    }
}
