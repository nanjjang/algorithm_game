using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Wizard.Solo.Net
{
    // 이메일·Google 계정과 로그인 표(docs/spec/13-ui-flows.md 3절, 08 8절). 비밀번호 해시는 무거워서(PBKDF2 10만 번) 서버가
    // 따로 스레드에서 계산하고, 여기 함수들은 그 결과를 받아 계정을 만들거나 로그인 표를 낸다(메인 스레드).
    public sealed partial class AccountStore
    {
        public const int PasswordMin = 10, PasswordMax = 128, EmailMax = 254, PasswordIterations = 100_000, SessionDays = 60, MaxSessions = 10;

        readonly Dictionary<string, AccountRecord> _byEmail = new Dictionary<string, AccountRecord>();
        readonly Dictionary<string, AccountRecord> _byGoogle = new Dictionary<string, AccountRecord>();

        public static string EmailKey(string email) => email == null ? null : email.Trim().ToLowerInvariant();

        /// <summary>꼴만 본다: @와 도메인 꼴, 254자 이하(13-ui-flows 3.2).</summary>
        public static bool IsValidEmail(string s)
            => !string.IsNullOrEmpty(s) && s.Length <= EmailMax && Regex.IsMatch(s.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        /// <summary>비밀번호 규칙: 10~128자, 문자 종류 강제 없음, 이메일 앞부분을 품으면 거절(13-ui-flows 3.2).</summary>
        public static bool IsValidPassword(string pw, string email)
        {
            if (pw == null || pw.Length < PasswordMin || pw.Length > PasswordMax) return false;
            string local = email != null && email.IndexOf('@') > 0 ? email.Substring(0, email.IndexOf('@')).Trim().ToLowerInvariant() : null;
            if (!string.IsNullOrEmpty(local) && local.Length >= 3 && pw.ToLowerInvariant().Contains(local)) return false;
            // 한 글자만 되풀이(aaaaaaaaaa)도 거절.
            bool same = true;
            for (int i = 1; i < pw.Length; i++) if (pw[i] != pw[0]) { same = false; break; }
            return !same;
        }

        public static string NewSalt()
        {
            var b = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return Convert.ToBase64String(b);
        }

        /// <summary>비밀번호 해시(PBKDF2-SHA256). 무겁다 — 서버는 메인 스레드 밖에서 부른다.</summary>
        public static string HashPassword(string pw, string saltB64, int iterations)
        {
            byte[] salt;
            try { salt = Convert.FromBase64String(saltB64); } catch (Exception) { return null; }
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(pw ?? string.Empty), salt, Math.Max(1, iterations), HashAlgorithmName.SHA256))
                return Convert.ToBase64String(kdf.GetBytes(32));
        }

        public static bool SameHash(string a, string b) => FixedEquals(a, b);

        public AccountRecord FindByEmail(string email) => email != null && _byEmail.TryGetValue(EmailKey(email), out var r) ? r : null;
        public AccountRecord FindByGoogle(string sub) => sub != null && _byGoogle.TryGetValue(sub, out var r) ? r : null;
        public bool NameTaken(string name) { var n = CleanName(name); return !string.IsNullOrEmpty(n) && _byName.ContainsKey(Key(n)); }

        public enum SignupResult { Ok, BadEmail, EmailTaken, BadName, NameTaken, WeakPassword, GoogleTaken }

        /// <summary>가입 전 검사(해시를 계산하기 전에 메인 스레드에서). Ok면 해시를 계산해 CreateEmailAccount로.</summary>
        public SignupResult CheckEmailSignup(string email, string password, string name)
        {
            if (!IsValidEmail(email)) return SignupResult.BadEmail;
            if (FindByEmail(email) != null) return SignupResult.EmailTaken;
            string n = CleanName(name);
            if (string.IsNullOrEmpty(n) || n.Length < NameMin) return SignupResult.BadName;
            if (_byName.ContainsKey(Key(n))) return SignupResult.NameTaken;
            if (!IsValidPassword(password, email)) return SignupResult.WeakPassword;
            return SignupResult.Ok;
        }

        AccountRecord NewAccount(string name)
        {
            string now = DateTime.UtcNow.ToString("o");
            var rec = new AccountRecord { Id = _data.NextId++, Name = name };
            rec.Profile.AccountId = rec.Id;
            rec.Profile.Name = name;
            rec.Profile.CreatedAt = now;
            rec.Profile.LastSeenAt = now;
            return rec;
        }

        void Add(AccountRecord rec)
        {
            _data.Accounts.Add(rec);
            Index(rec);
            Touch(rec.Id);
        }

        /// <summary>이메일 계정을 만든다(pwHash = HashPassword(password, pwSalt, PasswordIterations)). 그사이 이메일·이름이 잡혔으면 실패.</summary>
        public SignupResult CreateEmailAccount(string email, string name, string pwSalt, string pwHash, out AccountRecord rec)
        {
            rec = null;
            if (!IsValidEmail(email)) return SignupResult.BadEmail;
            if (FindByEmail(email) != null) return SignupResult.EmailTaken;
            string n = CleanName(name);
            if (string.IsNullOrEmpty(n) || n.Length < NameMin) return SignupResult.BadName;
            if (_byName.ContainsKey(Key(n))) return SignupResult.NameTaken;
            rec = NewAccount(n);
            rec.Email = email.Trim();
            rec.PwSalt = pwSalt;
            rec.PwHash = pwHash;
            rec.PwIter = PasswordIterations;
            Add(rec);
            Save();
            return SignupResult.Ok;
        }

        /// <summary>Google 계정을 만든다(처음 들어온 Google 사용자 + 고른 이름).</summary>
        public SignupResult CreateGoogleAccount(string sub, string email, bool emailVerified, string name, out AccountRecord rec)
        {
            rec = null;
            if (string.IsNullOrEmpty(sub)) return SignupResult.BadEmail;
            if (FindByGoogle(sub) != null) return SignupResult.GoogleTaken;
            string n = CleanName(name);
            if (string.IsNullOrEmpty(n) || n.Length < NameMin) return SignupResult.BadName;
            if (_byName.ContainsKey(Key(n))) return SignupResult.NameTaken;
            rec = NewAccount(n);
            rec.GoogleSub = sub;
            // 이메일 칸은 비밀번호 계정 것이라 여기 두지 않는다(같은 주소로 이메일 가입을 막지 않게). Google 주소는 기록만.
            rec.EmailVerified = false;
            Add(rec);
            Save();
            return SignupResult.Ok;
        }

        static string Sha256(string s)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? string.Empty)));
        }

        static bool SessionValid(AccountRecord rec, string token, long nowTicks)
        {
            if (rec.Sessions == null || rec.Sessions.Count == 0 || string.IsNullOrEmpty(token)) return false;
            string h = Sha256(token);
            foreach (var s in rec.Sessions) if (s.ExpiresTicks > nowTicks && FixedEquals(s.TokenHash, h)) return true;
            return false;
        }

        /// <summary>로그인 표를 새로 낸다(무작위 32바이트). 지난 표는 지우고, 10개를 넘으면 가장 오래된 것부터 지운다. 토큰 원문은 여기서만 나간다.</summary>
        public string IssueSession(AccountRecord rec)
        {
            var b = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            string token = Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            long now = DateTime.UtcNow.Ticks;
            if (rec.Sessions == null) rec.Sessions = new List<SessionRecord>();
            rec.Sessions.RemoveAll(s => s.ExpiresTicks <= now);
            while (rec.Sessions.Count >= MaxSessions) rec.Sessions.RemoveAt(0);
            rec.Sessions.Add(new SessionRecord { TokenHash = Sha256(token), CreatedTicks = now, ExpiresTicks = now + TimeSpan.FromDays(SessionDays).Ticks });
            rec.Profile.LastSeenAt = DateTime.UtcNow.ToString("o");
            Touch(rec.Id);
            Save();
            return token;
        }

        /// <summary>로그아웃: 그 토큰의 표를 지운다.</summary>
        public bool RevokeSession(long id, string token)
        {
            var rec = Peek(id);
            if (rec == null || rec.Sessions == null || string.IsNullOrEmpty(token)) return false;
            string h = Sha256(token);
            int n = rec.Sessions.RemoveAll(s => FixedEquals(s.TokenHash, h));
            if (n == 0) return false;
            Touch(id);
            Save();
            return true;
        }

        /// <summary>계정 종류(0 기기, 1 이메일, 2 Google).</summary>
        public static byte KindOf(AccountRecord rec)
            => rec == null ? (byte)0 : !string.IsNullOrEmpty(rec.GoogleSub) ? (byte)2 : !string.IsNullOrEmpty(rec.PwHash) ? (byte)1 : (byte)0;

        // ------------------------------------------------------------ 로그인 실패 대기(13-ui-flows 3.3)

        sealed class Throttle { public int Fails; public long UntilTicks; }
        readonly Dictionary<string, Throttle> _throttle = new Dictionary<string, Throttle>();

        /// <summary>그 이메일로 지금 시도해도 되나. 아니면 남은 초.</summary>
        public int ThrottleSeconds(string email, long nowTicks)
        {
            if (email == null || !_throttle.TryGetValue(EmailKey(email), out var t) || t.UntilTicks <= nowTicks) return 0;
            return (int)Math.Ceiling((t.UntilTicks - nowTicks) / (double)TimeSpan.TicksPerSecond);
        }

        /// <summary>틀림 하나를 센다: 5번째부터 1초에서 두 배씩(최대 15분) 기다리게 한다.</summary>
        public void LoginFailed(string email, long nowTicks)
        {
            if (email == null) return;
            string k = EmailKey(email);
            if (!_throttle.TryGetValue(k, out var t)) _throttle[k] = t = new Throttle();
            t.Fails++;
            if (t.Fails >= 5)
            {
                double sec = Math.Min(15 * 60, Math.Pow(2, t.Fails - 5));
                t.UntilTicks = nowTicks + (long)(sec * TimeSpan.TicksPerSecond);
            }
        }

        public void LoginSucceeded(string email) { if (email != null) _throttle.Remove(EmailKey(email)); }
    }
}
