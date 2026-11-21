using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Wizard.Solo.Net
{
    // 비밀번호 찾기(복구 메일). 6자리 코드를 이메일로 보내고, 코드로 새 비밀번호를 정한다.
    // 코드는 메모리에만 둔다(해시로, 서버를 다시 켜면 사라진다 — 15분짜리라 괜찮다). 요청 간격·횟수와 맞히기 횟수를 제한한다.
    // 없는 이메일도 같은 제한과 같은 답을 받는다(계정이 있는지 알아내지 못하게).
    public sealed partial class AccountStore
    {
        public const int RecoveryCodeMinutes = 15, RecoveryCooldownSeconds = 60, RecoveryPerHour = 5, RecoveryMaxAttempts = 5;

        sealed class RecoveryState
        {
            public string CodeHash;
            public long ExpiresTicks;
            public int Attempts;
            public long LastRequestTicks;
            public readonly List<long> HourRequests = new List<long>();
        }

        readonly Dictionary<string, RecoveryState> _recovery = new Dictionary<string, RecoveryState>();

        static string RecoveryHash(string emailKey, string code) => Sha256(emailKey + "|" + code);

        /// <summary>이 이메일로 지금 코드를 요청해도 되나. 아니면 기다릴 초(1 이상).</summary>
        public int RecoveryWaitSeconds(string email, long nowTicks)
        {
            if (email == null || !_recovery.TryGetValue(EmailKey(email), out var st)) return 0;
            long until = st.LastRequestTicks + TimeSpan.FromSeconds(RecoveryCooldownSeconds).Ticks;
            if (nowTicks < until) return (int)Math.Ceiling((until - nowTicks) / (double)TimeSpan.TicksPerSecond);
            long hourAgo = nowTicks - TimeSpan.FromHours(1).Ticks;
            st.HourRequests.RemoveAll(t => t < hourAgo);
            if (st.HourRequests.Count >= RecoveryPerHour)
            {
                long next = st.HourRequests[0] + TimeSpan.FromHours(1).Ticks;
                return Math.Max(1, (int)Math.Ceiling((next - nowTicks) / (double)TimeSpan.TicksPerSecond));
            }
            return 0;
        }

        /// <summary>요청을 센다(계정이 없어도 센다). 계정이 있으면 새 코드(6자리)를 만들어 돌려주고, 없으면 null.</summary>
        public string BeginRecovery(string email, long nowTicks)
        {
            if (email == null) return null;
            string key = EmailKey(email);
            if (!_recovery.TryGetValue(key, out var st)) _recovery[key] = st = new RecoveryState();
            st.LastRequestTicks = nowTicks;
            st.HourRequests.Add(nowTicks);
            var rec = FindByEmail(email);
            if (rec == null || string.IsNullOrEmpty(rec.PwHash)) { st.CodeHash = null; return null; }
            var b = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            string code = (BitConverter.ToUInt32(b, 0) % 1000000u).ToString("D6");
            st.CodeHash = RecoveryHash(key, code);
            st.ExpiresTicks = nowTicks + TimeSpan.FromMinutes(RecoveryCodeMinutes).Ticks;
            st.Attempts = 0;
            return code;
        }

        /// <summary>코드를 확인한다. 맞으면 true(코드는 한 번만 쓰인다). 틀리면 횟수를 하나 세고, 5번 틀리면 그 코드는 버린다.</summary>
        public bool ConfirmRecoveryCode(string email, string code, long nowTicks)
        {
            if (email == null || code == null || !_recovery.TryGetValue(EmailKey(email), out var st) || st.CodeHash == null) return false;
            if (nowTicks > st.ExpiresTicks) { st.CodeHash = null; return false; }
            if (++st.Attempts > RecoveryMaxAttempts) { st.CodeHash = null; return false; }
            if (!FixedEquals(RecoveryHash(EmailKey(email), code.Trim()), st.CodeHash)) { if (st.Attempts >= RecoveryMaxAttempts) st.CodeHash = null; return false; }
            st.CodeHash = null;
            return true;
        }

        /// <summary>새 비밀번호를 저장하고 그 계정의 로그인 표를 모두 지운다(다른 기기도 다시 로그인).</summary>
        public bool SetPassword(AccountRecord rec, string pwSalt, string pwHash)
        {
            if (rec == null || Peek(rec.Id) != rec || string.IsNullOrEmpty(pwHash)) return false;
            rec.PwSalt = pwSalt;
            rec.PwHash = pwHash;
            rec.PwIter = PasswordIterations;
            rec.Sessions?.Clear();
            Touch(rec.Id);
            Save();
            LoginSucceeded(rec.Email);
            return true;
        }
    }
}
