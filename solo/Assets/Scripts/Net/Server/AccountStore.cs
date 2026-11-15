using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Wizard.Solo;

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
        /// <summary>캐릭터 1~5의 서버 지팡이(한 사람 한 자루씩): 단계, 기운(실패로 쌓인 확률 보너스 칸), 최고 단계(떨어짐 바닥 계산).</summary>
        public int[] WandStages = new int[5];
        public int[] Auras = new int[5];
        public int[] PeakStages = new int[5];
        public int EnhanceTries;
        /// <summary>서버 재료 창고: 강화석(mat.stone_low/mid/high), 부적(charm.safeguard) … 아이템 id → 개수.</summary>
        public Dictionary<string, int> Items = new Dictionary<string, int>();

        public int Qty(string id) => Items != null && id != null && Items.TryGetValue(id, out int n) ? n : 0;
        public void Add(string id, int n)
        {
            if (Items == null) Items = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(id) || n == 0) return;
            int v = Qty(id) + n;
            if (v <= 0) Items.Remove(id); else Items[id] = v;
        }
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

        public int StageOf(long id, int character)
        {
            var a = Get(id);
            if (a == null || character < 1 || character > 5) return 0;
            Fix(a.Profile);
            return a.Profile.WandStages[character - 1];
        }

        /// <summary>옛 파일에 배열이 없으면 채운다.</summary>
        static void Fix(ServerProfile p)
        {
            if (p.WandStages == null || p.WandStages.Length != 5) p.WandStages = new int[5];
            if (p.Auras == null || p.Auras.Length != 5) p.Auras = new int[5];
            if (p.PeakStages == null || p.PeakStages.Length != 5) p.PeakStages = new int[5];
        }

        /// <summary>순위표: 레이팅 높은 순(같으면 승수, 이름). 결투를 한 번이라도 한 사람만.</summary>
        public List<ServerProfile> Top(int n)
        {
            var l = new List<ServerProfile>();
            foreach (var a in _data.Accounts)
                if (a.Profile.DuelWins + a.Profile.DuelLosses + a.Profile.DuelDraws > 0) l.Add(a.Profile);
            l.Sort((x, y) => x.Rating != y.Rating ? y.Rating.CompareTo(x.Rating)
                : x.DuelWins != y.DuelWins ? y.DuelWins.CompareTo(x.DuelWins) : string.CompareOrdinal(x.Name, y.Name));
            if (l.Count > n) l.RemoveRange(n, l.Count - n);
            return l;
        }

        // ------------------------------------------------------------ 서버 강화

        public enum EnhanceResult { Ok, NoAccount, BadCharacter, MaxStage, NotEnoughGold, NotEnoughStones }

        /// <summary>그 단계 강화에 드는 강화석 id와 개수.</summary>
        public static string StoneFor(Wizard.Solo.Data.EnhanceConfig cfg, int stage, out int count)
        {
            count = cfg != null ? Math.Max(0, cfg.StonesPerAttempt) : 0;
            return cfg != null ? Wizard.Solo.Meta.Enhance.EnhanceRules.StoneId(Wizard.Solo.Meta.Enhance.EnhanceRules.StoneTierFor(cfg, stage)) : null;
        }

        /// <summary>
        /// 서버 지팡이 강화 한 번(강화 규칙 = 오프라인과 같은 EnhanceRules: 기본 확률, 기운, 대성공·실패·떨어짐, 비용 100 × (단계+1)²).
        /// 골드 + 그 단계의 강화석(창고에서), 부적을 쓰면 떨어짐을 막는다(막았을 때만 하나 쓴다). [임시] 제물은 아직 없다. 변이 구간은 성공으로 친다(서버 지팡이에 변이 칸이 아직 없다).
        /// rollBp = 0~9999 주사위(서버가 굴린다).
        /// </summary>
        public EnhanceResult Enhance(long id, int character, Wizard.Solo.Data.EnhanceConfig cfg, int rollBp, out Wizard.Solo.Meta.EnhanceOutcome outcome, bool useCharm = false)
        {
            outcome = Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop;
            var a = Get(id);
            if (a == null) return EnhanceResult.NoAccount;
            if (character < 1 || character > 5 || cfg == null) return EnhanceResult.BadCharacter;
            var p = a.Profile;
            Fix(p);
            int i = character - 1;
            int stage = p.WandStages[i];
            if (stage >= cfg.MaxStage) return EnhanceResult.MaxStage;
            long cost = Wizard.Solo.Meta.Enhance.EnhanceRules.Cost(cfg, stage);
            if (p.Gold < cost) return EnhanceResult.NotEnoughGold;
            string stone = StoneFor(cfg, stage, out int stones);
            if (stone != null && p.Qty(stone) < stones) return EnhanceResult.NotEnoughStones;
            p.Gold -= cost;
            if (stone != null) p.Add(stone, -stones);
            bool charm = useCharm && p.Qty(Ids.Item.Charm) > 0;
            p.EnhanceTries++;
            int pct = Wizard.Solo.Meta.Enhance.EnhanceRules.SuccessPct(cfg, stage, p.Auras[i], 0);
            int succ = Wizard.Solo.Meta.Enhance.EnhanceRules.SuccessBp(pct);
            var band = Wizard.Solo.Meta.Enhance.EnhanceRules.Judge(rollBp, succ,
                Wizard.Solo.Meta.Enhance.EnhanceRules.GreatBp(cfg, stage, pct), Wizard.Solo.Meta.Enhance.EnhanceRules.MutationEndBp(cfg, pct));
            switch (band)
            {
                case Wizard.Solo.Meta.Enhance.JudgeBand.GreatSuccess:
                    p.WandStages[i] = Wizard.Solo.Meta.Enhance.EnhanceRules.StageAfterGreat(cfg, stage);
                    p.Auras[i] = 0;
                    outcome = Wizard.Solo.Meta.EnhanceOutcome.GreatSuccess;
                    break;
                case Wizard.Solo.Meta.Enhance.JudgeBand.Success:
                case Wizard.Solo.Meta.Enhance.JudgeBand.Mutation:
                    p.WandStages[i] = Wizard.Solo.Meta.Enhance.EnhanceRules.StageAfterSuccess(cfg, stage);
                    p.Auras[i] = 0;
                    outcome = Wizard.Solo.Meta.EnhanceOutcome.Success;
                    break;
                default:
                    var fb = Wizard.Solo.Meta.Enhance.EnhanceRules.ResolveFail(cfg, stage, p.PeakStages[i], p.Auras[i], charm);
                    if (fb.CharmConsumed) p.Add(Ids.Item.Charm, -1);
                    p.WandStages[i] = fb.StageAfter;
                    p.Auras[i] = fb.AuraAfter;
                    outcome = fb.Outcome;
                    break;
            }
            if (p.WandStages[i] > p.PeakStages[i]) p.PeakStages[i] = p.WandStages[i];
            Save();
            return EnhanceResult.Ok;
        }

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
            else if (winnerId == aId) { a.Profile.DuelWins++; b.Profile.DuelLosses++; a.Profile.Gold += DuelWinGold; a.Profile.Add(Ids.Item.StoneLow, DuelWinStones); }
            else { b.Profile.DuelWins++; a.Profile.DuelLosses++; b.Profile.Gold += DuelWinGold; b.Profile.Add(Ids.Item.StoneLow, DuelWinStones); }
            Save();
        }

        /// <summary>
        /// 협동 한 판(오프라인 정산과 같은 몫): 원정 지갑 골드 그대로 + 재료(판의 추첨 기회마다 오프라인과 같은 표로 사람마다 따로 굴린 것).
        /// 최고 웨이브·판 수도 적는다.
        /// </summary>
        public void RecordCoop(long id, int clearedWave, long purse = 0, IList<Wizard.Solo.Meta.MaterialGain> materials = null)
        {
            var a = Get(id);
            if (a == null) return;
            a.Profile.CoopRuns++;
            if (clearedWave > a.Profile.CoopBestWave) a.Profile.CoopBestWave = clearedWave;
            a.Profile.Gold += Math.Max(0, purse);
            if (materials != null) foreach (var g in materials) a.Profile.Add(g.ItemId, g.Qty);
            Save();
        }

        /// <summary>[임시] 서버 경제는 아직 설계 전(docs/online.md 2절). 결투 승리 50골드 + 하급 강화석 1.</summary>
        public const int DuelWinGold = 50, DuelWinStones = 1;

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
