namespace Wizard.Solo.Screens.Settings
{
    /// <summary>설정·키 재설정 화면의 이름 표 열쇠(원본 names.ko.json + NamesSolo/G24.ko.json).</summary>
    public static class SettingsKeys
    {
        public const string Title = "str.settings.title";
        public const string CategoryPrefix = "str.settings.category.";
        public const string OptionPrefix = "str.settings.opt.";
        public const string ValuePrefix = "str.settings.value.";
        /// <summary>G24 조각: 옵션 설명(설명 칸). 없는 열쇠면 설명 칸이 빈다.</summary>
        public const string DescPrefix = "str.settings.desc.";
        public const string Defaults = "str.settings.defaults", DefaultsConfirm = "str.settings.defaults_confirm",
            TraceNote = "str.settings.trace_note";
        public const string OptResolution = "str.settings.opt.resolution", OptKeyRebind = "str.settings.opt.key_rebind";
        /// <summary>G24 조각: 해상도 값 "{w} × {h}".</summary>
        public const string ResolutionValue = "str.settings.resolution_value";
        /// <summary>G24 조각: 전체 화면이면 해상도 줄이 꺼진 까닭.</summary>
        public const string ResolutionFullscreenReason = "str.settings.resolution_fullscreen";
        public const string AccountSavePath = "str.settings.account.save_path", AccountRestoreSave = "str.settings.account.restore_save";
        /// <summary>G24 조각: 되돌리기 확인 본문, 성공·실패 알림.</summary>
        public const string AccountRestoreConfirm = "str.settings.account.restore_confirm",
            AccountRestoreDone = "str.settings.account.restore_done", AccountRestoreFailed = "str.settings.account.restore_failed";
        public const string CommonClose = "str.common.close", CommonSave = "str.common.save", CommonCancel = "str.common.cancel";

        // ---- 키 재설정 (08 12.3절, 02 3.1절)
        public const string RebindTitle = "str.key_rebind.title", RebindContextCombat = "str.key_rebind.context.combat",
            RebindPrimary = "str.key_rebind.primary", RebindSecondary = "str.key_rebind.secondary",
            RebindCapture = "str.key_rebind.capture_prompt", RebindConflict = "str.key_rebind.conflict",
            RebindKoreanNote = "str.key_rebind.korean_note";
        public const string RebindActionPrefix = "str.key_rebind.action.";
        /// <summary>G24 조각: 일시정지 줄의 고정 칸 안내.</summary>
        public const string RebindPauseFixed = "str.key_rebind.pause_fixed";
        /// <summary>G24 조각: 겹친 키가 있어 [저장]이 꺼진 까닭.</summary>
        public const string RebindSaveBlocked = "str.key_rebind.save_blocked";
        /// <summary>G24 조각: 아래 안내(받기 전) — 칸을 고르면 키를 받고 K로 지운다.</summary>
        public const string RebindHelp = "str.key_rebind.help";
    }
}
