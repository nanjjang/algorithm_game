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

        public static TcpPipe Connect(IContentDb db)
        {
            var hosts = Hosts(db, out int port, out int timeout);
            foreach (var h in hosts)
            {
                string host = h;
                int p = port;
                int colon = h.LastIndexOf(':');
                if (colon > 0 && h.IndexOf(':') == colon && int.TryParse(h.Substring(colon + 1), out int pp)) { host = h.Substring(0, colon); p = pp; }
                var pipe = TcpPipe.Connect(host, p, timeout);
                if (pipe != null) { LastHost = host + ":" + p; return pipe; }
            }
            return null;
        }

        /// <summary>마지막으로 붙은 주소(화면 안내용).</summary>
        public static string LastHost;
    }
}
