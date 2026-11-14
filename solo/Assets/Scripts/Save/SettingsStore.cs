using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Save
{
    /// <summary>
    /// 기기 설정 저장소(08 요약서 12절, 10 요약서 7절, 00 문서 11절). 프로필과 따로 &lt;뿌리&gt;/settings.json.
    /// 파일이 없으면 기기 첫 실행(ExistedAtLoad = false → FirstRunPrompt). 기본값: 키 배치 = KeyLayouts.Default,
    /// Options = 빈 사전(옵션의 기본값은 SettingsOptions가 준다 — 여기 다시 적지 않는다).
    /// 쓰기는 임시 파일 → 이름 바꾸기. Save 때 Changed가 난다.
    /// </summary>
    public sealed class SettingsStore : ISettingsStore
    {
        readonly string _path;

        public SettingsData Current { get; private set; }
        public bool ExistedAtLoad { get; private set; }
        public event Action Changed;

        public SettingsStore(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath is empty", nameof(filePath));
            _path = filePath;
            Current = CreateDefault();
        }

        public string FilePath => _path;
        string TempPath => _path + ProfileStore.TempSuffix;

        /// <summary>처음 실행 때의 설정.</summary>
        public static SettingsData CreateDefault()
        {
            var s = new SettingsData();
            s.KeyPrimary = new List<int>(KeyLayouts.DefaultPrimary);
            s.KeySecondary = new List<int>(KeyLayouts.DefaultSecondary);
            s.Options = new Dictionary<string, JToken>();
            return s;
        }

        public void Load()
        {
            TryDelete(TempPath);
            ExistedAtLoad = File.Exists(_path);
            SettingsData loaded = null;
            if (ExistedAtLoad)
            {
                // 깨진 설정 파일은 기본값으로 연다(첫 실행은 아니다 — 물음을 다시 띄우지 않는다).
                try { loaded = SaveJson.Deserialize<SettingsData>(ProfileFile.ReadText(_path)); }
                catch (Exception) { loaded = null; }
            }
            if (loaded == null) loaded = CreateDefault();
            if (loaded.Options == null) loaded.Options = new Dictionary<string, JToken>();
            KeyLayouts.Normalize(loaded);
            Current = loaded;
        }

        public void Save()
        {
            if (Current == null) Current = CreateDefault();
            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ProfileFile.WriteTemp(TempPath, SaveJson.Serialize(Current) + "\n");
            ProfileFile.ReplaceWith(TempPath, _path);
            Changed?.Invoke();
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
