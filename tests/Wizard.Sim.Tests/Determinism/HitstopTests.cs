using System;
using System.Collections.Generic;
using Wizard.Client;
using Wizard.Client.Run;
using Wizard.Content;
using Wizard.ReplayCli;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // spec 01 T-C15, D12 "히트스톱은 표시 계층의 일. 게임 시간은 멈추지 않는다", T-13 "틱을 멈추는 것은 일시정지뿐".
    // T-C14 와 같은 10분짜리 입력을 하드웨어 시각의 키 이벤트로 바꿔 판을 하는 길(SimClock → InputQuantizer → InputRecorder → SimWorld)로 넣는다.
    // 히트스톱 0.12초를 10번 넣는다: 표시 계층이 그 동안 프레임 루프를 붙든 가장 나쁜 꼴(그 0.12초 동안 프레임이 오지 않는다)이다.
    // 틱 수와 틱별 해시가 히트스톱 없이 돈 판과 같아야 한다. 무한 웨이브 판은 기록의 바이트까지 같고, 그 기록의 재실행 해시도 같다.
    public sealed class HitstopTests
    {
        private const double StartSeconds = 100.0;
        private const double HitstopSeconds = 0.12;
        private const int Hitstops = 10;
        private const double HitstopEverySeconds = 55.0;
        private const double FirstHitstopSeconds = 7.31;
        private const double TickSeconds = 1.0 / 60;
        private const double OffsetSeconds = TickSeconds / 256;
        private const double FramePhase = 0.5;               // 프레임은 틱 경계 사이(경계의 부동소수 반올림에 기대지 않는다)

        // 논리 키 상태가 바뀔 때만 이벤트를 내는 입력원(IInputSource). now 까지의 이벤트만 넘긴다
        private sealed class ScriptedSource : IInputSource
        {
            private readonly RawKeyEvent[] _events;
            private int _next;
            private int _mask;

            public ScriptedSource(RawKeyEvent[] events)
            {
                _events = events;
            }

            public double Now { get; set; }

            public KeyMask CurrentKeys => (KeyMask)_mask;

            public int Drain(Span<RawKeyEvent> buffer)
            {
                int n = 0;
                while (n < buffer.Length && _next < _events.Length && _events[_next].TimeSeconds <= Now)
                {
                    RawKeyEvent e = _events[_next++];
                    _mask = e.Down ? _mask | (1 << (int)e.Key) : _mask & ~(1 << (int)e.Key);
                    buffer[n++] = e;
                }
                return n;
            }

            public void SetBindings(KeyBindings bindings)
            {
            }

            public void SetDuelPolling(bool on)
            {
            }
        }

        // 봇 입력(T-C14 와 같은 생성기)을 하드웨어 시각의 논리 키 변화로. 틱 안의 키는 오프셋 순, 상태를 바꾸지 않는 이벤트는 입력원이 내지 않으므로 뺀다
        private static RawKeyEvent[] Script(ulong botSeed, uint ticks)
        {
            var profile = new BotProfile { RedundantPermille = 0, BurstTickPermille = 0, CommandTickPermille = 0, Keys = BotProfile.PlayKeys };
            var bot = new BotScript(botSeed, profile);
            var keys = new QuantizedKey[BotScript.MaxKeysOut];
            var list = new List<RawKeyEvent>();
            int mask = 0;
            for (uint t = 0; t < ticks; t++)
            {
                int n = bot.Keys(t, 0, keys);
                var tick = new QuantizedKey[n];
                Array.Copy(keys, tick, n);
                StableByOffset(tick);
                foreach (QuantizedKey k in tick)
                {
                    int bit = 1 << (int)k.Key;
                    if (((mask & bit) != 0) == k.Down)
                    {
                        continue;
                    }
                    mask ^= bit;
                    list.Add(new RawKeyEvent(k.Key, k.Down, StartSeconds + (t * 256.0 + k.Offset + FramePhase) * OffsetSeconds));
                }
            }
            return list.ToArray();
        }

        // 오프셋 순의 안정 정렬(같은 오프셋은 생성 순서. 삽입 정렬)
        private static void StableByOffset(QuantizedKey[] keys)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                QuantizedKey x = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j].Offset > x.Offset)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }
                keys[j + 1] = x;
            }
        }

        private static bool InHitstop(double now, bool withHitstops)
        {
            if (!withHitstops)
            {
                return false;
            }
            for (int i = 0; i < Hitstops; i++)
            {
                double h = StartSeconds + FirstHitstopSeconds + i * HitstopEverySeconds;
                if (now >= h && now < h + HitstopSeconds)
                {
                    return true;
                }
            }
            return false;
        }

        // 프레임 루프(FixedTickDriver 의 꼴): OnInput → 밀린 틱. 틱마다 해시
        private static List<ulong> Drive(ScriptedSource source, uint ticks, bool withHitstops, Action<IInputSource, double> onInput,
                                         Func<SimClockView> clock, Action<uint> step, Func<ulong> hash, out int skippedFrames)
        {
            var hashes = new List<ulong> { hash() };
            skippedFrames = 0;
            for (uint frame = 1; frame <= ticks; frame++)
            {
                double now = StartSeconds + (frame + FramePhase) * TickSeconds;   // 프레임 n 뒤에는 틱 n 개가 돌았다
                if (InHitstop(now, withHitstops))
                {
                    skippedFrames++;
                    continue;                                   // 표시 계층이 붙든 동안 프레임이 오지 않는다
                }
                source.Now = now;
                onInput(source, now);
                while (clock().Due(now))
                {
                    step(clock().Tick);
                    hashes.Add(hash());
                    clock().Advance();
                }
            }
            return hashes;
        }

        private readonly struct SimClockView
        {
            private readonly SimClock _clock;

            public SimClockView(SimClock clock)
            {
                _clock = clock;
            }

            public uint Tick => _clock.Tick;

            public bool Due(double now) => _clock.NextTickDue(now);

            public void Advance() => _clock.Advance();
        }

        private static List<ulong> Practice(RawKeyEvent[] script, bool withHitstops, out int skipped)
        {
            var session = new PracticeSession(DetKit.Db, new PracticeSetup(), StartSeconds);
            return Drive(new ScriptedSource(script), DetKit.TenMinutes, withHitstops, session.OnInput, () => new SimClockView(session.Clock),
                         session.Step, session.ComputeStateHash, out skipped);
        }

        private static List<ulong> Endless(RawKeyEvent[] script, bool withHitstops, out byte[] record, out int skipped)
        {
            InputRecordHeader header = BotRecorder.EndlessHeader(DetKit.Db, 0x5EED_0C15_0000_0001UL);
            var session = new RunSession(DetKit.Db, header, GoldenRunner.Limits, StartSeconds);
            List<ulong> hashes = Drive(new ScriptedSource(script), DetKit.TenMinutes, withHitstops, session.OnInput,
                                       () => new SimClockView(session.Clock), session.Step, session.ComputeStateHash, out skipped);
            record = session.Finish(StartSeconds + (DetKit.TenMinutes + 1) * TickSeconds).ToArray();
            return hashes;
        }

        [Fact]
        public void S01_T_C15__HitstopsInTheDisplayLeaveTicksAndHashesAlone()
        {
            RawKeyEvent[] script = Script(0x0C15_0001UL, DetKit.TenMinutes);
            Assert.True(script.Length > 1000);

            List<ulong> practicePlain = Practice(script, false, out int skippedPlain);
            List<ulong> practiceStopped = Practice(script, true, out int skippedStopped);
            Assert.Equal(0, skippedPlain);
            Assert.True(skippedStopped >= Hitstops * 7, "each 0.12 s hitstop must swallow about 7 frames, got " + skippedStopped);
            Assert.Equal(DetKit.TenMinutes + 1, (uint)practicePlain.Count);         // 틱 수가 같다
            HashAssert.SameEveryTick(practicePlain.ToArray(), practiceStopped.ToArray(), "practice: no hitstop vs 10 hitstops");

            List<ulong> endlessPlain = Endless(script, false, out byte[] recordPlain, out _);
            List<ulong> endlessStopped = Endless(script, true, out byte[] recordStopped, out _);
            Assert.Equal(DetKit.TenMinutes + 1, (uint)endlessStopped.Count);
            HashAssert.SameEveryTick(endlessPlain.ToArray(), endlessStopped.ToArray(), "endless: no hitstop vs 10 hitstops");
            Assert.Equal(recordPlain, recordStopped);                               // 기록의 바이트까지 같다

            // 그 기록(T-C14 의 꼴)을 재실행한 해시도 같다
            RecordFeed feed = DetKit.Feed(recordStopped);
            HashAssert.SameEveryTick(endlessPlain.ToArray(), DetKit.Hashes(WorldKind.Endless, feed), "endless session vs replay of its record");
        }
    }
}
