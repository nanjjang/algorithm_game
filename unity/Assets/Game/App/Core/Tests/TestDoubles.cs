#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.Client.Run;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Unity.Input;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.App.Core.Tests
{
    internal sealed class FakeClock : IMonotonicClock
    {
        public double NowSeconds { get; set; }
    }

    internal sealed class FakeInput : IInputSource
    {
        public readonly List<RawKeyEvent> Pending = new List<RawKeyEvent>();
        public KeyMask Current;
        public int DrainCalls;

        public KeyMask CurrentKeys => Current;

        public int Drain(Span<RawKeyEvent> buffer)
        {
            DrainCalls++;
            int n = Math.Min(buffer.Length, Pending.Count);
            for (int i = 0; i < n; i++)
            {
                buffer[i] = Pending[i];
            }
            Pending.RemoveRange(0, n);
            return n;
        }

        public void SetBindings(KeyBindings bindings)
        {
        }

        public void SetDuelPolling(bool on)
        {
        }
    }

    // 실제 SimClock 으로 틱을 세는 가짜 세션. 부른 순서를 Log 에 남긴다
    internal sealed class FakeSession : ITickSession
    {
        public readonly List<string> Log;
        public readonly SimClock Clock;
        public readonly List<RawKeyEvent> Received = new List<RawKeyEvent>();
        public bool LogTicks = true;
        public uint ThrowAtTick = uint.MaxValue;

        private readonly RawKeyEvent[] _buffer = new RawKeyEvent[32];
        private readonly FakeSimView _view = new FakeSimView();
        private SimEvent[] _events = new SimEvent[8];
        private int _eventCount;

        public FakeSession(List<string> log, double startSeconds)
        {
            Log = log;
            Clock = new SimClock(startSeconds);
        }

        public uint Tick => Clock.Tick;

        public ReadOnlySpan<SimEvent> Events => new ReadOnlySpan<SimEvent>(_events, 0, _eventCount);

        public ISimView View => _view;

        public void OnInput(IInputSource source, double nowSeconds)
        {
            Log.Add("input");
            _eventCount = 0;
            int n;
            do
            {
                n = source.Drain(_buffer);
                for (int i = 0; i < n; i++)
                {
                    Received.Add(_buffer[i]);
                }
            }
            while (n == _buffer.Length);
        }

        public bool NextTickDue(double nowSeconds) => Clock.NextTickDue(nowSeconds);

        public void Step(uint tick)
        {
            if (tick != Clock.Tick)
            {
                throw new InvalidOperationException("Step: tick differs from the clock tick");
            }
            if (tick == ThrowAtTick)
            {
                throw new InvalidOperationException("fake session fault");
            }
            if (LogTicks)
            {
                Log.Add("step:" + tick);
            }
            if (_eventCount == _events.Length)
            {
                Array.Resize(ref _events, _events.Length * 2);
            }
            _events[_eventCount++] = new SimEvent { Tick = tick };
        }

        public void Advance()
        {
            if (LogTicks)
            {
                Log.Add("advance");
            }
            Clock.Advance();
        }

        public double Alpha(double nowSeconds)
        {
            Log.Add("alpha");
            return Clock.Alpha(nowSeconds);
        }
    }

    internal sealed class RecordingPresenter : IFramePresenter
    {
        private readonly List<string> _log;
        private readonly string _name;

        public RecordingPresenter(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public ISimView? LastView;
        public double LastAlpha = -1;

        public void OnEvents(ReadOnlySpan<SimEvent> events, ISimView view)
        {
            string ticks = string.Empty;
            for (int i = 0; i < events.Length; i++)
            {
                ticks += (i == 0 ? string.Empty : ",") + events[i].Tick;
            }
            _log.Add("events:" + _name + ":" + ticks);
            LastView = view;
        }

        public void Render(ISimView view, double alpha)
        {
            _log.Add("render:" + _name);
            LastView = view;
            LastAlpha = alpha;
        }
    }

    internal sealed class FakeSimView : ISimView
    {
        public uint Tick => 0;
        public int PlayerCount => 0;
        public PlayerView Player(PlayerSlot slot) => default;
        public ReadOnlySpan<EnemyView> Enemies => default;
        public ReadOnlySpan<ProjectileView> Projectiles => default;
        public ReadOnlySpan<TelegraphView> Telegraphs => default;
        public ReadOnlySpan<HazardView> Hazards => default;
        public WaveView Wave => default;
        public CampView Camp => default;
        public DuelView Duel => default;
    }

    internal sealed class FakePausable : IPausableSession
    {
        public bool Paused;
        public int ResumeRequests;

        public bool IsPaused => Paused;

        public void RequestResume() => ResumeRequests++;
    }

    internal sealed class FakeSceneLoader : ISceneLoader
    {
        public readonly List<string> Loaded = new List<string>();

        public void Load(string sceneName) => Loaded.Add(sceneName);
    }
}
