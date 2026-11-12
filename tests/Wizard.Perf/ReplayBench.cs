using System;
using System.Diagnostics;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;

namespace Wizard.Perf
{
    // 재실행 속도(PIPELINE 7.5, spec 12 4절: 화면 없이 실시간의 100배 이상). 검증 워커가 부르는 문(ReplayRunner.Run)을 그대로 잰다:
    // 머리말의 정규 바이트, 본문 확인(SHA-256 포함), 틱마다 나눠 넣기, Step 까지 전부 시계 안이다.
    //
    // 기록은 판을 하는 쪽의 흐름(InputRecorder.Append → Step)으로 한 번 만든다. M1 의 무한 웨이브 세계에는 판을 끝내는 모듈이 없으므로
    // 재실행은 end_tick 에 닿고도 판이 안 끝난 EndedLate 로 끝난다. 그것이 "끝까지 돌렸다"는 뜻이므로 상태와 돌린 틱 수를 확인한다.
    internal static class ReplayBench
    {
        private const double TickRate = SimVersion.TickRate;

        public static ReplayStats Measure(BenchSettings settings)
        {
            ContentDb db = PerfRepo.Db;
            int ticks = settings.ReplayTicks;
            var header = new InputRecordHeader { FormatVersion = EndlessSetup.CanonicalFormatVersion, Setup = ScenarioInfo.EndlessSetup(db) };
            byte[] body = Record(db, header, ticks);
            var limits = new ReplayLimits((uint)ticks, long.MaxValue);

            ReplayResult warm = ReplayRunner.Run(db, header, body, limits);
            RequireFullRun(warm, ticks);
            JitWarmup.For(settings.JitWarmupMillis, () => ReplayRunner.Run(db, header, body, limits));

            var stats = new ReplayStats { TicksPerSecond = 0, RecordTicks = ticks, RecordBytes = body.Length, Rounds = settings.ReplayRounds };
            for (int round = 0; round < settings.ReplayRounds; round++)
            {
                TickBench.Settle();
                long t0 = Stopwatch.GetTimestamp();
                ReplayResult r = ReplayRunner.Run(db, header, body, limits);
                long t1 = Stopwatch.GetTimestamp();
                RequireFullRun(r, ticks);
                double seconds = (double)(t1 - t0) / Stopwatch.Frequency;
                stats.TicksPerSecond = Math.Max(stats.TicksPerSecond, r.TicksSimulated / seconds);
            }
            stats.RealtimeMultiple = stats.TicksPerSecond / TickRate;
            return stats;
        }

        // 머리말을 뗀 본문 ‖ 꼬리말(ReplayRunner.Run 의 recordBody)
        private static byte[] Record(ContentDb db, InputRecordHeader header, int ticks)
        {
            KeyScript script = KeyScript.Generate(ScenarioInfo.ScriptSeed(PerfScenario.Endless1p), ticks, 1, false);
            SimWorld world = SimWorld.CreateEndless(db, SimConfig.ForEndless(db), header.Setup);
            var recorder = new InputRecorder(header);
            var sink = new CountingSink();
            var keys = new QuantizedKey[InputOrder.MaxEventsPerTickPerPlayer];
            var inputs = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            var commands = new RunCommand[1];
            var slot = new PlayerSlot(0);
            for (int t = 0; t < ticks; t++)
            {
                ReadOnlySpan<ScriptKey> k = script.KeysAt(t);
                for (int i = 0; i < k.Length; i++)
                {
                    keys[i] = new QuantizedKey(k[i].Tick, k[i].Offset, k[i].Key, k[i].Down);
                }
                recorder.Append((uint)t, slot, ReadOnlySpan<RunCommandDraft>.Empty, new ReadOnlySpan<QuantizedKey>(keys, 0, k.Length),
                                commands, out int nc, inputs, out int ni);
                world.Step(new ReadOnlySpan<PlayerInput>(inputs, 0, ni), new ReadOnlySpan<RunCommand>(commands, 0, nc), sink);
            }
            byte[] file = recorder.Finish(world.Tick, world.ComputeStateHash()).ToArray();
            if (!EndlessSetup.TryReadCanonicalPrefix(file, out _, out int headerLength))
            {
                throw new InvalidOperationException("perf: the recorder wrote a file without a canonical header");
            }
            return file.AsSpan(headerLength).ToArray();
        }

        private static void RequireFullRun(ReplayResult r, int ticks)
        {
            if (r.Status != ReplayStatus.EndedLate || r.TicksSimulated != (uint)ticks)
            {
                throw new InvalidOperationException("perf: replay did not run the whole record (status " + r.Status + ", ticks "
                                                    + r.TicksSimulated + " of " + ticks + ", " + r.FaultDetail + ")");
            }
        }
    }
}
