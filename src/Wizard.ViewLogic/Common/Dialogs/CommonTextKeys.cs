namespace Wizard.ViewLogic.Common.Dialogs
{
    // 공용 창이 찍는 글의 이름 표 열쇠(data/names/names.ko.json). spec 13 0.3 "str.<screen>.<name>", 1.5절의 문구가 시작값
    public static class CommonTextKeys
    {
        public const string Yes = "str.common.yes";
        public const string No = "str.common.no";
        public const string Ok = "str.common.ok";
        public const string Close = "str.common.close";

        public const string DisconnectedMessage = "str.disconnected.message";
        public const string DisconnectedRetry = "str.disconnected.retry";
        public const string DisconnectedGoOffline = "str.disconnected.go_offline";
        public const string DisconnectedRunNote = "str.disconnected.run_note";
        public const string DisconnectedReconnecting = "str.disconnected.reconnecting";

        public const string RunSubmitPendingMessage = "str.run_submit_pending.message";

        public const string SaveRestoredMessage = "str.save_restored.message";

        public const string SanctionPermanent = "str.sanction_notice.permanent";

        public const string TooltipTradable = "str.tooltip.tradable";
        public const string TooltipUntradable = "str.tooltip.untradable";

        public static readonly string[] All =
        {
            Yes, No, Ok, Close,
            DisconnectedMessage, DisconnectedRetry, DisconnectedGoOffline, DisconnectedRunNote, DisconnectedReconnecting,
            RunSubmitPendingMessage, SaveRestoredMessage, SanctionPermanent, TooltipTradable, TooltipUntradable,
        };
    }
}
