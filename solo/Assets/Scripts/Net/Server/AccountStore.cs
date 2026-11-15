using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Wizard.Solo.Net
{
    /// <summary>서버 프로필(11-online.md 4단계). 오프라인 프로필과 따로다 — 서로 옮기지 않는다.</summary>
    public sealed class ServerProfile
    {
        public long AccountId;
        public string Name;
        public long Gold;
        public int Rating = 1000;
        public int DuelWins, DuelLosses, DuelDraws;
        public int CoopRuns, CoopBestWave;
        public string CreatedAt;
        public string LastSeenAt;
    }

    /// <summary>계정 한 줄(저장용). 비밀은 저장하지 않고 소금 친 PBKDF2-SHA256 값만 둔다.</summary>
    public sealed class AccountRecord
    {
        public long Id;
        public string Name;
        public string Salt;
        public string SecretHash;
        public ServerProfile Profile = new ServerProfile();
    }

    /// <summary>
    /// 기기 계정 저장소(서버, JSON 파일 하나). solo 결정: 지금 통신에 암호화가 없어 비밀번호를 받지 않는다 — 처음 들어올 때 기기가 만든
    /// 긴 무작위 비밀(32바이트)을 이름과 함께 등록하고, 다음부터 (계정 id, 비밀)로 들어온다. TLS를 붙이면 비밀번호·이메일 복구를 더한다.
    /// 이름은 2~12자, 겹치지 않는다(대소문자 무시). 쓰기는 바뀔 때마다 임시 파일 → 바꿔치기.
    /// </summary>
    public sealed class AccountStore
    {
        public const int NameMin = 2, NameMax = 12, Iterations = 20000;

        sealed class FileData { public long NextId = 1; public List<AccountRecord> Accounts = new List<AccountRecord>(); }

        readonly string _path;
        FileData _data = new FileData();
        readonly Dictionary<long, AccountRecord> _byId = new Dictionary<long, AccountRecord>();
        readonly Dictionary<string, AccountRecord> _byName = new Dictionary<string, AccountRecord>();

        /// <summary>path = 저장 파일(null이면 메모리에만 — 테스트).</summary>
        public AccountStore(string path)
        {
            _path = path;
            if (_path != null && File.Exists(_path))
            {
                try { _data = JsonConvert.DeserializeObject<FileData>(File.ReadAllText(_path, Encoding.UTF8)) ?? new FileData(); }
                catch (Exception) { _data = new FileData(); }
            }
            foreach (var a in _data.Accounts) { _byId[a.Id] = a; _byName[Key(a.Name)] = a; }
        }

        public int Count => _data.Accounts.Count;

        static string Key(string name) => name.Trim().ToLowerInvariant();

        public static string CleanName(string name) => PlazaHost.Clean(name, NameMax);

        public enum RegisterResult { Ok, BadName, NameTaken, BadSecret }

        public RegisterResult Register(string name, string secret, out AccountRecord rec)
        {
            rec = null;
            name = CleanName(name);
            if (string.IsNullOrEmpty(name) || name.Length < NameMin) return RegisterResult.BadName;
            if (string.IsNullOrEmpty(secret) || secret.Length < 32) return RegisterResult.BadSecret;
            if (_byName.ContainsKey(Key(name))) return RegisterResult.NameTaken;
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            string now = DateTime.UtcNow.ToString("o");
            rec = new AccountRecord
            {
                Id = _data.NextId++,
                Name = name,
                Salt = Convert.ToBase64String(salt),
                SecretHash = Hash(secret, salt),
            };
            rec.Profile.AccountId = rec.Id;
            rec.Profile.Name = name;
            rec.Profile.CreatedAt = now;
            rec.Profile.LastSeenAt = now;
            _data.Accounts.Add(rec);
            _byId[rec.Id] = rec;
            _byName[Key(name)] = rec;
            Save();
            return RegisterResult.Ok;
        }

        /// <summary>(id, 비밀)이 맞으면 그 계정. 아니면 null.</summary>
        public AccountRecord Login(long id, string secret)
        {
            if (secret == null || !_byId.TryGetValue(id, out var rec)) return null;
            byte[] salt;
            try { salt = Convert.FromBase64String(rec.Salt); } catch (Exception) { return null; }
            if (!FixedEquals(Hash(secret, salt), rec.SecretHash)) return null;
            rec.Profile.LastSeenAt = DateTime.UtcNow.ToString("o");
            return rec;
        }

        public AccountRecord Get(long id) => _byId.TryGetValue(id, out var r) ? r : null;

        static string Hash(string secret, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(secret), salt, Iterations, HashAlgorithmName.SHA256))
                return Convert.ToBase64String(kdf.GetBytes(32));
        }

        static bool FixedEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // ------------------------------------------------------------ 판 결과

        /// <summary>결투 한 경기(Elo, K = 32). winnerId = 0이면 무승부.</summary>
        public void RecordDuel(long aId, long bId, long winnerId)
        {
            var a = Get(aId);
            var b = Get(bId);
            if (a == null || b == null || aId == bId) return;
            double ea = 1.0 / (1.0 + Math.Pow(10, (b.Profile.Rating - a.Profile.Rating) / 400.0));
            double sa = winnerId == 0 ? 0.5 : (winnerId == aId ? 1.0 : 0.0);
            int delta = (int)Math.Round(32 * (sa - ea));
            a.Profile.Rating += delta;
            b.Profile.Rating -= delta;
            if (winnerId == 0) { a.Profile.DuelDraws++; b.Profile.DuelDraws++; }
            else if (winnerId == aId) { a.Profile.DuelWins++; b.Profile.DuelLosses++; a.Profile.Gold += DuelWinGold; }
            else { b.Profile.DuelWins++; a.Profile.DuelLosses++; b.Profile.Gold += DuelWinGold; }
            Save();
        }

        /// <summary>협동 한 판: 마지막으로 깬 웨이브만큼 골드(웨이브 × CoopGoldPerWave), 최고 웨이브.</summary>
        public void RecordCoop(long id, int clearedWave)
        {
            var a = Get(id);
            if (a == null) return;
            a.Profile.CoopRuns++;
            if (clearedWave > a.Profile.CoopBestWave) a.Profile.CoopBestWave = clearedWave;
            a.Profile.Gold += Math.Max(0, clearedWave) * CoopGoldPerWave;
            Save();
        }

        /// <summary>[임시] 서버 경제는 아직 설계 전(docs/online.md 2절). 결투 승리 50골드, 협동 웨이브당 10골드.</summary>
        public const int DuelWinGold = 50, CoopGoldPerWave = 10;

        public void Save()
        {
            if (_path == null) return;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_data, Formatting.Indented), Encoding.UTF8);
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception) { }
        }
    }
}
