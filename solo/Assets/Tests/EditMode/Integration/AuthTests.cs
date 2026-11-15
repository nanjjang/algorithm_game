using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Net;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 이메일·Google 로그인(11-online.md 7절): 진짜 TLS(127.0.0.1, 시험용 인증서 + 지문 핀)로 RoomServer에 붙는다.
    /// 서버는 따로 스레드에서 돈다(로그인 결과를 기다리는 동안).
    /// </summary>
    public class AuthTests
    {
        sealed class FakeGoogle : IGoogleVerifier
        {
            public string ClientId => "test-client";
            public GoogleIdentity Next = new GoogleIdentity { Sub = "g-123", Email = "g@example.com", EmailVerified = true, Name = "구글마법사" };
            public GoogleIdentity Exchange(string code, string verifier, string redirect)
                => code == "good" ? Next : new GoogleIdentity { Error = "bad code" };
        }

        IntegrationKit _k;
        IPipeListener _listener;
        RoomServer _server;
        Thread _thread;
        volatile bool _stop;
        string _acctFile;
        FakeGoogle _google;

        static string Fixture => Path.Combine(Application.dataPath, "Tests/EditMode/Integration/Fixtures/test-server.pfx");

        void Start(bool tls, bool allowInsecure = false)
        {
            _k = new IntegrationKit();
            _server = new RoomServer(_k.Db, null, 7) { AllowInsecureAuth = allowInsecure };
            _google = new FakeGoogle();
            _server.Google = _google;
            if (tls)
            {
                var cert = TlsPipeListener.LoadCertificate(Fixture, "wizard");
                _listener = new TlsPipeListener(0, cert, bindAll: false);
                OnlineServer.OverridePins = new List<string> { TlsPipe.Fingerprint(cert) };
            }
            else
            {
                _listener = new TcpPipeListener(0, bindAll: false);
                OnlineServer.OverridePins = null;
            }
            OnlineServer.Override = new List<string> { "127.0.0.1:" + _listener.Port };
            _acctFile = Path.Combine(Path.GetTempPath(), "wizard_auth_" + Guid.NewGuid().ToString("N") + ".json");
            OnlineAccount.FilePath = _acctFile;
            _stop = false;
            _thread = new Thread(() =>
            {
                while (!_stop)
                {
                    lock (_server)
                    {
                        INetPipe p;
                        while ((p = _listener.Poll()) != null) _server.Accept(p);
                        _server.Tick();
                    }
                    Thread.Sleep(2);
                }
            }) { IsBackground = true };
            _thread.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _stop = true;
            _thread?.Join(2000);
            OnlineServer.Override = null;
            OnlineServer.OverridePins = null;
            OnlineAccount.FilePath = null;
            OnlineAuth.AllowInsecure = false;
            try { File.Delete(_acctFile); } catch (Exception) { }
            _listener?.Stop();
            _k?.Dispose();
        }

        static T Await<T>(System.Threading.Tasks.Task<T> t)
        {
            Assert.IsTrue(t.Wait(20000), "timed out");
            return t.Result;
        }

        [Test]
        public void Email_Signup_Then_TokenLogin_Then_PasswordLogin()
        {
            Start(tls: true);
            var info = Await(OnlineAuth.Info(_k.Db));
            Assert.IsNotNull(info);
            Assert.IsTrue(info.Secure);
            Assert.AreEqual("test-client", info.GoogleClientId);
            Assert.AreEqual(AccountStore.PasswordMin, info.PasswordMin);

            var su = Await(OnlineAuth.EmailSignup(_k.Db, "Wizard@Example.com", "correct horse battery", "메일마법사"));
            Assert.IsTrue(su.Ok, "signup " + su.Error);
            Assert.AreEqual(AccountKind.Email, su.Account.Kind);
            Assert.AreEqual("메일마법사", su.Profile.Name);
            var saved = OnlineAccount.Load();
            Assert.IsNotNull(saved);
            Assert.AreNotEqual("correct horse battery", saved.Secret, "password is never stored on the device");

            // 같은 이메일(대소문자 무시)·같은 이름은 안 된다.
            Assert.AreEqual(AuthError.EmailTaken, Await(OnlineAuth.EmailSignup(_k.Db, "wizard@example.com", "another long password", "다른이름")).Error);
            Assert.AreEqual(AuthError.NameTaken, Await(OnlineAuth.EmailSignup(_k.Db, "b@example.com", "another long password", "메일마법사")).Error);
            Assert.AreEqual(AuthError.WeakPassword, Await(OnlineAuth.EmailSignup(_k.Db, "c@example.com", "short", "셋째")).Error);
            Assert.AreEqual(AuthError.BadEmail, Await(OnlineAuth.EmailSignup(_k.Db, "not-an-email", "another long password", "넷째")).Error);

            // 저장된 로그인 표로 서비스 연결(OnlineService)이 된다.
            using (var svc = OnlineService.Open(_k.Db))
            {
                Assert.IsNotNull(svc);
                Assert.IsFalse(svc.IsGuest);
                Assert.AreEqual("메일마법사", svc.Profile.Name);
            }

            OnlineAccount.Forget();
            var li = Await(OnlineAuth.EmailLogin(_k.Db, " wizard@example.com ", "correct horse battery"));
            Assert.IsTrue(li.Ok, "login " + li.Error);
            Assert.AreEqual(su.Account.Id, li.Account.Id);
            Assert.AreNotEqual(su.Account.Secret, li.Account.Secret, "each login gets its own token");
        }

        [Test]
        public void Email_WrongPassword_IsThrottled_AfterFiveTries()
        {
            Start(tls: true);
            Assert.IsTrue(Await(OnlineAuth.EmailSignup(_k.Db, "t@example.com", "the right password", "틀림마법사")).Ok);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(AuthError.WrongLogin, Await(OnlineAuth.EmailLogin(_k.Db, "t@example.com", "the wrong password")).Error);
            var r = Await(OnlineAuth.EmailLogin(_k.Db, "t@example.com", "the right password"));
            Assert.AreEqual(AuthError.Throttled, r.Error);
            Assert.Greater(r.WaitSeconds, 0);
            // 없는 이메일도 같은 답(무엇이 틀렸는지 알려 주지 않는다).
            Assert.AreEqual(AuthError.WrongLogin, Await(OnlineAuth.EmailLogin(_k.Db, "nobody@example.com", "whatever password")).Error);
        }

        [Test]
        public void Password_IsRefused_OnPlainConnection()
        {
            Start(tls: false);
            // 클라이언트가 먼저 막는다.
            Assert.AreEqual(AuthError.NotSecure, Await(OnlineAuth.EmailSignup(_k.Db, "p@example.com", "a long enough password", "평문")).Error);
            // 클라이언트를 속여도 서버가 막는다.
            OnlineAuth.AllowInsecure = true;
            Assert.AreEqual(AuthError.NotSecure, Await(OnlineAuth.EmailSignup(_k.Db, "p@example.com", "a long enough password", "평문")).Error);
            var info = Await(OnlineAuth.Info(_k.Db));
            Assert.IsFalse(info.Secure);
        }

        [Test]
        public void Tls_WrongPin_DoesNotConnect()
        {
            Start(tls: true);
            OnlineServer.OverridePins = new List<string> { new string('0', 64) };
            Assert.IsNull(OnlineServer.Connect(_k.Db));
        }

        [Test]
        public void Google_FirstTime_AsksName_ThenLogsIn()
        {
            Start(tls: true);
            var code = new GoogleCode { Code = "good", Verifier = "v", RedirectUri = "http://127.0.0.1:5555/" };
            Assert.IsTrue(Await(OnlineAuth.EmailSignup(_k.Db, "x@example.com", "some long password", "먼저온이")).Ok);

            var first = Await(OnlineAuth.GoogleExchange(_k.Db, code));
            Assert.IsTrue(first.NeedName);
            Assert.AreEqual("구글마법사", first.SuggestedName);
            var taken = Await(OnlineAuth.GoogleFinish(first, "먼저온이"));
            Assert.AreEqual(AuthError.NameTaken, taken.Error);
            Assert.IsTrue(taken.NeedName, "can retry with another name on the same connection");
            var done = Await(OnlineAuth.GoogleFinish(taken, "구글마법사"));
            Assert.IsTrue(done.Ok, "finish " + done.Error);
            Assert.AreEqual(AccountKind.Google, done.Account.Kind);

            var again = Await(OnlineAuth.GoogleExchange(_k.Db, code));
            Assert.IsTrue(again.Ok);
            Assert.AreEqual(done.Account.Id, again.Account.Id);

            var bad = Await(OnlineAuth.GoogleExchange(_k.Db, new GoogleCode { Code = "bad", Verifier = "v", RedirectUri = "http://127.0.0.1:5555/" }));
            Assert.AreEqual(AuthError.GoogleFailed, bad.Error);
        }

        [Test]
        public void Logout_RevokesToken()
        {
            Start(tls: true);
            var su = Await(OnlineAuth.EmailSignup(_k.Db, "o@example.com", "logout password", "나갈사람"));
            Assert.IsTrue(su.Ok);
            string token = su.Account.Secret;
            Assert.IsTrue(OnlineAuth.Logout(_k.Db).Wait(10000));
            Assert.IsNull(OnlineAccount.Load());
            lock (_server) Assert.IsNull(_server.Accounts.Login(su.Account.Id, token));
        }

        [Test]
        public void GoogleSignIn_LoopbackReceivesCode()
        {
            using (var s = GoogleSignIn.Begin("cid", "<html>ok</html>", 20))
            {
                Assert.IsNotNull(s.Url);
                StringAssert.Contains("code_challenge_method=S256", s.Url);
                var q = new Uri(s.Url).Query.TrimStart('?').Split('&');
                string state = null, redirect = null;
                foreach (var kv in q)
                {
                    var p = kv.Split('=');
                    if (p[0] == "state") state = Uri.UnescapeDataString(p[1]);
                    if (p[0] == "redirect_uri") redirect = Uri.UnescapeDataString(p[1]);
                }
                using (var http = new HttpClient())
                {
                    var body = http.GetStringAsync(redirect + "?code=abc&state=" + Uri.EscapeDataString(state)).Result;
                    Assert.AreEqual("<html>ok</html>", body);
                }
                Assert.IsTrue(s.Result.Wait(10000));
                Assert.IsNull(s.Result.Result.Error);
                Assert.AreEqual("abc", s.Result.Result.Code);
                Assert.AreEqual(redirect, s.Result.Result.RedirectUri);
            }
        }

        [Test]
        public void IdToken_ChecksIssuerAudienceExpiry()
        {
            string Jwt(string payload) => "x." + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".sig";
            long now = 1_700_000_000;
            var ok = GoogleOAuthServer.ParseIdToken(Jwt("{\"iss\":\"https://accounts.google.com\",\"aud\":\"cid\",\"exp\":" + (now + 60) + ",\"sub\":\"42\",\"email\":\"a@b.c\",\"email_verified\":true,\"name\":\"A\"}"), "cid", now);
            Assert.IsNull(ok.Error);
            Assert.AreEqual("42", ok.Sub);
            Assert.IsTrue(ok.EmailVerified);
            Assert.AreEqual("aud", GoogleOAuthServer.ParseIdToken(Jwt("{\"iss\":\"accounts.google.com\",\"aud\":\"other\",\"exp\":" + (now + 60) + ",\"sub\":\"42\"}"), "cid", now).Error);
            Assert.AreEqual("iss", GoogleOAuthServer.ParseIdToken(Jwt("{\"iss\":\"evil.com\",\"aud\":\"cid\",\"exp\":" + (now + 60) + ",\"sub\":\"42\"}"), "cid", now).Error);
            Assert.AreEqual("exp", GoogleOAuthServer.ParseIdToken(Jwt("{\"iss\":\"accounts.google.com\",\"aud\":\"cid\",\"exp\":" + (now - 3600) + ",\"sub\":\"42\"}"), "cid", now).Error);
            Assert.IsFalse(GoogleOAuthServer.IsLoopback("https://evil.com/"));
            Assert.IsTrue(GoogleOAuthServer.IsLoopback("http://127.0.0.1:5000/"));
        }

        [Test]
        public void PasswordRules()
        {
            Assert.IsFalse(AccountStore.IsValidPassword("short", "a@b.c"));
            Assert.IsFalse(AccountStore.IsValidPassword("aaaaaaaaaaaa", "a@b.c"));
            Assert.IsFalse(AccountStore.IsValidPassword("wizard12345678", "wizard@b.c"), "contains the email's local part");
            Assert.IsTrue(AccountStore.IsValidPassword("correct horse battery", "wizard@b.c"));
            Assert.IsTrue(AccountStore.IsValidEmail("a.b@c.de"));
            Assert.IsFalse(AccountStore.IsValidEmail("a@b"));
            Assert.IsFalse(AccountStore.IsValidEmail("a b@c.de"));
        }
    }
}
