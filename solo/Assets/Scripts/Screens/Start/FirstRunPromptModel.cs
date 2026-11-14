using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 첫 실행 물음(08 요약서 3.1절, S13 2.2): 연출 감도 전체/줄임/최소. 상태 Asking → Saved.
    /// - 고르면 바로 motion_preset 저장(SettingsOptions.SetEffectsLevel + ISettingsStore.Save) → Title로 Replace. 확인 단계 없음.
    /// - 커서 기본 = 전체 [임시](U-13). Esc는 저장하지 않고 Title로 간다(다음 실행에 다시 묻는다 [임시]).
    /// - 받은 ProfileRestored·PendingSettled는 그대로 Title에 넘긴다(00 문서 8.1절).
    /// - 번쩍임 없는 화면: 번쩍 관리기 요청을 내지 않는다(TV-002) — 이 화면은 연출을 하나도 두지 않는다.
    /// </summary>
    public sealed class FirstRunPromptModel
    {
        /// <summary>선택지 순서 = 연출 감도 0 전체 / 1 줄임 / 2 최소(SettingsOptions.EffectsLevel과 같은 번호).</summary>
        public static readonly string[] OptionKeys =
            { StartNameKeys.FirstRunFull, StartNameKeys.FirstRunReduced, StartNameKeys.FirstRunMinimal };
        public static readonly string[] OptionDescKeys =
            { StartNameKeys.FirstRunFullDesc, StartNameKeys.FirstRunReducedDesc, StartNameKeys.FirstRunMinimalDesc };
        public const int DefaultCursor = 0;

        public bool Saved { get; private set; }

        /// <summary>선택 확정: 즉시 저장하고 Title로. 이미 저장했으면 아무것도 하지 않는다(한 번만).</summary>
        public StartNav Choose(ISettingsStore settings, int level, ScreenArgs incoming)
        {
            if (Saved) return StartNav.None;
            if (level < 0) level = 0;
            if (level >= OptionKeys.Length) level = OptionKeys.Length - 1;
            if (settings != null && settings.Current != null)
            {
                SettingsOptions.SetEffectsLevel(settings.Current, level);
                settings.Save();
            }
            Saved = true;
            return StartNav.Replace(ScreenId.Title, StartFlow.Forward(incoming, ScreenId.FirstRunPrompt, true));
        }

        /// <summary>Esc: 저장하지 않고 Title로(인자는 그대로).</summary>
        public StartNav Skip(ScreenArgs incoming)
        {
            if (Saved) return StartNav.None;
            Saved = true;
            return StartNav.Replace(ScreenId.Title, StartFlow.Forward(incoming, ScreenId.FirstRunPrompt, true));
        }

        /// <summary>화면이 다시 열리면 다시 묻는다.</summary>
        public void Reset() { Saved = false; }
    }
}
