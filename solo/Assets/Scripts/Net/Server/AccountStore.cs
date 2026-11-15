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
        /// <summary>캐릭터마다 서버 지팡이의 변이(오래된 것부터, 상한을 넘으면 가장 오래된 것이 밀려난다).</summary>
        public List<List<Wizard.Solo.Meta.MutationEntry>> Mutations = new List<List<Wizard.Solo.Meta.MutationEntry>>();
        /// <summary>캐릭터마다 결정 대기 중인 변이 제시(없으면 null).</summary>
        public List<ServerMutationOffer> PendingOffers = new List<ServerMutationOffer>();
        public long NextAttemptSeq = 1;
        /// <summary>친구(계정 id)와 나에게 온 친구 신청.</summary>
        public List<long> Friends = new List<long>();
        public List<long> FriendRequests = new List<long>();
        /// <summary>캐릭터마다 서버 지팡이 조립(코어 + 연결 칸). null = 기본(코어 마력탄, 빈 칸). 끼운 블록은 창고(Items)에서 빠져 있다.</summary>
        public List<ServerLoadout> Loadouts = new List<ServerLoadout>();
        /// <summary>캐릭터마다 대성공으로 늘어난 연결 칸 수.</summary>
        public int[] SlotsExtra = new int[5];

        public ServerLoadout LoadoutOf(int character)
        {
            if (Loadouts == null) Loadouts = new List<ServerLoadout>();
            while (Loadouts.Count < 5) Loadouts.Add(null);
            return Loadouts[character - 1];
        }

        public List<Wizard.Solo.Meta.MutationEntry> MutationsOf(int character)
        {
            if (Mutations == null) Mutations = new List<List<Wizard.Solo.Meta.MutationEntry>>();
            while (Mutations.Count < 5) Mutations.Add(new List<Wizard.Solo.Meta.MutationEntry>());
            return Mutations[character - 1];
        }

        public ServerMutationOffer OfferOf(int character)
        {
            if (PendingOffers == null) PendingOffers = new List<ServerMutationOffer>();
            while (PendingOffers.Count < 5) PendingOffers.Add(null);
            return PendingOffers[character - 1];
        }

        public void SetOffer(int character, ServerMutationOffer o)
        {
            OfferOf(character);
            PendingOffers[character - 1] = o;
        }
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

    /// <summary>서버 지팡이 조립 한 벌. Links 길이 = 그 지팡이의 연결 칸 수(빈 칸 = null).</summary>
    public sealed class ServerLoadout
    {
        public string Core;
        public List<string> Links = new List<string>();
    }

    /// <summary>서버 지팡이에 걸린 변이 제시: 받으면 변이 +기운 1, 거절하면 미리 계산해 둔 실패 결과.</summary>
    public sealed class ServerMutationOffer
    {
        public long AttemptSeq;
        public string Candidate;
        public string WouldPushOut;
        public int FallbackStage, FallbackAura;
        public bool FallbackCharm;
        public Wizard.Solo.Meta.EnhanceOutcome FallbackOutcome;
    }

    /// <summary>계정 한 줄(저장용). 비밀은 저장하지 않고 소금 친 PBKDF2-SHA256 값만 둔다.</summary>
    public sealed class AccountRecord
    {
        public long Id;
        public string Name;
        public string Salt;
        public string SecretHash;
        /// <summary>이메일 계정: 가입한 주소(보이는 꼴 그대로, 없으면 null). 확인 메일은 아직 보내지 않는다(복구 메일 꺼짐).</summary>
        public string Email;
        public bool EmailVerified;
        /// <summary>비밀번호: 소금 친 PBKDF2-SHA256(반복 PwIter번). 비밀번호 자체는 저장하지 않는다.</summary>
        public string PwSalt, PwHash;
        public int PwIter;
        /// <summary>Google 계정의 sub(바뀌지 않는 Google 사용자 id). 없으면 null.</summary>
        public string GoogleSub;
        /// <summary>로그인 표(세션). 토큰은 SHA-256 값만 둔다.</summary>
        public List<SessionRecord> Sessions = new List<SessionRecord>();
        public ServerProfile Profile = new ServerProfile();
    }

    /// <summary>로그인 표 하나: 이메일·Google로 들어오면 서버가 무작위 토큰을 주고, 기기는 다음부터 (계정 id, 토큰)으로 들어온다.</summary>
    public sealed class SessionRecord
    {
        public string TokenHash;
        public long CreatedTicks;
        public long ExpiresTicks;
    }

    /// <summary>
    /// 기기 계정 저장소(서버, JSON 파일 하나). solo 결정: 지금 통신에 암호화가 없어 비밀번호를 받지 않는다 — 처음 들어올 때 기기가 만든
    /// 긴 무작위 비밀(32바이트)을 이름과 함께 등록하고, 다음부터 (계정 id, 비밀)로 들어온다. TLS를 붙이면 비밀번호·이메일 복구를 더한다.
    /// 이름은 2~12자, 겹치지 않는다(대소문자 무시). 쓰기는 바뀔 때마다 임시 파일 → 바꿔치기.
    /// </summary>
    public sealed partial class AccountStore
    {
        public const int NameMin = 2, NameMax = 12, Iterations = 20000;

        readonly IAccountDb _db;
        AccountData _data = new AccountData();
        readonly Dictionary<long, AccountRecord> _byId = new Dictionary<long, AccountRecord>();
        readonly Dictionary<string, AccountRecord> _byName = new Dictionary<string, AccountRecord>();

        /// <summary>path = JSON 저장 파일(null이면 메모리에만 — 테스트).</summary>
        public AccountStore(string path) : this(new JsonAccountDb(path)) { }

        /// <summary>db = 저장소(JsonAccountDb, PostgresAccountDb). 시작할 때 전부 읽어 메모리에 둔다 — 서버 프로세스 하나만 쓴다.</summary>
        public AccountStore(IAccountDb db)
        {
            _db = db ?? new JsonAccountDb(null);
            _data = _db.Load() ?? new AccountData();
            foreach (var a in _data.Accounts) Index(a);
            foreach (var l in _data.Listings) _committedListings.Add(l.Id);
        }

        void Index(AccountRecord a)
        {
            if (a.Sessions == null) a.Sessions = new List<SessionRecord>();
            _byId[a.Id] = a;
            _byName[Key(a.Name)] = a;
            if (!string.IsNullOrEmpty(a.Email)) _byEmail[EmailKey(a.Email)] = a;
            if (!string.IsNullOrEmpty(a.GoogleSub)) _byGoogle[a.GoogleSub] = a;
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
            Index(rec);
            Touch(rec.Id);
            Save();
            return RegisterResult.Ok;
        }

        /// <summary>(id, 비밀)이 맞으면 그 계정. 아니면 null.</summary>
        public AccountRecord Login(long id, string secret)
        {
            if (secret == null || !_byId.TryGetValue(id, out var rec)) return null;
            if (!SessionValid(rec, secret, DateTime.UtcNow.Ticks))
            {
                // 옛 기기 계정: 기기가 만든 비밀(PBKDF2).
                if (string.IsNullOrEmpty(rec.Salt) || string.IsNullOrEmpty(rec.SecretHash)) return null;
                byte[] salt;
                try { salt = Convert.FromBase64String(rec.Salt); } catch (Exception) { return null; }
                if (!FixedEquals(Hash(secret, salt), rec.SecretHash)) return null;
            }
            rec.Profile.LastSeenAt = DateTime.UtcNow.ToString("o");
            Touch(rec.Id);
            return rec;
        }

        /// <summary>계정 하나. 이 계정은 다음 Save()에서 저장소에 다시 쓴다(바꿀 수 있으므로).</summary>
        public AccountRecord Get(long id)
        {
            if (!_byId.TryGetValue(id, out var r)) return null;
            _touched.Add(id);
            return r;
        }

        /// <summary>읽기만 할 때(저장하지 않음).</summary>
        public AccountRecord Peek(long id) => _byId.TryGetValue(id, out var r) ? r : null;

        public void Touch(long id) { if (_byId.ContainsKey(id)) _touched.Add(id); }

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
            if (p.SlotsExtra == null || p.SlotsExtra.Length != 5) p.SlotsExtra = new int[5];
            if (p.Loadouts == null) p.Loadouts = new List<ServerLoadout>();
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

        public enum EnhanceResult { Ok, NoAccount, BadCharacter, MaxStage, NotEnoughGold, NotEnoughStones, BadSacrifice, MutationPending, NoPendingMutation }

        /// <summary>그 단계 강화에 드는 강화석 id와 개수.</summary>
        public static string StoneFor(Wizard.Solo.Data.EnhanceConfig cfg, int stage, out int count)
        {
            count = cfg != null ? Math.Max(0, cfg.StonesPerAttempt) : 0;
            return cfg != null ? Wizard.Solo.Meta.Enhance.EnhanceRules.StoneId(Wizard.Solo.Meta.Enhance.EnhanceRules.StoneTierFor(cfg, stage)) : null;
        }

        /// <summary>
        /// 서버 지팡이 강화 한 번(규칙 = 오프라인과 같은 EnhanceRules.Breakdown: 기본 확률 + 제물 + 같은 속성 + 기운, 비용 골드·강화석).
        /// 제물은 서버 창고의 블록(협동 판에서 가져온 것)을 쓴다(최대 cfg.Sacrifice.MaxBlocks개, 시도 순간 사라진다).
        /// 변이 구간이면 후보를 뽑아 제시만 하고(받기 전엔 단계 그대로) ResolveMutation으로 정한다. 부적은 떨어짐을 막을 때만 쓴다.
        /// rng = 서버 주사위(0 ≤ Next(n) &lt; n).
        /// </summary>
        public EnhanceResult Enhance(long id, int character, Wizard.Solo.Data.IContentDb db, Wizard.Solo.Meta.Enhance.IEnhanceRandom rng,
            out Wizard.Solo.Meta.EnhanceOutcome outcome, bool useCharm = false, IReadOnlyList<string> sacrifices = null)
        {
            outcome = Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop;
            var cfg = db != null ? db.Enhance : null;
            var a = Get(id);
            if (a == null) return EnhanceResult.NoAccount;
            if (character < 1 || character > 5 || cfg == null) return EnhanceResult.BadCharacter;
            var p = a.Profile;
            Fix(p);
            int i = character - 1;
            if (p.OfferOf(character) != null) return EnhanceResult.MutationPending;
            int stage = p.WandStages[i];
            if (stage >= cfg.MaxStage) return EnhanceResult.MaxStage;

            // 제물 검사: 블록이어야 하고 창고에 있어야 한다.
            var sac = new List<string>();
            if (sacrifices != null)
            {
                if (sacrifices.Count > cfg.Sacrifice.MaxBlocks) return EnhanceResult.BadSacrifice;
                var need = new Dictionary<string, int>();
                foreach (var b in sacrifices)
                {
                    if (b == null || db.Block(b) == null) return EnhanceResult.BadSacrifice;
                    need.TryGetValue(b, out int n);
                    need[b] = n + 1;
                    sac.Add(b);
                }
                foreach (var kv in need) if (p.Qty(kv.Key) < kv.Value) return EnhanceResult.BadSacrifice;
            }
            var def = db.Character(Ids.Char.Of(character));
            var bd = Wizard.Solo.Meta.Enhance.EnhanceRules.Breakdown(cfg, stage, p.PeakStages[i], p.Auras[i],
                Wizard.Solo.Meta.Enhance.EnhanceRules.SacrificeBonus(cfg, db, sac, def != null ? def.Element : null));
            if (p.Gold < bd.CostGold) return EnhanceResult.NotEnoughGold;
            if (bd.StoneId != null && p.Qty(bd.StoneId) < bd.StoneCount) return EnhanceResult.NotEnoughStones;

            p.Gold -= bd.CostGold;
            if (bd.StoneId != null) p.Add(bd.StoneId, -bd.StoneCount);
            foreach (var b in sac) p.Add(b, -1);
            p.EnhanceTries++;
            bool charm = useCharm && p.Qty(Ids.Item.Charm) > 0;
            var band = Wizard.Solo.Meta.Enhance.EnhanceRules.Judge(rng.Next(Wizard.Solo.Meta.Enhance.EnhanceRules.BpFull), bd.SuccessBp, bd.GreatBp, bd.MutationEndBp);

            string candidate = null;
            var muts = p.MutationsOf(character);
            if (band == Wizard.Solo.Meta.Enhance.JudgeBand.Mutation)
            {
                // 서버 지팡이 코어는 언제나 마력탄(폭발 아님).
                var pool = Wizard.Solo.Meta.Enhance.EnhanceRules.MutationPool(muts, false);
                candidate = Wizard.Solo.Meta.Enhance.EnhanceRules.DrawCandidate(pool, rng);
                if (candidate == null) band = Wizard.Solo.Meta.Enhance.JudgeBand.Fail;
            }
            switch (band)
            {
                case Wizard.Solo.Meta.Enhance.JudgeBand.GreatSuccess:
                    // 대성공은 (정한 단계부터) 연결 칸을 하나 늘린다(오프라인과 같은 GreatGivesSlot, 상한 SlotExtraMax).
                    if (Wizard.Solo.Meta.Enhance.EnhanceRules.GreatGivesSlot(cfg, stage) && p.SlotsExtra[i] < cfg.SlotExtraMax)
                    {
                        p.SlotsExtra[i]++;
                        var lo = p.LoadoutOf(character);
                        if (lo != null) lo.Links.Add(null);
                    }
                    p.WandStages[i] = Wizard.Solo.Meta.Enhance.EnhanceRules.StageAfterGreat(cfg, stage);
                    p.Auras[i] = 0;
                    outcome = Wizard.Solo.Meta.EnhanceOutcome.GreatSuccess;
                    break;
                case Wizard.Solo.Meta.Enhance.JudgeBand.Success:
                    p.WandStages[i] = Wizard.Solo.Meta.Enhance.EnhanceRules.StageAfterSuccess(cfg, stage);
                    p.Auras[i] = 0;
                    outcome = Wizard.Solo.Meta.EnhanceOutcome.Success;
                    break;
                case Wizard.Solo.Meta.Enhance.JudgeBand.Mutation:
                    {
                        var fb = Wizard.Solo.Meta.Enhance.EnhanceRules.ResolveFail(cfg, stage, p.PeakStages[i], p.Auras[i], charm);
                        p.SetOffer(character, new ServerMutationOffer
                        {
                            AttemptSeq = p.NextAttemptSeq++,
                            Candidate = candidate,
                            WouldPushOut = Wizard.Solo.Meta.Enhance.EnhanceRules.WouldPushOut(muts, Wizard.Solo.Meta.Enhance.EnhanceRules.MutationCap(cfg, db, character)),
                            FallbackStage = fb.StageAfter, FallbackAura = fb.AuraAfter, FallbackCharm = fb.CharmConsumed, FallbackOutcome = fb.Outcome,
                        });
                        outcome = Wizard.Solo.Meta.EnhanceOutcome.MutationOffered;
                        break;
                    }
                default:
                    {
                        var fb = Wizard.Solo.Meta.Enhance.EnhanceRules.ResolveFail(cfg, stage, p.PeakStages[i], p.Auras[i], charm);
                        p.WandStages[i] = fb.StageAfter;
                        p.Auras[i] = fb.AuraAfter;
                        if (fb.CharmConsumed) p.Add(Ids.Item.Charm, -1);
                        outcome = fb.Outcome;
                        break;
                    }
            }
            if (p.WandStages[i] > p.PeakStages[i]) p.PeakStages[i] = p.WandStages[i];
            LogEnhance(id, character, stage, p.WandStages[i], outcome.ToString());
            Save();
            return EnhanceResult.Ok;
        }

        /// <summary>대기 중인 변이 제시에 답한다. 받으면 변이(넘치면 가장 오래된 것이 밀려남) + 기운 1, 단계 그대로. 거절하면 미리 계산한 실패.</summary>
        public EnhanceResult ResolveMutation(long id, int character, Wizard.Solo.Data.IContentDb db, bool accept, out Wizard.Solo.Meta.EnhanceOutcome outcome)
        {
            outcome = Wizard.Solo.Meta.EnhanceOutcome.FailNoDrop;
            var cfg = db != null ? db.Enhance : null;
            var a = Get(id);
            if (a == null) return EnhanceResult.NoAccount;
            if (character < 1 || character > 5 || cfg == null) return EnhanceResult.BadCharacter;
            var p = a.Profile;
            Fix(p);
            var o = p.OfferOf(character);
            if (o == null) return EnhanceResult.NoPendingMutation;
            int i = character - 1;
            if (accept)
            {
                Wizard.Solo.Meta.Enhance.EnhanceRules.AcceptMutation(p.MutationsOf(character), o.Candidate,
                    Wizard.Solo.Meta.Enhance.EnhanceRules.MutationCap(cfg, db, character), o.AttemptSeq);
                p.Auras[i] = Math.Min(cfg.AuraMax, p.Auras[i] + 1);
                outcome = Wizard.Solo.Meta.EnhanceOutcome.MutationAccepted;
            }
            else
            {
                bool charm = o.FallbackCharm && p.Qty(Ids.Item.Charm) > 0;
                if (o.FallbackCharm && !charm)
                {
                    var fb = Wizard.Solo.Meta.Enhance.EnhanceRules.ResolveFail(cfg, p.WandStages[i], p.PeakStages[i], p.Auras[i], false);
                    o.FallbackStage = fb.StageAfter; o.FallbackAura = fb.AuraAfter; o.FallbackOutcome = fb.Outcome;
                }
                p.WandStages[i] = o.FallbackStage;
                p.Auras[i] = o.FallbackAura;
                if (charm) p.Add(Ids.Item.Charm, -1);
                outcome = o.FallbackOutcome;
            }
            p.SetOffer(character, null);
            Save();
            return EnhanceResult.Ok;
        }

        // ------------------------------------------------------------ 서버 지팡이 조립

        public enum LoadoutResult { Ok, NoAccount, BadCharacter, BadShape, NotOwned, CompileError }

        /// <summary>그 캐릭터 서버 지팡이의 연결 칸 수 = 캐릭터 기본 칸 + 대성공으로 늘어난 칸.</summary>
        public int LinkSlots(long id, int character, Wizard.Solo.Data.IContentDb db)
        {
            var a = Get(id);
            var def = db != null ? db.Character(Ids.Char.Of(character)) : null;
            int baseSlots = def != null ? def.BaseLinkSlots : 0;
            if (a == null || character < 1 || character > 5) return baseSlots;
            Fix(a.Profile);
            return baseSlots + a.Profile.SlotsExtra[character - 1];
        }

        /// <summary>지금 조립(없으면 기본: 마력탄 + 빈 칸).</summary>
        public Wizard.Solo.Sim.LoadoutSpec LoadoutSpecOf(long id, int character, Wizard.Solo.Data.IContentDb db)
        {
            int n = LinkSlots(id, character, db);
            var a = Get(id);
            var lo = a != null && character >= 1 && character <= 5 ? a.Profile.LoadoutOf(character) : null;
            var spec = new Wizard.Solo.Sim.LoadoutSpec { Core = lo != null && !string.IsNullOrEmpty(lo.Core) ? lo.Core : Ids.Block.MagicBolt, Links = new List<string>() };
            for (int k = 0; k < n; k++) spec.Links.Add(lo != null && k < lo.Links.Count ? lo.Links[k] : null);
            return spec;
        }

        static void CountBlocks(Wizard.Solo.Sim.LoadoutSpec s2, Dictionary<string, int> into)
        {
            if (!string.IsNullOrEmpty(s2.Core) && s2.Core != Ids.Block.MagicBolt) { into.TryGetValue(s2.Core, out int c); into[s2.Core] = c + 1; }
            foreach (var l in s2.Links) if (!string.IsNullOrEmpty(l) && l != Ids.Block.MagicBolt) { into.TryGetValue(l, out int c); into[l] = c + 1; }
        }

        /// <summary>
        /// 조립을 바꾼다. 모양(코어는 투사체, 칸 수, 아는 블록)·주문 컴파일(오프라인과 같은 InventoryRules)·창고 수량을 본다.
        /// 마력탄은 누구나 가진 기본 블록이라 창고 없이 끼운다. 바뀐 블록은 창고와 맞바꾼다(빼면 돌아오고, 끼우면 빠진다).
        /// </summary>
        public LoadoutResult SetLoadout(long id, int character, string core, IList<string> links, Wizard.Solo.Data.IContentDb db, Wizard.Solo.Sim.SimModuleSet rules)
        {
            var a = Get(id);
            if (a == null) return LoadoutResult.NoAccount;
            if (character < 1 || character > 5 || db == null) return LoadoutResult.BadCharacter;
            var p = a.Profile;
            Fix(p);
            int n = LinkSlots(id, character, db);
            var spec = new Wizard.Solo.Sim.LoadoutSpec { Core = string.IsNullOrEmpty(core) ? Ids.Block.MagicBolt : core, Links = new List<string>() };
            for (int k = 0; k < n; k++) spec.Links.Add(links != null && k < links.Count && !string.IsNullOrEmpty(links[k]) ? links[k] : null);
            if (Wizard.Solo.Meta.Inventory.InventoryRules.CheckShape(db, spec, n) != Wizard.Solo.Meta.ErrorCode.None) return LoadoutResult.BadShape;

            var have = new Dictionary<string, int>();
            var need = new Dictionary<string, int>();
            CountBlocks(LoadoutSpecOf(id, character, db), have);
            CountBlocks(spec, need);
            foreach (var kv in need)
            {
                have.TryGetValue(kv.Key, out int socketed);
                if (socketed + p.Qty(kv.Key) < kv.Value) return LoadoutResult.NotOwned;
            }
            var muts = MutationIds(id, character);
            if (rules != null && Wizard.Solo.Meta.Inventory.InventoryRules.CheckCompile(rules, db, character, p.WandStages[character - 1], muts, spec, n) != Wizard.Solo.Meta.ErrorCode.None)
                return LoadoutResult.CompileError;

            foreach (var kv in have) p.Add(kv.Key, kv.Value);
            foreach (var kv in need) p.Add(kv.Key, -kv.Value);
            p.LoadoutOf(character);
            p.Loadouts[character - 1] = new ServerLoadout { Core = spec.Core, Links = new List<string>(spec.Links) };
            Save();
            return LoadoutResult.Ok;
        }

        /// <summary>받은 변이 하나를 지운다(골드 cfg.MutationRemoveGold, 오프라인과 같은 값). 단계·기운·칸은 그대로.</summary>
        public EnhanceResult RemoveMutation(long id, int character, string mutationId, Wizard.Solo.Data.EnhanceConfig cfg)
        {
            var a = Get(id);
            if (a == null) return EnhanceResult.NoAccount;
            if (character < 1 || character > 5 || cfg == null) return EnhanceResult.BadCharacter;
            var p = a.Profile;
            Fix(p);
            if (p.OfferOf(character) != null) return EnhanceResult.MutationPending;
            var muts = p.MutationsOf(character);
            int index = muts.FindIndex(m => m.Id == mutationId);
            if (index < 0) return EnhanceResult.BadCharacter;
            if (p.Gold < cfg.MutationRemoveGold) return EnhanceResult.NotEnoughGold;
            p.Gold -= cfg.MutationRemoveGold;
            muts.RemoveAt(index);
            Save();
            return EnhanceResult.Ok;
        }

        // ------------------------------------------------------------ 거래

        /// <summary>거래 한쪽이 내놓는 것: 골드와 아이템(강화석·부적·블록 — 창고에 있는 것만).</summary>
        public sealed class TradeOffer
        {
            public long Gold;
            public Dictionary<string, int> Items = new Dictionary<string, int>();
        }

        public const int TradeFeePercent = 5;
        public static long TradeFee(long gold) => gold <= 0 ? 0 : (gold * TradeFeePercent + 99) / 100;

        public bool Owns(long id, TradeOffer o)
        {
            var a = Get(id);
            if (a == null || o == null || o.Gold < 0 || a.Profile.Gold < o.Gold) return false;
            foreach (var kv in o.Items) if (kv.Value <= 0 || a.Profile.Qty(kv.Key) < kv.Value) return false;
            return true;
        }

        /// <summary>두 계정이 내놓은 것을 한 번에 맞바꾼다(둘 다 가진 게 맞을 때만, 아니면 아무 일도 없다).</summary>
        public bool Swap(long aId, TradeOffer aOffer, long bId, TradeOffer bOffer)
        {
            if (aId == bId || !Owns(aId, aOffer) || !Owns(bId, bOffer)) return false;
            var a = Get(aId).Profile;
            var b = Get(bId).Profile;
            // 직접 거래 수수료(docs/online.md 경제 표): 오간 골드의 5% — 받는 쪽에서 뺀다(골드가 사라지는 곳).
            a.Gold -= aOffer.Gold; b.Gold += aOffer.Gold - TradeFee(aOffer.Gold);
            b.Gold -= bOffer.Gold; a.Gold += bOffer.Gold - TradeFee(bOffer.Gold);
            foreach (var kv in aOffer.Items) { a.Add(kv.Key, -kv.Value); b.Add(kv.Key, kv.Value); }
            foreach (var kv in bOffer.Items) { b.Add(kv.Key, -kv.Value); a.Add(kv.Key, kv.Value); }
            Save();
            return true;
        }

        /// <summary>서버 지팡이의 변이 id들(온라인 판 출발 설정에 넣는다).</summary>
        public List<string> MutationIds(long id, int character)
        {
            var l = new List<string>();
            var a = Get(id);
            if (a == null || character < 1 || character > 5) return l;
            foreach (var m in a.Profile.MutationsOf(character)) l.Add(m.Id);
            return l;
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
        public void RecordCoop(long id, int clearedWave, long purse = 0, IList<Wizard.Solo.Meta.MaterialGain> materials = null, IList<string> blocks = null)
        {
            var a = Get(id);
            if (a == null) return;
            a.Profile.CoopRuns++;
            if (clearedWave > a.Profile.CoopBestWave) a.Profile.CoopBestWave = clearedWave;
            a.Profile.Gold += Math.Max(0, purse);
            if (materials != null) foreach (var g in materials) a.Profile.Add(g.ItemId, g.Qty);
            // 판의 보관함 블록(빌린 것 제외)을 사람마다 한 벌씩 창고로 — 제물로 쓴다. [임시] 귀환 가방 칸 제한은 아직 없다.
            if (blocks != null) foreach (var b in blocks) a.Profile.Add(b, 1);
            Save();
        }

        /// <summary>[임시] 서버 경제는 아직 설계 전(docs/online.md 2절). 결투 승리 50골드 + 하급 강화석 1.</summary>
        public const int DuelWinGold = 50, DuelWinStones = 1;

        // ------------------------------------------------------------ 저장(쓰기 묶음)

        readonly HashSet<long> _touched = new HashSet<long>();
        readonly HashSet<long> _committedListings = new HashSet<long>();
        readonly List<EnhanceLogEntry> _pendingLog = new List<EnhanceLogEntry>();

        /// <summary>저장소에 마지막으로 쓰다 실패한 이유(없으면 null). 실패한 바뀜은 다음 Save()에서 다시 쓴다.</summary>
        public string LastSaveError { get; private set; }

        void LogEnhance(long id, int character, int before, int after, string outcome)
        {
            _pendingLog.Add(new EnhanceLogEntry { AccountId = id, Character = character, StageBefore = before, StageAfter = after, Outcome = outcome, AtTicks = DateTime.UtcNow.Ticks });
        }

        /// <summary>
        /// 한 처리(강화·사기·거래 …)에서 바뀐 것을 저장소에 한 번에 쓴다(Postgres는 트랜잭션 하나): 이번에 손댄 계정 줄 전부,
        /// 새로 올라온·사라진 장터 물건, 강화 기록. 실패하면 바뀜을 그대로 들고 있다가 다음 Save()에서 다시 쓴다.
        /// </summary>
        public void Save()
        {
            var c = new AccountDbChanges { NextId = _data.NextId, NextListingId = _data.NextListingId };
            foreach (var id in _touched) if (_byId.TryGetValue(id, out var a)) c.Accounts.Add(a);
            var now = new HashSet<long>();
            foreach (var l in _data.Listings)
            {
                now.Add(l.Id);
                if (!_committedListings.Contains(l.Id)) c.AddedListings.Add(l);
            }
            foreach (var id in _committedListings) if (!now.Contains(id)) c.RemovedListings.Add(id);
            c.EnhanceLog.AddRange(_pendingLog);
            try
            {
                _db.Commit(_data, c);
                LastSaveError = null;
            }
            catch (Exception e)
            {
                LastSaveError = e.Message;
                SaveFailed?.Invoke(e);
                return;
            }
            _touched.Clear();
            _pendingLog.Clear();
            _committedListings.Clear();
            foreach (var id in now) _committedListings.Add(id);
        }

        /// <summary>저장 실패를 알린다(서버 기록용).</summary>
        public Action<Exception> SaveFailed;
    }
}
