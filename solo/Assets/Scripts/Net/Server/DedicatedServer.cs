using System;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 전용 협동 서버(11-online.md 3단계). 같은 solo 빌드를 화면 없이 띄운다:
    ///   "Wizard.app/Contents/MacOS/Wizard Solo" -batchmode -nographics -coopServer [-port 27870]
    /// 방 하나를 연다. 손님은 TCP로 붙고, 방장(가장 낮은 슬롯)이 StartRequest를 보내면 판이 시작된다.
    /// 판이 끝나고 손님이 모두 나가면(또는 판 전에 모두 나가면) 새 방을 연다. 60틱 고정(뒤처지면 한 프레임에 최대 5틱까지 따라잡는다).
    /// UI·프로필·소리는 만들지 않는다 — Bootstrap.AutoBoot가 -coopServer를 보면 이것만 세운다.
    /// </summary>
    public sealed class DedicatedServer : MonoBehaviour
    {
        public const string Flag = "-coopServer";
        public const string PortFlag = "-port";

        IContentDb _content;
        TcpPipeListener _listener;
        CoopHost _host;
        double _acc;
        double _last;
        int _rooms;

        public static bool RequestedByCommandLine()
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a == Flag) return true;
            return false;
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
            NewRoom();
            _last = Time.realtimeSinceStartupAsDouble;
            Debug.Log("[coop-server] listening on port " + _listener.Port + " (protocol " + NetProtocol.Version + ")");
        }

        void NewRoom()
        {
            _host = new CoopHost(_content);
            _rooms++;
            Debug.Log("[coop-server] room " + _rooms + " open");
        }

        void Update()
        {
            if (_host == null) return;
            TcpPipe pipe;
            while ((pipe = _listener.Poll()) != null) _host.Accept(pipe);

            double now = Time.realtimeSinceStartupAsDouble;
            _acc += now - _last;
            _last = now;
            const double dt = 1.0 / SimConst.TicksPerSecond;
            int steps = 0;
            while (_acc >= dt && steps < 5) { _host.Tick(); _acc -= dt; steps++; }
            if (_acc > dt * 5) _acc = 0; // 너무 밀리면 따라잡기를 버린다

            if (_host.Finished)
            {
                Debug.Log("[coop-server] room " + _rooms + " finished at tick " + _host.World.Tick + ", wave " + _host.World.Run.LastClearedWave);
                NewRoom();
            }
        }

        void OnDestroy() { _listener?.Stop(); }
    }
}
