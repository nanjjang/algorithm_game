using System;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 판 실행기(IGameSession). 진행 규칙은 전부 SessionCore(순수)에 있고, 이 MonoBehaviour는 프레임 시간(Time.unscaledDeltaTime)과
    /// 창 초점을 넘기기만 한다. 00 문서 4절·4.1절. Time.timeScale·Time.fixedDeltaTime은 쓰지 않는다(누적기 방식).
    /// 카메라·HUD 캔버스·풀은 WorldViewHost가 갖고, 이 객체가 부서질 때 같이 걷는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRunner : MonoBehaviour, IGameSession, IOnlineSession
    {
        SessionCore _core;
        WorldViewHost _host;

        public SessionCore Core => _core;
        public WorldViewHost Host => _host;

        /// <summary>GameObject에 붙인 뒤 한 번 부른다(AppFactory.CreateSession).</summary>
        public void Init(GameContext ctx)
        {
            if (_core != null) return;
            _host = new WorldViewHost(ctx);
            _core = new SessionCore(ctx, () => new SimWorld(ctx != null ? ctx.Content : null, SimModuleSet.CreateDefault()), _host);
        }

        void Update()
        {
            if (_core != null) _core.Frame(Time.unscaledDeltaTime);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (_core != null) _core.SetFocused(hasFocus);
        }

        void OnDestroy()
        {
            if (_core != null) _core.EndRun();
            if (_host != null) _host.Destroy();
            _core = null;
            _host = null;
        }

        // ------------------------------------------------------------ IGameSession (SessionCore로 넘긴다)

        SessionCore C
        {
            get
            {
                if (_core == null) throw new InvalidOperationException("GameRunner.Init was not called.");
                return _core;
            }
        }

        public SimWorld World => _core != null ? _core.World : null;
        public SimWorld MainWorld => _core != null ? _core.MainWorld : null;
        public bool HasRun => _core != null && _core.HasRun;
        public bool HasSubRun => _core != null && _core.HasSubRun;
        public bool Paused { get => _core != null && _core.Paused; set => C.Paused = value; }
        public float PausedSeconds => _core != null ? _core.PausedSeconds : 0f;
        public ScreenId InputScreen { get => _core != null ? _core.InputScreen : ScreenId.None; set => C.InputScreen = value; }
        public RunSummary LastSummary => _core != null ? _core.LastSummary : null;

        public void StartRun(RunConfig cfg) => C.StartRun(cfg);
        public bool IsOnline => _core != null && _core.IsOnline;
        public void StartOnline(IOnlineLink link) => C.StartOnline(link);
        public void DetachOnline() { if (_core != null) _core.DetachOnline(); }
        public void EndRun() { if (_core != null) _core.EndRun(); }
        public void PushSubRun(RunConfig cfg) => C.PushSubRun(cfg);
        public void PopSubRun() { if (_core != null) _core.PopSubRun(); }
        public void SetInputOverride(IInputSource source) => C.SetInputOverride(source);

        public event Action<SimWorld> RunStarted { add => C.RunStarted += value; remove { if (_core != null) _core.RunStarted -= value; } }
        public event Action<SimWorld, SimEvent> SimEventRaised { add => C.SimEventRaised += value; remove { if (_core != null) _core.SimEventRaised -= value; } }
        public event Action<RunSummary> RunEnded { add => C.RunEnded += value; remove { if (_core != null) _core.RunEnded -= value; } }
        public event Action PauseRequested { add => C.PauseRequested += value; remove { if (_core != null) _core.PauseRequested -= value; } }
    }
}
