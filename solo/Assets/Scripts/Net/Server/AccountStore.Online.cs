using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Net
{
    // 우편함 · 차단 · 신고 · 아이템 레벨과 리그 · 자동 매치 결투 점수 · 판 기록과 순위표 · 시즌(docs/online.md 3.5·4.7~4.10·5.2~5.3·6절, 11-online.md 6.14절).
    // 시각은 전부 UTC ticks로 받는다(테스트가 시계를 준다).
    public sealed partial class AccountStore
    {
        /// <summary>온라인 규칙 수치(DataSolo/OnlineRules.json). 방 서버가 넣는다.</summary>
        public OnlineRules Rules = new OnlineRules();
        /// <summary>콘텐츠(블록 등급·캐릭터 칸 수). 있으면 Save()마다 손댄 계정의 보유 IL·리그를 다시 본다.</summary>
        public IContentDb Content;
        /// <summary>서버 시계(UTC ticks). 테스트는 바꾼다.</summary>
        public Func<long> Clock = () => DateTime.UtcNow.Ticks;

        bool _worldDirty;

        static int DayOf(long ticks) => (int)(ticks / TimeSpan.TicksPerDay);
        /// <summary>주 번호(월요일 0시 UTC에 바뀐다 — 0001-01-01이 월요일).</summary>
        public static int WeekOf(long ticks) => DayOf(ticks) / 7;

        public static OnlineExtra X(ServerProfile p)
        {
            var x = p.X ?? (p.X = new OnlineExtra());
            if (x.Mail == null) x.Mail = new List<MailItem>();
            if (x.Blocked == null) x.Blocked = new List<long>();
            if (x.IlHistory == null) x.IlHistory = new List<IlDay>();
            if (x.Ranked == null || x.Ranked.Length != OnlineRules.LeagueCount) x.Ranked = new[] { new RankedRating(), new RankedRating(), new RankedRating() };
            for (int i = 0; i < x.Ranked.Length; i++) if (x.Ranked[i] == null) x.Ranked[i] = new RankedRating();
            if (x.Opponents == null) x.Opponents = new List<OpponentDay>();
            if (x.LeaveTicks == null) x.LeaveTicks = new List<long>();
            if (x.SeasonTop == null) x.SeasonTop = new List<RunBrief>();
            if (x.SeasonCharBest == null || x.SeasonCharBest.Length != 5) x.SeasonCharBest = new RunBrief[5];
            if (x.SeasonRanked == null || x.SeasonRanked.Length != OnlineRules.LeagueCount) x.SeasonRanked = new int[OnlineRules.LeagueCount];
            if (x.Titles == null) x.Titles = new List<string>();
            return x;
        }

        RankedRating NewRating() => new RankedRating { Rating = Rules.RatingStart, Deviation = Rules.RatingDeviation, Volatility = Rules.RatingVolatility };

        // ------------------------------------------------------------ 우편함

        public enum MailResult { Ok, NoAccount, NotFound, Empty }

        public const string MailMarketSold = "market_sold", MailMarketExpired = "market_expired", MailSeasonReward = "season_reward", MailSystem = "system";

        /// <summary>우편 한 통을 넣는다(받기 전에는 창고·골드에 들어가지 않는다). 저장은 부른 쪽의 Save()가 한다.</summary>
        public MailItem SendMail(long id, string reason, long gold, IDictionary<string, int> items, string note, long now)
        {
            var a = Get(id);
            if (a == null) return null;
            var x = X(a.Profile);
            var m = new MailItem
            {
                Id = x.NextMailId++, Reason = reason ?? MailSystem, Gold = Math.Max(0, gold), Note = note,
                CreatedTicks = now, ExpiresTicks = now + TimeSpan.FromDays(Rules.MailDays).Ticks,
            };
            if (items != null) foreach (var kv in items) if (kv.Value > 0 && !string.IsNullOrEmpty(kv.Key)) m.Items[kv.Key] = kv.Value;
            x.Mail.Add(m);
            return m;
        }

        /// <summary>우편 목록(기한이 지난 것은 지운다). 오래된 것부터.</summary>
        public List<MailItem> MailOf(long id, long now)
        {
            var a = Peek(id);
            if (a == null) return new List<MailItem>();
            var x = X(a.Profile);
            int removed = x.Mail.RemoveAll(m => m == null || m.ExpiresTicks <= now);
            if (removed > 0) { Touch(id); Save(); }
            return new List<MailItem>(x.Mail);
        }

        /// <summary>우편을 받는다(mailId = 0이면 전부): 골드와 물건이 창고로 간다.</summary>
        public MailResult ClaimMail(long id, long mailId, long now)
        {
            var a = Get(id);
            if (a == null) return MailResult.NoAccount;
            var x = X(a.Profile);
            x.Mail.RemoveAll(m => m == null || m.ExpiresTicks <= now);
            if (x.Mail.Count == 0) return mailId == 0 ? MailResult.Empty : MailResult.NotFound;
            bool any = false;
            for (int i = x.Mail.Count - 1; i >= 0; i--)
            {
                var m = x.Mail[i];
                if (mailId != 0 && m.Id != mailId) continue;
                a.Profile.Gold += m.Gold;
                if (m.Items != null) foreach (var kv in m.Items) a.Profile.Add(kv.Key, kv.Value);
                x.Mail.RemoveAt(i);
                any = true;
            }
            if (!any) return MailResult.NotFound;
            Save();
            return MailResult.Ok;
        }

        // ------------------------------------------------------------ 차단

        public enum BlockResult { Ok, NoAccount, NotFound, Self, Already, TooMany }

        /// <summary>owner가 other를 차단했나.</summary>
        public bool IsBlocked(long owner, long other)
        {
            var a = Peek(owner);
            return a != null && a.Profile.X != null && a.Profile.X.Blocked != null && a.Profile.X.Blocked.Contains(other);
        }

        /// <summary>둘 중 누구든 상대를 차단했나(채팅·귓속말·거래 청·친구 신청이 오가지 않는다).</summary>
        public bool BlockedEither(long a, long b) => a > 0 && b > 0 && (IsBlocked(a, b) || IsBlocked(b, a));

        /// <summary>이름으로 차단한다. 친구였다면 끊고, 걸려 있던 친구 신청도 지운다.</summary>
        public BlockResult Block(long id, string name)
        {
            var a = Get(id);
            if (a == null) return BlockResult.NoAccount;
            var b = FindByName(name);
            if (b == null) return BlockResult.NotFound;
            if (b.Id == id) return BlockResult.Self;
            var x = X(a.Profile);
            if (x.Blocked.Contains(b.Id)) return BlockResult.Already;
            if (x.Blocked.Count >= Rules.BlockMax) return BlockResult.TooMany;
            x.Blocked.Add(b.Id);
            Fix2(a.Profile);
            a.Profile.Friends.Remove(b.Id);
            a.Profile.FriendRequests.Remove(b.Id);
            var br = Get(b.Id);
            Fix2(br.Profile);
            br.Profile.Friends.Remove(id);
            br.Profile.FriendRequests.Remove(id);
            Save();
            return BlockResult.Ok;
        }

        public BlockResult Unblock(long id, long otherId)
        {
            var a = Get(id);
            if (a == null) return BlockResult.NoAccount;
            if (!X(a.Profile).Blocked.Remove(otherId)) return BlockResult.NotFound;
            Save();
            return BlockResult.Ok;
        }

        public List<FriendInfo> BlockedOf(long id)
        {
            var l = new List<FriendInfo>();
            var a = Peek(id);
            if (a == null) return l;
            foreach (var b in X(a.Profile).Blocked)
            {
                var r = Peek(b);
                if (r != null) l.Add(new FriendInfo { Id = b, Name = r.Name, Rating = r.Profile.Rating });
            }
            return l;
        }

        // ------------------------------------------------------------ 신고

        public enum ReportResult { Ok, NoAccount, NotFound, Self, BadReason, Duplicate }

        /// <summary>신고 대기열(운영자가 본다).</summary>
        public IReadOnlyList<ReportEntry> Reports => _data.Reports;

        /// <summary>
        /// 신고 한 건(사유 1~ReportReasons: 욕설·사기·현금 거래·부정 프로그램·기타). chat = 서버가 붙이는 그 사람의 최근 채팅.
        /// 서로 다른 계정 ReportDistinct개 이상에게 ReportWindowHours 안에 신고당하면 채팅이 ReportMuteMinutes 동안 막힌다.
        /// 같은 사람을 그 시간 안에 다시 신고할 수는 없다.
        /// </summary>
        public ReportResult Report(long reporterId, string targetName, int reason, IList<string> chat, long now)
        {
            var a = Peek(reporterId);
            if (a == null) return ReportResult.NoAccount;
            var t = FindByName(targetName);
            if (t == null) return ReportResult.NotFound;
            if (t.Id == reporterId) return ReportResult.Self;
            if (reason < 1 || reason > Rules.ReportReasons) return ReportResult.BadReason;
            long window = TimeSpan.FromHours(Rules.ReportWindowHours).Ticks;
            var who = new HashSet<long>();
            foreach (var r in _data.Reports)
            {
                if (r.TargetId != t.Id || now - r.AtTicks >= window) continue;
                if (r.ReporterId == reporterId) return ReportResult.Duplicate;
                who.Add(r.ReporterId);
            }
            var e = new ReportEntry { Id = _data.NextReportId++, ReporterId = reporterId, TargetId = t.Id, Reason = reason, AtTicks = now };
            if (chat != null)
                for (int i = Math.Max(0, chat.Count - Rules.ReportChatLines); i < chat.Count; i++) e.Chat.Add(chat[i]);
            _data.Reports.Add(e);
            _worldDirty = true;
            who.Add(reporterId);
            if (who.Count >= Rules.ReportDistinct)
            {
                var tr = Get(t.Id);
                long until = now + TimeSpan.FromMinutes(Rules.ReportMuteMinutes).Ticks;
                if (X(tr.Profile).ChatMutedUntilTicks < until) X(tr.Profile).ChatMutedUntilTicks = until;
            }
            Save();
            return ReportResult.Ok;
        }

        /// <summary>지금 채팅이 막혀 있나(남은 초, 아니면 0).</summary>
        public int ChatMutedSeconds(long id, long now)
        {
            var a = Peek(id);
            if (a == null || a.Profile.X == null) return 0;
            long left = a.Profile.X.ChatMutedUntilTicks - now;
            return left > 0 ? (int)Math.Ceiling(left / (double)TimeSpan.TicksPerSecond) : 0;
        }

        // ------------------------------------------------------------ 아이템 레벨 · 리그

        /// <summary>가진 블록 점수들(내림차순): 창고 + 어느 지팡이에든 끼운 것 + 장터에 올린 것 + 우편함의 것(4.7·4.9절).</summary>
        List<int> OwnedScores(AccountRecord a)
        {
            var scores = new List<int>();
            var p = a.Profile;
            void Add(string id, int n)
            {
                int s = LeagueRules.BlockScore(Rules, Content, id);
                if (s <= 0) return;
                for (int i = 0; i < n && i < 16; i++) scores.Add(s);
            }
            if (p.Items != null) foreach (var kv in p.Items) Add(kv.Key, kv.Value);
            if (p.Loadouts != null)
                foreach (var lo in p.Loadouts)
                {
                    if (lo == null) continue;
                    Add(lo.Core, 1);
                    if (lo.Links != null) foreach (var l in lo.Links) Add(l, 1);
                }
            foreach (var l in _data.Listings) if (l.SellerId == a.Id) Add(l.ItemId, l.Qty);
            if (p.X != null && p.X.Mail != null)
                foreach (var m in p.X.Mail) if (m != null && m.Items != null) foreach (var kv in m.Items) Add(kv.Key, kv.Value);
            scores.Sort((u, v) => v.CompareTo(u));
            return scores;
        }

        /// <summary>지금 가진 것으로 낼 수 있는 가장 높은 장착 IL(가장 좋은 한 자루의 값).</summary>
        public int CurrentHoldingIl(long id)
        {
            var a = Peek(id);
            if (a == null) return 0;
            Fix(a.Profile);
            var scores = OwnedScores(a);
            int best = 0;
            for (int c = 1; c <= 5; c++)
            {
                int il = LeagueRules.BestIl(Rules, Content, c, a.Profile.WandStages[c - 1], a.Profile.MutationsOf(c).Count, a.Profile.SlotsExtra[c - 1], scores);
                if (il > best) best = il;
            }
            return best;
        }

        /// <summary>그 캐릭터의 지금 조립으로 계산한 장착 IL.</summary>
        public int EquippedIl(long id, int character)
        {
            var a = Peek(id);
            if (a == null || character < 1 || character > 5) return 0;
            Fix(a.Profile);
            var lo = a.Profile.LoadoutOf(character);
            return LeagueRules.EquippedIl(Rules, Content, character, a.Profile.WandStages[character - 1], lo != null && !string.IsNullOrEmpty(lo.Core) ? lo.Core : Ids.Block.MagicBolt,
                lo != null ? lo.Links : null, a.Profile.MutationsOf(character).Count, a.Profile.SlotsExtra[character - 1]);
        }

        /// <summary>
        /// 보유 IL(최근 IlWindowDays일의 최고값: 오를 때는 바로, 내릴 때는 그 뒤에)과 리그를 다시 본다.
        /// 리그는 시즌 안에서 올라가기만 하고, 시즌이 바뀐 뒤 처음 볼 때 그때의 보유 IL로 다시 정한다(4.8절).
        /// </summary>
        void RefreshIl(AccountRecord a, long now)
        {
            if (Content == null || a == null) return;
            var x = X(a.Profile);
            int today = DayOf(now), cur = CurrentHoldingIl(a.Id);
            x.IlHistory.RemoveAll(d => d == null || today - d.Day >= Rules.IlWindowDays || d.Day > today);
            var row = x.IlHistory.Find(d => d.Day == today);
            if (row == null) x.IlHistory.Add(new IlDay { Day = today, Il = cur });
            else if (cur > row.Il) row.Il = cur;
            int max = 0;
            foreach (var d in x.IlHistory) if (d.Il > max) max = d.Il;
            x.HoldingIl = max;
            int season = EnsureSeason(now).Id, byIl = Rules.LeagueFor(max);
            if (x.LeagueSeason != season) { x.League = byIl; x.LeagueSeason = season; x.LeagueOptedUp = false; }
            else if (byIl > x.League) x.League = byIl;
        }

        public int LeagueOf(long id)
        {
            var a = Peek(id);
            return a != null && a.Profile.X != null ? a.Profile.X.League : 0;
        }

        public enum LeagueResult { Ok, NoAccount, AlreadyTop, AlreadyUsed }

        /// <summary>스스로 위 리그로 올라간다(시즌에 한 번, 그 시즌에는 못 내려온다).</summary>
        public LeagueResult OptUpLeague(long id)
        {
            var a = Get(id);
            if (a == null) return LeagueResult.NoAccount;
            var x = X(a.Profile);
            RefreshIl(a, Clock());
            if (x.League >= OnlineRules.LeagueTop) return LeagueResult.AlreadyTop;
            if (x.LeagueOptedUp) return LeagueResult.AlreadyUsed;
            x.League++;
            x.LeagueOptedUp = true;
            Save();
            return LeagueResult.Ok;
        }

        // ------------------------------------------------------------ 자동 매치 결투 점수

        public enum QueueCheck : byte { Ok = 0, NoAccount = 1, NeedWave = 2, NeedPlayTime = 3, Banned = 4, Busy = 5 }

        /// <summary>자동 매치에 들어갈 수 있나(4.9절: 웨이브 10 도달 + 누적 플레이 2시간, 이탈 금지 시간). waitSeconds = 금지가 풀릴 때까지.</summary>
        public QueueCheck CanQueue(long id, long now, out int waitSeconds)
        {
            waitSeconds = 0;
            var a = Peek(id);
            if (a == null) return QueueCheck.NoAccount;
            var x = X(a.Profile);
            if (x.QueueBanUntilTicks > now)
            {
                waitSeconds = (int)Math.Ceiling((x.QueueBanUntilTicks - now) / (double)TimeSpan.TicksPerSecond);
                return QueueCheck.Banned;
            }
            if (a.Profile.CoopBestWave < Rules.QueueMinWave) return QueueCheck.NeedWave;
            if (x.PlaySeconds < Rules.QueueMinPlaySeconds) return QueueCheck.NeedPlayTime;
            return QueueCheck.Ok;
        }

        public RankedRating RatingOf(long id, int league)
        {
            var a = Peek(id);
            if (a == null || league < 0 || league >= OnlineRules.LeagueCount) return NewRating();
            var r = X(a.Profile).Ranked[league];
            if (r.Games == 0 && r.Deviation == 350 && r.Rating == 1500) { r.Rating = Rules.RatingStart; r.Deviation = Rules.RatingDeviation; r.Volatility = Rules.RatingVolatility; }
            return r;
        }

        static OpponentDay Opp(OnlineExtra x, long other, int day)
        {
            x.Opponents.RemoveAll(o => o == null || o.Day != day);
            var o2 = x.Opponents.Find(o => o.Opponent == other);
            if (o2 == null) { o2 = new OpponentDay { Opponent = other, Day = day }; x.Opponents.Add(o2); }
            return o2;
        }

        /// <summary>
        /// 자동 매치 한 경기(winnerId = 0이면 무승부). 리그의 결투 점수(Glicko-2)를 고친다. 점수에 반영하지 않는 때: 같은 상대와 하루
        /// SameOpponentPerDay판을 넘겼을 때, 정산 기간. 전적과 승리 보상은 그대로 준다. 돌려주는 값 = 점수에 반영했나.
        /// </summary>
        public bool RecordRankedDuel(long aId, long bId, long winnerId, int league, long now)
        {
            var a = Get(aId);
            var b = Get(bId);
            if (a == null || b == null || aId == bId || league < 0 || league >= OnlineRules.LeagueCount) return false;
            var xa = X(a.Profile);
            var xb = X(b.Profile);
            var season = EnsureSeason(now);
            RollStats(xa, season.Id, now);
            RollStats(xb, season.Id, now);
            int day = DayOf(now);
            var oa = Opp(xa, bId, day);
            var ob = Opp(xb, aId, day);
            bool rated = oa.Count < Rules.SameOpponentPerDay && ob.Count < Rules.SameOpponentPerDay && PhaseAt(now) == SeasonPhase.Active;
            oa.Count++; ob.Count++;
            xa.LastRankedOpponent = bId;
            xb.LastRankedOpponent = aId;
            var ra = RatingOf(aId, league);
            var rb = RatingOf(bId, league);
            double sa = winnerId == 0 ? 0.5 : (winnerId == aId ? 1.0 : 0.0);
            if (rated)
            {
                double aR = ra.Rating, aD = ra.Deviation, bR = rb.Rating, bD = rb.Deviation;
                Glicko2.Update(Rules, ra, bR, bD, sa);
                Glicko2.Update(Rules, rb, aR, aD, 1.0 - sa);
                ra.Games++; rb.Games++;
                xa.SeasonRanked[league]++; xb.SeasonRanked[league]++;
            }
            if (winnerId == 0) { a.Profile.DuelDraws++; b.Profile.DuelDraws++; if (rated) { ra.Draws++; rb.Draws++; } }
            else
            {
                var w = winnerId == aId ? a : b;
                var l = winnerId == aId ? b : a;
                w.Profile.DuelWins++; l.Profile.DuelLosses++;
                w.Profile.Gold += DuelWinGold;
                w.Profile.Add(Ids.Item.StoneLow, DuelWinStones);
                if (rated) { (winnerId == aId ? ra : rb).Wins++; (winnerId == aId ? rb : ra).Losses++; }
            }
            Save();
            return rated;
        }

        /// <summary>자동 매치 중도 이탈 한 번. 하루 LeavesPerDay번을 넘기면 QueueBanMinutes 동안 대기열에 못 들어간다.</summary>
        public void RecordLeave(long id, long now)
        {
            var a = Get(id);
            if (a == null) return;
            var x = X(a.Profile);
            x.LeaveTicks.RemoveAll(t => now - t >= TimeSpan.TicksPerDay);
            x.LeaveTicks.Add(now);
            if (x.LeaveTicks.Count > Rules.LeavesPerDay) x.QueueBanUntilTicks = now + TimeSpan.FromMinutes(Rules.QueueBanMinutes).Ticks;
            Save();
        }

        public void AddPlayTime(long id, int seconds)
        {
            var a = Get(id);
            if (a == null || seconds <= 0) return;
            X(a.Profile).PlaySeconds += seconds;
        }

        // ------------------------------------------------------------ 판 기록 · 순위표

        /// <summary>시즌·주가 바뀌었으면 그 계정의 시즌·주간 기록을 비운다.</summary>
        void RollStats(OnlineExtra x, int seasonId, long now)
        {
            if (x.SeasonId != seasonId)
            {
                x.SeasonId = seasonId;
                x.SeasonTop.Clear();
                x.SeasonCharBest = new RunBrief[5];
                x.SeasonRuns = 0;
                x.SeasonRanked = new int[OnlineRules.LeagueCount];
            }
            int week = WeekOf(now);
            if (x.Week != week) { x.Week = week; x.WeekBest = null; x.WeekBestLow = null; }
        }

        public IReadOnlyList<RunRecord> Runs => _data.Runs;

        /// <summary>
        /// 서버가 돌린 판 하나를 적는다(판 기록 + 시즌 상위 판·캐릭터별 최고·주간·역대). 판은 처음부터 서버에서 돌았으므로
        /// 입력 기록 재검증은 없다(11-online.md 1절 solo 결정). 정산 기간의 판은 기록만 남고 시즌·주간 순위에는 들어가지 않는다.
        /// </summary>
        public RunRecord RecordRun(long id, int character, int stage, int wave, long score, long gold, int party, long now)
        {
            var a = Get(id);
            if (a == null || character < 1 || character > 5) return null;
            var x = X(a.Profile);
            var season = EnsureSeason(now);
            RollStats(x, season.Id, now);
            var rec = new RunRecord
            {
                Id = _data.NextRunId++, AccountId = id, Name = a.Name, Character = character, Stage = stage, Wave = wave, Party = party,
                SeasonId = season.Id, Score = Math.Max(0, score), Gold = gold, AtTicks = now, Ranked = PhaseAt(now) == SeasonPhase.Active,
            };
            _data.Runs.Add(rec);
            if (_data.Runs.Count > Rules.RunLogMax) _data.Runs.RemoveRange(0, _data.Runs.Count - Rules.RunLogMax);
            _worldDirty = true;
            var brief = new RunBrief { Score = rec.Score, Wave = wave, Character = character, Stage = stage, AtTicks = now };
            if (x.EverBest == null || brief.Score > x.EverBest.Score) x.EverBest = brief;
            if (rec.Ranked)
            {
                x.SeasonRuns++;
                x.SeasonTop.Add(brief);
                x.SeasonTop.Sort((u, v) => u.Score != v.Score ? v.Score.CompareTo(u.Score) : u.AtTicks.CompareTo(v.AtTicks));
                if (x.SeasonTop.Count > Rules.SeasonTopRuns) x.SeasonTop.RemoveRange(Rules.SeasonTopRuns, x.SeasonTop.Count - Rules.SeasonTopRuns);
                var cb = x.SeasonCharBest[character - 1];
                if (cb == null || brief.Score > cb.Score) x.SeasonCharBest[character - 1] = brief;
                if (x.WeekBest == null || brief.Score > x.WeekBest.Score) x.WeekBest = brief;
                if (stage <= Rules.WeeklyLowStageMax && (x.WeekBestLow == null || brief.Score > x.WeekBestLow.Score)) x.WeekBestLow = brief;
            }
            Save();
            return rec;
        }

        /// <summary>시즌 점수 S = 시즌 상위 SeasonTopRuns판의 평균(모자란 판은 0점).</summary>
        public double SeasonScore(OnlineExtra x)
        {
            long sum = 0;
            foreach (var r in x.SeasonTop) sum += r.Score;
            return sum / (double)Rules.SeasonTopRuns;
        }

        /// <summary>
        /// 순위표(3.5절). arg = 캐릭터(Character) 또는 리그(RankedLeague·Heir). viewerId = 친구 순위표의 주인.
        /// </summary>
        public List<BoardRow> Board(BoardKind kind, int arg, int max, long viewerId, long now)
        {
            var rows = new List<BoardRow>();
            int season = EnsureSeason(now).Id, week = WeekOf(now);
            if (kind == BoardKind.Heir)
            {
                foreach (var h in HeirScores(arg, now))
                    rows.Add(new BoardRow { Rank = h.Rank, AccountId = h.AccountId, Name = h.Name, Score = (long)Math.Round(h.Total), League = h.League, Wave = h.RankedGames, Stage = h.Runs });
                if (rows.Count > max) rows.RemoveRange(max, rows.Count - max);
                return rows;
            }
            if (kind == BoardKind.DuelCasual)
            {
                foreach (var p in Top(max)) rows.Add(new BoardRow { AccountId = p.AccountId, Name = p.Name, Score = p.Rating, Wave = p.DuelWins, Stage = p.DuelLosses });
                for (int i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
                return rows;
            }
            HashSet<long> only = null;
            if (kind == BoardKind.Friends)
            {
                only = new HashSet<long> { viewerId };
                var v = Peek(viewerId);
                if (v != null && v.Profile.Friends != null) foreach (var f in v.Profile.Friends) only.Add(f);
            }
            foreach (var a in _data.Accounts)
            {
                var x = a.Profile.X;
                if (x == null || (only != null && !only.Contains(a.Id))) continue;
                X(a.Profile);
                RunBrief b = null;
                long score = 0;
                switch (kind)
                {
                    case BoardKind.SeasonOverall:
                    case BoardKind.Friends:
                        if (x.SeasonId != season || x.SeasonTop.Count == 0) continue;
                        b = x.SeasonTop[0];
                        score = (long)Math.Round(SeasonScore(x));
                        break;
                    case BoardKind.Character:
                        if (x.SeasonId != season || arg < 1 || arg > 5) continue;
                        b = x.SeasonCharBest[arg - 1];
                        break;
                    case BoardKind.Weekly:
                        if (x.Week != week) continue;
                        b = x.WeekBest;
                        break;
                    case BoardKind.WeeklyLow:
                        if (x.Week != week) continue;
                        b = x.WeekBestLow;
                        break;
                    case BoardKind.AllTime:
                        b = x.EverBest;
                        break;
                    case BoardKind.RankedLeague:
                        if (arg < 0 || arg >= OnlineRules.LeagueCount || x.League != arg || x.Ranked[arg].Games == 0) continue;
                        rows.Add(new BoardRow { AccountId = a.Id, Name = a.Name, Score = (long)Math.Round(x.Ranked[arg].Rating), Wave = x.Ranked[arg].Wins, Stage = x.Ranked[arg].Losses, League = arg });
                        continue;
                }
                if (b == null) continue;
                if (score == 0) score = b.Score;
                rows.Add(new BoardRow { AccountId = a.Id, Name = a.Name, Score = score, Wave = b.Wave, Character = b.Character, Stage = b.Stage, League = x.League });
            }
            rows.Sort((u, v) => u.Score != v.Score ? v.Score.CompareTo(u.Score) : u.AccountId.CompareTo(v.AccountId));
            if (rows.Count > max) rows.RemoveRange(max, rows.Count - max);
            for (int i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
            return rows;
        }

        // ------------------------------------------------------------ 시즌

        public enum SeasonPhase : byte { Active = 0, Settling = 1 }

        public SeasonState Season => _data.Season;
        public IReadOnlyList<SeasonArchive> Hall => _data.Hall;

        SeasonState NewSeason(int id, long start)
        {
            long end = start + TimeSpan.FromDays(7.0 * Rules.SeasonWeeks).Ticks;
            return new SeasonState { Id = id, StartTicks = start, EndTicks = end, SettleUntilTicks = end + TimeSpan.FromDays(Rules.SettleDays).Ticks };
        }

        /// <summary>지금 시즌(없으면 지금부터 시즌 1을 연다).</summary>
        public SeasonState EnsureSeason(long now)
        {
            if (_data.Season == null || _data.Season.EndTicks <= _data.Season.StartTicks)
            {
                _data.Season = NewSeason(1, now);
                _worldDirty = true;
            }
            return _data.Season;
        }

        /// <summary>시즌 10주가 끝난 뒤 정산 기간(3일)에는 게임은 되지만 점수는 반영되지 않는다(6.2절).</summary>
        public SeasonPhase PhaseAt(long now) => now >= EnsureSeason(now).EndTicks ? SeasonPhase.Settling : SeasonPhase.Active;

        static double Percentile(int n, int rank) => n < 2 ? 1.0 : (n - rank) / (double)(n - 1);

        /// <summary>
        /// 그 리그의 후계 점수 표(6.3절): E = 500 × qE², P = 500 × qP². qE = 일반 판을 WaveScoreMinRuns판 이상 한 계정 안에서 S의 백분위,
        /// qP = 자동 매치를 DuelScoreMinRanked판 이상 한 계정 안에서 R의 백분위. 대상이 아니면 그 몫은 0, 대상자가 2명 미만이면 q = 1.
        /// 동점: E·P 중 낮은 쪽이 높은 사람 → S가 높은 사람 → 먼저 달성한 사람.
        /// </summary>
        public List<HeirRow> HeirScores(int league, long now)
        {
            var rows = new List<HeirRow>();
            if (league < 0 || league >= OnlineRules.LeagueCount) return rows;
            int season = EnsureSeason(now).Id;
            foreach (var a in _data.Accounts)
            {
                if (a.Profile.X == null) continue;
                var x = X(a.Profile);
                if (x.League != league || x.SeasonId != season) continue;
                if (x.SeasonRuns == 0 && x.SeasonRanked[league] == 0) continue;
                long at = 0;
                foreach (var r in x.SeasonTop) if (r.AtTicks > at) at = r.AtTicks;
                rows.Add(new HeirRow
                {
                    AccountId = a.Id, Name = a.Name, League = league, S = SeasonScore(x), R = x.Ranked[league].Rating,
                    Runs = x.SeasonRuns, RankedGames = x.SeasonRanked[league], AchievedTicks = at,
                });
            }
            var es = rows.FindAll(r => r.Runs >= Rules.WaveScoreMinRuns);
            foreach (var r in es)
            {
                int better = 0;
                foreach (var o in es) if (o.S > r.S) better++;
                r.QE = Percentile(es.Count, better + 1);
                r.E = 500.0 * r.QE * r.QE;
            }
            var ps = rows.FindAll(r => r.RankedGames >= Rules.DuelScoreMinRanked);
            foreach (var r in ps)
            {
                int better = 0;
                foreach (var o in ps) if (o.R > r.R) better++;
                r.QP = Percentile(ps.Count, better + 1);
                r.P = 500.0 * r.QP * r.QP;
            }
            foreach (var r in rows) r.Total = r.E + r.P;
            rows.Sort((u, v) =>
            {
                if (u.Total != v.Total) return v.Total.CompareTo(u.Total);
                double mu = Math.Min(u.E, u.P), mv = Math.Min(v.E, v.P);
                if (mu != mv) return mv.CompareTo(mu);
                if (u.S != v.S) return v.S.CompareTo(u.S);
                if (u.AchievedTicks != v.AchievedTicks) return u.AchievedTicks.CompareTo(v.AchievedTicks);
                return u.AccountId.CompareTo(v.AccountId);
            });
            for (int i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
            return rows;
        }

        /// <summary>그 계정의 후계 점수 줄(자기 리그). 기록이 없으면 순위 0인 빈 줄.</summary>
        public HeirRow HeirRowOf(long id, long now, out int population)
        {
            population = 0;
            var a = Peek(id);
            if (a == null) return null;
            var x = X(a.Profile);
            var rows = HeirScores(x.League, now);
            population = rows.Count;
            var mine = rows.Find(r => r.AccountId == id);
            return mine ?? new HeirRow { AccountId = id, Name = a.Name, League = x.League, R = x.Ranked[x.League].Rating };
        }

        sealed class Grant
        {
            public long Gold;
            public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
            public int League, Rank;
            public void Add(OnlineRules.Reward r, double coef)
            {
                if (r == null) return;
                Gold += (long)Math.Floor(r.Gold * coef);
                AddItem(Ids.Item.StoneHigh, r.StoneHigh, coef);
                AddItem(Ids.Item.StoneMid, r.StoneMid, coef);
                AddItem(Ids.Item.StoneLow, r.StoneLow, coef);
                AddItem(Ids.Item.Charm, r.Charm, coef);
            }
            void AddItem(string id, int n, double coef)
            {
                int q = (int)Math.Floor(n * coef);
                if (q <= 0) return;
                Items.TryGetValue(id, out int have);
                Items[id] = have + q;
            }
        }

        static int TopCount(int n, double fraction) => n <= 0 ? 0 : (int)Math.Ceiling(n * fraction);

        /// <summary>
        /// 시계를 본다. 정산 기간이 끝났으면 시즌을 정산하고(순위 확정 → 보상을 우편으로 → 초기화) 다음 시즌을 연다.
        /// 정산했으면 그 시즌의 기록을 돌려준다(아니면 null).
        /// </summary>
        public SeasonArchive TickSeason(long now)
        {
            var s = EnsureSeason(now);
            if (now < s.SettleUntilTicks) { if (_worldDirty) Save(); return null; }
            return Settle(now);
        }

        /// <summary>
        /// 시즌 정산(6.4·6.5절). 후계 리그 1위(자동 매치 HeirMinRanked판·일반 판 WaveScoreMinRuns판 이상) = 후계자, 그다음 9명 = 수제자.
        /// 리그마다 상위 1%·10%·30%와 참가 보상(재료·골드 × 리그 계수), 결투 점수 상위 10% 골드. 보상은 우편으로 간다.
        /// 초기화: 시즌·주간 기록, 결투 점수(1500 쪽으로 반 당기고 편차 200), 스스로 올라간 리그. 리그는 그때의 보유 IL로 다시 정한다.
        /// 지팡이·블록·재료·골드·친구·차단·역대 기록은 남는다. [solo] 꾸미기·시즌 주화·시즌 상점은 solo에 없어 보상에서 빠진다.
        /// </summary>
        public SeasonArchive Settle(long now)
        {
            var s = EnsureSeason(now);
            var arch = new SeasonArchive { SeasonId = s.Id, EndedTicks = now };
            var grants = new Dictionary<long, Grant>();
            Grant G(long id, int league, int rank)
            {
                if (!grants.TryGetValue(id, out var g)) grants[id] = g = new Grant { League = league, Rank = rank };
                return g;
            }
            for (int league = 0; league < OnlineRules.LeagueCount; league++)
            {
                var rows = HeirScores(league, now);
                arch.Results.AddRange(rows);
                double coef = Rules.LeagueRewardCoef[league];
                var scored = rows.FindAll(r => r.Total > 0);
                int top1 = TopCount(scored.Count, 0.01), top10 = TopCount(scored.Count, 0.10), top30 = TopCount(scored.Count, 0.30);
                for (int i = 0; i < scored.Count; i++)
                {
                    var g = G(scored[i].AccountId, league, scored[i].Rank);
                    if (i < top1) g.Add(Rules.Top1Reward, coef);
                    else if (i < top10) g.Add(Rules.Top10Reward, coef);
                    else if (i < top30) g.Add(Rules.Top30Reward, coef);
                }
                foreach (var r in rows)
                    if (r.Runs >= Rules.ParticipationGames || r.RankedGames >= Rules.ParticipationGames) G(r.AccountId, league, r.Rank).Add(Rules.ParticipationReward, coef);
                var rated = rows.FindAll(r => r.RankedGames >= Rules.DuelScoreMinRanked);
                rated.Sort((u, v) => u.R != v.R ? v.R.CompareTo(u.R) : u.AccountId.CompareTo(v.AccountId));
                int rTop = TopCount(rated.Count, 0.10);
                for (int i = 0; i < rTop; i++) G(rated[i].AccountId, league, rated[i].Rank).Gold += Rules.RatingTop10Gold[league];

                if (league != OnlineRules.LeagueTop) continue;
                // 후계자: 자격을 갖춘 가장 높은 순위. 수제자: 그다음 순위들(DiscipleRankMax위까지).
                var heir = scored.Find(r => r.RankedGames >= Rules.HeirMinRanked && r.Runs >= Rules.WaveScoreMinRuns);
                int place = heir != null ? 1 : 0;
                if (heir != null)
                {
                    arch.HeirId = heir.AccountId; arch.HeirName = heir.Name; arch.HeirTotal = heir.Total;
                    G(heir.AccountId, league, heir.Rank).Add(Rules.HeirReward, 1.0);
                    var ha = Get(heir.AccountId);
                    if (ha != null) X(ha.Profile).Titles.Add("title.heir:" + s.Id);
                    foreach (var r in scored)
                    {
                        if (r == heir) continue;
                        if (++place > Rules.DiscipleRankMax) break;
                        G(r.AccountId, league, r.Rank).Add(Rules.DiscipleReward, 1.0);
                        var da = Get(r.AccountId);
                        if (da != null) X(da.Profile).Titles.Add("title.disciple:" + s.Id);
                    }
                }
            }
            foreach (var kv in grants)
            {
                if (kv.Value.Gold <= 0 && kv.Value.Items.Count == 0) continue;
                SendMail(kv.Key, MailSeasonReward, kv.Value.Gold, kv.Value.Items, s.Id + ":" + kv.Value.League + ":" + kv.Value.Rank, now);
            }
            _data.Hall.Add(arch);

            // 다음 시즌.
            _data.Season = NewSeason(s.Id + 1, now);
            foreach (var a in _data.Accounts)
            {
                if (a.Profile.X == null) continue;
                var x = X(a.Profile);
                Touch(a.Id);
                foreach (var r in x.Ranked)
                {
                    if (r.Games == 0) continue;
                    r.Rating = Rules.RatingStart + Rules.SeasonResetPull * (r.Rating - Rules.RatingStart);
                    r.Deviation = Rules.SeasonResetDeviation;
                }
                x.Opponents.Clear();
                RollStats(x, _data.Season.Id, now);
                x.LeagueOptedUp = false;
                x.League = Rules.LeagueFor(x.HoldingIl);
                x.LeagueSeason = _data.Season.Id;
            }
            _worldDirty = true;
            Save();
            return arch;
        }
    }
}
