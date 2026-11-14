using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Save
{
    /// <summary>
    /// 오프라인 프로필 저장소(07 요약서 9.3절, 01 요약서 3.3절, 00 문서 11절).
    /// &lt;dir&gt;/profile.json + 이전 세대 .1 .2 .3. 쓰기 = 임시 파일 → 세대 밀기(.2→.3, .1→.2, 현재→.1) → 이름 바꾸기.
    /// 읽기 = 해시가 안 맞거나 파싱이 안 되면 .1 → .2 → .3 순으로 되돌린다(restored). 다 깨졌으면 새 프로필.
    /// 기기 안에서만 읽고 쓴다(서버로 올리거나 옮기는 길이 없다 — TV-PF-06, DECISIONS 불변식).
    /// </summary>
    public sealed class ProfileStore : IProfileStore
    {
        /// <summary>보관하는 이전 세대 수(.1 .2 .3).</summary>
        public const int Generations = 3;
        public const string TempSuffix = ".tmp";

        readonly string _dir;
        readonly string _path;
        readonly Func<ProfileData> _newProfile;
        readonly string _dataVersion;

        public ProfileData Current { get; private set; }
        public string Directory => _dir;
        public event Action Saved;

        /// <param name="directory">저장 폴더(보통 SavePaths.ProfileDirectory(ctx.DataRoot). 테스트는 임시 폴더).</param>
        /// <param name="newProfile">파일이 없거나 전부 깨졌을 때 쓸 새 프로필(NewProfile.Create).</param>
        /// <param name="dataVersion">저장 때 data_version에 적을 값(ctx.Content.DataVersion). null이면 프로필의 값을 그대로 둔다.</param>
        public ProfileStore(string directory, Func<ProfileData> newProfile, string dataVersion)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory is empty", nameof(directory));
            _dir = directory;
            _path = Path.Combine(directory, SavePaths.ProfileFileName);
            _newProfile = newProfile ?? (() => new ProfileData());
            _dataVersion = dataVersion;
        }

        /// <summary>현재 파일 경로.</summary>
        public string FilePath => _path;

        /// <summary>세대 k(1~3)의 경로. 0이면 현재 파일.</summary>
        public string GenerationPath(int k) => k <= 0 ? _path : _path + "." + k;

        public string TempPath => _path + TempSuffix;

        public void LoadOrCreate(out bool created, out bool restored)
        {
            created = false;
            restored = false;
            System.IO.Directory.CreateDirectory(_dir);
            // 저장 중 꺼졌으면 남은 임시 파일은 버린다(완전한 이전 파일로 연다 — TV-PF-04).
            TryDelete(TempPath);

            for (int k = 0; k <= Generations; k++)
            {
                string p = GenerationPath(k);
                if (!File.Exists(p)) continue;
                if (!TryLoadFile(p, out var profile)) continue;
                Current = profile;
                if (k > 0)
                {
                    restored = true;
                    // 깨진(또는 없는) 현재 자리를 되살린 세대로 덮는다. 세대는 밀지 않는다(깨진 것을 .1로 넣지 않으려고).
                    WriteCurrentFromText(ProfileFile.ReadText(p));
                }
                return;
            }

            // 파일이 하나도 없으면 첫 실행, 있는데 전부 깨졌으면 새로 시작한다(깨진 현재 파일은 세대 밀기로 .1에 남는다).
            Current = _newProfile() ?? new ProfileData();
            created = true;
            Save();
        }

        public void Save()
        {
            if (Current == null) return;
            System.IO.Directory.CreateDirectory(_dir);
            if (_dataVersion != null) Current.DataVersion = _dataVersion;
            string text = ProfileFile.Compose(SaveJson.Serialize(Current));

            // 1) 임시 파일에 다 쓴다. 여기서 꺼지면 현재 파일이 그대로다.
            ProfileFile.WriteTemp(TempPath, text);
            // 2) 세대를 민다(.2→.3, .1→.2). 현재 파일은 옮기지 않고 .1로 복사한다 → 어느 순간에 꺼져도 완전한 파일이 남는다.
            for (int k = Generations - 1; k >= 1; k--)
            {
                string src = GenerationPath(k);
                if (File.Exists(src)) ProfileFile.MoveOver(src, GenerationPath(k + 1));
            }
            if (File.Exists(_path)) File.Copy(_path, GenerationPath(1), true);
            // 3) 이름 바꾸기(임시 → 현재).
            ProfileFile.ReplaceWith(TempPath, _path);
            Saved?.Invoke();
        }

        public bool RestorePrevious()
        {
            string g1 = GenerationPath(1);
            if (!File.Exists(g1) || !TryLoadFile(g1, out var profile)) return false;

            WriteCurrentFromText(ProfileFile.ReadText(g1));
            // 세대를 한 칸씩 당긴다: .2→.1, .3→.2 (되돌린 .1은 이제 현재다). 빈 자리는 지운다.
            for (int k = 1; k < Generations; k++)
            {
                string next = GenerationPath(k + 1);
                if (File.Exists(next)) ProfileFile.MoveOver(next, GenerationPath(k));
                else TryDelete(GenerationPath(k));
            }
            TryDelete(GenerationPath(Generations));
            Current = profile;
            Saved?.Invoke();
            return true;
        }

        void WriteCurrentFromText(string text)
        {
            ProfileFile.WriteTemp(TempPath, text);
            ProfileFile.ReplaceWith(TempPath, _path);
        }

        /// <summary>파일 하나를 해시 확인 + 파싱한다. 실패하면 false(예외를 밖으로 내지 않는다).</summary>
        public static bool TryLoadFile(string path, out ProfileData profile)
        {
            profile = null;
            try
            {
                if (!ProfileFile.TrySplit(ProfileFile.ReadText(path), out var body)) return false;
                return TryParse(body, out profile);
            }
            catch (Exception)
            {
                profile = null;
                return false;
            }
        }

        /// <summary>본문 JSON → 프로필. 최상위가 객체가 아니면 false. 빠진 묶음은 빈 값으로 채운다.</summary>
        public static bool TryParse(string body, out ProfileData profile)
        {
            profile = null;
            try
            {
                var token = JToken.Parse(body);
                if (token.Type != JTokenType.Object) return false;
                profile = SaveJson.Deserialize<ProfileData>(body);
                if (profile == null) return false;
                Normalize(profile);
                return true;
            }
            catch (Exception)
            {
                profile = null;
                return false;
            }
        }

        /// <summary>파일에 null로 적힌(또는 빠진) 묶음을 빈 값으로 채운다 — 쓰는 쪽이 null 검사를 하지 않아도 되게.</summary>
        static void Normalize(ProfileData p)
        {
            if (p.Characters == null) p.Characters = new List<CharacterRecord>();
            if (p.Wands == null) p.Wands = new List<WandData>();
            if (p.Currencies == null) p.Currencies = new CurrencyData();
            if (p.Items == null) p.Items = new Dictionary<string, ItemStack>();
            if (p.ShopWeek == null) p.ShopWeek = new ShopWeekData();
            if (p.Flags == null) p.Flags = new ProfileFlags();
            if (p.Revenge == null) p.Revenge = new RevengeData();
            if (p.Titles == null) p.Titles = new TitlesData();
            if (p.Titles.Owned == null) p.Titles.Owned = new List<string>();
            if (p.Cosmetics == null) p.Cosmetics = new CosmeticsData();
            if (p.Cosmetics.Owned == null) p.Cosmetics.Owned = new List<string>();
            if (p.Cosmetics.Equipped == null) p.Cosmetics.Equipped = new Dictionary<string, string>();
            if (p.Feats == null) p.Feats = new FeatsData();
            if (p.Weekly == null) p.Weekly = new WeeklyData();
            if (p.EnhanceLogTail == null) p.EnhanceLogTail = new List<JObject>();
            if (p.RunHistory == null) p.RunHistory = new List<RunRecordData>();
            if (p.KnownAffinities == null) p.KnownAffinities = new List<string>();
            p.Characters.RemoveAll(c => c == null);
            p.Wands.RemoveAll(w => w == null);
            for (int i = 0; i < p.Characters.Count; i++)
                if (p.Characters[i].UnlockedArts == null) p.Characters[i].UnlockedArts = new List<string>();
            for (int i = 0; i < p.Wands.Count; i++)
            {
                var w = p.Wands[i];
                if (w.Mutations == null) w.Mutations = new List<MutationEntry>();
                if (w.Loadout == null) w.Loadout = new LoadoutData();
                if (w.Loadout.Links == null) w.Loadout.Links = new List<string>();
                if (w.Loadout.Extra == null) w.Loadout.Extra = new List<string>();
            }
            var keys = new List<string>(p.Items.Keys);
            for (int i = 0; i < keys.Count; i++)
                if (p.Items[keys[i]] == null) p.Items[keys[i]] = new ItemStack();
            for (int i = 0; i < p.RunHistory.Count; i++)
                if (p.RunHistory[i] != null && p.RunHistory[i].Loadout == null) p.RunHistory[i].Loadout = new List<string>();
            p.RunHistory.RemoveAll(r => r == null);
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
