using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Wizard.Solo.Net
{
    // 이메일·Google 로그인(docs/spec/13-ui-flows.md 3절). 무거운 일(비밀번호 해시, Google 토큰 끝점)은 Task로 돌리고, 끝나면 다음 Tick의
    // 메인 스레드에서 계정을 만들거나 로그인 표를 낸다. 그동안 그 연결의 메시지는 읽지 않는다.
    // 비밀번호와 Google 코드는 암호화된 연결(TlsPipe)에서만 받는다(AllowInsecureAuth는 테스트·한 기기 개발용).
    public sealed partial class RoomServer
    {
        /// <summary>Google 확인기(null 또는 ClientId 빈 문자열 = Google 로그인 꺼짐).</summary>
        public IGoogleVerifier Google;
        /// <summary>평문 연결에서도 비밀번호를 받는다(테스트·같은 기기 개발만).</summary>
        public bool AllowInsecureAuth;
        /// <summary>복구 메일(확인 코드·비밀번호 찾기). 메일 발송을 붙이기 전까지 꺼져 있다(13-ui-flows 3.5 "복구 메일 꺼짐").</summary>
        public bool RecoveryMailEnabled;
        /// <summary>복구 코드를 보내는 쪽(null이면 보내지 않는다). RecoveryMailEnabled가 켜져 있어야 한다.</summary>
        public IRecoveryMailer Mailer;

        sealed class Job { public Task Work; public Action Finish; }
        readonly Dictionary<INetPipe, Job> _jobs = new Dictionary<INetPipe, Job>();
        readonly List<INetPipe> _doneJobs = new List<INetPipe>();

        sealed class GoogleTicket { public GoogleIdentity Who; public long ExpiresTicks; }
        readonly Dictionary<string, GoogleTicket> _tickets = new Dictionary<string, GoogleTicket>();

        /// <summary>계정 일이 걸려 있는 연결 수(테스트가 기다릴 때).</summary>
        public int PendingJobs => _jobs.Count;

        void StartJob(INetPipe pipe, Action work, Action finish)
        {
            _jobs[pipe] = new Job { Work = Task.Run(work), Finish = finish };
        }

        void RunJobs()
        {
            if (_jobs.Count == 0) return;
            foreach (var kv in _jobs) if (kv.Value.Work.IsCompleted) _doneJobs.Add(kv.Key);
            foreach (var pipe in _doneJobs)
            {
                var j = _jobs[pipe];
                _jobs.Remove(pipe);
                if (!pipe.Connected) continue;
                try { j.Finish(); }
                catch (Exception e) { Log?.Invoke("[room-server] auth job " + e.GetType().Name + ": " + e.Message); Fail(pipe, AuthError.ServerError); }
            }
            _doneJobs.Clear();
        }

        const string RecoveryMailSubject = "[엄청엄청 위대한 마법사 키우기] 비밀번호 재설정 코드";

        static string RecoveryMailBody(string code)
            => "비밀번호 재설정 코드: " + code + "\n\n"
             + "게임의 비밀번호 찾기 화면에 이 코드를 넣고 새 비밀번호를 정해 주세요. 코드는 " + AccountStore.RecoveryCodeMinutes + "분 동안만 쓸 수 있습니다.\n"
             + "직접 요청하지 않았다면 이 메일은 무시해도 됩니다. 비밀번호는 바뀌지 않습니다.";

        bool Secure(INetPipe pipe) => AllowInsecureAuth || (pipe is ISecurePipe s && s.IsSecure);

        void Fail(INetPipe pipe, AuthError err, int waitSeconds = 0, bool close = true)
        {
            var f = new NetWriter(NetMsg.AuthFail);
            f.Byte((byte)err);
            f.Int(waitSeconds);
            pipe.Send(f.ToArray());
            if (!close) return;
            pipe.Close();
            _pending.Remove(pipe);
            _authed.Remove(pipe);
        }

        /// <summary>AuthOk: 계정 id, 서버 프로필, 새 로그인 표(없으면 null), 이메일, 계정 종류.</summary>
        void SendAuthOk(INetPipe pipe, AccountRecord rec, string token)
        {
            var ok = new NetWriter(NetMsg.AuthOk);
            ok.Long(rec.Id);
            NetCodec.WriteProfile(ok, rec.Profile);
            ok.Str(token);
            ok.Str(rec.Email);
            ok.Byte(AccountStore.KindOf(rec));
            pipe.Send(ok.ToArray());
        }

        void LoggedIn(INetPipe pipe, AccountRecord rec, string how)
        {
            string token = Accounts.IssueSession(rec);
            _authed[pipe] = rec;
            SendAuthOk(pipe, rec, token);
            Log?.Invoke("[room-server] account " + rec.Id + " " + how);
        }

        static AuthError ErrorOf(AccountStore.SignupResult r)
        {
            switch (r)
            {
                case AccountStore.SignupResult.BadEmail: return AuthError.BadEmail;
                case AccountStore.SignupResult.EmailTaken: return AuthError.EmailTaken;
                case AccountStore.SignupResult.BadName: return AuthError.BadName;
                case AccountStore.SignupResult.NameTaken: return AuthError.NameTaken;
                case AccountStore.SignupResult.WeakPassword: return AuthError.WeakPassword;
                default: return AuthError.ServerError;
            }
        }

        static string NewTicket()
        {
            var b = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return Convert.ToBase64String(b);
        }

        /// <summary>서버 정보·이메일·Google·로그아웃 메시지면 처리하고 true.</summary>
        bool TryAccountMessage(INetPipe pipe, byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            var kind = (NetMsg)data[0];
            switch (kind)
            {
                case NetMsg.ServerInfoRequest:
                case NetMsg.EmailSignup:
                case NetMsg.EmailLogin:
                case NetMsg.GoogleLogin:
                case NetMsg.GoogleFinish:
                case NetMsg.Logout:
                case NetMsg.RecoveryRequest:
                case NetMsg.RecoveryConfirm:
                    break;
                default:
                    return false;
            }
            NetReader r;
            try { r = new NetReader(data); r.Byte(); }
            catch (Exception) { Fail(pipe, AuthError.ServerError); return true; }
            try
            {
                switch (kind)
                {
                    case NetMsg.ServerInfoRequest:
                    {
                        var m = new NetWriter(NetMsg.ServerInfo);
                        m.Str(Google != null ? Google.ClientId ?? string.Empty : string.Empty);
                        m.Bool(RecoveryMailEnabled);
                        m.Int(AccountStore.PasswordMin);
                        m.Bool(Secure(pipe));
                        pipe.Send(m.ToArray());
                        return true;
                    }
                    case NetMsg.EmailLogin:
                    {
                        string email = r.Str(), pw = r.Str();
                        if (!Secure(pipe)) { Fail(pipe, AuthError.NotSecure); return true; }
                        long now = DateTime.UtcNow.Ticks;
                        int wait = Accounts.ThrottleSeconds(email, now);
                        if (wait > 0) { Fail(pipe, AuthError.Throttled, wait); return true; }
                        var rec = Accounts.FindByEmail(email);
                        bool has = rec != null && !string.IsNullOrEmpty(rec.PwHash);
                        // 없는 계정도 같은 일을 해서 걸리는 시간으로 알아내지 못하게 한다.
                        string salt = has ? rec.PwSalt : "AAAAAAAAAAAAAAAAAAAAAA==", want = has ? rec.PwHash : null;
                        int iter = has ? rec.PwIter : AccountStore.PasswordIterations;
                        bool ok = false;
                        StartJob(pipe, () => { var h = AccountStore.HashPassword(pw, salt, iter); ok = want != null && AccountStore.SameHash(h, want); }, () =>
                        {
                            if (!ok || Accounts.Peek(rec.Id) != rec) { Accounts.LoginFailed(email, DateTime.UtcNow.Ticks); Fail(pipe, AuthError.WrongLogin); return; }
                            Accounts.LoginSucceeded(email);
                            LoggedIn(pipe, rec, "email login");
                        });
                        return true;
                    }
                    case NetMsg.EmailSignup:
                    {
                        string email = r.Str(), pw = r.Str(), name = r.Str();
                        if (!Secure(pipe)) { Fail(pipe, AuthError.NotSecure); return true; }
                        var check = Accounts.CheckEmailSignup(email, pw, name);
                        if (check != AccountStore.SignupResult.Ok) { Fail(pipe, ErrorOf(check)); return true; }
                        string salt = AccountStore.NewSalt(), hash = null;
                        StartJob(pipe, () => hash = AccountStore.HashPassword(pw, salt, AccountStore.PasswordIterations), () =>
                        {
                            var res = Accounts.CreateEmailAccount(email, name, salt, hash, out var rec);
                            if (res != AccountStore.SignupResult.Ok) { Fail(pipe, ErrorOf(res)); return; }
                            LoggedIn(pipe, rec, "email signup");
                        });
                        return true;
                    }
                    case NetMsg.GoogleLogin:
                    {
                        string code = r.Str(), verifier = r.Str(), redirect = r.Str();
                        if (!Secure(pipe)) { Fail(pipe, AuthError.NotSecure); return true; }
                        var g = Google;
                        if (g == null || string.IsNullOrEmpty(g.ClientId)) { Fail(pipe, AuthError.GoogleOff); return true; }
                        GoogleIdentity who = null;
                        StartJob(pipe, () => who = g.Exchange(code, verifier, redirect), () =>
                        {
                            if (who == null || who.Error != null)
                            {
                                Log?.Invoke("[room-server] google failed: " + (who != null ? who.Error : "null"));
                                Fail(pipe, AuthError.GoogleFailed);
                                return;
                            }
                            var rec = Accounts.FindByGoogle(who.Sub);
                            if (rec != null) { LoggedIn(pipe, rec, "google login"); return; }
                            long now = DateTime.UtcNow.Ticks;
                            var expired = new List<string>();
                            foreach (var kv in _tickets) if (kv.Value.ExpiresTicks < now) expired.Add(kv.Key);
                            foreach (var k in expired) _tickets.Remove(k);
                            string ticket = NewTicket();
                            _tickets[ticket] = new GoogleTicket { Who = who, ExpiresTicks = now + TimeSpan.FromMinutes(10).Ticks };
                            var m = new NetWriter(NetMsg.GoogleNeedName);
                            m.Str(ticket);
                            m.Str(AccountStore.CleanName(who.Name) ?? string.Empty);
                            pipe.Send(m.ToArray());
                        });
                        return true;
                    }
                    case NetMsg.GoogleFinish:
                    {
                        string ticket = r.Str(), name = r.Str();
                        if (ticket == null || !_tickets.TryGetValue(ticket, out var t) || t.ExpiresTicks < DateTime.UtcNow.Ticks)
                        {
                            if (ticket != null) _tickets.Remove(ticket);
                            Fail(pipe, AuthError.BadTicket);
                            return true;
                        }
                        var existing = Accounts.FindByGoogle(t.Who.Sub);
                        if (existing != null) { _tickets.Remove(ticket); LoggedIn(pipe, existing, "google login"); return true; }
                        var res = Accounts.CreateGoogleAccount(t.Who.Sub, t.Who.Email, t.Who.EmailVerified, name, out var rec);
                        if (res == AccountStore.SignupResult.BadName || res == AccountStore.SignupResult.NameTaken) { Fail(pipe, ErrorOf(res), 0, close: false); return true; }
                        if (res != AccountStore.SignupResult.Ok) { Fail(pipe, ErrorOf(res)); return true; }
                        _tickets.Remove(ticket);
                        LoggedIn(pipe, rec, "google signup");
                        return true;
                    }
                    case NetMsg.RecoveryRequest:
                    {
                        string email = r.Str();
                        if (!Secure(pipe)) { Fail(pipe, AuthError.NotSecure); return true; }
                        var mailer = Mailer;
                        if (!RecoveryMailEnabled || mailer == null || !mailer.Enabled) { Fail(pipe, AuthError.MailOff); return true; }
                        if (!AccountStore.IsValidEmail(email)) { Fail(pipe, AuthError.BadEmail); return true; }
                        long now = DateTime.UtcNow.Ticks;
                        int wait = Accounts.RecoveryWaitSeconds(email, now);
                        if (wait > 0) { Fail(pipe, AuthError.Throttled, wait); return true; }
                        string code = Accounts.BeginRecovery(email, now);
                        long accountId = code != null ? Accounts.FindByEmail(email).Id : 0;
                        string to = email.Trim();
                        StartJob(pipe, () =>
                        {
                            if (code == null) { System.Threading.Thread.Sleep(400); return; } // 계정이 없어도 비슷한 시간이 걸리게
                            if (!mailer.Send(to, RecoveryMailSubject, RecoveryMailBody(code), out string err))
                                Log?.Invoke("[room-server] recovery mail failed: " + err);
                        }, () =>
                        {
                            if (code != null) Log?.Invoke("[room-server] account " + accountId + " recovery code requested");
                            pipe.Send(new NetWriter(NetMsg.RecoveryRequested).ToArray());
                        });
                        return true;
                    }
                    case NetMsg.RecoveryConfirm:
                    {
                        string email = r.Str(), code = r.Str(), pw = r.Str();
                        if (!Secure(pipe)) { Fail(pipe, AuthError.NotSecure); return true; }
                        if (!RecoveryMailEnabled) { Fail(pipe, AuthError.MailOff); return true; }
                        if (!AccountStore.IsValidPassword(pw, email)) { Fail(pipe, AuthError.WeakPassword); return true; }
                        if (!Accounts.ConfirmRecoveryCode(email, code, DateTime.UtcNow.Ticks)) { Fail(pipe, AuthError.BadCode); return true; }
                        var rec = Accounts.FindByEmail(email);
                        if (rec == null) { Fail(pipe, AuthError.BadCode); return true; }
                        string salt = AccountStore.NewSalt(), hash = null;
                        StartJob(pipe, () => hash = AccountStore.HashPassword(pw, salt, AccountStore.PasswordIterations), () =>
                        {
                            if (!Accounts.SetPassword(rec, salt, hash)) { Fail(pipe, AuthError.ServerError); return; }
                            Log?.Invoke("[room-server] account " + rec.Id + " password reset");
                            pipe.Send(new NetWriter(NetMsg.RecoveryDone).ToArray());
                        });
                        return true;
                    }
                    case NetMsg.Logout:
                    {
                        string token = r.Str();
                        if (_authed.TryGetValue(pipe, out var acct)) Accounts.RevokeSession(acct.Id, token);
                        pipe.Send(new NetWriter(NetMsg.LogoutDone).ToArray());
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log?.Invoke("[room-server] account message " + e.GetType().Name + ": " + e.Message);
                Fail(pipe, AuthError.ServerError);
                return true;
            }
            return false;
        }
    }
}
