using System;
using System.Collections.Generic;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 판 실행기의 순수 진행부(IGameSession 구현). MonoBehaviour가 아니다 — GameRunner가 프레임마다 Frame(dt)을 부르고,
    /// EditMode 테스트는 스텁 모듈 세계로 직접 부른다. 00 문서 4절 Update 의사코드와 4.1절 그대로.
    ///
    /// 입력 붙이기 규칙(세계가 믿는 키 상태와 입력원 내부 상태를 어긋나지 않게):
    /// - 떼기(Detach) = ReleaseAll → 세계에 넣는다. 세계는 "전부 뗌"을 믿는다.
    /// - 붙이기(Attach) = ReleaseAll(버림) → Resync(swallowHeldActions: true) → 세계에 넣는다. 눌려 있는 상태 키(방향·자세)는 누름으로
    ///   들어가고, 눌려 있는 동작 키(J·K·Space·Pause)는 뗐다 다시 누를 때까지 삼킨다.
    /// - 문이 닫혀 있는 동안 Poll 한 것은 버린다. 문이 열리는 프레임에 붙이기를 한다.
    /// - 일시정지는 떼지 않는다(Poll도 안 한다). 풀릴 때 Resync(swallow) 한 번이면 "일시정지 중 뗀 키는 풀리는 틱의 뗌"이 된다.
    /// 창 초점을 잃은 동안은 문이 닫힌 것으로 본다(잃는 프레임에 떼기, 돌아온 프레임에 붙이기).
    /// </summary>
    public sealed class SessionCore : IGameSession, IOnlineSession
    {
        readonly GameContext _ctx;
        readonly Func<SimWorld> _worldFactory;
        readonly IWorldViewHost _host;
        readonly TickAccumulator _acc = new TickAccumulator();
        readonly List<InputEvent> _inBuf = new List<InputEvent>();
        readonly List<SimEvent> _evBuf = new List<SimEvent>();

        SimWorld _main;
        SimWorld _sub;
        bool _paused;
        float _pausedSeconds;
        IInputSource _override;
        RunSummary _lastSummary;
        bool _runEndedRaised;
        bool _gateOpen;
        bool _needAttach;
        bool _focused = true;
        IOnlineLink _online;
        readonly List<InputEvent> _netInputs = new List<InputEvent>();
        readonly List<RunCommand> _netCommands = new List<RunCommand>();

        /// <param name="ctx">Input·Screens를 읽는다(null 서비스도 된다).</param>
        /// <param name="worldFactory">새 세계를 만든다. null이면 new SimWorld(ctx.Content, SimModuleSet.CreateDefault()).</param>
        /// <param name="host">표시 호스트(null이면 그리지 않는다).</param>
        public SessionCore(GameContext ctx, Func<SimWorld> worldFactory = null, IWorldViewHost host = null)
        {
            _ctx = ctx ?? new GameContext();
            _worldFactory = worldFactory ?? (() => new SimWorld(_ctx.Content, SimModuleSet.CreateDefault()));
            _host = host;
        }

        // ------------------------------------------------------------ IGameSession

        public SimWorld World => _sub ?? _main;
        public SimWorld MainWorld => _main;
        public bool HasRun => _main != null;
        public bool HasSubRun => _sub != null;
        public float PausedSeconds => _pausedSeconds;
        public ScreenId InputScreen { get; set; }
        /// <summary>입력원의 이벤트를 넣을 플레이어 슬롯(솔로 = 0). 세계의 플레이어 배열은 그대로 돈다.</summary>
        public int LocalSlot { get; set; }
        public RunSummary LastSummary => _lastSummary;

        public event Action<SimWorld> RunStarted;
        public event Action<SimWorld, SimEvent> SimEventRaised;
        public event Action<RunSummary> RunEnded;
        public event Action PauseRequested;

        /// <summary>지금 입력원(덮어쓰기가 있으면 그것, 없으면 ctx.Input. 둘 다 없으면 null).</summary>
        public IInputSource Input => _override ?? _ctx.Input;

        /// <summary>마지막 프레임에 전투 입력의 문이 열려 있었나(테스트·디버그용).</summary>
        public bool GateOpen => _gateOpen;
        public TickAccumulator Accumulator => _acc;

        /// <summary>일시정지의 유일한 진입점(00 문서 4.1절, T-F19).</summary>
        public bool Paused
        {
            get => _paused;
            set
            {
                if (value == _paused) return;
                if (value && _online != null) return; // 온라인 판은 멈추지 않는다(서버가 계속 돈다)
                _paused = value;
                var w = World;
                if (w == null) return;
                if (value)
                {
                    // 활동 중인 플레이어마다 커맨드 판정기 초기화 한 번.
                    var players = w.Players;
                    for (int i = 0; i < players.Length; i++)
                        if (players[i] != null && players[i].Active)
                            w.Mods.Arts.OnCommandReset(w, players[i], CommandResetReason.Pause);
                }
                else
                {
                    // 일시정지 중 뗀 키는 풀리는 틱의 뗌으로, 새로 누른 동작 키는 삼킨다.
                    // 일시정지 전에 문이 닫혀 있었으면(세계는 이미 "전부 뗌") 문이 열릴 때 붙이기가 맞춘다.
                    var input = Input;
                    if (input == null || !_gateOpen || _needAttach) return;
                    _inBuf.Clear();
                    input.Resync(SimTimeU(w), _inBuf, true);
                    Feed(w, _inBuf, false);
                }
            }
        }

        public void StartRun(RunConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (_main != null) EndRun();
            ResetRunState();
            var w = _worldFactory();
            _main = w;
            w.StartRun(cfg);
            EnterRun(w);
        }

        public bool IsOnline => _online != null;

        public void StartOnline(IOnlineLink link)
        {
            if (link == null) throw new ArgumentNullException(nameof(link));
            var w = link.World;
            if (w == null) throw new InvalidOperationException("StartOnline needs link.World (the run has not started yet).");
            // 같은 방에서 다시 연 판이면 걷기만 하고 연결은 남긴다.
            if (_main != null) EndRunInternal(!ReferenceEquals(_online, link));
            ResetRunState();
            _online = link;
            _main = w;
            w.ViewSlot = link.Slot;
            LocalSlot = link.Slot;
            w.InputQueues[LocalSlot].Clear();
            w.Commands.Clear();
            EnterRun(w);
        }

        void ResetRunState()
        {
            _paused = false;
            _pausedSeconds = 0f;
            _lastSummary = null;
            _runEndedRaised = false;
            _acc.Reset();
        }

        void EnterRun(SimWorld w)
        {
            DiscardInput();
            _gateOpen = false;
            _needAttach = true;
            _host?.Build(w);
            RunStarted?.Invoke(w);
        }

        public void EndRun() => EndRunInternal(true);

        /// <summary>온라인 판만 걷고 연결은 남긴다(판이 끝나 같은 방으로 돌아갈 때). 오프라인 판이면 EndRun과 같다.</summary>
        public void DetachOnline() => EndRunInternal(false);

        void EndRunInternal(bool leaveLink)
        {
            if (_online != null)
            {
                var link = _online;
                _online = null;
                LocalSlot = 0;
                if (leaveLink) link.Leave();
            }
            if (_main == null && _sub == null) return;
            if (_host != null) _host.DisposeLayers();
            DiscardInput();
            _sub = null;
            _main = null;
            _paused = false;
            _gateOpen = false;
            _needAttach = false;
            _acc.Reset();
        }

        public void PushSubRun(RunConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (_main == null) throw new InvalidOperationException("PushSubRun needs a main run (use StartRun).");
            if (_online != null) return; // 온라인 판에는 보조 판(조립 시험)이 없다 — 본판이 서버에서 계속 돈다
            if (_sub != null) PopSubRunInternal(false);
            // 본판은 그대로 멈춘다: 눌린 키를 떼어 두고(돌아올 때 붙이기), 표시 층을 걷는다.
            // 문이 이미 닫혀 있었으면 본판은 이미 "전부 뗌"이라 입력원만 비운다.
            if (_gateOpen && !_needAttach) Detach(_main);
            else DiscardInput();
            if (_host != null) _host.DisposeLayers();
            var w = _worldFactory();
            _sub = w;
            w.StartRun(cfg);
            _acc.Reset();
            _gateOpen = false;
            _needAttach = true;
            if (_host != null) _host.Build(w);
        }

        public void PopSubRun() => PopSubRunInternal(true);

        void PopSubRunInternal(bool rebuildMain)
        {
            if (_sub == null) return;
            DiscardInput();
            if (_host != null) _host.DisposeLayers();
            _sub = null;
            _acc.Reset();
            _needAttach = true;
            // 본판 세계는 보조 판을 열 때 떼어 둔 상태("전부 뗌")다. 다음 프레임에 문이 열려 있으면 붙이기
            // (ReleaseAll 버림 → Resync(swallowHeldActions: true))로 다시 맞춘다.
            _gateOpen = false;
            if (!rebuildMain || _main == null) return;
            if (_host != null) _host.Build(_main);
        }

        public void SetInputOverride(IInputSource source)
        {
            if (ReferenceEquals(source, _override)) return;
            var w = World;
            if (w != null && _gateOpen && !_needAttach) Detach(w);
            else DiscardInput();
            _override = source;
            _needAttach = true;
        }

        // ------------------------------------------------------------ 프레임

        /// <summary>창 초점(OnApplicationFocus). 잃은 동안 문은 닫힌 것으로 본다.</summary>
        public void SetFocused(bool focused) { _focused = focused; }

        /// <summary>지금 문이 열려 있어야 하나: InputScreen == None, 화면 관리자 없음, 또는 맨 위가 InputScreen(그리고 창 초점이 있음).</summary>
        public bool ComputeGate()
        {
            if (!_focused) return false;
            var screens = _ctx.Screens;
            return InputScreen == ScreenId.None || screens == null || screens.Top == InputScreen;
        }

        /// <summary>틱 t에 속하는 지금 입력 시각(u) = t × 256 + 남은 누적 시간.</summary>
        long SimTimeU(SimWorld w) => SimConst.TickStartU(w.Tick) + _acc.OffsetU;

        /// <summary>한 프레임: 00 문서 4절 의사코드.</summary>
        public void Frame(float dt)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            var w = World;
            if (w == null)
            {
                if (_host != null) _host.Render(null, 0f, dt);
                return;
            }
            if (_paused)
            {
                _pausedSeconds += dt;
                if (_host != null) _host.Render(w, _acc.Alpha, 0f);
                return;
            }

            // 전투 입력의 문
            bool open = ComputeGate();
            var input = Input;
            if (input != null)
            {
                if (!open && _gateOpen) Detach(w);
                else if (open && (!_gateOpen || _needAttach)) Attach(w);
                _gateOpen = open;
                if (!open) _needAttach = true;

                _inBuf.Clear();
                input.Poll(SimTimeU(w), _inBuf);
                if (open) Feed(w, _inBuf, true);
            }
            else _gateOpen = open;

            // 고정 틱
            int steps = _acc.Advance(dt);
            if (_online != null)
            {
                for (int s = 0; s < steps; s++)
                {
                    OnlineStep(w);
                    if (World != w) break;
                }
                var cur = World;
                if (_host != null) _host.Render(cur, cur != null && _online != null ? _online.InterpAlpha : 0f, dt);
                return;
            }
            for (int s = 0; s < steps; s++)
            {
                w.Step();
                Dispatch(w);
                // 이벤트 처리 중 화면이 판을 바꿨거나(EndRun, 보조 판) 일시정지했으면 이 세계의 남은 스텝은 돌리지 않는다.
                if (World != w || _paused) break;
            }

            var now = World;
            if (_host != null) _host.Render(now, now != null ? _acc.Alpha : 0f, dt);
        }

        /// <summary>온라인 한 틱: 꼭두각시에 쌓인 내 입력·화면 명령을 서버로 옮기고, 연결을 한 틱 돌리고, 받은 이벤트를 넘긴다.
        /// 판 끝(RunEnded)의 요약은 만들지 않는다 — 온라인 판은 오프라인 프로필에 정산하지 않는다(4단계에서 서버 프로필로).</summary>
        void OnlineStep(SimWorld w)
        {
            var link = _online;
            _netInputs.Clear();
            _netCommands.Clear();
            var q = w.InputQueues[LocalSlot];
            _netInputs.AddRange(q);
            q.Clear();
            _netCommands.AddRange(w.Commands);
            w.Commands.Clear();
            link.Send(_netInputs, _netCommands);
            link.Step();
            _evBuf.Clear();
            link.DrainEvents(_evBuf);
            for (int i = 0; i < _evBuf.Count; i++)
            {
                var e = _evBuf[i];
                if (_host != null && World == w) _host.OnEvent(w, e);
                SimEventRaised?.Invoke(w, e);
                if (World != w) break;
            }
            _evBuf.Clear();
        }

        void Dispatch(SimWorld w)
        {
            _evBuf.Clear();
            w.DrainEvents(_evBuf);
            for (int i = 0; i < _evBuf.Count; i++)
            {
                var e = _evBuf[i];
                if (_host != null && World == w) _host.OnEvent(w, e);
                SimEventRaised?.Invoke(w, e);
            }
            _evBuf.Clear();
            if (w == _main && _sub == null && !_runEndedRaised && w.Run != null && w.Run.Phase == RunPhase.RunEnded)
            {
                _runEndedRaised = true;
                _lastSummary = w.Mods.Run.BuildSummary(w);
                RunEnded?.Invoke(_lastSummary);
            }
        }

        // ------------------------------------------------------------ 입력 붙이기

        /// <summary>입력원의 이벤트를 세계에 넣는다. Pause 누름은 넣지 않고(allowPauseRequest면) PauseRequested만 낸다.</summary>
        void Feed(SimWorld w, List<InputEvent> events, bool allowPauseRequest)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Key == LogicalKey.Pause)
                {
                    if (e.Down && allowPauseRequest) PauseRequested?.Invoke();
                    continue;
                }
                w.EnqueueInput(LocalSlot, e);
            }
            events.Clear();
        }

        void Detach(SimWorld w)
        {
            var input = Input;
            if (input == null || w == null) return;
            _inBuf.Clear();
            input.ReleaseAll(SimTimeU(w), _inBuf);
            Feed(w, _inBuf, false);
        }

        void Attach(SimWorld w)
        {
            var input = Input;
            _needAttach = false;
            if (input == null || w == null) return;
            long u = SimTimeU(w);
            _inBuf.Clear();
            input.ReleaseAll(u, _inBuf);
            _inBuf.Clear();
            input.Resync(u, _inBuf, true);
            Feed(w, _inBuf, false);
        }

        void DiscardInput()
        {
            var input = Input;
            if (input == null) return;
            _inBuf.Clear();
            input.ReleaseAll(0, _inBuf);
            _inBuf.Clear();
        }
    }
}
