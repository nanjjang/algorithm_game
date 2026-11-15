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
        TcpPipe _pipe;
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
        public static OnlineService Open(IContentDb db, int timeoutMs = 4000)
        {
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return null;
            var s = new OnlineService { _pipe = pipe };
            var acct = OnlineAccount.Load();
            if (acct == null) { s.IsGuest = true; return s; }
            acct.SendLogin(pipe);
            var data = s.Wait(timeoutMs, NetMsg.AuthOk, NetMsg.AuthFail);
            if (data == null || (NetMsg)data[0] != NetMsg.AuthOk) { s.Dispose(); return null; }
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

        public void Dispose()
        {
            _pipe?.Close();
            _pipe = null;
        }
    }
}
