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
        TcpPipeListener _listener;
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
            _listener = new TcpPipeListener(port, bindAll: true);
            // 계정·서버 프로필: ~/WizardCoopServer/accounts.json (-data 폴더로 바꿀 수 있다).
            string dataDir = ArgValue("-data") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "WizardCoopServer");
            var accounts = new AccountStore(System.IO.Path.Combine(dataDir, "accounts.json"));
            Debug.Log("[coop-server] accounts " + accounts.Count + " (" + dataDir + ")");
            _rooms = new RoomServer(_content, null, 0, accounts) { Log = m => Debug.Log(m) };
            _last = Time.realtimeSinceStartupAsDouble;
            Debug.Log("[coop-server] listening on port " + _listener.Port + " (protocol " + NetProtocol.Version + ")");
        }


        void Update()
        {
            if (_rooms == null) return;
            TcpPipe pipe;
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

        void OnDestroy() { _listener?.Stop(); }
    }
}
