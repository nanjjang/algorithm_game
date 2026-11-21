using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 묶음 화면이 쓰는 이름 표 열쇠. 원본(names.ko.json)에 있는 것은 그대로, 없는 것은 NamesSolo/G23.ko.json에 있다.
    /// 값이 정해지지 않은 열쇠(캐릭터 한마디, 강화 안내 본문, 변이 이득·대가 설명)는 이름 표에 없으면 그 줄을 숨긴다(08 N-05, 00 D-66).
    /// </summary>
    public static class EnhanceNameKeys
    {
        // 원본
        public const string Title = "str.enhance.title", Enhance = "str.enhance.enhance", Retry = "str.enhance.retry";
        public const string Slots = "str.enhance.slots", ExtSlots = "str.enhance.ext_slots", Mutations = "str.enhance.mutations";
        public const string BestStage = "str.enhance.best_stage", TracePublic = "str.enhance.trace_public", TraceHiddenNote = "str.enhance.trace_hidden_note";
        public const string Sacrifice = "str.enhance.sacrifice", Charm = "str.enhance.charm", CharmNotNeeded = "str.enhance.charm_not_needed";
        public const string ProbBase = "str.enhance.prob.base", ProbSacrifice = "str.enhance.prob.sacrifice", ProbSameElement = "str.enhance.prob.same_element",
            ProbAura = "str.enhance.prob.aura", ProbCap = "str.enhance.prob.cap";
        public const string HintNoDropLow = "str.enhance.hint.no_drop_low", HintAtFloor = "str.enhance.hint.at_floor",
            HintDropTo = "str.enhance.hint.drop_to", HintCharmProtects = "str.enhance.hint.charm_protects";
        public const string ExpectTarget = "str.enhance.expect.target", ExpectAvgTries = "str.enhance.expect.avg_tries",
            ExpectAvgGold = "str.enhance.expect.avg_gold", ExpectAvgStone = "str.enhance.expect.avg_stone", ExpectWithOwned = "str.enhance.expect.with_owned";
        public const string Candidates = "str.enhance.candidates", Cost = "str.enhance.cost", FastMode = "str.enhance.fast_mode",
            AutoContinue = "str.enhance.auto_continue", Tally = "str.enhance.tally", DropPossible = "str.enhance.drop_possible";
        public const string Engrave = "str.enhance.engrave", RemoveMutation = "str.enhance.remove_mutation";
        public const string CannotPlace = "str.enhance.cannot_place", LastProjectileTip = "str.enhance.last_projectile_tip";
        public const string LineFailCharm = "str.enhance.line.fail_charm", LineFailDrop = "str.enhance.line.fail_drop",
            LineFailFloor = "str.enhance.line.fail_floor", LineFailNoDrop = "str.enhance.line.fail_no_drop", LineRoll = "str.enhance.line.roll",
            LineServerNotified = "str.enhance.line.server_notified", LineSlotFull = "str.enhance.line.slot_full";
        public const string Milestone = "str.enhance.milestone";
        public const string MutationAccept = "str.enhance.mutation.accept", MutationReject = "str.enhance.mutation.reject",
            MutationPushOut = "str.enhance.mutation.push_out";
        public const string TraceBanner = "str.enhance.trace_banner", TraceStateHidden = "str.enhance.trace_state_hidden",
            TraceStatePublic = "str.enhance.trace_state_public";
        public const string TutorialTitle = "str.enhance_tutorial.title", TutorialPage = "str.enhance_tutorial.page";
        public const string FormatBound = "str.format.bound_count", FormatWandStage = "str.format.wand_stage",
            FormatChange = "str.format.score_change", FormatRatio = "str.format.ratio", FormatPercent = "str.format.percent",
            FormatGold = "str.format.gold_amount", FormatWandCount = "str.format.wand_count";
        public const string ErrorMaxStage = "error.max_stage", ErrorNotEligible = "error.not_eligible";
        public const string CommonNext = "str.common.next", CommonPrev = "str.common.prev", CommonClose = "str.common.close",
            CommonOn = "str.common.on", CommonOff = "str.common.off", CommonNone = "str.common.none";
        public const string PickerTitle = "str.wand_picker.title", PickerRelease = "str.wand_picker.release",
            PickerReleaseMulti = "str.wand_picker.release_multi", PickerReleaseSummary = "str.wand_picker.release_summary",
            PickerReasonLastWand = "str.wand_picker.reason.last_wand", PickerReasonOnExpedition = "str.wand_picker.reason.on_expedition";
        public const string ListCount = "str.wand_list.count", ListHeldTag = "str.wand_list.held_tag", ListSetHeld = "str.wand_list.set_held";
        public const string SortStageDesc = "str.wand_list.sort.stage_desc", SortStageAsc = "str.wand_list.sort.stage_asc",
            SortRecent = "str.wand_list.sort.recent";
        public const string FilterHasMutation = "str.wand_list.filter.has_mutation", FilterHasExtSlot = "str.wand_list.filter.has_ext_slot",
            FilterTradeLocked = "str.wand_list.filter.trade_locked", FilterPlusZero = "str.wand_list.filter.plus_zero_only";
        public const string ReleaseTitle = "str.wand_release_confirm.title", ReleaseConfirm = "str.wand_release_confirm.confirm",
            ReleaseIrreversible = "str.wand_release_confirm.irreversible", ReleaseRefund = "str.wand_release_confirm.refund",
            ReleaseReturns = "str.wand_release_confirm.returns", ReleaseTypeName = "str.wand_release_confirm.type_name",
            ReleaseMultiSummary = "str.wand_release_confirm.multi_summary";
        public const string EngraveTitle = "str.slot_engrave.title", MutationRemoveTitle = "str.mutation_remove.title";
        public const string StoneLow = "mat.stone_low", StoneMid = "mat.stone_mid", StoneHigh = "mat.stone_high";
        /// <summary>칩에 쓰는 강화석 짧은 이름(하/중/상).</summary>
        public const string StoneShortLow = "str.enhance.stone_short.low", StoneShortMid = "str.enhance.stone_short.mid", StoneShortHigh = "str.enhance.stone_short.high";

        // 솔로 조각(G23.ko.json)
        public const string SuccessRate = "str.enhance.prob.total", FailRate = "str.enhance.prob.failure";
        public const string FloorStage = "str.enhance.floor_stage";
        public const string StopOnDrop = "str.enhance.stop_on_drop";
        public const string LineAura = "str.enhance.line.aura", LineSlotExtended = "str.enhance.line.slot_extended",
            LineStage = "str.enhance.line.stage";
        public const string AutoStopPrefix = "str.enhance.auto_stop.";
        public const string CharLinePrefix = "str.enhance.char_line.";
        public const string MutationGainPrefix = "str.enhance.mutation_gain.", MutationCostPrefix = "str.enhance.mutation_cost.";
        public const string MutationPending = "str.wand_picker.pending_mutation";
        public const string ReleaseCancel = "str.wand_release_confirm.cancel";
        public const string EngraveChance = "str.slot_engrave.chance", EngraveCondition = "str.slot_engrave.condition",
            EngraveFailMarks = "str.slot_engrave.fail_marks", EngraveCost = "str.slot_engrave.cost", EngraveResultOk = "str.slot_engrave.result_ok",
            EngraveResultFail = "str.slot_engrave.result_fail";
        public const string RemoveCost = "str.mutation_remove.cost", RemovePick = "str.mutation_remove.pick",
            RemoveConfirm = "str.mutation_remove.confirm", RemoveNone = "str.mutation_remove.none";

        /// <summary>결과 카드 캐릭터 한마디의 outcome 이름(D-66).</summary>
        public static string OutcomeName(EnhanceOutcome o)
        {
            switch (o)
            {
                case EnhanceOutcome.GreatSuccess: return "great_success";
                case EnhanceOutcome.Success: return "success";
                case EnhanceOutcome.FailDrop: return "fail_drop";
                case EnhanceOutcome.FailCharmBlocked: return "fail_charm_blocked";
                case EnhanceOutcome.FailNoDrop: return "fail_no_drop";
                default: return "mutation";
            }
        }

        /// <summary>str.enhance.char_line.&lt;outcome&gt;.</summary>
        public static string CharLine(EnhanceOutcome o) => CharLinePrefix + OutcomeName(o);

        /// <summary>str.enhance_tutorial.page.N.body(N = 1..4).</summary>
        public static string TutorialBody(int page) => "str.enhance_tutorial.page." + page + ".body";

        /// <summary>결과 제목 열쇠(10.7절 표).</summary>
        public static string ResultTitle(EnhanceOutcome o)
        {
            switch (o)
            {
                case EnhanceOutcome.GreatSuccess: return "str.enhance.result.great_success";
                case EnhanceOutcome.Success: return "str.enhance.result.success";
                case EnhanceOutcome.FailDrop: return "str.enhance.result.fail_drop";
                case EnhanceOutcome.FailCharmBlocked: return "str.enhance.result.fail_charm";
                case EnhanceOutcome.FailNoDrop: return "str.enhance.result.fail_no_drop";
                default: return "str.enhance.result.mutation";
            }
        }

        /// <summary>그 열쇠가 이름 표에 있으면 글, 없으면 null(줄을 숨긴다).</summary>
        public static string IfPresent(INameTable names, string key) => names != null && names.Has(key) ? names.Get(key) : null;

        /// <summary>오류 코드 → error.&lt;snake_case&gt;.</summary>
        public static string ErrorKey(ErrorCode e)
        {
            string n = e.ToString();
            var sb = new System.Text.StringBuilder("error.");
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (char.IsUpper(c)) { if (i > 0) sb.Append('_'); sb.Append(char.ToLowerInvariant(c)); }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>"+N" 글.</summary>
        public static string Stage(INameTable names, int stage) =>
            names != null ? names.Format(FormatWandStage, "n", stage) : "+" + stage;

        /// <summary>지팡이 이름(wand.N).</summary>
        public static string WandName(INameTable names, int character) =>
            names != null ? names.Get(Ids.Char.WandKindOf(character)) : Ids.Char.WandKindOf(character);

        /// <summary>단계 별명 wand.N.stage.S.nickname.</summary>
        public static string Nickname(INameTable names, int character, int stage)
        {
            string key = Ids.Char.WandKindOf(character) + ".stage." + stage + ".nickname";
            return names != null ? names.Get(key) : key;
        }

        /// <summary>칭호 title.stage.N.bK(b4의 {char.N} 자리를 캐릭터 표시 이름으로 채운다).</summary>
        public static string StageTitle(INameTable names, EnhanceConfig cfg, int character, int stage)
        {
            int band = 0;
            if (cfg != null && cfg.Milestones != null)
                for (int i = 0; i < cfg.Milestones.Count; i++) if (cfg.Milestones[i] <= stage) band++;
            string key = "title.stage." + character + ".b" + band;
            if (names == null) return key;
            string charId = Ids.Char.Of(character);
            return names.Format(key, charId, names.Get(charId));
        }
    }
}
