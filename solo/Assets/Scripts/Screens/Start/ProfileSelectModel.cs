using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 프로필 고르기(08 요약서 3.3절). 카드 둘: 오프라인(동작) / 서버(솔로 제외 — 보이되 꺼짐 + error.not_supported_offline, N-03 [임시]).
    /// 오프라인 카드 → (처음이면 OfflineGuide) → MainMenu. 오프라인 프로필을 서버로 옮기는 길은 없다(옮기는 기능을 만들지 않는다).
    /// </summary>
    public static class ProfileSelectModel
    {
        public const int CardCount = 2;
        public static readonly string[] CardNameKeys = { StartNameKeys.ProfileCardOffline, StartNameKeys.ProfileCardServer };
        public static readonly string[] CardDescKeys = { StartNameKeys.ProfileCardOfflineDesc, StartNameKeys.ProfileCardServerDesc };
        public static readonly ProfileKind[] CardKinds = { ProfileKind.Offline, ProfileKind.Server };
        /// <summary>S-0301 카드 그림 변형.</summary>
        public static readonly string[] CardArtVariants = { "offline_card", "server_card" };

        /// <summary>그 카드가 동작하나. 솔로 빌드는 오프라인만.</summary>
        public static bool CardEnabled(ProfileKind kind) => kind == ProfileKind.Offline;

        /// <summary>꺼진 카드의 이유 열쇠(켜졌으면 null).</summary>
        public static string CardDisabledReason(ProfileKind kind) => CardEnabled(kind) ? null : StartNameKeys.ErrorNotSupportedOffline;

        /// <summary>카드를 골랐다. 꺼진 카드는 아무 데도 가지 않는다.</summary>
        public static StartNav Pick(ProfileKind kind, ProfileData profile, ScreenArgs args)
        {
            if (!CardEnabled(kind)) return StartNav.None;
            return StartFlow.EnterOffline(profile, args, ScreenId.ProfileSelect);
        }
    }
}
