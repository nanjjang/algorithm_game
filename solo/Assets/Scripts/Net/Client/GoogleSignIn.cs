using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wizard.Solo.Net
{
    /// <summary>브라우저에서 받은 Google 인가 코드.</summary>
    public sealed class GoogleCode
    {
        public string Code, Verifier, RedirectUri;
        /// <summary>실패 이유(성공이면 null): "denied", "timeout", "state", "listen".</summary>
        public string Error;
    }

    /// <summary>
    /// Google 로그인(데스크톱 앱 OAuth, RFC 8252): 이 기기 루프백 주소(http://127.0.0.1:빈포트/)에서 기다리며 시스템 브라우저로
    /// Google 로그인 쪽을 연다. PKCE(S256)와 state로 코드를 가로채지 못하게 한다. 받은 코드는 게임 서버로 보내고, 서버가 Google과 바꾼다
    /// (클라이언트 비밀은 서버에만 있다).
    /// 쓰는 법: var s = GoogleSignIn.Begin(clientId, okHtml); Application.OpenURL(s.Url); 이후 s.Result(Task)를 기다린다.
    /// </summary>
    public sealed class GoogleSignIn : IDisposable
    {
        public const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

        HttpListener _http;
        readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        public string Url { get; private set; }
        public Task<GoogleCode> Result { get; private set; }

        static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static string Random(int bytes)
        {
            var b = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return B64Url(b);
        }

        public static string Challenge(string verifier)
        {
            using (var sha = SHA256.Create()) return B64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        }

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        /// <summary>
        /// 기다리기 시작한다. doneHtml = 코드를 받은 뒤 브라우저에 보여 줄 쪽(이름 표의 글로 만든 HTML).
        /// 실패하면 Result가 Error를 가진 채 바로 끝난다.
        /// </summary>
        public static GoogleSignIn Begin(string clientId, string doneHtml, int timeoutSeconds = 180)
        {
            var s = new GoogleSignIn();
            string verifier = Random(48), state = Random(16);
            string redirect;
            try
            {
                int port = FreePort();
                redirect = "http://127.0.0.1:" + port + "/";
                s._http = new HttpListener();
                s._http.Prefixes.Add(redirect);
                s._http.Start();
            }
            catch (Exception)
            {
                s.Result = Task.FromResult(new GoogleCode { Error = "listen" });
                return s;
            }
            s.Url = AuthEndpoint
                + "?client_id=" + Uri.EscapeDataString(clientId)
                + "&redirect_uri=" + Uri.EscapeDataString(redirect)
                + "&response_type=code"
                + "&scope=" + Uri.EscapeDataString("openid email profile")
                + "&code_challenge=" + Challenge(verifier)
                + "&code_challenge_method=S256"
                + "&state=" + state
                + "&prompt=select_account";
            var http = s._http;
            var token = s._cancel.Token;
            s.Result = Task.Run(async () =>
            {
                try
                {
                    var get = http.GetContextAsync();
                    var done = await Task.WhenAny(get, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), token)).ConfigureAwait(false);
                    if (done != get) return new GoogleCode { Error = "timeout" };
                    var ctx = get.Result;
                    var q = ctx.Request.QueryString;
                    var html = Encoding.UTF8.GetBytes(doneHtml ?? string.Empty);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = html.Length;
                    ctx.Response.OutputStream.Write(html, 0, html.Length);
                    ctx.Response.OutputStream.Close();
                    if (q["state"] != state) return new GoogleCode { Error = "state" };
                    if (!string.IsNullOrEmpty(q["error"]) || string.IsNullOrEmpty(q["code"])) return new GoogleCode { Error = "denied" };
                    return new GoogleCode { Code = q["code"], Verifier = verifier, RedirectUri = redirect };
                }
                catch (Exception) { return new GoogleCode { Error = "timeout" }; }
                finally { try { http.Stop(); } catch (Exception) { } }
            });
            return s;
        }

        public void Dispose()
        {
            try { _cancel.Cancel(); } catch (Exception) { }
            try { _http?.Stop(); } catch (Exception) { }
        }
    }
}
