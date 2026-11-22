using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 온라인 규칙 수치(docs/online.md 4.7~4.10절·5.3절·6절). 값은 DataSolo/OnlineRules.json(열쇠는 snake_case)에서 읽고,
    /// 파일이나 열쇠가 없으면 아래 기본값(= 원문 [제안] 값)을 쓴다.
    /// 리그 번호: 0 새싹, 1 맹수, 2 후계.
    /// </summary>
    public sealed class OnlineRules
    {
        public const string ExtraName = "OnlineRules";
        public const int LeagueCount = 3, LeagueTop = 2;

        // ---- 아이템 레벨(4.7절)
        public int IlPerStage = 10, IlCommon = 2, IlRare = 5, IlHero = 10, IlPerMutation = 6, IlMutationCount = 2, IlPerExtraSlot = 5;
        /// <summary>IL에 세는 기본 연결 칸 수 상한(캐릭터가 타고난 다섯째 칸은 세지 않는다).</summary>
        public int IlBaseLinkSlots = 4;
        public int IlWindowDays = 14;

        // ---- 리그(4.8절). 배열 길이 2 = 새싹·맹수(후계는 제한 없음).
        public int[] LeagueMaxIl = { 70, 155 };
        public int[] LeagueStageCap = { 4, 9 };
        public int[] LeagueHeroCap = { 1, 3 };
        public int[] LeagueMutationCap = { 0, 1 };
        public int[] LeagueExtraSlotCap = { 0, 2 };
        public double[] LeagueRewardCoef = { 0.4, 0.7, 1.0 };

        // ---- 결투 점수(4.10절)
        public double RatingStart = 1500, RatingDeviation = 350, RatingVolatility = 0.06, RatingTau = 0.5;
        public double SeasonResetPull = 0.5, SeasonResetDeviation = 200;
        public int SameOpponentPerDay = 3, LeavesPerDay = 3, QueueBanMinutes = 30;
        public int MatchRangeStart = 100, MatchRangeStep = 50, MatchRangeStepSeconds = 10;
        /// <summary>자동 매치가 열리는 조건(4.9절): 도달 웨이브, 누적 플레이(초).</summary>
        public int QueueMinWave = 10, QueueMinPlaySeconds = 7200;
        /// <summary>잡힌 경기 방에 둘 다 들어오기를 기다리는 시간(초).</summary>
        public int MatchJoinSeconds = 30;
        public int SpectatorsMax = 6;

        // ---- 시즌(6절)
        public int SeasonWeeks = 10, SettleDays = 3;
        public int HeirMinRanked = 50, DuelScoreMinRanked = 30, WaveScoreMinRuns = 3, ParticipationGames = 10;
        public int SeasonTopRuns = 3;
        /// <summary>주간 "+7 이하" 부문의 지팡이 단계 상한(3.5절).</summary>
        public int WeeklyLowStageMax = 7;

        public sealed class Reward
        {
            public int StoneHigh, StoneMid, StoneLow, Charm;
            public long Gold;
        }
        /// <summary>후계 리그 1위·2~10위(리그 계수를 곱하지 않는다).</summary>
        public Reward HeirReward = new Reward { StoneHigh = 30, Charm = 10, Gold = 200000 };
        public Reward DiscipleReward = new Reward { StoneHigh = 15, Charm = 5, Gold = 80000 };
        public int DiscipleRankMax = 10;
        /// <summary>리그 안 순위 보상(재료·골드에 리그 계수를 곱한다).</summary>
        public Reward Top1Reward = new Reward { StoneHigh = 8, Gold = 40000 };
        public Reward Top10Reward = new Reward { StoneHigh = 3, StoneMid = 10, Gold = 15000 };
        public Reward Top30Reward = new Reward { StoneMid = 5, Gold = 5000 };
        public Reward ParticipationReward = new Reward { StoneLow = 10 };
        /// <summary>리그 결투 점수 상위 10%의 골드(새싹·맹수·후계, 계수를 곱하지 않는다).</summary>
        public long[] RatingTop10Gold = { 5000, 10000, 20000 };

        // ---- 우편함(5.2절)
        public int MailDays = 30, MailMax = 100;

        // ---- 차단·신고(5.3절)
        public int BlockMax = 200, ReportDistinct = 5, ReportWindowHours = 24, ReportMuteMinutes = 60, ReportChatLines = 30, ReportReasons = 5;

        /// <summary>최근 판 기록(run 표)을 몇 줄까지 들고 있나.</summary>
        public int RunLogMax = 2000;

        static readonly JsonSerializerSettings Snake = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static OnlineRules FromJson(string json)
        {
            var r = new OnlineRules();
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonConvert.PopulateObject(json, r, Snake); }
                catch (Exception) { return new OnlineRules(); }
            }
            r.Fix();
            return r;
        }

        public static OnlineRules Load(IContentDb db)
        {
            Newtonsoft.Json.Linq.JObject jo = null;
            try { jo = db != null ? db.SoloExtra(ExtraName) : null; } catch (Exception) { jo = null; }
            return jo != null ? FromJson(jo.ToString()) : new OnlineRules();
        }

        void Fix()
        {
            var d = new OnlineRules();
            if (LeagueMaxIl == null || LeagueMaxIl.Length < 2) LeagueMaxIl = d.LeagueMaxIl;
            if (LeagueStageCap == null || LeagueStageCap.Length < 2) LeagueStageCap = d.LeagueStageCap;
            if (LeagueHeroCap == null || LeagueHeroCap.Length < 2) LeagueHeroCap = d.LeagueHeroCap;
            if (LeagueMutationCap == null || LeagueMutationCap.Length < 2) LeagueMutationCap = d.LeagueMutationCap;
            if (LeagueExtraSlotCap == null || LeagueExtraSlotCap.Length < 2) LeagueExtraSlotCap = d.LeagueExtraSlotCap;
            if (LeagueRewardCoef == null || LeagueRewardCoef.Length < 3) LeagueRewardCoef = d.LeagueRewardCoef;
            if (RatingTop10Gold == null || RatingTop10Gold.Length < 3) RatingTop10Gold = d.RatingTop10Gold;
            HeirReward = HeirReward ?? d.HeirReward; DiscipleReward = DiscipleReward ?? d.DiscipleReward;
            Top1Reward = Top1Reward ?? d.Top1Reward; Top10Reward = Top10Reward ?? d.Top10Reward; Top30Reward = Top30Reward ?? d.Top30Reward;
            ParticipationReward = ParticipationReward ?? d.ParticipationReward;
            if (SeasonTopRuns < 1) SeasonTopRuns = 1;
            if (IlWindowDays < 1) IlWindowDays = 1;
        }

        /// <summary>보유 IL → 리그(4.8절).</summary>
        public int LeagueFor(int holdingIl) => holdingIl <= LeagueMaxIl[0] ? 0 : (holdingIl <= LeagueMaxIl[1] ? 1 : 2);
    }

    /// <summary>아이템 레벨과 리그 제한(docs/online.md 4.7~4.8절). 소켓·시계 없는 순수 계산.</summary>
    public static class LeagueRules
    {
        public static int BlockScore(OnlineRules r, IContentDb db, string blockId)
        {
            if (string.IsNullOrEmpty(blockId) || db == null) return 0;
            var b = db.Block(blockId);
            if (b == null) return 0;
            if (b.Rarity == Ids.Rarity.Hero) return r.IlHero;
            if (b.Rarity == Ids.Rarity.Rare) return r.IlRare;
            return r.IlCommon;
        }

        static int BaseLinks(OnlineRules r, IContentDb db, int character)
        {
            var def = db != null ? db.Character(Ids.Char.Of(character)) : null;
            return Math.Min(def != null ? def.BaseLinkSlots : r.IlBaseLinkSlots, r.IlBaseLinkSlots);
        }

        /// <summary>
        /// 장착 IL = 10 × 단계 + 장착 블록 점수 합(코어 1 + 연결 칸) + 6 × 변이 수(2개까지) + 5 × 확장 칸.
        /// 타고난 다섯째 기본 칸은 점수가 가장 낮은 블록부터 빼서 세지 않는다.
        /// </summary>
        public static int EquippedIl(OnlineRules r, IContentDb db, int character, int stage, string core, IList<string> links, int mutations, int extraSlots)
        {
            var scores = new List<int>();
            if (links != null) foreach (var l in links) scores.Add(BlockScore(r, db, l));
            scores.Sort((a, b) => b.CompareTo(a));
            int count = Math.Min(scores.Count, BaseLinks(r, db, character) + Math.Max(0, extraSlots));
            int sum = BlockScore(r, db, core);
            for (int i = 0; i < count; i++) sum += scores[i];
            return r.IlPerStage * Math.Max(0, stage) + sum + r.IlPerMutation * Math.Min(Math.Max(0, mutations), r.IlMutationCount) + r.IlPerExtraSlot * Math.Max(0, extraSlots);
        }

        /// <summary>
        /// 그 캐릭터 지팡이로 낼 수 있는 가장 높은 장착 IL: 가진 블록(창고·어느 지팡이에든 끼운 것·장터·우편 전부) 중 점수 높은 것을 칸 수만큼 채웠다고 친다.
        /// ownedScores = 가진 블록 점수들(내림차순).
        /// </summary>
        public static int BestIl(OnlineRules r, IContentDb db, int character, int stage, int mutations, int extraSlots, IList<int> ownedScoresDesc)
        {
            int slots = 1 + BaseLinks(r, db, character) + Math.Max(0, extraSlots);
            int sum = 0;
            // 코어 한 칸은 누구나 가진 마력탄(일반)이 최소다.
            for (int i = 0; i < slots; i++)
            {
                int s = ownedScoresDesc != null && i < ownedScoresDesc.Count ? ownedScoresDesc[i] : 0;
                if (i == 0 && s < r.IlCommon) s = r.IlCommon;
                sum += s;
            }
            return r.IlPerStage * Math.Max(0, stage) + sum + r.IlPerMutation * Math.Min(Math.Max(0, mutations), r.IlMutationCount) + r.IlPerExtraSlot * Math.Max(0, extraSlots);
        }

        /// <summary>
        /// 리그 제한을 출발 설정에 건다(4.8절: 못 들고 들어가는 것이 아니라 경기 중에 제한값으로 깎여 적용된다).
        /// 단계 상한, 변이 수 상한(오래된 것부터 남긴다), 확장 칸(뒤에서부터 닫는다), 영웅 블록 수(넘는 것은 뒤에서부터 빈 칸).
        /// 후계 리그(2)는 그대로 돌려준다.
        /// </summary>
        public static RunConfig ApplyLimits(OnlineRules r, IContentDb db, RunConfig member, int league)
        {
            if (member == null || league >= OnlineRules.LeagueTop || league < 0) return member;
            if (member.WandStage > r.LeagueStageCap[league]) member.WandStage = r.LeagueStageCap[league];
            if (member.Mutations != null && member.Mutations.Count > r.LeagueMutationCap[league])
                member.Mutations = member.Mutations.GetRange(0, r.LeagueMutationCap[league]);
            var lo = member.Loadout;
            if (lo == null) return member;
            int character = Ids.Char.NumberOf(member.CharId);
            var def = db != null ? db.Character(member.CharId) : null;
            int baseSlots = def != null ? def.BaseLinkSlots : (lo.Links != null ? lo.Links.Count : 0);
            int maxLinks = baseSlots + r.LeagueExtraSlotCap[league];
            if (lo.Links != null && lo.Links.Count > maxLinks) lo.Links.RemoveRange(maxLinks, lo.Links.Count - maxLinks);
            if (lo.Links != null) member.LinkSlotCount = lo.Links.Count;
            int hero = 0, cap = r.LeagueHeroCap[league];
            if (IsHero(db, lo.Core)) { if (hero < cap) hero++; else lo.Core = Ids.Block.MagicBolt; }
            if (lo.Links != null)
                for (int i = 0; i < lo.Links.Count; i++)
                    if (IsHero(db, lo.Links[i])) { if (hero < cap) hero++; else lo.Links[i] = null; }
            return member;
        }

        static bool IsHero(IContentDb db, string id)
        {
            if (string.IsNullOrEmpty(id) || db == null) return false;
            var b = db.Block(id);
            return b != null && b.Rarity == Ids.Rarity.Hero;
        }
    }

    /// <summary>리그 하나의 결투 점수(Glicko-2, 4.10절).</summary>
    public sealed class RankedRating
    {
        public double Rating = 1500, Deviation = 350, Volatility = 0.06;
        public int Games, Wins, Losses, Draws;
    }

    /// <summary>Glicko-2 한 경기 갱신(Glickman 2012). score: 1 승, 0.5 무, 0 패.</summary>
    public static class Glicko2
    {
        const double Scale = 173.7178;

        public static void Update(OnlineRules rules, RankedRating me, double oppRating, double oppDeviation, double score)
        {
            double mu = (me.Rating - 1500) / Scale, phi = me.Deviation / Scale;
            double muJ = (oppRating - 1500) / Scale, phiJ = oppDeviation / Scale;
            double g = 1.0 / Math.Sqrt(1.0 + 3.0 * phiJ * phiJ / (Math.PI * Math.PI));
            double e = 1.0 / (1.0 + Math.Exp(-g * (mu - muJ)));
            double v = 1.0 / (g * g * e * (1 - e));
            double delta = v * g * (score - e);
            double sigma = me.Volatility, tau = rules.RatingTau;
            double a = Math.Log(sigma * sigma);
            Func<double, double> f = x =>
            {
                double ex = Math.Exp(x);
                double d = phi * phi + v + ex;
                return ex * (delta * delta - d) / (2 * d * d) - (x - a) / (tau * tau);
            };
            double A = a, B;
            if (delta * delta > phi * phi + v) B = Math.Log(delta * delta - phi * phi - v);
            else
            {
                int k = 1;
                while (f(a - k * tau) < 0 && k < 100) k++;
                B = a - k * tau;
            }
            double fA = f(A), fB = f(B);
            for (int it = 0; it < 100 && Math.Abs(B - A) > 1e-6; it++)
            {
                double C = A + (A - B) * fA / (fB - fA), fC = f(C);
                if (fC * fB <= 0) { A = B; fA = fB; } else fA /= 2;
                B = C; fB = fC;
            }
            double sigma2 = Math.Exp(A / 2);
            double phiStar = Math.Sqrt(phi * phi + sigma2 * sigma2);
            double phi2 = 1.0 / Math.Sqrt(1.0 / (phiStar * phiStar) + 1.0 / v);
            double mu2 = mu + phi2 * phi2 * g * (score - e);
            me.Rating = mu2 * Scale + 1500;
            me.Deviation = Math.Min(rules.RatingDeviation, phi2 * Scale);
            me.Volatility = sigma2;
        }
    }

    // ------------------------------------------------------------ 서버 프로필에 더한 것(계정마다)

    /// <summary>우편 한 통(장터 대금·돌아온 물건·시즌 보상). 받기 전에는 창고·골드에 들어가지 않는다.</summary>
    public sealed class MailItem
    {
        public long Id;
        /// <summary>까닭: "market_sold", "market_expired", "season_reward", "system".</summary>
        public string Reason;
        public long Gold;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        /// <summary>덧붙인 값(아이템 id, 시즌 번호 …). 글자는 클라이언트 이름 표가 만든다.</summary>
        public string Note;
        public long CreatedTicks, ExpiresTicks;
    }

    /// <summary>판 하나의 요약(순위표 줄).</summary>
    public sealed class RunBrief
    {
        public long Score;
        public int Wave, Character, Stage;
        public long AtTicks;
    }

    public sealed class IlDay { public int Day; public int Il; }
    public sealed class OpponentDay { public long Opponent; public int Day; public int Count; }

    /// <summary>계정마다의 온라인 상태(우편·차단·리그·결투 점수·시즌 기록). ServerProfile.X로 붙는다.</summary>
    public sealed class OnlineExtra
    {
        public List<MailItem> Mail = new List<MailItem>();
        public long NextMailId = 1;
        public List<long> Blocked = new List<long>();

        /// <summary>리그(0 새싹·1 맹수·2 후계)와 그것을 정한 시즌. 시즌 안에서는 올라가기만 한다.</summary>
        public int League;
        public int LeagueSeason;
        public bool LeagueOptedUp;
        /// <summary>날마다의 보유 IL(최근 IlWindowDays일)과 그 최고값.</summary>
        public List<IlDay> IlHistory = new List<IlDay>();
        public int HoldingIl;

        public RankedRating[] Ranked = { new RankedRating(), new RankedRating(), new RankedRating() };
        public List<OpponentDay> Opponents = new List<OpponentDay>();
        public long LastRankedOpponent;
        public List<long> LeaveTicks = new List<long>();
        public long QueueBanUntilTicks;
        public long ChatMutedUntilTicks;

        public long PlaySeconds;

        /// <summary>이번 시즌의 일반 판(점수 높은 순, 최대 SeasonTopRuns개)·캐릭터별 최고·판 수.</summary>
        public int SeasonId;
        public List<RunBrief> SeasonTop = new List<RunBrief>();
        public RunBrief[] SeasonCharBest = new RunBrief[5];
        public int SeasonRuns;
        /// <summary>리그마다 이번 시즌 자동 매치 판 수.</summary>
        public int[] SeasonRanked = new int[3];
        /// <summary>주간(월요일 0시 UTC 기준 주 번호) 최고와 "+7 이하" 부문 최고.</summary>
        public int Week;
        public RunBrief WeekBest, WeekBestLow;
        /// <summary>역대 최고.</summary>
        public RunBrief EverBest;

        public List<string> Titles = new List<string>();
    }

    /// <summary>시즌 하나(서버 전체). 시각은 UTC ticks.</summary>
    public sealed class SeasonState
    {
        public int Id = 1;
        public long StartTicks, EndTicks, SettleUntilTicks;
    }

    /// <summary>후계 점수 한 줄(6.3절).</summary>
    public sealed class HeirRow
    {
        public long AccountId;
        public string Name;
        public int League, Rank;
        public double S, R, QE, QP, E, P, Total;
        public int Runs, RankedGames;
        public long AchievedTicks;
    }

    /// <summary>끝난 시즌의 기록(역대 후계자 명단 + 그 시즌 결과).</summary>
    public sealed class SeasonArchive
    {
        public int SeasonId;
        public long EndedTicks;
        public long HeirId;
        public string HeirName;
        public double HeirTotal;
        public List<HeirRow> Results = new List<HeirRow>();
    }

    /// <summary>신고 한 건(5.3절). 서버가 그 사람의 최근 채팅을 붙인다.</summary>
    public sealed class ReportEntry
    {
        public long Id, ReporterId, TargetId;
        public int Reason;
        public List<string> Chat = new List<string>();
        public long AtTicks;
        public string Status = "open";
    }

    /// <summary>판 기록 한 줄(run 표). 판은 서버가 돌리므로 재실행 검증은 없다(11-online.md 1절 solo 결정).</summary>
    public sealed class RunRecord
    {
        public long Id, AccountId;
        public string Name;
        public int Character, Stage, Wave, Party, SeasonId;
        public long Score, Gold, AtTicks;
        public bool Ranked;
    }

    /// <summary>순위표 한 줄(화면용).</summary>
    public sealed class BoardRow
    {
        public int Rank;
        public long AccountId;
        public string Name;
        public long Score;
        public int Wave, Character, Stage, League;
    }

    public enum BoardKind : byte { SeasonOverall = 0, Character = 1, Weekly = 2, WeeklyLow = 3, AllTime = 4, Friends = 5, RankedLeague = 6, Heir = 7, DuelCasual = 8 }
}

namespace Wizard.Solo.Net
{
    /// <summary>시즌·리그·내 점수 한 벌(시즌 화면용, NetMsg.SeasonInfo).</summary>
    public sealed class SeasonInfo
    {
        public int SeasonId;
        /// <summary>0 = 진행 중, 1 = 정산 기간.</summary>
        public byte Phase;
        public long EndTicks, SettleUntilTicks, NowTicks;
        public int League;
        public bool OptedUp;
        public int HoldingIl;
        public int[] EquippedIl = new int[5];
        public int[] Rating = new int[OnlineRules.LeagueCount], RatedGames = new int[OnlineRules.LeagueCount], RatedWins = new int[OnlineRules.LeagueCount], RatedLosses = new int[OnlineRules.LeagueCount];
        /// <summary>S(시즌 상위 판 평균), 일반 판 수, 내 리그의 자동 매치 판 수.</summary>
        public long SeasonScore;
        public int SeasonRuns, SeasonRanked;
        /// <summary>후계 점수(E + P)와 내 리그 안 순위(0 = 아직 없음), 리그 인원.</summary>
        public int E, P, Total, Rank, Population;
        public int MailCount;
        public long PlaySeconds;
        /// <summary>AccountStore.QueueCheck.</summary>
        public byte Queue;
        public int QueueWaitSeconds, ChatMutedSeconds;
        public List<string> Titles = new List<string>();
    }
}
