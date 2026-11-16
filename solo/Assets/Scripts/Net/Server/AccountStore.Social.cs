using System;
using System.Collections.Generic;

namespace Wizard.Solo.Net
{
    /// <summary>장터에 올린 것 하나. 올린 동안 물건은 판 사람 창고에서 빠져 있다(맡김).</summary>
    public sealed class MarketListing
    {
        public long Id;
        public long SellerId;
        public string SellerName;
        public string ItemId;
        public int Qty;
        /// <summary>묶음 전체 값(골드).</summary>
        public long Price;
        public long Deposit;
        /// <summary>올린 시각(UTC ticks). ListingHours가 지나면 판 사람 창고로 돌아간다.</summary>
        public long CreatedTicks;
    }

    /// <summary>친구 한 줄(친구 목록 화면용).</summary>
    public sealed class FriendInfo
    {
        public long Id;
        public string Name;
        public int Rating;
        /// <summary>지금 어디(빈 문자열 = 접속 안 함, "plaza", "room"). 방 서버가 채운다.</summary>
        public string Where = string.Empty;
    }

    // 장터·친구(docs/online.md 5.5절·8절의 값, 11-online.md 6.12절).
    public sealed partial class AccountStore
    {
        // ------------------------------------------------------------ 장터

        /// <summary>장터 수수료 5%(판 값에서), 보증금 1%(올릴 때 내고 팔리면 돌려받는다, 내리거나 기한이 지나면 사라진다), 계정당 동시 20건, 48시간.</summary>
        public const int MarketFeePercent = 5, DepositPercent = 1, MaxListings = 20, ListingHours = 48;

        public enum MarketResult { Ok, NoAccount, BadItem, NotOwned, BadPrice, TooMany, NotFound, NotEnoughGold, OwnListing }

        static long Ceil(long v, int pct) => v <= 0 ? 0 : (v * pct + 99) / 100;
        public static long MarketFee(long price) => Ceil(price, MarketFeePercent);
        public static long DepositFor(long price) => Ceil(price, DepositPercent);

        /// <summary>기한이 지난 것을 판 사람 창고로 돌린다(보증금은 사라진다). nowTicks = UTC.</summary>
        public void ExpireListings(long nowTicks)
        {
            long limit = TimeSpan.FromHours(ListingHours).Ticks;
            bool changed = false;
            for (int i = _data.Listings.Count - 1; i >= 0; i--)
            {
                var l = _data.Listings[i];
                if (nowTicks - l.CreatedTicks < limit) continue;
                // 돌아온 물건은 우편함으로(docs/online.md 5.2절 까마귀 우편함). 보증금은 사라진다.
                SendMail(l.SellerId, MailMarketExpired, 0, new Dictionary<string, int> { { l.ItemId, l.Qty } }, l.ItemId, nowTicks);
                _data.Listings.RemoveAt(i);
                changed = true;
            }
            if (changed) Save();
        }

        public MarketResult List(long sellerId, string itemId, int qty, long price, long nowTicks, out MarketListing listing)
        {
            listing = null;
            var a = Get(sellerId);
            if (a == null) return MarketResult.NoAccount;
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return MarketResult.BadItem;
            if (price <= 0 || price > 1_000_000_000L) return MarketResult.BadPrice;
            if (a.Profile.Qty(itemId) < qty) return MarketResult.NotOwned;
            int mine = 0;
            foreach (var l in _data.Listings) if (l.SellerId == sellerId) mine++;
            if (mine >= MaxListings) return MarketResult.TooMany;
            long dep = DepositFor(price);
            if (a.Profile.Gold < dep) return MarketResult.NotEnoughGold;
            a.Profile.Gold -= dep;
            a.Profile.Add(itemId, -qty);
            listing = new MarketListing
            {
                Id = _data.NextListingId++, SellerId = sellerId, SellerName = a.Name, ItemId = itemId, Qty = qty, Price = price,
                Deposit = dep, CreatedTicks = nowTicks,
            };
            _data.Listings.Add(listing);
            Save();
            return MarketResult.Ok;
        }

        /// <summary>내린다: 물건은 돌아오고 보증금은 사라진다.</summary>
        public MarketResult Unlist(long sellerId, long listingId)
        {
            int i = _data.Listings.FindIndex(l => l.Id == listingId);
            if (i < 0) return MarketResult.NotFound;
            var l = _data.Listings[i];
            if (l.SellerId != sellerId) return MarketResult.NotFound;
            var a = Get(sellerId);
            if (a != null) a.Profile.Add(l.ItemId, l.Qty);
            _data.Listings.RemoveAt(i);
            Save();
            return MarketResult.Ok;
        }

        /// <summary>산다: 산 사람 골드 → 판 사람의 우편함(수수료 5%를 뺀 값 + 보증금), 물건 → 산 사람 창고.</summary>
        public MarketResult Buy(long buyerId, long listingId)
        {
            var b = Get(buyerId);
            if (b == null) return MarketResult.NoAccount;
            int i = _data.Listings.FindIndex(l => l.Id == listingId);
            if (i < 0) return MarketResult.NotFound;
            var l = _data.Listings[i];
            if (l.SellerId == buyerId) return MarketResult.OwnListing;
            if (b.Profile.Gold < l.Price) return MarketResult.NotEnoughGold;
            b.Profile.Gold -= l.Price;
            b.Profile.Add(l.ItemId, l.Qty);
            // 대금(판 값 − 수수료 + 보증금)은 판 사람의 우편함으로 간다(받기를 눌러야 골드가 된다).
            SendMail(l.SellerId, MailMarketSold, l.Price - MarketFee(l.Price) + l.Deposit, null, l.ItemId, Clock());
            _data.Listings.RemoveAt(i);
            Save();
            return MarketResult.Ok;
        }

        /// <summary>둘러보기: itemId가 있으면 그 물건만, 개당 값 싼 순, 최대 max개.</summary>
        public List<MarketListing> Browse(string itemId, int max)
        {
            var l = new List<MarketListing>();
            foreach (var x in _data.Listings) if (string.IsNullOrEmpty(itemId) || x.ItemId == itemId) l.Add(x);
            l.Sort((x, y) => (x.Price * y.Qty).CompareTo(y.Price * x.Qty) != 0 ? (x.Price * y.Qty).CompareTo(y.Price * x.Qty) : x.Id.CompareTo(y.Id));
            if (l.Count > max) l.RemoveRange(max, l.Count - max);
            return l;
        }

        public List<MarketListing> ListingsOf(long sellerId)
        {
            var l = new List<MarketListing>();
            foreach (var x in _data.Listings) if (x.SellerId == sellerId) l.Add(x);
            return l;
        }

        // ------------------------------------------------------------ 친구

        public enum FriendResult { Ok, NoAccount, NotFound, Self, Already, Requested, TooMany }
        public const int MaxFriends = 100;

        public AccountRecord FindByName(string name) => name != null && _byName.TryGetValue(Key(name), out var r) ? r : null;

        /// <summary>이름으로 친구 신청. 상대도 나에게 신청해 둔 상태면 바로 친구가 된다.</summary>
        public FriendResult RequestFriend(long fromId, string toName)
        {
            var a = Get(fromId);
            if (a == null) return FriendResult.NoAccount;
            var b = FindByName(toName);
            if (b == null) return FriendResult.NotFound;
            if (b.Id == fromId) return FriendResult.Self;
            // 차단(5.3절): 내가 막은 사람에게는 신청하지 못하고, 나를 막은 사람에게는 신청이 가지 않는다(보낸 쪽에는 알리지 않는다).
            if (IsBlocked(fromId, b.Id)) return FriendResult.NotFound;
            if (IsBlocked(b.Id, fromId)) return FriendResult.Requested;
            Fix2(a.Profile); Fix2(b.Profile);
            if (a.Profile.Friends.Contains(b.Id)) return FriendResult.Already;
            if (a.Profile.FriendRequests.Contains(b.Id)) return AcceptFriend(fromId, b.Id); // 서로 신청 = 친구
            if (a.Profile.Friends.Count >= MaxFriends || b.Profile.Friends.Count >= MaxFriends) return FriendResult.TooMany;
            if (!b.Profile.FriendRequests.Contains(fromId)) b.Profile.FriendRequests.Add(fromId);
            Save();
            return FriendResult.Requested;
        }

        public FriendResult AcceptFriend(long id, long fromId)
        {
            var a = Get(id);
            var b = Get(fromId);
            if (a == null || b == null) return FriendResult.NotFound;
            Fix2(a.Profile); Fix2(b.Profile);
            if (!a.Profile.FriendRequests.Remove(fromId)) return FriendResult.NotFound;
            if (!a.Profile.Friends.Contains(fromId)) a.Profile.Friends.Add(fromId);
            if (!b.Profile.Friends.Contains(id)) b.Profile.Friends.Add(id);
            b.Profile.FriendRequests.Remove(id);
            Save();
            return FriendResult.Ok;
        }

        public FriendResult DeclineFriend(long id, long fromId)
        {
            var a = Get(id);
            if (a == null) return FriendResult.NoAccount;
            Fix2(a.Profile);
            if (!a.Profile.FriendRequests.Remove(fromId)) return FriendResult.NotFound;
            Save();
            return FriendResult.Ok;
        }

        public FriendResult RemoveFriend(long id, long otherId)
        {
            var a = Get(id);
            var b = Get(otherId);
            if (a == null) return FriendResult.NoAccount;
            Fix2(a.Profile);
            a.Profile.Friends.Remove(otherId);
            if (b != null) { Fix2(b.Profile); b.Profile.Friends.Remove(id); }
            Save();
            return FriendResult.Ok;
        }

        public List<FriendInfo> FriendsOf(long id, Func<long, string> whereOf)
        {
            var l = new List<FriendInfo>();
            var a = Get(id);
            if (a == null) return l;
            Fix2(a.Profile);
            foreach (var f in a.Profile.Friends)
            {
                var r = Get(f);
                if (r != null) l.Add(new FriendInfo { Id = f, Name = r.Name, Rating = r.Profile.Rating, Where = whereOf != null ? whereOf(f) ?? string.Empty : string.Empty });
            }
            l.Sort((x, y) => (x.Where == string.Empty).CompareTo(y.Where == string.Empty) != 0 ? (x.Where == string.Empty).CompareTo(y.Where == string.Empty) : string.CompareOrdinal(x.Name, y.Name));
            return l;
        }

        public List<FriendInfo> RequestsOf(long id)
        {
            var l = new List<FriendInfo>();
            var a = Get(id);
            if (a == null) return l;
            Fix2(a.Profile);
            foreach (var f in a.Profile.FriendRequests)
            {
                var r = Get(f);
                if (r != null) l.Add(new FriendInfo { Id = f, Name = r.Name, Rating = r.Profile.Rating });
            }
            return l;
        }

        static void Fix2(ServerProfile p)
        {
            if (p.Friends == null) p.Friends = new List<long>();
            if (p.FriendRequests == null) p.FriendRequests = new List<long>();
        }
    }
}
