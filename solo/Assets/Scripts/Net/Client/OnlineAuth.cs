using System;
using System.Threading;
using System.Threading.Tasks;
using Wizard.Solo.Data;

namespace Wizard.Solo.Net
{
    /// <summary>서버가 켠 로그인 기능(로그인 전에 묻는다).</summary>
    public sealed class ServerInfo
    {
        public string GoogleClientId;
        public bool RecoveryMail;
        public int PasswordMin;
        public bool Secure;
        public bool GoogleOn => !string.IsNullOrEmpty(GoogleClientId);
    }

    /// <summary>로그인·가입 결과. 성공이면 Account(이 기기에 저장됨), Google 첫 가입이면 NeedName(같은 연결로 GoogleFinish).</summary>
    public sealed class AuthResult : IDisposable
    {
        public OnlineAccount Account;
        public ServerProfile Profile;
        public AuthError? Error;
        public int WaitSeconds;
        /// <summary>서버에 못 붙었다(또는 답이 없다).</summary>
        public bool NoServer;
        public bool NeedName;
        public string Ticket, SuggestedName;
        internal INetPipe Pipe;
        public bool Ok => Account != null;
        public void Dispose() { try { Pipe?.Close(); } catch (Exception) { } Pipe = null; }
    }

    /// <summary>
    /// 로그인 화면용(11-online.md 7절): 전부 Task로 돈다(화면은 Tick에서 IsCompleted를 본다 — 게임이 멈추지 않는다).
    /// 비밀번호는 TLS 연결에서만 보낸다(Online.json의 tls_pins가 비어 있으면 보내지 않고 NotSecure).
    /// </summary>
    public static class OnlineAuth
    {
        public const int ReplyTimeoutMs = 15000;

        static byte[] Wait(INetPipe pipe, int timeoutMs, params NetMsg[] kinds)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && pipe.Connected)
            {
                if (!pipe.TryReceive(out var data)) { Thread.Sleep(5); continue; }
                if (data.Length == 0) continue;
                foreach (var k in kinds) if ((NetMsg)data[0] == k) return data;
            }
            // 끊긴 뒤에도 큐에 남은 답을 마저 본다.
            while (pipe.TryReceive(out var rest)) { if (rest.Length == 0) continue; foreach (var k in kinds) if ((NetMsg)rest[0] == k) return rest; }
            return null;
        }

        static bool IsSecure(INetPipe p) => p is ISecurePipe s && s.IsSecure;

        /// <summary>테스트·같은 기기 개발용: 평문 연결에서도 비밀번호를 보낸다.</summary>
        public static bool AllowInsecure;

        public static Task<ServerInfo> Info(IContentDb db) => Task.Run(() =>
        {
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return null;
            try
            {
                pipe.Send(new NetWriter(NetMsg.ServerInfoRequest).ToArray());
                var data = Wait(pipe, 5000, NetMsg.ServerInfo);
                if (data == null) return null;
                var r = new NetReader(data);
                r.Byte();
                var info = new ServerInfo { GoogleClientId = r.Str(), RecoveryMail = r.Bool(), PasswordMin = r.Int(), Secure = r.Bool() };
                if (!IsSecure(pipe) && !AllowInsecure) info.Secure = false;
                return info;
            }
            catch (Exception) { return null; }
            finally { pipe.Close(); }
        });

        /// <summary>AuthOk / AuthFail / GoogleNeedName 하나를 읽어 결과로. keepPipe면 성공·이름 필요일 때 연결을 닫지 않는다.</summary>
        static AuthResult Read(INetPipe pipe)
        {
            var data = Wait(pipe, ReplyTimeoutMs, NetMsg.AuthOk, NetMsg.AuthFail, NetMsg.GoogleNeedName);
            if (data == null) return new AuthResult { NoServer = true };
            var r = new NetReader(data);
            var kind = (NetMsg)r.Byte();
            if (kind == NetMsg.AuthFail)
            {
                var err = (AuthError)r.Byte();
                int wait = 0;
                try { wait = r.Int(); } catch (Exception) { }
                return new AuthResult { Error = err, WaitSeconds = wait };
            }
            if (kind == NetMsg.GoogleNeedName)
                return new AuthResult { NeedName = true, Ticket = r.Str(), SuggestedName = r.Str(), Pipe = pipe };
            long id = r.Long();
            var profile = NetCodec.ReadProfile(r);
            string token = r.Str(), email = r.Str();
            var k = (AccountKind)r.Byte();
            var acct = OnlineAccount.SaveLogin(id, profile, token, email, k);
            if (acct == null) return new AuthResult { Error = AuthError.ServerError };
            return new AuthResult { Account = acct, Profile = profile };
        }

        static AuthResult Send(IContentDb db, NetWriter m)
        {
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return new AuthResult { NoServer = true };
            if (!IsSecure(pipe) && !AllowInsecure) { pipe.Close(); return new AuthResult { Error = AuthError.NotSecure }; }
            AuthResult res = null;
            try
            {
                pipe.Send(m.ToArray());
                res = Read(pipe);
                return res;
            }
            catch (Exception) { return res = new AuthResult { NoServer = true }; }
            finally { if (res == null || !res.NeedName) pipe.Close(); }
        }

        public static Task<AuthResult> EmailLogin(IContentDb db, string email, string password) => Task.Run(() =>
        {
            var m = new NetWriter(NetMsg.EmailLogin);
            m.Str(email != null ? email.Trim() : null);
            m.Str(password);
            return Send(db, m);
        });

        public static Task<AuthResult> EmailSignup(IContentDb db, string email, string password, string name) => Task.Run(() =>
        {
            var m = new NetWriter(NetMsg.EmailSignup);
            m.Str(email != null ? email.Trim() : null);
            m.Str(password);
            m.Str(name);
            return Send(db, m);
        });

        /// <summary>브라우저에서 받은 코드를 서버로. 처음 온 Google 사용자면 NeedName(연결을 들고 있다 → GoogleFinish).</summary>
        public static Task<AuthResult> GoogleExchange(IContentDb db, GoogleCode code) => Task.Run(() =>
        {
            var m = new NetWriter(NetMsg.GoogleLogin);
            m.Str(code.Code);
            m.Str(code.Verifier);
            m.Str(code.RedirectUri);
            return Send(db, m);
        });

        /// <summary>Google 가입 마무리(이름). 이름이 문제면 같은 결과(NeedName 그대로, Error 채움)를 돌려준다 — 다시 부를 수 있다.</summary>
        public static Task<AuthResult> GoogleFinish(AuthResult pending, string name) => Task.Run(() =>
        {
            var pipe = pending != null ? pending.Pipe : null;
            if (pipe == null || !pipe.Connected) return new AuthResult { Error = AuthError.BadTicket };
            var m = new NetWriter(NetMsg.GoogleFinish);
            m.Str(pending.Ticket);
            m.Str(name);
            pipe.Send(m.ToArray());
            var res = Read(pipe);
            if (res.Error == AuthError.NameTaken || res.Error == AuthError.BadName)
            {
                pending.Error = res.Error;
                return pending;
            }
            pending.Pipe = null;
            pipe.Close();
            return res;
        });

        /// <summary>로그아웃: 서버의 로그인 표를 지우고(붙을 수 있으면) 이 기기 파일을 지운다.</summary>
        public static Task Logout(IContentDb db) => Logout(db, OnlineAccount.Load(), forget: true);

        /// <summary>그 계정의 로그인 표를 서버에서 지운다(acct는 부르는 쪽이 미리 읽어 둔 것). forget이면 이 기기 파일도 지운다.</summary>
        public static Task Logout(IContentDb db, OnlineAccount acct, bool forget = false) => Task.Run(() =>
        {
            if (forget) OnlineAccount.Forget();
            if (acct == null || acct.Kind == AccountKind.Device) return;
            var pipe = OnlineServer.Connect(db);
            if (pipe == null) return;
            try
            {
                acct.SendLogin(pipe);
                if (Wait(pipe, 5000, NetMsg.AuthOk, NetMsg.AuthFail) is byte[] d && (NetMsg)d[0] == NetMsg.AuthOk)
                {
                    var m = new NetWriter(NetMsg.Logout);
                    m.Str(acct.Secret);
                    pipe.Send(m.ToArray());
                    Wait(pipe, 3000, NetMsg.LogoutDone);
                }
            }
            catch (Exception) { }
            finally { pipe.Close(); }
        });
    }
}
