using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 시작 흐름(08 요약서 2절, 00 문서 4절 10번·8.1절)의 공용 규칙.
    /// Boot ─▶ (설정 없음) FirstRunPrompt ─▶ Title ─J─▶ (offline_guide_seen = false면 OfflineGuide) ─▶ MainMenu.
    /// - 인자는 ScreenArgKeys 상수와 8.1절 표의 형으로만 주고받는다: ProfileRestored(bool), PendingSettled(SettlementResult).
    /// - FirstRunPrompt는 둘 다 Title로 그대로 넘긴다. Title은 ProfileRestored를 한 번 쓰고(SaveRestored 창) PendingSettled만 넘긴다.
    ///   Title·ProfileSelect·OfflineGuide는 PendingSettled를 MainMenu까지 그대로 넘기고 MainMenu가 한 번 쓰고 버린다(D-68).
    /// - MainMenu에 닿을 때는 PopTo — 시작 화면을 전부 걷어 메인 메뉴가 맨 아래가 된다(Esc로 타이틀에 돌아가지 않는다.
    ///   프로필 전환 수단은 타이틀에만 있다, TV-005).
    /// </summary>
    public static class StartFlow
    {
        /// <summary>자동 정산 결과(없으면 null).</summary>
        public static SettlementResult Pending(ScreenArgs args) =>
            args != null ? args.Get<SettlementResult>(ScreenArgKeys.PendingSettled) : null;

        public static bool Restored(ScreenArgs args) =>
            args != null && args.Get(ScreenArgKeys.ProfileRestored, false);

        /// <summary>
        /// 다음 화면 인자: Entry = entry, PendingSettled는 있으면 그대로, ProfileRestored는 keepRestored일 때만(참일 때만 싣는다).
        /// </summary>
        public static ScreenArgs Forward(ScreenArgs from, ScreenId entry, bool keepRestored)
        {
            var next = ScreenArgs.From(entry);
            var pending = Pending(from);
            if (pending != null) next.With(ScreenArgKeys.PendingSettled, pending);
            if (keepRestored && Restored(from)) next.With(ScreenArgKeys.ProfileRestored, true);
            return next;
        }

        /// <summary>ProfileRestored를 꺼내고 지운다(창을 한 번만 띄운다).</summary>
        public static bool TakeRestored(ScreenArgs args)
        {
            bool r = Restored(args);
            if (args != null) args.Values.Remove(ScreenArgKeys.ProfileRestored);
            return r;
        }

        /// <summary>PendingSettled를 꺼내고 지운다(알림 창을 한 번만 띄운다).</summary>
        public static SettlementResult TakePending(ScreenArgs args)
        {
            var p = Pending(args);
            if (args != null) args.Values.Remove(ScreenArgKeys.PendingSettled);
            return p;
        }

        /// <summary>오프라인 안내를 먼저 보여야 하나(TV-028: offline_guide_seen이 false일 때 한 번).</summary>
        public static bool NeedsOfflineGuide(ProfileData profile) => profile != null && !profile.Flags.OfflineGuideSeen;

        /// <summary>
        /// 타이틀 J·프로필 고르기의 오프라인 카드 뒤: 처음이면 OfflineGuide(Push — Esc면 돌아온다), 아니면 MainMenu(PopTo).
        /// PendingSettled는 그대로 따라간다.
        /// </summary>
        public static StartNav EnterOffline(ProfileData profile, ScreenArgs from, ScreenId entry)
        {
            var args = Forward(from, entry, false);
            return NeedsOfflineGuide(profile)
                ? StartNav.Push(ScreenId.OfflineGuide, args)
                : StartNav.PopTo(ScreenId.MainMenu, args);
        }
    }
}
