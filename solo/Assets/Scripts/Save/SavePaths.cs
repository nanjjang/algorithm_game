using System.IO;
using UnityEngine;

namespace Wizard.Solo.Save
{
    /// <summary>
    /// 저장 경로(00 문서 11절). 뿌리 = ctx.DataRoot(null이면 Application.persistentDataPath).
    /// 프로필 = &lt;뿌리&gt;/profiles/offline/profile.json(+ .1 .2 .3), 설정 = &lt;뿌리&gt;/settings.json.
    /// 오프라인 프로필 폴더 하나뿐이다(서버 프로필로 옮기는 길은 없다 — DECISIONS 불변식).
    /// </summary>
    public static class SavePaths
    {
        public const string ProfilesFolder = "profiles";
        public const string OfflineFolder = "offline";
        public const string ProfileFileName = "profile.json";
        public const string SettingsFileName = "settings.json";

        /// <summary>뿌리 폴더. dataRoot가 비었으면 persistentDataPath(테스트는 늘 임시 폴더를 준다).</summary>
        public static string Root(string dataRoot) =>
            string.IsNullOrEmpty(dataRoot) ? Application.persistentDataPath : dataRoot;

        public static string ProfileDirectory(string dataRoot) =>
            Path.Combine(Path.Combine(Root(dataRoot), ProfilesFolder), OfflineFolder);

        public static string SettingsFile(string dataRoot) => Path.Combine(Root(dataRoot), SettingsFileName);
    }
}
