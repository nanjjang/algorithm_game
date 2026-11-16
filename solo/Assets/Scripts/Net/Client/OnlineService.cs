using System;
using System.Collections.Generic;
using System.Threading;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 방 서버에 "요청 → 답" 한 번씩 하는 연결(내 기록 화면용): 계정으로 들어가 서버 프로필을 받고, 서버 강화·순위표를 묻는다.
    /// 답을 기다리며 잠깐 막힌다(timeoutMs). 화면은 버튼을 누를 때만 부른다.
    /// </summary>
    public sealed class OnlineService : IDisposable
    {
        INetPipe _pipe;
        public ServerProfile Profile { get; private set; }
        public bool IsGuest { get; private set; }
        public bool Connected => _pipe != null && _pipe.Connected;

        public struct EnhanceReply
        {
            public AccountStore.EnhanceResult Result;
            public EnhanceOutcome Outcome;
            public int StageBefore;
            public ServerProfile Profile;
        }

        /// <summary>붙고 계정이 있으면 프로필을 받는다. 서버에 못 붙거나 계정이 틀리면 null.</summary>
        /// <summary>마지막 Open이 계정 실패(AuthFail)로 끝났다 — 로그인 표가 지워졌거나 만료됐다.</summary>
        public static volatile bool LastAuthFailed;

        public static OnlineService Open(IContentDb db, int timeoutMs = 4000)
        {
            LastAuthFailed = false;
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return null;
            var s = new OnlineService { _pipe = pipe };
            var acct = OnlineAccount.Load();
            if (acct == null) { s.IsGuest = true; return s; }
            acct.SendLogin(pipe);
            var data = s.Wait(timeoutMs, NetMsg.AuthOk, NetMsg.AuthFail);
            if (data == null || (NetMsg)data[0] != NetMsg.AuthOk) { LastAuthFailed = data != null; s.Dispose(); return null; }
            var r = new NetReader(data);
            r.Byte();
            r.Long();
            s.Profile = NetCodec.ReadProfile(r);
            return s;
        }

        byte[] Wait(int timeoutMs, params NetMsg[] kinds)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && _pipe.Connected)
            {
                if (!_pipe.TryReceive(out var data)) { Thread.Sleep(3); continue; }
                if (data.Length == 0) continue;
                foreach (var k in kinds) if ((NetMsg)data[0] == k) return data;
            }
            return null;
        }

        public EnhanceReply? Enhance(int character, bool useCharm = false, IList<string> sacrifices = null, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.EnhanceRequest);
            m.Int(character);
            m.Bool(useCharm);
            m.StrList(sacrifices ?? new List<string>());
            _pipe.Send(m.ToArray());
            return ReadReply(timeoutMs);
        }

        /// <summary>서버 지팡이 조립을 바꾼다. 결과와 바뀐(또는 그대로인) 프로필.</summary>
        public AccountStore.LoadoutResult? SetLoadout(int character, string core, IList<string> links, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.SetLoadout);
            m.Int(character);
            m.Str(core);
            m.StrList(links != null ? new List<string>(links) : new List<string>());
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.LoadoutReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            var res = (AccountStore.LoadoutResult)r.Byte();
            var prof = NetCodec.ReadProfile(r);
            if (res != AccountStore.LoadoutResult.NoAccount) Profile = prof;
            return res;
        }

        /// <summary>받은 변이 하나를 지운다(골드).</summary>
        public EnhanceReply? RemoveMutation(int character, string mutationId, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.RemoveMutation);
            m.Int(character);
            m.Str(mutationId);
            _pipe.Send(m.ToArray());
            return ReadReply(timeoutMs);
        }

        // ------------------------------------------------------------ 장터

        public List<MarketListing> MarketBrowse(string itemId = null, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.MarketBrowse);
            m.Str(itemId);
            _pipe.Send(m.ToArray());
            return ReadListings(timeoutMs);
        }

        public List<MarketListing> MarketMine(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.MarketMine).ToArray());
            return ReadListings(timeoutMs);
        }

        List<MarketListing> ReadListings(int timeoutMs)
        {
            var data = Wait(timeoutMs, NetMsg.MarketList);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            var l = new List<MarketListing>(n);
            for (int i = 0; i < n; i++)
                l.Add(new MarketListing { Id = r.Long(), SellerId = r.Long(), SellerName = r.Str(), ItemId = r.Str(), Qty = r.Int(), Price = r.Long() });
            return l;
        }

        AccountStore.MarketResult? MarketCall(NetWriter m, int timeoutMs)
        {
            if (!Connected) return null;
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.MarketReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            var res = (AccountStore.MarketResult)r.Byte();
            var prof = NetCodec.ReadProfile(r);
            if (res != AccountStore.MarketResult.NoAccount) Profile = prof;
            return res;
        }

        public AccountStore.MarketResult? MarketSell(string itemId, int qty, long price, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.MarketSell);
            m.Str(itemId); m.Int(qty); m.Long(price);
            return MarketCall(m, timeoutMs);
        }

        public AccountStore.MarketResult? MarketBuy(long listingId, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.MarketBuy);
            m.Long(listingId);
            return MarketCall(m, timeoutMs);
        }

        public AccountStore.MarketResult? MarketUnlist(long listingId, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.MarketUnlist);
            m.Long(listingId);
            return MarketCall(m, timeoutMs);
        }

        // ------------------------------------------------------------ 친구

        public bool Friends(out List<FriendInfo> friends, out List<FriendInfo> requests, int timeoutMs = 4000)
        {
            friends = null; requests = null;
            if (!Connected) return false;
            _pipe.Send(new NetWriter(NetMsg.FriendListRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.FriendList);
            if (data == null) return false;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            friends = new List<FriendInfo>(n);
            for (int i = 0; i < n; i++) friends.Add(new FriendInfo { Id = r.Long(), Name = r.Str(), Rating = r.Int(), Where = r.Str() });
            int k = r.Int();
            requests = new List<FriendInfo>(k);
            for (int i = 0; i < k; i++) requests.Add(new FriendInfo { Id = r.Long(), Name = r.Str(), Rating = r.Int() });
            return true;
        }

        AccountStore.FriendResult? FriendCall(NetWriter m, int timeoutMs)
        {
            if (!Connected) return null;
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.FriendReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            return (AccountStore.FriendResult)r.Byte();
        }

        public AccountStore.FriendResult? RequestFriend(string name, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.FriendRequest);
            m.Str(name);
            return FriendCall(m, timeoutMs);
        }

        public AccountStore.FriendResult? AnswerFriend(long fromId, bool accept, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.FriendAnswer);
            m.Long(fromId); m.Bool(accept);
            return FriendCall(m, timeoutMs);
        }

        public AccountStore.FriendResult? RemoveFriend(long id, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.FriendRemove);
            m.Long(id);
            return FriendCall(m, timeoutMs);
        }

        /// <summary>대기 중인 변이 제시에 답한다(받기 / 거절).</summary>
        public EnhanceReply? ResolveMutation(int character, bool accept, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.MutationResolve);
            m.Int(character);
            m.Bool(accept);
            _pipe.Send(m.ToArray());
            return ReadReply(timeoutMs);
        }

        EnhanceReply? ReadReply(int timeoutMs)
        {
            var data = Wait(timeoutMs, NetMsg.EnhanceReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            var rep = new EnhanceReply
            {
                Result = (AccountStore.EnhanceResult)r.Byte(),
                Outcome = (EnhanceOutcome)r.Byte(),
                StageBefore = r.Int(),
                Profile = NetCodec.ReadProfile(r),
            };
            if (rep.Result != AccountStore.EnhanceResult.NoAccount && rep.Result != AccountStore.EnhanceResult.BadCharacter) Profile = rep.Profile;
            return rep;
        }

        public List<ServerProfile> Leaderboard(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.LeaderboardRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.LeaderboardReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            var l = new List<ServerProfile>(n);
            for (int i = 0; i < n; i++) l.Add(NetCodec.ReadProfile(r));
            return l;
        }

        // ------------------------------------------------------------ 우편함 · 차단 · 신고 (13판)

        public List<MailItem> Mail(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.MailListRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.MailList);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            var l = new List<MailItem>(n);
            for (int i = 0; i < n; i++) l.Add(NetCodec.ReadMail(r));
            return l;
        }

        /// <summary>우편을 받는다(mailId = 0이면 전부). 받은 골드·물건이 든 프로필로 바뀐다.</summary>
        public AccountStore.MailResult? ClaimMail(long mailId, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.MailClaim);
            m.Long(mailId);
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.MailReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            var res = (AccountStore.MailResult)r.Byte();
            var prof = NetCodec.ReadProfile(r);
            if (res != AccountStore.MailResult.NoAccount) Profile = prof;
            return res;
        }

        public List<FriendInfo> Blocked(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.BlockListRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.BlockList);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            var l = new List<FriendInfo>(n);
            for (int i = 0; i < n; i++) l.Add(new FriendInfo { Id = r.Long(), Name = r.Str() });
            return l;
        }

        AccountStore.BlockResult? BlockCall(NetWriter m, int timeoutMs)
        {
            if (!Connected) return null;
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.BlockReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            return (AccountStore.BlockResult)r.Byte();
        }

        public AccountStore.BlockResult? Block(string name, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.BlockAdd);
            m.Str(name);
            return BlockCall(m, timeoutMs);
        }

        public AccountStore.BlockResult? Unblock(long id, int timeoutMs = 4000)
        {
            var m = new NetWriter(NetMsg.BlockRemove);
            m.Long(id);
            return BlockCall(m, timeoutMs);
        }

        /// <summary>신고(사유 1 욕설 · 2 사기 · 3 현금 거래 · 4 부정 프로그램 · 5 기타). 서버가 그 사람의 최근 광장 채팅을 붙인다.</summary>
        public AccountStore.ReportResult? Report(string name, int reason, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.ReportSend);
            m.Str(name); m.Int(reason);
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.ReportReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            return (AccountStore.ReportResult)r.Byte();
        }

        // ------------------------------------------------------------ 시즌 · 순위표 · 리그 · 자동 매치 (13판)

        public SeasonInfo Season(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.SeasonInfoRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.SeasonInfo);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            return NetCodec.ReadSeasonInfo(r);
        }

        /// <summary>순위표. arg = 캐릭터(BoardKind.Character) 또는 리그(RankedLeague·Heir).</summary>
        public List<BoardRow> Board(BoardKind kind, int arg = 0, int timeoutMs = 4000)
        {
            if (!Connected) return null;
            var m = new NetWriter(NetMsg.BoardRequest);
            m.Byte((byte)kind); m.Int(arg);
            _pipe.Send(m.ToArray());
            var data = Wait(timeoutMs, NetMsg.BoardReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte(); r.Byte(); r.Int();
            int n = r.Int();
            var l = new List<BoardRow>(n);
            for (int i = 0; i < n; i++) l.Add(NetCodec.ReadBoardRow(r));
            return l;
        }

        public struct HallEntry { public int SeasonId; public string HeirName; public int Total; }

        /// <summary>역대 후계자 명단(후계자 비석).</summary>
        public List<HallEntry> Hall(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.HallRequest).ToArray());
            var data = Wait(timeoutMs, NetMsg.HallReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            int n = r.Int();
            var l = new List<HallEntry>(n);
            for (int i = 0; i < n; i++) l.Add(new HallEntry { SeasonId = r.Int(), HeirName = r.Str(), Total = r.Int() });
            return l;
        }

        /// <summary>스스로 위 리그로 올라간다(시즌에 한 번). league = 답에 든 지금 리그.</summary>
        public AccountStore.LeagueResult? OptUpLeague(out int league, int timeoutMs = 4000)
        {
            league = 0;
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.LeagueOptUp).ToArray());
            var data = Wait(timeoutMs, NetMsg.LeagueReply);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            var res = (AccountStore.LeagueResult)r.Byte();
            league = r.Int();
            return res;
        }

        public struct QueueReply { public AccountStore.QueueCheck Check; public int WaitSeconds, League; public bool Waiting; }

        QueueReply? ReadQueue(int timeoutMs)
        {
            var data = Wait(timeoutMs, NetMsg.QueueState);
            if (data == null) return null;
            var r = new NetReader(data);
            r.Byte();
            return new QueueReply { Check = (AccountStore.QueueCheck)r.Byte(), WaitSeconds = r.Int(), League = r.Int(), Waiting = r.Bool() };
        }

        /// <summary>자동 매치 대기열에 들어간다. Waiting이면 이 연결을 열어 둔 채 <see cref="PollMatch"/>로 상대가 잡혔는지 본다.</summary>
        public QueueReply? QueueJoin(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.QueueJoin).ToArray());
            return ReadQueue(timeoutMs);
        }

        public QueueReply? QueueLeave(int timeoutMs = 4000)
        {
            if (!Connected) return null;
            _pipe.Send(new NetWriter(NetMsg.QueueLeave).ToArray());
            return ReadQueue(timeoutMs);
        }

        /// <summary>
        /// 대기 중에 프레임마다 부른다(막히지 않는다). 상대가 잡혔으면 방 코드(그 코드로 결투 방에 참가한다), 아직이면 null.
        /// stillWaiting = false면 대기열에서 빠진 것이다(연결이 끊겼거나 리그가 올랐다).
        /// </summary>
        public string PollMatch(out bool stillWaiting)
        {
            stillWaiting = Connected;
            if (!Connected) return null;
            while (_pipe.TryReceive(out var data))
            {
                if (data == null || data.Length == 0) continue;
                var r = new NetReader(data);
                var kind = (NetMsg)r.Byte();
                if (kind == NetMsg.MatchFound) { stillWaiting = false; return r.Str(); }
                if (kind == NetMsg.QueueState) { r.Byte(); r.Int(); r.Int(); stillWaiting = r.Bool(); }
            }
            return null;
        }

        public void Dispose()
        {
            _pipe?.Close();
            _pipe = null;
        }
    }
}
