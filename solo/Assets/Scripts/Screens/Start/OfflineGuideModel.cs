using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 오프라인 안내(08 요약서 3.4절, S13 4.1). 세 줄 + 되는 것·안 되는 것 표 + [확인].
    /// - 시작 흐름(Title·ProfileSelect에서): offline_guide_seen이 false일 때 한 번(TV-028). [확인] → seen = true 저장 → MainMenu(PendingSettled 넘김).
    /// - 메인 메뉴의 서버 전용 묶음에서(Entry = MainMenu): 언제나 다시 본다. [확인] → 닫기(메인 메뉴로).
    /// - 안 되는 것은 흐리게 하지 않고 빈 동그라미로 그린다.
    /// </summary>
    public static class OfflineGuideModel
    {
        public static readonly string[] LineKeys =
        {
            "str.offline_guide.line.1", "str.offline_guide.line.2", "str.offline_guide.line.3"
        };
        public static readonly string[] CanKeys =
        {
            "str.offline_guide.can.endless", "str.offline_guide.can.assembly", "str.offline_guide.can.enhance",
            "str.offline_guide.can.practice", "str.offline_guide.can.records", "str.offline_guide.can.shop",
            "str.offline_guide.can.wardrobe"
        };
        public static readonly string[] CannotKeys =
        {
            "str.offline_guide.cannot.duel", "str.offline_guide.cannot.plaza", "str.offline_guide.cannot.mail",
            "str.offline_guide.cannot.ranking", "str.offline_guide.cannot.season", "str.offline_guide.cannot.trade"
        };

        /// <summary>메인 메뉴에서 다시 보는 중인가.</summary>
        public static bool IsRevisit(ScreenArgs args) => args != null && args.Entry == ScreenId.MainMenu;

        /// <summary>
        /// [확인]: 처음 보는 것이면 offline_guide_seen = true로 저장하고, 시작 흐름이면 MainMenu로(PopTo), 다시 보기면 닫는다.
        /// </summary>
        public static StartNav Confirm(IProfileStore profiles, ScreenArgs args)
        {
            var p = profiles != null ? profiles.Current : null;
            if (p != null && !p.Flags.OfflineGuideSeen)
            {
                p.Flags.OfflineGuideSeen = true;
                profiles.Save();
            }
            if (IsRevisit(args)) return StartNav.Close();
            return StartNav.PopTo(ScreenId.MainMenu, StartFlow.Forward(args, ScreenId.OfflineGuide, false));
        }
    }
}
