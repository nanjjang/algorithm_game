using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Net
{
    /// <summary>비밀번호 재설정 코드를 보내는 쪽(서버). 테스트는 가짜를 넣는다.</summary>
    public interface IRecoveryMailer
    {
        /// <summary>false면 복구 메일이 꺼져 있다.</summary>
        bool Enabled { get; }
        /// <summary>블로킹(네트워크). 서버는 메인 스레드 밖에서 부른다. 성공이면 true, 실패면 error에 이유(비밀 값은 담지 않는다).</summary>
        bool Send(string toAddress, string subject, string textBody, out string error);
    }

    /// <summary>
    /// Resend(https://resend.com) HTTPS API로 보낸다. API 키는 서버 설정 파일(server.json)에만 둔다 — 게임 빌드·저장소에 넣지 않는다.
    /// 발신 주소의 도메인은 Resend에서 인증(SPF·DKIM)된 것이어야 한다.
    /// </summary>
    public sealed class ResendMailer : IRecoveryMailer
    {
        public const string Endpoint = "https://api.resend.com/emails";
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        readonly string _apiKey, _from;

        public ResendMailer(string apiKey, string from)
        {
            _apiKey = apiKey ?? string.Empty;
            _from = from ?? string.Empty;
        }

        public bool Enabled => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_from);

        public bool Send(string toAddress, string subject, string textBody, out string error)
        {
            error = null;
            if (!Enabled) { error = "off"; return false; }
            try
            {
                var body = new JObject
                {
                    ["from"] = _from,
                    ["to"] = new JArray(toAddress),
                    ["subject"] = subject,
                    ["text"] = textBody,
                };
                var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"),
                };
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
                req.Headers.TryAddWithoutValidation("User-Agent", "wizard-coop-server/1");
                using (var res = Http.SendAsync(req).GetAwaiter().GetResult())
                {
                    if (res.IsSuccessStatusCode) return true;
                    error = "http " + (int)res.StatusCode;
                    return false;
                }
            }
            catch (Exception e)
            {
                error = e.GetType().Name;
                return false;
            }
        }
    }
}
