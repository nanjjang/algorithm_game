using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Net
{
    /// <summary>Google에서 확인된 사용자.</summary>
    public sealed class GoogleIdentity
    {
        public string Sub, Email, Name;
        public bool EmailVerified;
        /// <summary>실패 이유(성공이면 null).</summary>
        public string Error;
    }

    /// <summary>인가 코드 → 사용자(서버 쪽). 테스트는 가짜를 넣는다.</summary>
    public interface IGoogleVerifier
    {
        /// <summary>빈 문자열이면 Google 로그인이 꺼져 있다.</summary>
        string ClientId { get; }
        /// <summary>블로킹(네트워크). 서버는 메인 스레드 밖에서 부른다.</summary>
        GoogleIdentity Exchange(string code, string codeVerifier, string redirectUri);
    }

    /// <summary>
    /// Google OAuth 2.0(데스크톱 앱 유형, PKCE + 루프백 주소). 클라이언트가 브라우저로 받은 인가 코드를 서버가 Google 토큰 끝점에
    /// 직접(HTTPS) 바꿔 id_token을 받는다 — Google에서 바로 받은 토큰이라 서명 검사 대신 iss·aud·exp만 본다(Google 문서의 허용 범위).
    /// 클라이언트 비밀은 서버 설정 파일에만 둔다(게임 빌드에 넣지 않는다).
    /// </summary>
    public sealed class GoogleOAuthServer : IGoogleVerifier
    {
        public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

        readonly string _clientId, _clientSecret;
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public GoogleOAuthServer(string clientId, string clientSecret)
        {
            _clientId = clientId ?? string.Empty;
            _clientSecret = clientSecret ?? string.Empty;
        }

        public string ClientId => _clientId;

        public GoogleIdentity Exchange(string code, string codeVerifier, string redirectUri)
        {
            if (string.IsNullOrEmpty(_clientId)) return new GoogleIdentity { Error = "off" };
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(codeVerifier) || !IsLoopback(redirectUri)) return new GoogleIdentity { Error = "bad request" };
            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = codeVerifier,
                });
                var res = Http.PostAsync(TokenEndpoint, form).GetAwaiter().GetResult();
                string body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!res.IsSuccessStatusCode) return new GoogleIdentity { Error = "token " + (int)res.StatusCode };
                var idToken = (string)JObject.Parse(body)["id_token"];
                return ParseIdToken(idToken, _clientId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (Exception e) { return new GoogleIdentity { Error = e.GetType().Name }; }
        }

        /// <summary>redirect_uri는 이 기기 루프백(http://127.0.0.1:포트/…)만 받는다.</summary>
        public static bool IsLoopback(string uri)
            => uri != null && (uri.StartsWith("http://127.0.0.1:", StringComparison.Ordinal) || uri.StartsWith("http://[::1]:", StringComparison.Ordinal));

        static byte[] B64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }

        /// <summary>id_token(JWT)의 몸을 읽고 iss·aud·exp를 확인한다. 서명은 보지 않는다(토큰 끝점에서 직접 받은 것만 넘긴다).</summary>
        public static GoogleIdentity ParseIdToken(string jwt, string clientId, long nowUnix)
        {
            if (string.IsNullOrEmpty(jwt)) return new GoogleIdentity { Error = "no id_token" };
            var parts = jwt.Split('.');
            if (parts.Length < 2) return new GoogleIdentity { Error = "bad id_token" };
            JObject o;
            try { o = JObject.Parse(Encoding.UTF8.GetString(B64Url(parts[1]))); }
            catch (Exception) { return new GoogleIdentity { Error = "bad id_token" }; }
            string iss = (string)o["iss"];
            if (iss != "accounts.google.com" && iss != "https://accounts.google.com") return new GoogleIdentity { Error = "iss" };
            var aud = o["aud"];
            bool audOk = aud is JArray arr ? arr.Values<string>().Contains(clientId) : (string)aud == clientId;
            if (!audOk) return new GoogleIdentity { Error = "aud" };
            long exp = o["exp"] != null ? (long)o["exp"] : 0;
            if (exp + 300 < nowUnix) return new GoogleIdentity { Error = "exp" };
            string sub = (string)o["sub"];
            if (string.IsNullOrEmpty(sub)) return new GoogleIdentity { Error = "sub" };
            var ev = o["email_verified"];
            bool verified = ev != null && (ev.Type == JTokenType.Boolean ? (bool)ev : string.Equals((string)ev, "true", StringComparison.OrdinalIgnoreCase));
            return new GoogleIdentity { Sub = sub, Email = (string)o["email"], EmailVerified = verified, Name = (string)o["name"] ?? (string)o["given_name"] };
        }
    }
}
