using System;
using NUnit.Framework;
using Wizard.Solo.Net;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// Postgres 계정 저장소(docs/spec/08 8.7절). 이 기기의 Postgres(기본: 127.0.0.1의 wizard_test, 환경 변수 WIZARD_TEST_PG로 바꿈)에 붙는다.
    /// 붙을 수 없으면 건너뛴다(Ignore — 통과로 세지 않는다).
    /// </summary>
    public class PostgresAccountDbTests
    {
        PostgresAccountDb _db;

        static string Cs => Environment.GetEnvironmentVariable("WIZARD_TEST_PG")
            ?? "Host=127.0.0.1;Port=5432;Database=wizard_test;Username=" + Environment.UserName + ";Timeout=5";

        [SetUp]
        public void SetUp()
        {
            _db = new PostgresAccountDb(Cs);
            try { _db.DropAllForTests(); }
            catch (Exception e) { _db.Dispose(); _db = null; Assert.Ignore("no postgres: " + e.Message); }
        }

        [TearDown]
        public void TearDown() { _db?.Dispose(); }

        [Test]
        public void RoundTrip_AccountsItemsFriendsListingsSessions()
        {
            long aId, bId;
            string token;
            {
                var s = new AccountStore(_db);
                Assert.AreEqual(0, s.Count);
                Assert.AreEqual(AccountStore.RegisterResult.Ok, s.Register("가나다", new string('a', 40), out var a));
                var salt = AccountStore.NewSalt();
                Assert.AreEqual(AccountStore.SignupResult.Ok, s.CreateEmailAccount("Mail@Example.com", "메일이", salt, AccountStore.HashPassword("pw long enough", salt, 10), out var b));
                Assert.AreEqual(AccountStore.SignupResult.Ok, s.CreateGoogleAccount("sub-1", "g@x.y", true, "구글이", out var g));
                aId = a.Id; bId = b.Id;
                var ap = s.Get(aId).Profile;
                ap.Gold = 1234;
                ap.Add(Ids.Item.StoneLow, 7);
                ap.WandStages[2] = 5;
                s.Save();
                Assert.AreEqual(AccountStore.MarketResult.Ok, s.List(aId, Ids.Item.StoneLow, 3, 300, DateTime.UtcNow.Ticks, out var listing));
                Assert.AreEqual(AccountStore.FriendResult.Requested, s.RequestFriend(aId, "메일이"));
                Assert.AreEqual(AccountStore.FriendResult.Ok, s.AcceptFriend(bId, aId));
                token = s.IssueSession(s.Get(bId));
                Assert.IsNull(s.LastSaveError);
            }
            using (var db2 = new PostgresAccountDb(Cs))
            {
                var s = new AccountStore(db2);
                Assert.AreEqual(3, s.Count);
                var a = s.Peek(aId);
                Assert.AreEqual("가나다", a.Name);
                Assert.AreEqual(1234 - AccountStore.DepositFor(300), a.Profile.Gold);
                Assert.AreEqual(4, a.Profile.Qty(Ids.Item.StoneLow));
                Assert.AreEqual(5, a.Profile.WandStages[2]);
                Assert.Contains(bId, a.Profile.Friends);
                Assert.Contains(aId, s.Peek(bId).Profile.Friends);
                Assert.AreEqual(1, s.ListingsOf(aId).Count);
                Assert.AreEqual(3, s.ListingsOf(aId)[0].Qty);
                Assert.AreSame(s.Peek(bId), s.FindByEmail("mail@example.com"));
                Assert.IsNotNull(s.FindByGoogle("sub-1"));
                Assert.IsNotNull(s.Login(bId, token), "session survives restart");
                Assert.IsNotNull(s.Login(aId, new string('a', 40)), "device secret survives restart");
                // 새 id는 겹치지 않는다.
                Assert.AreEqual(AccountStore.RegisterResult.Ok, s.Register("넷째", new string('b', 40), out var d));
                Assert.Greater(d.Id, bId);
                // 사기: 산 사람·판 사람·장터 물건이 한 트랜잭션.
                var buyer = s.Get(d.Id);
                buyer.Profile.Gold = 1000;
                s.Save();
                Assert.AreEqual(AccountStore.MarketResult.Ok, s.Buy(d.Id, s.ListingsOf(aId)[0].Id));
            }
            using (var db3 = new PostgresAccountDb(Cs))
            {
                var s = new AccountStore(db3);
                Assert.AreEqual(0, s.ListingsOf(aId).Count);
                Assert.AreEqual(3, s.FindByName("넷째").Profile.Qty(Ids.Item.StoneLow));
                Assert.AreEqual(700, s.FindByName("넷째").Profile.Gold);
            }
        }

        [Test]
        public void Constraints_RejectNegativeGold_AndKeepChangesForRetry()
        {
            var s = new AccountStore(_db);
            Assert.AreEqual(AccountStore.RegisterResult.Ok, s.Register("음수", new string('a', 40), out var a));
            s.Get(a.Id).Profile.Gold = -5;
            s.Save();
            Assert.IsNotNull(s.LastSaveError, "check (gold >= 0)");
            s.Get(a.Id).Profile.Gold = 5;
            s.Save();
            Assert.IsNull(s.LastSaveError);
            using (var db2 = new PostgresAccountDb(Cs)) Assert.AreEqual(5, new AccountStore(db2).Peek(a.Id).Profile.Gold);
        }

        [Test]
        public void Import_FromJson()
        {
            var json = new AccountStore((string)null);
            json.Register("옮길이", new string('c', 40), out var a);
            json.Get(a.Id).Profile.Add(Ids.Item.Charm, 2);
            var data = new AccountData { NextId = 50 };
            data.Accounts.Add(json.Peek(a.Id));
            Assert.IsTrue(_db.IsEmpty());
            _db.Import(data);
            Assert.IsFalse(_db.IsEmpty());
            var s = new AccountStore(_db);
            Assert.AreEqual(2, s.Peek(a.Id).Profile.Qty(Ids.Item.Charm));
            s.Register("다음", new string('d', 40), out var b);
            Assert.AreEqual(50, b.Id, "next id carried over");
        }
    }
}
