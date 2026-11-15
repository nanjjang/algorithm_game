using System;
using System.IO;
using NUnit.Framework;
using Wizard.Solo.Net;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 진짜 서버(Online.json의 주소 + TLS 지문) 확인. 환경 변수 WIZARD_LIVE=1일 때만 돈다(평소엔 Ignore — 통과로 세지 않는다).
    /// 가입·로그인·로그아웃을 해 보고, 만든 시험 계정의 이메일을 로그에 남긴다(지우는 법: 13-coop-server-deploy.md).
    /// </summary>
    public class LiveServerSmokeTests
    {
        IntegrationKit _k;
        string _acct;

        [SetUp]
        public void SetUp()
        {
            if (Environment.GetEnvironmentVariable("WIZARD_LIVE") != "1") Assert.Ignore("WIZARD_LIVE=1 only");
            _k = new IntegrationKit();
            _acct = Path.Combine(Path.GetTempPath(), "wizard_live_" + Guid.NewGuid().ToString("N") + ".json");
            OnlineAccount.FilePath = _acct;
        }

        [TearDown]
        public void TearDown()
        {
            OnlineAccount.FilePath = null;
            try { File.Delete(_acct); } catch (Exception) { }
            _k?.Dispose();
        }

        [Test]
        public void Live_Tls_Signup_Login_Logout()
        {
            var info = OnlineAuth.Info(_k.Db).Result;
            Assert.IsNotNull(info, "server reachable over TLS");
            Assert.IsTrue(info.Secure);
            string tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = "qa-live-" + tag + "@example.com";
            UnityEngine.Debug.Log("[live] test account " + email);
            var su = OnlineAuth.EmailSignup(_k.Db, email, "live smoke password " + tag, "시험" + tag.Substring(0, 6)).Result;
            Assert.IsTrue(su.Ok, "signup " + su.Error + " noServer=" + su.NoServer);
            using (var svc = OnlineService.Open(_k.Db))
            {
                Assert.IsNotNull(svc, "token login");
                Assert.AreEqual(su.Profile.Name, svc.Profile.Name);
            }
            OnlineAccount.Forget();
            var li = OnlineAuth.EmailLogin(_k.Db, email, "live smoke password " + tag).Result;
            Assert.IsTrue(li.Ok, "password login " + li.Error);
            Assert.AreEqual(AuthError.WrongLogin, OnlineAuth.EmailLogin(_k.Db, email, "wrong password!!").Result.Error);
            Assert.IsTrue(OnlineAuth.Logout(_k.Db).Wait(10000));
            Assert.IsNull(OnlineService.Open(_k.Db) is OnlineService s && !s.IsGuest ? s : null, "token revoked");
        }
    }
}
