using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Net;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 우편함·차단·신고·아이템 레벨과 리그·자동 매치 결투 점수·판 기록과 순위표·시즌(11-online.md 6.14절).
    /// 저장소 규칙은 AccountStore에 바로, 요청·대기열·관전은 같은 프로세스 통로(LoopbackPipe)로 RoomServer에 붙어 본다.
    /// </summary>
    public class OnlineSeasonTests
    {
        long _now;
        AccountStore _st;

        static readonly long Day = TimeSpan.TicksPerDay;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc).Ticks;
            _st = new AccountStore((string)null) { Content = TestContent.Db };
            _st.Clock = () => _now;
            _server = null;
        }

        AccountRecord Acct(string name)
        {
            Assert.AreEqual(AccountStore.RegisterResult.Ok, _st.Register(name, new string('s', 40) + name, out var rec));
            return rec;
        }

        static string Hero => TestContent.Db.BlocksOfRarity(Ids.Rarity.Hero)[0];
        static string Rare => TestContent.Db.BlocksOfRarity(Ids.Rarity.Rare)[0];

        // ------------------------------------------------------------ 우편함

        [Test]
        public void Mail_MarketProceedsArriveByMail_ClaimOneOrAll_Expire()
        {
            var sel = Acct("판매자");
            var buy = Acct("구매자");
            sel.Profile.Gold = 100;
            sel.Profile.Add(Ids.Item.StoneMid, 5);
            buy.Profile.Gold = 5000;
            Assert.AreEqual(AccountStore.MarketResult.Ok, _st.List(sel.Id, Ids.Item.StoneMid, 2, 1000, _now, out var l1));
            Assert.AreEqual(AccountStore.MarketResult.Ok, _st.Buy(buy.Id, l1.Id));
            long afterDeposit = 100 - AccountStore.DepositFor(1000);
            Assert.AreEqual(afterDeposit, sel.Profile.Gold, "받기 전에는 골드가 아니다");
            var mail = _st.MailOf(sel.Id, _now);
            Assert.AreEqual(1, mail.Count);
            Assert.AreEqual(AccountStore.MailMarketSold, mail[0].Reason);
            Assert.AreEqual(1000 - AccountStore.MarketFee(1000) + AccountStore.DepositFor(1000), mail[0].Gold);
            Assert.AreEqual(Ids.Item.StoneMid, mail[0].Note);

            // 기한이 지난 등록물은 우편으로 돌아온다.
            Assert.AreEqual(AccountStore.MarketResult.Ok, _st.List(sel.Id, Ids.Item.StoneMid, 3, 300, _now, out _));
            _now += TimeSpan.FromHours(AccountStore.ListingHours + 1).Ticks;
            _st.ExpireListings(_now);
            mail = _st.MailOf(sel.Id, _now);
            Assert.AreEqual(2, mail.Count);
            Assert.AreEqual(3, mail[1].Items[Ids.Item.StoneMid]);
            Assert.AreEqual(0, sel.Profile.Qty(Ids.Item.StoneMid));

            Assert.AreEqual(AccountStore.MailResult.NotFound, _st.ClaimMail(sel.Id, 999, _now));
            Assert.AreEqual(AccountStore.MailResult.Ok, _st.ClaimMail(sel.Id, mail[1].Id, _now));
            Assert.AreEqual(3, sel.Profile.Qty(Ids.Item.StoneMid));
            Assert.AreEqual(AccountStore.MailResult.Ok, _st.ClaimMail(sel.Id, 0, _now));
            Assert.AreEqual(afterDeposit - AccountStore.DepositFor(300) + mail[0].Gold, sel.Profile.Gold);
            Assert.AreEqual(AccountStore.MailResult.Empty, _st.ClaimMail(sel.Id, 0, _now));

            // 기한(MailDays)이 지난 우편은 사라진다.
            _st.SendMail(sel.Id, AccountStore.MailSystem, 7, null, null, _now);
            _st.Save();
            Assert.AreEqual(1, _st.MailOf(sel.Id, _now + Day).Count);
            Assert.AreEqual(0, _st.MailOf(sel.Id, _now + (_st.Rules.MailDays + 1) * Day).Count);
        }

        // ------------------------------------------------------------ 차단 · 신고

        [Test]
        public void Block_CutsFriendship_AndStopsFriendRequests()
        {
            var a = Acct("가가");
            var b = Acct("나나");
            Assert.AreEqual(AccountStore.FriendResult.Requested, _st.RequestFriend(a.Id, "나나"));
            Assert.AreEqual(AccountStore.FriendResult.Ok, _st.AcceptFriend(b.Id, a.Id));
            Assert.AreEqual(AccountStore.BlockResult.Self, _st.Block(a.Id, "가가"));
            Assert.AreEqual(AccountStore.BlockResult.NotFound, _st.Block(a.Id, "없는이"));
            Assert.AreEqual(AccountStore.BlockResult.Ok, _st.Block(a.Id, "나나"));
            Assert.AreEqual(AccountStore.BlockResult.Already, _st.Block(a.Id, "나나"));
            Assert.IsFalse(a.Profile.Friends.Contains(b.Id));
            Assert.IsFalse(b.Profile.Friends.Contains(a.Id), "차단하면 양쪽에서 친구가 끊긴다");
            Assert.IsTrue(_st.BlockedEither(b.Id, a.Id));
            // 막힌 쪽의 신청은 가지 않지만 보낸 쪽에는 알리지 않는다.
            Assert.AreEqual(AccountStore.FriendResult.Requested, _st.RequestFriend(b.Id, "가가"));
            Assert.AreEqual(0, a.Profile.FriendRequests.Count);
            Assert.AreEqual(AccountStore.FriendResult.NotFound, _st.RequestFriend(a.Id, "나나"));
            Assert.AreEqual(1, _st.BlockedOf(a.Id).Count);
            Assert.AreEqual(AccountStore.BlockResult.Ok, _st.Unblock(a.Id, b.Id));
            Assert.AreEqual(AccountStore.FriendResult.Requested, _st.RequestFriend(b.Id, "가가"));
            Assert.AreEqual(1, a.Profile.FriendRequests.Count);
        }

        [Test]
        public void Report_FiveDistinctReportersIn24h_MutesChatForAnHour()
        {
            var bad = Acct("말썽");
            var chat = new List<string>();
            for (int i = 0; i < 40; i++) chat.Add("line" + i);
            var reporters = new List<AccountRecord>();
            for (int i = 0; i < 5; i++) reporters.Add(Acct("신고" + i));
            Assert.AreEqual(AccountStore.ReportResult.BadReason, _st.Report(reporters[0].Id, "말썽", 9, chat, _now));
            Assert.AreEqual(AccountStore.ReportResult.Self, _st.Report(bad.Id, "말썽", 1, chat, _now));
            for (int i = 0; i < 4; i++) Assert.AreEqual(AccountStore.ReportResult.Ok, _st.Report(reporters[i].Id, "말썽", 1, chat, _now));
            Assert.AreEqual(AccountStore.ReportResult.Duplicate, _st.Report(reporters[0].Id, "말썽", 2, chat, _now));
            Assert.AreEqual(0, _st.ChatMutedSeconds(bad.Id, _now), "네 명까지는 막히지 않는다");
            Assert.AreEqual(AccountStore.ReportResult.Ok, _st.Report(reporters[4].Id, "말썽", 5, chat, _now));
            Assert.AreEqual(_st.Rules.ReportMuteMinutes * 60, _st.ChatMutedSeconds(bad.Id, _now));
            Assert.AreEqual(0, _st.ChatMutedSeconds(bad.Id, _now + TimeSpan.FromMinutes(_st.Rules.ReportMuteMinutes).Ticks));
            Assert.AreEqual(5, _st.Reports.Count);
            Assert.AreEqual(_st.Rules.ReportChatLines, _st.Reports[0].Chat.Count, "최근 30줄만 붙는다");
            Assert.AreEqual("line39", _st.Reports[0].Chat[_st.Rules.ReportChatLines - 1]);
        }

        // ------------------------------------------------------------ 아이템 레벨 · 리그

        [Test]
        public void HoldingIl_CountsBestWandAndOwnedBlocks_EvenListedOrMailed()
        {
            var a = Acct("부자");
            _st.Save();
            var x = AccountStore.X(a.Profile);
            var r = _st.Rules;
            int baseLinks = Math.Min(TestContent.Db.Character("char.1").BaseLinkSlots, r.IlBaseLinkSlots);
            // 아무것도 없으면 코어 한 칸의 기본 블록(일반)만.
            Assert.AreEqual(r.IlCommon, x.HoldingIl);
            Assert.AreEqual(0, x.League);

            _st.Get(a.Id).Profile.WandStages[0] = 4;
            a.Profile.Add(Hero, 1);
            a.Profile.Add(Rare, 4);
            _st.Save();
            // 블록을 "가장 좋은 것부터 칸 수만큼" 채웠다고 친다(코어 1 + 기본 연결 칸).
            int expect = 40 + r.IlHero + r.IlRare * Math.Min(4, baseLinks);
            Assert.AreEqual(expect, x.HoldingIl);

            // 장터에 올려도 보유에서 빠지지 않는다.
            a.Profile.Gold = 1000;
            Assert.AreEqual(AccountStore.MarketResult.Ok, _st.List(a.Id, Hero, 1, 500, _now, out var l));
            Assert.AreEqual(expect, x.HoldingIl);
            Assert.AreEqual(expect, _st.CurrentHoldingIl(a.Id));
            // 기한이 지나 우편함에 있어도 센다.
            _now += TimeSpan.FromHours(AccountStore.ListingHours + 1).Ticks;
            _st.ExpireListings(_now);
            Assert.AreEqual(expect, _st.CurrentHoldingIl(a.Id));
        }

        [Test]
        public void League_RisesAtOnce_FallsOnlyAfter14DaysAndNextSeason()
        {
            var a = Acct("지팡이");
            var b = Acct("상인");
            _st.Save();
            var xa = AccountStore.X(a.Profile);
            var xb = AccountStore.X(b.Profile);
            Assert.AreEqual(0, xa.League);

            // +10: 보유 IL 100 이상 → 맹수 리그로 즉시.
            _st.Get(a.Id).Profile.WandStages[0] = 10;
            _st.Save();
            Assert.AreEqual(1, xa.League);
            int high = xa.HoldingIl;
            Assert.Greater(high, _st.Rules.LeagueMaxIl[0]);

            // 단계가 떨어져도(하락) 14일 동안 보유 IL은 그대로, 리그도 그대로.
            _st.Get(a.Id).Profile.WandStages[0] = 0;
            _st.Save();
            Assert.AreEqual(high, xa.HoldingIl);
            _now += 13 * Day;
            _st.Touch(a.Id); _st.Save();
            Assert.AreEqual(high, xa.HoldingIl, "14일 최고값");
            _now += 2 * Day;
            _st.Touch(a.Id); _st.Save();
            Assert.Less(xa.HoldingIl, high, "14일이 지나면 내려간다");
            Assert.AreEqual(1, xa.League, "보유 IL이 내려가도 리그는 그 시즌 동안 그대로");

            // 영웅 블록을 사면(거래로 받으면) 받은 쪽은 그 순간 오른다.
            a.Profile.Add(Hero, 6);
            a.Profile.Add(Rare, 6);
            var offer = new AccountStore.TradeOffer();
            offer.Items[Hero] = 6;
            offer.Items[Rare] = 6;
            Assert.IsTrue(_st.Swap(a.Id, offer, b.Id, new AccountStore.TradeOffer()));
            Assert.Greater(xb.HoldingIl, _st.Rules.IlCommon);

            // 스스로 위 리그로: 시즌에 한 번.
            Assert.AreEqual(AccountStore.LeagueResult.Ok, _st.OptUpLeague(b.Id));
            int after = xb.League;
            Assert.AreEqual(after >= OnlineRules.LeagueTop ? AccountStore.LeagueResult.AlreadyTop : AccountStore.LeagueResult.AlreadyUsed, _st.OptUpLeague(b.Id));

            // 다음 시즌이 시작하면 그때의 보유 IL로 다시 정한다(내려갈 수 있다).
            _now = _st.Season.SettleUntilTicks + 1;
            Assert.IsNotNull(_st.TickSeason(_now));
            Assert.AreEqual(2, _st.Season.Id);
            Assert.AreEqual(_st.Rules.LeagueFor(xa.HoldingIl), xa.League);
            Assert.AreEqual(0, xa.League);
            Assert.IsFalse(xb.LeagueOptedUp);
        }

        [Test]
        public void LeagueLimits_ClampStageMutationsSlotsAndHeroBlocks()
        {
            var r = new OnlineRules();
            var db = TestContent.Db;
            int baseSlots = db.Character("char.1").BaseLinkSlots;
            var links = new List<string>();
            for (int i = 0; i < baseSlots + 3; i++) links.Add(Hero);
            var m = OnlineMember.Build(db, 1, 11, null, new LoadoutSpec { Core = Hero, Links = links });
            m.Mutations = new List<string> { "m1", "m2", "m3" };

            var sprout = LeagueRules.ApplyLimits(r, db, NetCodec.Clone(m), 0);
            Assert.AreEqual(4, sprout.WandStage);
            Assert.AreEqual(0, sprout.Mutations.Count);
            Assert.AreEqual(baseSlots, sprout.Loadout.Links.Count, "새싹은 기본 칸만");
            Assert.AreEqual(baseSlots, sprout.LinkSlotCount);
            int heroes = (sprout.Loadout.Core == Hero ? 1 : 0);
            foreach (var l in sprout.Loadout.Links) if (l == Hero) heroes++;
            Assert.AreEqual(1, heroes, "영웅 블록 1개까지");

            var beast = LeagueRules.ApplyLimits(r, db, NetCodec.Clone(m), 1);
            Assert.AreEqual(9, beast.WandStage);
            Assert.AreEqual(1, beast.Mutations.Count);
            Assert.AreEqual(baseSlots + 2, beast.Loadout.Links.Count);

            var heir = LeagueRules.ApplyLimits(r, db, NetCodec.Clone(m), 2);
            Assert.AreEqual(11, heir.WandStage);
            Assert.AreEqual(3, heir.Mutations.Count);
            Assert.AreEqual(baseSlots + 3, heir.Loadout.Links.Count);
        }

        [Test]
        public void EquippedIl_MatchesTheDocumentFormula()
        {
            var r = new OnlineRules();
            var db = TestContent.Db;
            // 10 × 단계 + 블록 점수 + 6 × 변이(2개까지) + 5 × 확장 칸.
            var links = new List<string> { Rare, Rare, null, null };
            Assert.AreEqual(90 + r.IlHero + 2 * r.IlRare + 12 + 10, LeagueRules.EquippedIl(r, db, 1, 9, Hero, links, 3, 2));
            Assert.AreEqual(0, r.LeagueFor(70));
            Assert.AreEqual(1, r.LeagueFor(71));
            Assert.AreEqual(1, r.LeagueFor(155));
            Assert.AreEqual(2, r.LeagueFor(156));
        }

        // ------------------------------------------------------------ 결투 점수

        [Test]
        public void Glicko_KnownExample()
        {
            // Glickman(2012)의 예: 1500/200, 상대 1400/30에게 승 → 한 경기만 반영하면 점수가 오르고 편차는 줄어든다.
            var r = new OnlineRules();
            var me = new RankedRating { Rating = 1500, Deviation = 200, Volatility = 0.06 };
            Glicko2.Update(r, me, 1400, 30, 1.0);
            Assert.That(me.Rating, Is.EqualTo(1563.6).Within(0.5));
            Assert.That(me.Deviation, Is.EqualTo(175.4).Within(0.5));
            var lose = new RankedRating { Rating = 1500, Deviation = 200, Volatility = 0.06 };
            Glicko2.Update(r, lose, 1400, 30, 0.0);
            Assert.Less(lose.Rating, 1500);
        }

        [Test]
        public void Ranked_SameOpponentCountsThreeTimesADay_LeaversGetQueueBan()
        {
            var a = Acct("에이");
            var b = Acct("비이");
            for (int i = 0; i < 3; i++) Assert.IsTrue(_st.RecordRankedDuel(a.Id, b.Id, a.Id, 0, _now), "하루 3판까지 반영");
            double after3 = _st.RatingOf(a.Id, 0).Rating;
            Assert.Greater(after3, 1500);
            Assert.Less(_st.RatingOf(b.Id, 0).Rating, 1500);
            Assert.IsFalse(_st.RecordRankedDuel(a.Id, b.Id, a.Id, 0, _now), "넷째 판은 점수에 넣지 않는다");
            Assert.AreEqual(after3, _st.RatingOf(a.Id, 0).Rating);
            Assert.AreEqual(4, a.Profile.DuelWins, "전적은 남는다");
            Assert.AreEqual(3, _st.RatingOf(a.Id, 0).Games);
            Assert.AreEqual(1500, _st.RatingOf(a.Id, 1).Rating, "리그마다 따로");
            Assert.IsTrue(_st.RecordRankedDuel(a.Id, b.Id, 0, 0, _now + Day), "다음 날은 다시 반영");

            // 자동 매치 조건: 웨이브 10 + 누적 2시간.
            Assert.AreEqual(AccountStore.QueueCheck.NeedWave, _st.CanQueue(a.Id, _now, out _));
            a.Profile.CoopBestWave = 10;
            Assert.AreEqual(AccountStore.QueueCheck.NeedPlayTime, _st.CanQueue(a.Id, _now, out _));
            _st.AddPlayTime(a.Id, 7200);
            Assert.AreEqual(AccountStore.QueueCheck.Ok, _st.CanQueue(a.Id, _now, out _));
            for (int i = 0; i < 3; i++) _st.RecordLeave(a.Id, _now);
            Assert.AreEqual(AccountStore.QueueCheck.Ok, _st.CanQueue(a.Id, _now, out _), "하루 3회까지는 봐준다");
            _st.RecordLeave(a.Id, _now);
            Assert.AreEqual(AccountStore.QueueCheck.Banned, _st.CanQueue(a.Id, _now, out int wait));
            Assert.AreEqual(_st.Rules.QueueBanMinutes * 60, wait);
            Assert.AreEqual(AccountStore.QueueCheck.Ok, _st.CanQueue(a.Id, _now + TimeSpan.FromMinutes(31).Ticks, out _));
        }

        // ------------------------------------------------------------ 판 기록 · 순위표

        [Test]
        public void Runs_FeedSeasonTop3_CharacterWeeklyAndAllTimeBoards()
        {
            var a = Acct("일등");
            var b = Acct("이등");
            foreach (long s in new long[] { 100, 900, 300, 600 }) _st.RecordRun(a.Id, 1, 3, 12, s, 50, 1, _now);
            _st.RecordRun(b.Id, 2, 9, 20, 1500, 80, 2, _now);
            var xa = AccountStore.X(a.Profile);
            Assert.AreEqual(4, xa.SeasonRuns);
            Assert.AreEqual(3, xa.SeasonTop.Count);
            Assert.AreEqual(600.0, _st.SeasonScore(xa), "상위 3판 평균 (900+600+300)/3");
            Assert.AreEqual(500.0, _st.SeasonScore(AccountStore.X(b.Profile)), "3판 미만이면 없는 판은 0점");
            Assert.AreEqual(5, _st.Runs.Count);

            var season = _st.Board(BoardKind.SeasonOverall, 0, 10, 0, _now);
            Assert.AreEqual("일등", season[0].Name);
            Assert.AreEqual(600, season[0].Score);
            Assert.AreEqual(2, season.Count);
            var ch2 = _st.Board(BoardKind.Character, 2, 10, 0, _now);
            Assert.AreEqual(1, ch2.Count);
            Assert.AreEqual(1500, ch2[0].Score);
            var weekly = _st.Board(BoardKind.Weekly, 0, 10, 0, _now);
            Assert.AreEqual("이등", weekly[0].Name);
            var low = _st.Board(BoardKind.WeeklyLow, 0, 10, 0, _now);
            Assert.AreEqual(1, low.Count, "+7 이하 부문에는 +9 판이 없다");
            Assert.AreEqual("일등", low[0].Name);
            Assert.AreEqual(0, _st.Board(BoardKind.Friends, 0, 10, a.Id, _now).FindAll(x => x.Name == "이등").Count);
            _st.RequestFriend(a.Id, "이등");
            _st.AcceptFriend(b.Id, a.Id);
            Assert.AreEqual(2, _st.Board(BoardKind.Friends, 0, 10, a.Id, _now).Count);

            // 다음 주: 주간은 비고, 시즌·역대는 남는다.
            long next = _now + 7 * Day;
            Assert.AreEqual(0, _st.Board(BoardKind.Weekly, 0, 10, 0, next).Count);
            Assert.AreEqual(2, _st.Board(BoardKind.SeasonOverall, 0, 10, 0, next).Count);
            Assert.AreEqual(1500, _st.Board(BoardKind.AllTime, 0, 10, 0, next)[0].Score);
        }

        // ------------------------------------------------------------ 시즌

        [Test]
        public void HeirScore_IsPercentileSquared_PerLeague_NeedsBothModes()
        {
            // 한 리그에 다섯 명: S 순위와 R 순위가 엇갈리게.
            var ids = new List<AccountRecord>();
            for (int i = 0; i < 5; i++) ids.Add(Acct("제자" + i));
            _st.Rules.DuelScoreMinRanked = 2;
            for (int i = 0; i < 5; i++)
                for (int k = 0; k < 3; k++) _st.RecordRun(ids[i].Id, 1, 0, 10, 1000 * (5 - i), 0, 1, _now + k);
            // 결투: 제자4가 가장 높다(4 > 3 > 2 > 1 > 0).
            for (int i = 0; i < 5; i++)
            {
                var x = AccountStore.X(ids[i].Profile);
                x.Ranked[0].Rating = 1500 + 100 * i;
                x.Ranked[0].Games = 5;
                x.SeasonRanked[0] = i == 2 ? 1 : 5; // 제자2는 판 수가 모자라 P = 0
            }
            var rows = _st.HeirScores(0, _now);
            Assert.AreEqual(5, rows.Count);
            HeirRow Of(int i) => rows.Find(r => r.AccountId == ids[i].Id);
            Assert.AreEqual(500.0, Of(0).E, 1e-9, "무한 웨이브 1위 = 500");
            Assert.AreEqual(500.0 * 0.75 * 0.75, Of(1).E, 1e-9);
            Assert.AreEqual(0.0, Of(4).E, 1e-9, "꼴찌 = 0");
            Assert.AreEqual(500.0, Of(4).P, 1e-9, "결투 1위 = 500");
            Assert.AreEqual(0.0, Of(2).P, 1e-9, "자동 매치 판 수가 모자라면 P = 0");
            // 결투 대상자는 넷(0·1·3·4): 제자3은 2위 → q = 2/3.
            Assert.AreEqual(500.0 * (2.0 / 3) * (2.0 / 3), Of(3).P, 1e-9);
            Assert.AreEqual(1, rows[0].Rank);
            for (int i = 1; i < rows.Count; i++) Assert.GreaterOrEqual(rows[i - 1].Total, rows[i].Total);
            // 다른 리그 표에는 없다.
            Assert.AreEqual(0, _st.HeirScores(2, _now).Count);
        }

        [Test]
        public void Season_SettlesAfterSettlePeriod_RewardsByMail_ResetsAndKeeps()
        {
            _st.Rules.DuelScoreMinRanked = 1;
            _st.Rules.HeirMinRanked = 1;
            var heir = Acct("후계자");
            var second = Acct("수제자");
            var sprout = Acct("새싹");
            var third = Acct("막내");
            // 후계 리그 세 명(+13), 새싹 한 명. 리그 꼴찌는 백분위 0이라 후계 점수가 0이다.
            foreach (var a in new[] { heir, second, third })
            {
                _st.Get(a.Id).Profile.WandStages[0] = 13;
                a.Profile.Add(Hero, 8);
            }
            _st.Save();
            Assert.AreEqual(2, AccountStore.X(heir.Profile).League);
            for (int k = 0; k < 10; k++)
            {
                _st.RecordRun(heir.Id, 1, 13, 30, 9000, 0, 1, _now + k);
                _st.RecordRun(second.Id, 1, 13, 20, 4000, 0, 1, _now + k);
                _st.RecordRun(third.Id, 1, 13, 10, 1000, 0, 1, _now + k);
                _st.RecordRun(sprout.Id, 1, 0, 5, 100, 0, 1, _now + k);
            }
            Assert.IsTrue(_st.RecordRankedDuel(heir.Id, second.Id, heir.Id, 2, _now));
            heir.Profile.Gold = 0;
            long goldBefore = heir.Profile.Gold;
            double ratingBefore = _st.RatingOf(heir.Id, 2).Rating;
            Assert.Greater(ratingBefore, 1500);

            var s1 = _st.Season;
            Assert.AreEqual(1, s1.Id);
            Assert.AreEqual(TimeSpan.FromDays(70).Ticks, s1.EndTicks - s1.StartTicks, "시즌 10주");
            Assert.AreEqual(TimeSpan.FromDays(3).Ticks, s1.SettleUntilTicks - s1.EndTicks, "정산 3일");

            // 정산 기간: 게임은 되지만 점수는 반영되지 않는다.
            _now = s1.EndTicks + 1;
            Assert.AreEqual(AccountStore.SeasonPhase.Settling, _st.PhaseAt(_now));
            Assert.IsNull(_st.TickSeason(_now));
            _st.RecordRun(second.Id, 1, 13, 40, 99999, 0, 1, _now);
            Assert.AreEqual(4000.0, _st.SeasonScore(AccountStore.X(second.Profile)));
            Assert.IsFalse(_st.RecordRankedDuel(heir.Id, second.Id, second.Id, 2, _now + Day));

            // 정산.
            _now = s1.SettleUntilTicks + 1;
            var arch = _st.TickSeason(_now);
            Assert.IsNotNull(arch);
            Assert.AreEqual(heir.Id, arch.HeirId);
            Assert.AreEqual("후계자", arch.HeirName);
            Assert.AreEqual(1000.0, arch.HeirTotal, 1e-6, "두 모드 모두 1위 = 1000");
            Assert.AreEqual(1, _st.Hall.Count);
            Assert.Contains("title.heir:1", AccountStore.X(heir.Profile).Titles);
            Assert.Contains("title.disciple:1", AccountStore.X(second.Profile).Titles);

            // 보상은 우편으로: 후계자 = 후계자 보상 + 리그 상위 1% + 참가 + 결투 점수 상위 10%.
            var r = _st.Rules;
            var mail = _st.MailOf(heir.Id, _now);
            Assert.AreEqual(1, mail.Count);
            Assert.AreEqual(AccountStore.MailSeasonReward, mail[0].Reason);
            Assert.AreEqual(r.HeirReward.Gold + r.Top1Reward.Gold + r.RatingTop10Gold[2], mail[0].Gold);
            Assert.AreEqual(r.HeirReward.StoneHigh + r.Top1Reward.StoneHigh, mail[0].Items[Ids.Item.StoneHigh]);
            Assert.AreEqual(r.ParticipationReward.StoneLow, mail[0].Items[Ids.Item.StoneLow]);
            Assert.AreEqual(goldBefore, heir.Profile.Gold, "받기 전에는 들어오지 않는다");
            // 새싹 리그는 계수 0.4.
            var sm = _st.MailOf(sprout.Id, _now);
            Assert.AreEqual(1, sm.Count);
            Assert.AreEqual((long)(r.Top1Reward.Gold * 0.4), sm[0].Gold);
            Assert.AreEqual((int)(r.ParticipationReward.StoneLow * 0.4), sm[0].Items[Ids.Item.StoneLow]);

            // 초기화되는 것과 남는 것.
            Assert.AreEqual(2, _st.Season.Id);
            var hx = AccountStore.X(heir.Profile);
            Assert.AreEqual(0, hx.SeasonRuns);
            Assert.AreEqual(0, hx.SeasonTop.Count);
            Assert.AreEqual(1500 + 0.5 * (ratingBefore - 1500), _st.RatingOf(heir.Id, 2).Rating, 1e-6, "결투 점수는 1500 쪽으로 반 당긴다");
            Assert.AreEqual(r.SeasonResetDeviation, _st.RatingOf(heir.Id, 2).Deviation);
            Assert.AreEqual(13, heir.Profile.WandStages[0], "지팡이는 남는다");
            Assert.AreEqual(8, heir.Profile.Qty(Hero));
            Assert.AreEqual(9000, hx.EverBest.Score, "역대 기록은 남는다");
            Assert.AreEqual(0, _st.Board(BoardKind.SeasonOverall, 0, 10, 0, _now).Count);
            Assert.AreEqual(4, _st.Board(BoardKind.AllTime, 0, 10, 0, _now).Count);
            Assert.AreEqual(0, _st.MailOf(third.Id, _now).FindAll(m => m.Gold > 0).Count, "점수 0인 꼴찌는 순위 보상이 없다(참가 보상만)");
        }

        // ------------------------------------------------------------ 저장

        [Test]
        public void Json_RoundTrip_KeepsMailLeagueSeasonAndReports()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wizard_online_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var st = new AccountStore(path) { Content = TestContent.Db };
                st.Clock = () => _now;
                st.Register("저장", new string('q', 40), out var a);
                st.Register("상대", new string('w', 40), out var b);
                st.SendMail(a.Id, AccountStore.MailSystem, 55, new Dictionary<string, int> { { Ids.Item.Charm, 2 } }, "note", _now);
                st.Block(a.Id, "상대");
                st.Report(a.Id, "상대", 2, new List<string> { "hi" }, _now);
                st.RecordRun(a.Id, 3, 2, 7, 777, 10, 1, _now);
                st.RecordRankedDuel(a.Id, b.Id, a.Id, 0, _now);
                int season = st.Season.Id;

                var st2 = new AccountStore(path) { Content = TestContent.Db };
                st2.Clock = () => _now;
                var a2 = st2.FindByName("저장");
                var x = AccountStore.X(a2.Profile);
                Assert.AreEqual(1, st2.MailOf(a2.Id, _now).Count);
                Assert.AreEqual(2, st2.MailOf(a2.Id, _now)[0].Items[Ids.Item.Charm]);
                Assert.IsTrue(st2.IsBlocked(a2.Id, b.Id));
                Assert.AreEqual(1, st2.Reports.Count);
                Assert.AreEqual(777, x.EverBest.Score);
                Assert.AreEqual(1, st2.Runs.Count);
                Assert.AreEqual(season, st2.Season.Id);
                Assert.Greater(st2.RatingOf(a2.Id, 0).Rating, 1500);
            }
            finally { try { System.IO.File.Delete(path); } catch (Exception) { } }
        }

        // ------------------------------------------------------------ 방 서버: 요청 · 대기열 · 관전

        sealed class Client
        {
            public LoopbackPipe Pipe;
            public long Id;
        }

        RoomServer _server;

        RoomServer Server()
        {
            if (_server == null) _server = new RoomServer(TestContent.Db, null, 11, _st);
            return _server;
        }

        void Ticks(int n) { for (int i = 0; i < n; i++) _server.Tick(); }

        static byte[] Read(LoopbackPipe p, NetMsg kind)
        {
            byte[] found = null;
            var keep = new List<byte[]>();
            while (p.TryReceive(out var d)) { if (found == null && d.Length > 0 && (NetMsg)d[0] == kind) found = d; else keep.Add(d); }
            return found;
        }

        Client Login(string name)
        {
            var s = Server();
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            s.Accept(serverEnd);
            var m = new NetWriter(NetMsg.Register);
            m.Str(name); m.Str(new string('k', 40) + name);
            clientEnd.Send(m.ToArray());
            Ticks(2);
            var ok = Read(clientEnd, NetMsg.AuthOk);
            Assert.IsNotNull(ok, "계정을 만든다");
            var r = new NetReader(ok);
            r.Byte();
            return new Client { Pipe = clientEnd, Id = r.Long() };
        }

        LoopbackPipe Again(Client c, string name)
        {
            LoopbackPipe.CreatePair(out var serverEnd, out var clientEnd);
            _server.Accept(serverEnd);
            var m = new NetWriter(NetMsg.Login);
            m.Long(c.Id); m.Str(new string('k', 40) + name);
            clientEnd.Send(m.ToArray());
            Ticks(2);
            Assert.IsNotNull(Read(clientEnd, NetMsg.AuthOk));
            return clientEnd;
        }

        [Test]
        public void Server_MailSeasonBoardBlockReport_RequestsAnswer()
        {
            var a = Login("요청자");
            var b = Login("상대방");
            _st.SendMail(a.Id, AccountStore.MailSystem, 300, new Dictionary<string, int> { { Ids.Item.StoneLow, 2 } }, null, _now);
            _st.RecordRun(a.Id, 1, 0, 9, 4321, 0, 1, _now);

            a.Pipe.Send(new NetWriter(NetMsg.MailListRequest).ToArray());
            Ticks(2);
            var d = Read(a.Pipe, NetMsg.MailList);
            Assert.IsNotNull(d);
            var r = new NetReader(d); r.Byte();
            Assert.AreEqual(1, r.Int());
            var mail = NetCodec.ReadMail(r);
            Assert.AreEqual(300, mail.Gold);
            Assert.AreEqual(2, mail.Items[Ids.Item.StoneLow]);

            var claim = new NetWriter(NetMsg.MailClaim); claim.Long(0);
            a.Pipe.Send(claim.ToArray());
            Ticks(2);
            d = Read(a.Pipe, NetMsg.MailReply);
            r = new NetReader(d); r.Byte();
            Assert.AreEqual((byte)AccountStore.MailResult.Ok, r.Byte());
            Assert.AreEqual(300, NetCodec.ReadProfile(r).Gold);

            a.Pipe.Send(new NetWriter(NetMsg.SeasonInfoRequest).ToArray());
            Ticks(2);
            d = Read(a.Pipe, NetMsg.SeasonInfo);
            r = new NetReader(d); r.Byte();
            var info = NetCodec.ReadSeasonInfo(r);
            Assert.AreEqual(1, info.SeasonId);
            Assert.AreEqual(0, info.Phase);
            Assert.AreEqual(1, info.SeasonRuns);
            Assert.AreEqual(1440, info.SeasonScore, "4321 / 3");
            Assert.AreEqual(0, info.MailCount);
            Assert.AreEqual((byte)AccountStore.QueueCheck.NeedWave, info.Queue);
            Assert.AreEqual(1500, info.Rating[0]);

            var board = new NetWriter(NetMsg.BoardRequest); board.Byte((byte)BoardKind.AllTime); board.Int(0);
            a.Pipe.Send(board.ToArray());
            Ticks(2);
            d = Read(a.Pipe, NetMsg.BoardReply);
            r = new NetReader(d); r.Byte(); r.Byte(); r.Int();
            Assert.AreEqual(1, r.Int());
            var row = NetCodec.ReadBoardRow(r);
            Assert.AreEqual("요청자", row.Name);
            Assert.AreEqual(4321, row.Score);

            var block = new NetWriter(NetMsg.BlockAdd); block.Str("상대방");
            a.Pipe.Send(block.ToArray());
            Ticks(2);
            d = Read(a.Pipe, NetMsg.BlockReply);
            Assert.AreEqual((byte)AccountStore.BlockResult.Ok, d[1]);
            Assert.IsTrue(_st.IsBlocked(a.Id, b.Id));

            var rep = new NetWriter(NetMsg.ReportSend); rep.Str("상대방"); rep.Int(1);
            a.Pipe.Send(rep.ToArray());
            Ticks(2);
            d = Read(a.Pipe, NetMsg.ReportReply);
            Assert.AreEqual((byte)AccountStore.ReportResult.Ok, d[1]);
            Assert.AreEqual(1, _st.Reports.Count);
        }

        void MakeEligible(long id)
        {
            _st.Get(id).Profile.CoopBestWave = 10;
            _st.AddPlayTime(id, 7200);
            _st.Save();
        }

        static string MatchCode(LoopbackPipe p)
        {
            var d = Read(p, NetMsg.MatchFound);
            if (d == null) return null;
            var r = new NetReader(d); r.Byte();
            return r.Str();
        }

        [Test]
        public void Queue_PairsSameLeague_AutoStarts_ForfeitIsRatedLossAndLeave_SpectatorWatches()
        {
            var a = Login("대기일");
            var b = Login("대기이");
            var c = Login("윗리그");
            a.Pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            Ticks(2);
            var d = Read(a.Pipe, NetMsg.QueueState);
            Assert.AreEqual((byte)AccountStore.QueueCheck.NeedWave, d[1], "조건을 못 채우면 못 들어간다");
            foreach (var x in new[] { a, b, c }) MakeEligible(x.Id);
            _st.Get(c.Id).Profile.WandStages[0] = 13;
            _st.Save();

            // 리그가 다르면 만나지 않는다.
            a.Pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            c.Pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            Ticks(200);
            Assert.AreEqual(2, _server.QueueCount);
            Assert.IsNull(MatchCode(a.Pipe));

            b.Pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            Ticks(120);
            string code = MatchCode(a.Pipe);
            Assert.IsNotNull(code, "같은 리그 둘이 잡힌다");
            Assert.AreEqual(code, MatchCode(b.Pipe));
            Assert.AreEqual(1, _server.QueueCount);
            var room = _server.Room(code);
            Assert.IsTrue(room.Ranked);

            // 다른 계정은 그 방에 못 들어간다.
            var intruder = Again(c, "윗리그");
            var j = new NetWriter(NetMsg.JoinRoom); j.Str(code);
            intruder.Send(j.ToArray());
            Ticks(2);
            Assert.IsNotNull(Read(intruder, NetMsg.RoomError));

            // 둘이 새 연결로 들어오면 서버가 시작한다.
            var ca = new CoopClient(TestContent.Db);
            var cb = new CoopClient(TestContent.Db);
            ca.ConnectJoin(Again(a, "대기일"), code, OnlineMember.Build(TestContent.Db, 1, 0));
            cb.ConnectJoin(Again(b, "대기이"), code, OnlineMember.Build(TestContent.Db, 2, 0));
            for (int i = 0; i < 30 && !room.Started; i++) { _server.Tick(); ca.Pump(); cb.Pump(); }
            Assert.IsTrue(room.Started, "둘이 차면 자동으로 시작");
            Assert.AreEqual(RunMode.Duel, room.World.Run.Config.Mode);

            // 관전자는 판 중에도 들어와 상태를 받는다.
            LoopbackPipe.CreatePair(out var specServer, out var specClient);
            _server.Accept(specServer);
            var spec = new CoopClient(TestContent.Db);
            spec.ConnectSpectate(specClient, code);
            for (int i = 0; i < 10; i++) { _server.Tick(); ca.Pump(); cb.Pump(); spec.Pump(); }
            Assert.IsTrue(spec.Spectator);
            Assert.AreEqual(CoopClientState.InRun, spec.State);
            Assert.Greater(spec.SnapshotsReceived, 0);
            Assert.AreEqual(1, room.SpectatorCount);
            Assert.AreEqual(2, room.PlayerCount, "관전자는 자리를 차지하지 않는다");

            // 한쪽이 나가면 패배 + 이탈 기록, 점수에 반영.
            cb.Disconnect();
            for (int i = 0; i < 600 && room.World.Run.Phase != RunPhase.RunEnded; i++) { _server.Tick(); ca.Pump(); spec.Pump(); }
            Assert.AreEqual(RunPhase.RunEnded, room.World.Run.Phase);
            Ticks(2);
            Assert.Greater(_st.RatingOf(a.Id, 0).Rating, 1500);
            Assert.Less(_st.RatingOf(b.Id, 0).Rating, 1500);
            Assert.AreEqual(1, _st.RatingOf(a.Id, 0).Wins);
            Assert.AreEqual(1, AccountStore.X(_st.Peek(b.Id).Profile).LeaveTicks.Count);
            Assert.AreEqual(a.Id, AccountStore.X(_st.Peek(b.Id).Profile).LastRankedOpponent);
        }

        [Test]
        public void Queue_NoShowRoomCloses_WithoutRatingChange()
        {
            var a = Login("안옴일");
            var b = Login("안옴이");
            MakeEligible(a.Id); MakeEligible(b.Id);
            a.Pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            b.Pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            Ticks(120);
            string code = MatchCode(a.Pipe);
            Assert.IsNotNull(code);
            Ticks(_st.Rules.MatchJoinSeconds * 60 + 5);
            Assert.IsNull(_server.Room(code), "둘 다 안 들어오면 방을 닫는다");
            Assert.AreEqual(0, _st.RatingOf(a.Id, 0).Games);
        }
    }
}
