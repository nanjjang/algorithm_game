using System;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 전용 협동 서버(11-online.md 3단계). 같은 solo 빌드를 화면 없이 띄운다:
    ///   "Wizard.app/Contents/MacOS/Wizard Solo" -batchmode -nographics -coopServer [-port 27870]
    /// 방 서버(RoomServer)를 연다: 방 여러 개(6자리 코드로 참가) + 광장 하나. 방장이 StartRequest를 보내면 그 방의 판이 시작된다.
    /// 끝난 방·빈 방은 지운다. 60틱 고정(뒤처지면 한 프레임에 최대 5틱까지 따라잡는다).
    /// UI·프로필·소리는 만들지 않는다 — Bootstrap.AutoBoot가 -coopServer를 보면 이것만 세운다.
    /// </summary>
    public sealed class DedicatedServer : MonoBehaviour
    {
        public const string Flag = "-coopServer";
        public const string PortFlag = "-port";

        IContentDb _content;
        IPipeListener _listener;
        AccountStore _accounts;
        RoomServer _rooms;
        double _acc;
        double _last;
        int _statusTicks;

        public static bool RequestedByCommandLine()
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a == Flag) return true;
            return false;
        }

        static string ArgValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == flag) return args[i + 1];
            return null;
        }

        public static int PortFromCommandLine(int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == PortFlag && int.TryParse(args[i + 1], out int p) && p > 0 && p < 65536) return p;
            return fallback;
        }

        public static DedicatedServer Launch(int port)
        {
            var go = new GameObject("DedicatedCoopServer");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<DedicatedServer>();
            s.Begin(port);
            return s;
        }

        void Begin(int port)
        {
            Application.targetFrameRate = SimConst.TicksPerSecond;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
            _content = ContentDb.LoadFromResources(msg => Debug.LogError(msg));
            // 설정: ~/WizardCoopServer/server.json (-data 폴더로 바꿀 수 있다). 13-coop-server-deploy.md 참고.
            string dataDir = ArgValue("-data") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "WizardCoopServer");
            var cfg = ServerConfig.Load(dataDir);
            _listener = OpenListener(cfg, dataDir, port);
            _accounts = new AccountStore(OpenDb(cfg, dataDir));
            _accounts.SaveFailed = e => Debug.LogError("[coop-server] DB save failed: " + e.Message);
            Debug.Log("[coop-server] accounts " + _accounts.Count + " (" + (string.IsNullOrEmpty(cfg.Db) ? "json " + dataDir : "postgres") + ")");
            _rooms = new RoomServer(_content, null, 0, _accounts) { Log = m => Debug.Log(m), AllowInsecureAuth = cfg.AllowInsecureAuth };
            if (!string.IsNullOrEmpty(cfg.GoogleClientId))
            {
                _rooms.Google = new GoogleOAuthServer(cfg.GoogleClientId, cfg.GoogleClientSecret);
                Debug.Log("[coop-server] google login on");
            }
            _last = Time.realtimeSinceStartupAsDouble;
            Debug.Log("[coop-server] listening on port " + _listener.Port + " (protocol " + NetProtocol.Version + ")");
        }


        static IPipeListener OpenListener(ServerConfig cfg, string dataDir, int port)
        {
            string pfx = string.IsNullOrEmpty(cfg.TlsPfx) ? null : System.IO.Path.IsPathRooted(cfg.TlsPfx) ? cfg.TlsPfx : System.IO.Path.Combine(dataDir, cfg.TlsPfx);
            if (pfx != null && System.IO.File.Exists(pfx))
            {
                var cert = TlsPipeListener.LoadCertificate(pfx, cfg.TlsPassword);
                Debug.Log("[coop-server] tls on, fingerprint sha256 " + TlsPipe.Fingerprint(cert));
                return new TlsPipeListener(port, cert, bindAll: true);
            }
            Debug.LogWarning("[coop-server] tls off (no " + (pfx ?? "tls_pfx") + ") — email/google login refused");
            return new TcpPipeListener(port, bindAll: true);
        }

        static IAccountDb OpenDb(ServerConfig cfg, string dataDir)
        {
            string json = System.IO.Path.Combine(dataDir, "accounts.json");
            if (string.IsNullOrEmpty(cfg.Db)) return new JsonAccountDb(json);
            var pg = new PostgresAccountDb(cfg.Db);
            // 처음 Postgres로 바꿀 때: 빈 DB면 예전 JSON 저장 파일을 통째로 옮기고 파일 이름을 바꿔 둔다.
            if (System.IO.File.Exists(json) && pg.IsEmpty())
            {
                var old = new JsonAccountDb(json).Load();
                pg.Import(old);
                System.IO.File.Move(json, json + ".imported-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                Debug.Log("[coop-server] imported " + old.Accounts.Count + " accounts from accounts.json");
            }
            return pg;
        }

        void Update()
        {
            if (_rooms == null) return;
            INetPipe pipe;
            while ((pipe = _listener.Poll()) != null) _rooms.Accept(pipe);

            double now = Time.realtimeSinceStartupAsDouble;
            _acc += now - _last;
            _last = now;
            const double dt = 1.0 / SimConst.TicksPerSecond;
            int steps = 0;
            while (_acc >= dt && steps < 5) { _rooms.Tick(); _acc -= dt; steps++; }
            if (_acc > dt * 5) _acc = 0; // 너무 밀리면 따라잡기를 버린다

            if (++_statusTicks >= 60 * 60)
            {
                _statusTicks = 0;
                Debug.Log("[coop-server] rooms " + _rooms.RoomCount + ", plaza " + _rooms.Plaza.Count);
            }
        }

        void OnDestroy()
        {
            _listener?.Stop();
            _accounts?.Save();
        }
    }

    /// <summary>
    /// 서버 설정(데이터 폴더의 server.json). 없으면 기본값 = JSON 저장, TLS 끔, Google 끔.
    ///   { "db": "Host=127.0.0.1;Database=wizard;Username=wizard;Password=…", "tls_pfx": "server.pfx", "tls_password": "",
    ///     "google_client_id": "", "google_client_secret": "", "allow_insecure_auth": false }
    /// 비밀(DB 비밀번호, Google 클라이언트 비밀, 인증서)은 이 폴더에만 둔다 — 저장소에 올리지 않는다.
    /// </summary>
    public sealed class ServerConfig
    {
        [Newtonsoft.Json.JsonProperty("db")] public string Db;
        [Newtonsoft.Json.JsonProperty("tls_pfx")] public string TlsPfx = "server.pfx";
        [Newtonsoft.Json.JsonProperty("tls_password")] public string TlsPassword = string.Empty;
        [Newtonsoft.Json.JsonProperty("google_client_id")] public string GoogleClientId;
        [Newtonsoft.Json.JsonProperty("google_client_secret")] public string GoogleClientSecret;
        [Newtonsoft.Json.JsonProperty("allow_insecure_auth")] public bool AllowInsecureAuth;

        public static ServerConfig Load(string dataDir)
        {
            string p = System.IO.Path.Combine(dataDir, "server.json");
            try
            {
                if (System.IO.File.Exists(p)) return Newtonsoft.Json.JsonConvert.DeserializeObject<ServerConfig>(System.IO.File.ReadAllText(p)) ?? new ServerConfig();
            }
            catch (Exception e) { Debug.LogError("[coop-server] bad server.json: " + e.Message); }
            return new ServerConfig();
        }
    }
}
