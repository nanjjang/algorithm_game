using UnityEngine;
using Wizard.Solo.Meta;

namespace Wizard.Solo
{
    /// <summary>F4 기반 서비스 등록(00 문서 2.1절): 시계, 설정, 프로필, 소리. 경로 뿌리 = ctx.DataRoot(null = persistentDataPath).</summary>
    public static partial class AppFactory
    {
        static partial void CreateClock(ref IClock clock)
        {
            clock = new global::Wizard.Solo.Save.SystemClock();
        }

        static partial void CreateSettings(GameContext ctx, ref ISettingsStore settings)
        {
            settings = new global::Wizard.Solo.Save.SettingsStore(global::Wizard.Solo.Save.SavePaths.SettingsFile(ctx.DataRoot));
        }

        static partial void CreateProfiles(GameContext ctx, ref IProfileStore profiles)
        {
            var content = ctx.Content;
            var clock = ctx.Clock;
            profiles = new global::Wizard.Solo.Save.ProfileStore(
                global::Wizard.Solo.Save.SavePaths.ProfileDirectory(ctx.DataRoot),
                () => global::Wizard.Solo.Save.NewProfile.Create(content, clock),
                content != null ? content.DataVersion : null);
        }

        static partial void CreateSound(GameContext ctx, ref ISoundPlayer sound)
        {
            // 실제 클립 재생기(Scripts/Audio/UnitySoundPlayer). 섞는 규칙의 시각은 실제 경과 시간(일시정지·Time.timeScale과 무관).
            sound = global::Wizard.Solo.Audio.UnitySoundPlayer.Create(ctx);
        }
    }
}
