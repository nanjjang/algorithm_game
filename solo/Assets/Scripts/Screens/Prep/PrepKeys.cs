namespace Wizard.Solo.Screens.Prep
{
    /// <summary>
    /// 원정 준비 화면(08 요약서 5절)이 쓰는 이름 표 열쇠와 화면 사건 소리 열쇠.
    /// 원본 names.ko.json에 있는 것 + 이 작업의 조각(Resources/NamesSolo/G19.ko.json)에 더한 것(Mutations, Scars).
    /// </summary>
    public static class PrepKeys
    {
        // ---- 이름 표 (원본)
        public const string Title = "str.expedition_prep.title";
        public const string BlockedCoreEmpty = "str.expedition_prep.blocked.core_empty";
        public const string BlockedOnExpedition = "str.expedition_prep.blocked.on_expedition";
        public const string ChangeLoadout = "str.expedition_prep.change_loadout";
        public const string Depart = "str.expedition_prep.depart";
        public const string HeldTag = "str.expedition_prep.held_tag";
        public const string Loadout = "str.expedition_prep.loadout";
        public const string SetHeld = "str.expedition_prep.set_held";
        public const string StartWaveLabel = "str.expedition_prep.start_wave.label";
        public const string StartWaveOption = "str.expedition_prep.start_wave.option";
        public const string StartWaveScoreNote = "str.expedition_prep.start_wave.score_note";
        public const string WandCount = "str.expedition_prep.wand_count";
        public const string Wands = "str.expedition_prep.wands";
        public const string FormatStartWave = "str.format.start_wave";
        public const string FormatWandStage = "str.format.wand_stage";
        public const string FormatSeconds = "str.format.seconds";
        public const string FormatRatio = "str.format.ratio";
        public const string EnhanceExtSlots = "str.enhance.ext_slots";
        public const string RevengeShort = "revenge.tracker.short";
        public const string WarnCoreEmpty = "warn.core_empty";
        public const string CommonEmpty = "str.common.empty";
        public const string CommonNone = "str.common.none";
        public const string StatPassive = "str.stat.passive";
        public const string StatWandTrait = "str.stat.wand_trait";
        public const string StatUniqueArt = "str.stat.unique_art";
        public const string SortStageDesc = "str.wand_list.sort.stage_desc";
        public const string SortStageAsc = "str.wand_list.sort.stage_asc";
        public const string SortRecent = "str.wand_list.sort.recent";
        public const string ErrorNotFound = "error.not_found";

        // ---- 이름 표 (G19 조각)
        /// <summary>"변이 {n}/{max}".</summary>
        public const string Mutations = "str.expedition_prep.mutations";
        /// <summary>"흔적 {n}" — 하락 흔적 수(내 화면에서는 공개 여부와 상관없이 본인에게 보인다).</summary>
        public const string Scars = "str.expedition_prep.scars";

        // ---- 화면 사건 소리(08 요약서 5.5절 이벤트 줄, event_bindings.json)
        public const string SndCharacterConfirm = "ui.prep.character_confirm";
        public const string SndWandSelect = "ui.prep.wand_select";
        public const string SndHeldWandSet = "ui.prep.held_wand_set";
        public const string SndDepartBlocked = "ui.prep.depart_blocked";
        public const string SndDepart = "ui.prep.depart";

        /// <summary>정렬 이름 열쇠(08 5.3절. 계약의 WandSort 셋만 — IL 순은 솔로에 없다).</summary>
        public static string SortKey(Wizard.Solo.Meta.WandSort sort)
        {
            switch (sort)
            {
                case Wizard.Solo.Meta.WandSort.StageAsc: return SortStageAsc;
                case Wizard.Solo.Meta.WandSort.Recent: return SortRecent;
                default: return SortStageDesc;
            }
        }
    }
}
