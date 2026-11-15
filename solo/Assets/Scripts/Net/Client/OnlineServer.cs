using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Net
{
    /// <summary>온라인 서버 주소(DataSolo/Online.json). 앞에서부터 붙어 보고 처음 붙은 통로를 돌려준다.</summary>
    public static class OnlineServer
    {
        public const string ExtraName = "Online";
        public const int DefaultPort = 27870;

        /// <summary>테스트·개발용: 비어 있지 않으면 Online.json 대신 이 주소들("호스트" 또는 "호스트:포트")을 쓴다.</summary>
        public static List<string> Override;
        /// <summary>테스트·개발용: Override를 쓸 때의 TLS 지문(null·비어 있으면 평문 TCP).</summary>
        public static List<string> OverridePins;

        /// <summary>
        /// 서버 인증서 지문(SHA-256, Online.json "tls_pins"). 비어 있지 않으면 TLS로만 붙는다(지문이 맞는 서버만).
        /// 비밀번호·Google 로그인은 TLS 연결에서만 보낸다.
        /// </summary>
        public static List<string> Pins(IContentDb db)
        {
            var l = new List<string>();
            if (Override != null && Override.Count > 0) { if (OverridePins != null) l.AddRange(OverridePins); return l; }
            var root = db != null ? db.SoloExtra(ExtraName) : null;
            if (root != null && root["tls_pins"] is Newtonsoft.Json.Linq.JArray a) foreach (var h in a) { var v = (string)h; if (!string.IsNullOrEmpty(v)) l.Add(v); }
            return l;
        }

        public static List<string> Hosts(IContentDb db, out int port, out int timeoutMs)
        {
            port = DefaultPort;
            timeoutMs = 1500;
            var l = new List<string>();
            if (Override != null && Override.Count > 0) { l.AddRange(Override); return l; }
            var root = db != null ? db.SoloExtra(ExtraName) : null;
            if (root != null)
            {
                if (root["server_port"] != null) port = (int)root["server_port"];
                if (root["connect_timeout_ms"] != null) timeoutMs = (int)root["connect_timeout_ms"];
                if (root["server_hosts"] is Newtonsoft.Json.Linq.JArray a) foreach (var h in a) l.Add((string)h);
            }
            if (l.Count == 0) l.Add("127.0.0.1");
            return l;
        }

        public static INetPipe Connect(IContentDb db)
        {
            var hosts = Hosts(db, out int port, out int timeout);
            var pins = Pins(db);
            foreach (var h in hosts)
            {
                string host = h;
                int p = port;
                int colon = h.LastIndexOf(':');
                if (colon > 0 && h.IndexOf(':') == colon && int.TryParse(h.Substring(colon + 1), out int pp)) { host = h.Substring(0, colon); p = pp; }
                INetPipe pipe = pins.Count > 0 ? (INetPipe)TlsPipe.Connect(host, p, pins, timeout) : TcpPipe.Connect(host, p, timeout);
                if (pipe != null) { LastHost = host + ":" + p; return pipe; }
            }
            return null;
        }

        /// <summary>붙고, 이 기기 계정이 있으면 맨 앞에 Login을 보낸다(없으면 손님으로).</summary>
        public static INetPipe ConnectAuthed(IContentDb db)
        {
            var pipe = Connect(db);
            if (pipe == null) return null;
            OnlineAccount.Load()?.SendLogin(pipe);
            return pipe;
        }

        /// <summary>마지막으로 붙은 주소(화면 안내용).</summary>
        public static string LastHost;
    }
}
