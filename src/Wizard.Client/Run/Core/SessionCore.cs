using System;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Client.Run
{
    // RunSession·PracticeSession 이 같이 쓰는 몸통(ARCHITECTURE 6.1, CONTRACTS 3.1·6.5, T-13).
    //
    // 프레임마다(OnInput):  Drain → Quantize(SimClock 의 InputClock, D = 0) → TickInputQueue.Enqueue
    // 틱마다(Step):         TakeForTick → (명령 확인: SimWorld.CanApply) → InputRecorder.Append(순번) → SimWorld.Step
    // 순번은 InputRecorder 만 매긴다(CONTRACTS 3.1). 이 클래스는 순번을 만들지 않는다.
    //
    // 키를 세계에 넣지 않는 동안(창이 떠 있다, 일시정지, 판이 끝났다)에도 꺼낸 키는 양자화한다(양자화기의 "시각은 줄지 않는다" 바닥을 지키려고).
    // 그 프레임에 꺼낸 키에는 OnInput 을 부른 때의 상태가 적용된다. 창이 닫히는 프레임의 키도 창 몫이다.
    // 넣지 않던 기간이 끝나면 입력원의 논리 키 상태(CurrentKeys)와 세계 쪽 키 상태의 차이를 이벤트로 만든다(spec 01 3.6, CONTRACTS 3.1):
    //   - 창이 닫힘: 닫힌 프레임의 시각(now)에. 양자화기를 거치므로 이미 줄에 있는 키보다 앞서지 않는다.
    //   - 일시정지 풀림: 풀린 틱(세계의 Tick)의 오프셋 0에(spec 01 3.6). 일시정지가 시작될 때 줄에 남은 키(그 뒤 틱의 키)는 일시정지 중의
    //     조작으로 보고 버린다. 풀 때 차이로 다시 맞춘다.
    //
    // [임시] (OPEN-QUESTIONS)
    //   - 일시정지는 Pause 키의 누름이 세계에 들어간 틱 뒤에 시작한다(spec 01 3.6: 시작은 Pause 누름 이벤트로 기록). 풀기는 화면(일시정지 메뉴)이
    //     RequestResume 으로 청하고, 다음 OnInput 에서 풀린다. 창이 떠 있는 동안에는 키가 세계에 가지 않으므로 Pause 키로 멈추지 않는다.
    //   - 명령은 한 틱에 하나만 넣는다. CanApply 는 그 틱의 Step 전 상태로 보므로, 같은 틱의 둘째 명령은 첫째가 바꾼 상태를 보지 못한다.
    //     그래서 둘째부터는 다음 틱으로 넘기고 그 틱에서 다시 확인한다(무효 명령이 기록에 들어가면 재실행이 RecordCorrupt 다).
    //     CanApply 가 거짓인 명령은 기록하지 않고 버린다(RejectedCommandCount).
    //   - 이미 지나간 틱으로 양자화된 키(입력원이 늦게 넘긴 키)는 다음 틱(세계의 Tick)의 오프셋 0으로 옮긴다. 버리면 키 상태가 어긋난다.
    //   - 한 틱의 아홉 번째 키부터는 TickInputQueue 가 버린다(spec 01 4.3). 늦게 온 키를 다음 틱의 오프셋 0 한 칸에 모으므로(위) 사람 손으로도
    //     한 틱에 9개가 들어갈 수 있다. 버린 틱이 있으면 다음 OnInput 에서(키를 넣는 동안이면) 그 프레임 시각에 차이를 낸다. 넣지 않는 동안이면
    //     창이 닫히거나 일시정지가 풀릴 때의 차이 맞추기가 같이 맞춘다. 차이는 늘 "세계가 지금 줄을 다 먹은 뒤의 상태"(QueuedMask)에서 낸다.
    //     QueuedMask 는 세계에 넣은 키(_fedMask)에 줄에 남은 키(_queued, TickInputQueue 와 같은 내용)를 넣은 순서대로 더해 매번 셈한다.
    //     버린 키는 줄에서 이미 빠졌으므로 셈에 들어가지 않는다(넣을 때 미리 더해 두면 버린 키가 남는다).
    //     넣은 순서로 더해도 되는 까닭: 양자화기의 시각은 줄지 않고, 늦은 키를 옮긴 자리(세계의 Tick, 0)는 줄에 남은 어느 키보다 앞서지 않으며,
    //     같은 키끼리는 KeyClass 가 같아 TakeForTick 의 안정 정렬이 넣은 순서를 지킨다. 그래서 키마다 적용 순서가 넣은 순서와 같다.
    internal sealed class SessionCore
    {
        private const int RawChunk = 32;
        private const int MsPerSecond = 1000;
        private const int InitialPendingCommands = 4;

        private static readonly PlayerSlot Slot0 = new PlayerSlot(0);

        private readonly SimWorld _world;
        private readonly InputRecorder _recorder;
        private readonly SimClock _clock;
        private readonly uint _maxTicks;
        private readonly InputQuantizer _quantizer = new InputQuantizer();
        private readonly SimEventBuffer _events = new SimEventBuffer();
        private TickInputQueue _queue = new TickInputQueue();

        private readonly RawKeyEvent[] _raw = new RawKeyEvent[RawChunk];
        private readonly QuantizedKey[] _quantized = new QuantizedKey[RawChunk];
        private readonly QuantizedKey[] _tickKeys = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
        private readonly RunCommandDraft[] _tickCommand = new RunCommandDraft[1];
        private readonly RunCommand[] _commandsOut = new RunCommand[1];
        private readonly PlayerInput[] _inputsOut = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
        private RunCommandDraft[] _pending = new RunCommandDraft[InitialPendingCommands];
        private int _pendingCount;

        private int _fedMask;                             // 세계에 넣은 키로 셈한 논리 키 상태(= 세계의 KeyState 의 눌림 비트)
        private QuantizedKey[] _queued = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer * 2];   // _queue 에 남은 키(넣은 순서)
        private int _queuedCount;
        private bool _dropPending;                        // 버린 키가 있었고 아직 차이를 맞추지 않았다
        private bool _windowRequested;
        private bool _windowApplied;
        private bool _resumeRequested;
        private bool _finished;

        private int _droppedOverflowBefore, _droppedLateBefore;   // 버린 줄의 셈(일시정지마다 줄을 새로 만든다)

        internal SessionCore(SimWorld world, InputRecorder recorder, double startSeconds, uint maxTicks)
        {
            _world = world;
            _recorder = recorder;
            _maxTicks = maxTicks;
            _clock = new SimClock(startSeconds, world.Tick);
            if (world.IsOver || world.Tick >= maxTicks)
            {
                _clock.Halt();
            }
        }

        public SimClock Clock => _clock;

        public ISimView View => _world.View;

        public ReadOnlySpan<SimEvent> Events => _events.Events;

        public uint Tick => _world.Tick;

        public bool IsOver => _world.IsOver;

        public bool IsPaused => _clock.IsPaused;

        public bool IsHalted => _clock.IsHalted;

        public bool IsWindowOpen => _windowRequested;

        public bool IsResumeRequested => _resumeRequested;

        public KeyMask WorldKeys => (KeyMask)_fedMask;

        public int RejectedCommandCount { get; private set; }

        public int WithheldKeyCount { get; private set; }

        public int RetimedLateKeyCount { get; private set; }

        // 버린 키 때문에 낸 차이 이벤트의 수(창·일시정지의 차이 맞추기는 세지 않는다)
        public int DropCorrectionKeyCount { get; private set; }

        public int DroppedOverflowKeyCount => _droppedOverflowBefore + _queue.DroppedOverflowCount;

        public int DroppedLateKeyCount => _droppedLateBefore + _queue.DroppedLateCount;

        public int PauseCount { get; private set; }

        public long PauseTotalMs { get; private set; }

        public int PendingCommandCount => _pendingCount;

        internal SimWorld World => _world;

        internal InputRecorder Recorder => _recorder;

        // 창(전리품, 야영 화면)이 떠 있는가. 다음 OnInput 부터 적용된다. 창이 떠 있어도 틱은 간다(T-13)
        public void SetWindowOpen(bool open) => _windowRequested = open;

        // 일시정지를 풀어 달라(일시정지 메뉴). 다음 OnInput 에서 풀린다. 멈춰 있지 않으면 아무 일도 없다
        public void RequestResume()
        {
            if (_clock.IsPaused && !_clock.IsHalted)
            {
                _resumeRequested = true;
            }
        }

        // 키로 적을 수 없는 판 안의 조작(CONTRACTS 3.1 RunCommand). 다음에 도는 틱에 확인하고 넣는다
        public void Submit(in RunCommandDraft command)
        {
            if (_clock.IsHalted)
            {
                RejectedCommandCount++;
                return;
            }
            if (_pendingCount == _pending.Length)
            {
                Array.Resize(ref _pending, _pending.Length * 2);
            }
            _pending[_pendingCount++] = command;
        }

        // 프레임마다 한 번, 틱 루프 전에(ARCHITECTURE 6.1)
        public void OnInput(IInputSource source, double nowSeconds)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            double now = _clock.Sample(nowSeconds);
            _events.Clear();

            bool withhold = _clock.IsHalted || _clock.IsPaused || _windowApplied || _windowRequested;
            InputClock clock = _clock.InputClock;
            while (true)
            {
                int n = source.Drain(_raw);
                if (n < 0 || n > _raw.Length)
                {
                    throw new InvalidOperationException("IInputSource.Drain returned a count outside the buffer");
                }
                if (n > 0)
                {
                    int m = _quantizer.Quantize(new ReadOnlySpan<RawKeyEvent>(_raw, 0, n), in clock, 0, _quantized);
                    if (withhold)
                    {
                        WithheldKeyCount += m;
                    }
                    else
                    {
                        Enqueue(new Span<QuantizedKey>(_quantized, 0, m));
                    }
                }
                if (n < _raw.Length)
                {
                    break;
                }
            }

            if (_clock.IsHalted)
            {
                _resumeRequested = false;
                _windowApplied = _windowRequested;
                return;
            }
            if (_clock.IsPaused)
            {
                if (_resumeRequested)
                {
                    ResumeNow(source.CurrentKeys, now);
                }
                return;                                    // 멈춰 있는 동안의 창 변화는 풀 때 본다
            }
            if (_windowApplied && !_windowRequested)
            {
                ReconcileAt(now, source.CurrentKeys);
            }
            else if (_dropPending && !_windowApplied && !_windowRequested)
            {
                DropCorrectionKeyCount += ReconcileAt(now, source.CurrentKeys);
            }
            _windowApplied = _windowRequested;
        }

        // 틱 하나. tick 은 세계의 Tick(= Clock.Tick)이어야 한다. 부른 뒤 Clock.Advance()
        public void Step(uint tick)
        {
            if (_clock.IsHalted)
            {
                throw new InvalidOperationException("Step after the session has ended");
            }
            if (_clock.IsPaused)
            {
                throw new InvalidOperationException("Step while paused");
            }
            if (tick != _world.Tick)
            {
                throw new InvalidOperationException("Step: tick differs from the world tick");
            }

            int droppedBefore = _queue.DroppedOverflowCount + _queue.DroppedLateCount;
            int keyCount = _queue.TakeForTick(tick, _tickKeys);
            ForgetQueuedUpTo(tick);
            if (_queue.DroppedOverflowCount + _queue.DroppedLateCount != droppedBefore)
            {
                _dropPending = true;
            }
            bool pausePressed = false;
            for (int i = 0; i < keyCount; i++)
            {
                QuantizedKey k = _tickKeys[i];
                if (ApplyToMask(ref _fedMask, k.Key, k.Down) && k.Down && k.Key == LogicalKey.Pause)
                {
                    pausePressed = true;
                }
            }

            int commandCount = TakeCommand(tick);
            _recorder.Append(tick, Slot0, new ReadOnlySpan<RunCommandDraft>(_tickCommand, 0, commandCount),
                             new ReadOnlySpan<QuantizedKey>(_tickKeys, 0, keyCount),
                             _commandsOut, out int fedCommands, _inputsOut, out int fedInputs);
            _world.Step(new ReadOnlySpan<PlayerInput>(_inputsOut, 0, fedInputs),
                        new ReadOnlySpan<RunCommand>(_commandsOut, 0, fedCommands), _events);

            if (_world.IsOver || _world.Tick >= _maxTicks)
            {
                _clock.Halt();
            }
            else if (pausePressed)
            {
                _clock.Pause();
            }
        }

        // ARCHITECTURE 6.1 의 프레임 하나 전부: 입력 → 밀린 틱 → (돌려주는 값) 표시 보간 비율
        public double RunFrame(IInputSource source, double nowSeconds)
        {
            OnInput(source, nowSeconds);
            while (_clock.NextTickDue(nowSeconds))
            {
                Step(_clock.Tick);
                _clock.Advance();
            }
            return _clock.Alpha(nowSeconds);
        }

        public ulong ComputeStateHash() => _world.ComputeStateHash();

        // 기록을 닫는다. 일시정지 중이면 지금까지 멈춘 시간을 PauseMark 로 남긴다. 이 뒤로는 틱이 돌지 않는다
        public ReadOnlyMemory<byte> Finish(double nowSeconds)
        {
            RequireOpen();
            double now = _clock.Sample(nowSeconds);
            if (_clock.IsPaused)
            {
                MarkPause(_clock.PausedSeconds(now));
            }
            _clock.Halt();
            _finished = true;
            return _recorder.Finish(_world.Tick, _world.ComputeStateHash());
        }

        public void RequireOpen()
        {
            if (_finished)
            {
                throw new InvalidOperationException("the session record is finished");
            }
        }

        private void Enqueue(Span<QuantizedKey> keys)
        {
            uint next = _world.Tick;
            for (int i = 0; i < keys.Length; i++)
            {
                QuantizedKey k = keys[i];
                if (k.Tick < next)
                {
                    k = new QuantizedKey(next, 0, k.Key, k.Down);
                    keys[i] = k;
                    RetimedLateKeyCount++;
                }
            }
            _queue.Enqueue(keys);
            if (_queuedCount + keys.Length > _queued.Length)
            {
                int size = _queued.Length;
                while (size < _queuedCount + keys.Length)
                {
                    size *= 2;
                }
                Array.Resize(ref _queued, size);
            }
            keys.CopyTo(_queued.AsSpan(_queuedCount));
            _queuedCount += keys.Length;
        }

        // TakeForTick(tick) 이 줄에서 뺀 것(유효 틱이 tick 이하: 꺼낸 키, 넘쳐 버린 키, 늦어 버린 키)을 _queued 에서도 뺀다
        private void ForgetQueuedUpTo(uint tick)
        {
            int kept = 0;
            for (int i = 0; i < _queuedCount; i++)
            {
                if (_queued[i].Tick > tick)
                {
                    _queued[kept++] = _queued[i];
                }
            }
            _queuedCount = kept;
        }

        // 세계가 지금 줄에 남은 키를 다 먹은 뒤의 논리 키 상태(머리 주석의 순서 논증)
        private int QueuedMask()
        {
            int mask = _fedMask;
            for (int i = 0; i < _queuedCount; i++)
            {
                ApplyToMask(ref mask, _queued[i].Key, _queued[i].Down);
            }
            return mask;
        }

        // now 에 "입력원의 논리 키 상태 − 세계가 줄을 다 먹은 뒤의 상태"를 이벤트로 낸다. 키 번호 오름차순. 돌려주는 값 = 낸 이벤트 수
        private int ReconcileAt(double now, KeyMask current)
        {
            _dropPending = false;
            int changed = ((int)current ^ QueuedMask()) & AllKeysMask;
            if (changed == 0)
            {
                return 0;
            }
            int n = 0;
            for (int k = 0; k < KeyState.KeyCount; k++)
            {
                if ((changed & (1 << k)) != 0)
                {
                    _raw[n++] = new RawKeyEvent((LogicalKey)k, ((int)current & (1 << k)) != 0, now);
                }
            }
            InputClock clock = _clock.InputClock;
            int m = _quantizer.Quantize(new ReadOnlySpan<RawKeyEvent>(_raw, 0, n), in clock, 0, _quantized);
            Enqueue(new Span<QuantizedKey>(_quantized, 0, m));
            return m;
        }

        private void ResumeNow(KeyMask current, double now)
        {
            double paused = _clock.Resume(now);
            MarkPause(paused);
            _resumeRequested = false;

            // 일시정지 전에 줄에 남은 키(멈춘 틱 뒤의 키)는 일시정지 중의 조작이다. 버리고 차이로 맞춘다
            _droppedOverflowBefore += _queue.DroppedOverflowCount;
            _droppedLateBefore += _queue.DroppedLateCount;
            WithheldKeyCount += _queue.Count;
            _queue = new TickInputQueue();
            _queuedCount = 0;

            _windowApplied = _windowRequested;
            if (_windowRequested)
            {
                return;                                    // 창이 아직 떠 있다. 차이는 창이 닫힐 때 맞춘다
            }
            _dropPending = false;
            int changed = ((int)current ^ _fedMask) & AllKeysMask;   // 줄은 방금 비웠다
            if (changed == 0)
            {
                return;
            }
            uint tick = _world.Tick;
            int n = 0;
            for (int k = 0; k < KeyState.KeyCount; k++)
            {
                if ((changed & (1 << k)) != 0)
                {
                    _quantized[n++] = new QuantizedKey(tick, 0, (LogicalKey)k, ((int)current & (1 << k)) != 0);
                }
            }
            Enqueue(new Span<QuantizedKey>(_quantized, 0, n));
        }

        private void MarkPause(double pausedSeconds)
        {
            uint wallMs = ToWallMs(pausedSeconds);
            _recorder.MarkPause(_world.Tick, wallMs);
            PauseCount++;
            PauseTotalMs += wallMs;
        }

        // 맨 앞의 확인된 명령 하나. CanApply 가 거짓인 것은 버린다(기록하지 않는다)
        private int TakeCommand(uint tick)
        {
            while (_pendingCount > 0)
            {
                RunCommandDraft d = _pending[0];
                Array.Copy(_pending, 1, _pending, 0, _pendingCount - 1);
                _pendingCount--;
                var probe = new RunCommand(tick, 0, Slot0, d.Op, d.A, d.B);   // 순번은 Append 가 매긴다. 확인은 순번을 보지 않는다
                if (_world.CanApply(in probe))
                {
                    _tickCommand[0] = d;
                    return 1;
                }
                RejectedCommandCount++;
            }
            return 0;
        }

        private const int AllKeysMask = (1 << KeyState.KeyCount) - 1;

        // KeyState.Apply 와 같은 규칙: 눌린 키의 누름, 안 눌린 키의 뗌은 무시한다. 바뀌었으면 참
        private static bool ApplyToMask(ref int mask, LogicalKey key, bool down)
        {
            int bit = 1 << (int)key;
            bool isDown = (mask & bit) != 0;
            if (down == isDown)
            {
                return false;
            }
            mask = down ? mask | bit : mask & ~bit;
            return true;
        }

        private static uint ToWallMs(double seconds)
        {
            double ms = Math.Floor(seconds * MsPerSecond);
            if (!(ms > 0))
            {
                return 0;
            }
            return ms >= uint.MaxValue ? uint.MaxValue : (uint)ms;
        }
    }
}
