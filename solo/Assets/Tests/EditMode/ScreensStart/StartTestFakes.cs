using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Tests.ScreensStart
{
    /// <summary>저장 횟수를 세는 설정 저장소(파일 없음).</summary>
    public sealed class CountingSettingsStore : ISettingsStore
    {
        public SettingsData Current { get; set; } = new SettingsData { Options = new Dictionary<string, JToken>() };
        public bool ExistedAtLoad { get; set; }
        public int Saves;
        public event Action Changed;
        public void Load() { }
        public void Save() { Saves++; Changed?.Invoke(); }
    }

    /// <summary>저장 횟수를 세는 프로필 저장소(파일 없음).</summary>
    public sealed class CountingProfileStore : IProfileStore
    {
        public ProfileData Current { get; set; }
        public int Saves;
        public string Directory => string.Empty;
        public event Action Saved;
        public CountingProfileStore(ProfileData p) { Current = p; }
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { Saves++; Saved?.Invoke(); }
        public bool RestorePrevious() => false;
    }

    public static class StartTestData
    {
        /// <summary>캐릭터 다섯(최고 웨이브를 차례로 넣는다) 오프라인 프로필.</summary>
        public static ProfileData Profile(params int[] bestWaves)
        {
            var p = new ProfileData();
            for (int i = 0; i < 5; i++)
                p.Characters.Add(new CharacterRecord { Id = i + 1, BestWave = i < bestWaves.Length ? bestWaves[i] : 0 });
            return p;
        }
    }
}
