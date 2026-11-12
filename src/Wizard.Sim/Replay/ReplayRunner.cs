using System;
using System.Globalization;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;
using Wizard.Sim.Waves;

namespace Wizard.Sim
{
    // CONTRACTS 3.8, spec 09 9절. 검증 워커, 다시 보기, 골든 시험이 같은 문으로 다시 돌린다. 예외를 밖으로 던지지 않는다.
    //
    // Run 의 recordBody 는 .wzrc 에서 머리말을 뺀 바이트(본문 ‖ 꼬리말)다. 머리말은 호출자가 믿는 원천(서버는 run.setup_canonical)에서
    // 만든 header 이고, record_sha256 은 header 의 정규 바이트 ‖ 본문 ‖ 꼬리말의 칸으로 대조한다. 그래서 파일의 머리말이 믿는 머리말과
    // 한 바이트라도 다른 기록은 RecordCorrupt 다(파일의 머리말은 재실행 입력으로 쓰지 않는다, CONTRACTS 8.3).
    //
    // 판정(Run 과 RunDuel 이 같다):
    //   구조 오류, end_tick > limits.MaxTicks, 무효 명령(0단계가 받지 않은 명령), 0x04 의 상태 해시 ≠ 재실행의 해시,
    //   꼬리말의 final_state_hash ≠ 재실행의 최종 해시                                           → RecordCorrupt
    //   머리말의 DataVersion ≠ db.DataVersion                                                  → PackMissing (다른 팩으로 돌려 무고한 판을 회수하지 않게)
    //   end_tick 전에 판이 끝남 → EndedEarly, end_tick 에 닿았는데 판이 안 끝남 → EndedLate
    //   Step 을 limits.MaxStepCount 번 넘게 불러야 함 → Timeout
    //   SimInvariantException(또는 다른 예외, 인자 없음, 형식에 담기지 않는 header) → SimFault(FaultDetail 에 종류와 틱)
    //
    // 중간 지점(0x04)의 해시는 world.Tick == 그 틱일 때(그 틱의 Step 전) 대조한다. wave 는 세계가 기록한 중간 지점(RunOutcome.Checkpoints)과
    // 대조할 값인데 그것을 기록하는 Waves·Outcome 모듈이 아직 없다. 그 모듈의 작업(M6-SIM-01)이 여기에 대조를 더한다(T-49).
    //
    // upToWave > 0(원정 정리): wave == upToWave 인 첫 0x04 의 틱까지만 돌리고 그 지점까지의 중간 지점을 대조한다. 그런 0x04 가 없으면 RecordCorrupt.
    // Outcome 은 그 지점까지의 결과다(EndKind = None, EndTick = 그 틱, FinalStateHash = 그 틱의 해시). 웨이브·점수·드랍 칸은
    // GetOutcome 과 마찬가지로 Outcome 모듈이 채울 자리다(M6-SIM-01).
    public static class ReplayRunner
    {
        private const int InitialCommandCapacity = 4;

        public static ReplayResult Run(ContentDb db, InputRecordHeader header, ReadOnlySpan<byte> recordBody,
                                       ReplayLimits limits, int upToWave = 0)
        {
            return RunCore(db, header, recordBody, limits, upToWave, null);
        }

        // WZDR(호스트의 확정 기록, 다시 보기). 시작 조건은 파일의 머리말(DuelSetup 의 정규 바이트)에서 읽는다:
        // WZDR 은 서버(호스트)가 만든 파일이고, 이 문의 결과로 보상을 정하지 않는다(결투의 정본은 호스트의 세계다)
        public static DuelReplayResult RunDuel(ContentDb db, ReadOnlySpan<byte> duelRecord, ReplayLimits limits)
        {
            return RunDuelCore(db, duelRecord, limits, null);
        }

        internal static ReplayResult RunCore(ContentDb? db, InputRecordHeader? header, ReadOnlySpan<byte> recordBody,
                                             ReplayLimits limits, int upToWave, IReplayObserver? observer)
        {
            var result = new ReplayResult { Status = ReplayStatus.SimFault };
            try
            {
                RunEndless(db, header, recordBody, limits, upToWave, observer, result);
            }
            catch (SimInvariantException e)
            {
                Fault(result, e);
            }
            catch (Exception e)
            {
                Fault(result, e);   // 구현 결함도 밖으로 던지지 않는다. 판정은 unverifiable 쪽이다(CONTRACTS 3.8)
            }
            return result;
        }

        internal static DuelReplayResult RunDuelCore(ContentDb? db, ReadOnlySpan<byte> duelRecord, ReplayLimits limits, IReplayObserver? observer)
        {
            var result = new DuelReplayResult { Status = ReplayStatus.SimFault };
            try
            {
                RunDuelInner(db, duelRecord, limits, observer, result);
            }
            catch (Exception e)
            {
                // SimInvariantException 과 그 밖의 구현 결함 모두 밖으로 던지지 않는다. 판정은 unverifiable 쪽이다(CONTRACTS 3.8)
                result.Status = ReplayStatus.SimFault;
                result.Outcome = null;
                result.FaultDetail = Describe(e, result.TicksSimulated);
            }
            return result;
        }

        private static void RunEndless(ContentDb? db, InputRecordHeader? header, ReadOnlySpan<byte> recordBody, ReplayLimits limits,
                                       int upToWave, IReplayObserver? observer, ReplayResult result)
        {
            if (db == null || header == null || header.Setup == null)
            {
                result.Status = ReplayStatus.SimFault;
                result.FaultDetail = "missing db or header";
                return;
            }
            var headerBytes = new ByteWriter();
            try
            {
                InputRecordCodec.WriteHeader(headerBytes, header);
            }
            catch (InvalidOperationException e)
            {
                result.Status = ReplayStatus.SimFault;
                result.FaultDetail = "header has no canonical bytes: " + e.Message;
                return;
            }
            if (!InputRecordCodec.TryReadBody(headerBytes.Written, recordBody, limits.MaxTicks, out RecordFooter footer, out _, out _))
            {
                result.Status = ReplayStatus.RecordCorrupt;
                return;
            }
            if (db.DataVersion != header.Setup.DataVersion)
            {
                result.Status = ReplayStatus.PackMissing;
                return;
            }

            bool tidy = upToWave > 0;
            uint stopTick = footer.EndTick;
            if (tidy && !TryFindCheckpoint(recordBody, upToWave, out stopTick))
            {
                result.Status = ReplayStatus.RecordCorrupt;
                return;
            }

            SimWorld world = SimWorld.CreateEndless(db, SimConfig.ForEndless(db), header.Setup);
            var sink = new ReplaySink();
            var inputs = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            var commands = new RunCommand[InitialCommandCapacity];
            var slot = new PlayerSlot(0);
            var reader = new WzrcBodyReader(recordBody);       // 이미 확인한 본문이다. 두 번째로 읽으며 틱마다 나눠 넣는다
            bool pending = reader.Next(out RecordEntry next, out _);
            long steps = 0;

            while (world.Tick < stopTick)
            {
                uint t = world.Tick;
                int inputCount = 0, commandCount = 0;
                while (pending && next.Tick == t)
                {
                    switch (next.Kind)
                    {
                        case RecordKind.Checkpoint:
                            if (world.ComputeStateHash() != next.StateHash)
                            {
                                result.Status = ReplayStatus.RecordCorrupt;
                                return;
                            }
                            break;
                        case RecordKind.Command:
                            if (commandCount == commands.Length)
                            {
                                Array.Resize(ref commands, commands.Length * 2);
                            }
                            commands[commandCount++] = new RunCommand(t, next.Seq, slot, next.Op, next.A, next.B);
                            break;
                        case RecordKind.Input:
                            inputs[inputCount++] = new PlayerInput(slot, new InputEvent(t, next.Offset, next.Key, next.Down, next.Seq));
                            break;
                        default:
                            break;   // 일시정지는 시뮬레이션에 영향이 없다
                    }
                    pending = reader.Next(out next, out _);
                }

                if (steps >= limits.MaxStepCount)
                {
                    result.Status = ReplayStatus.Timeout;
                    return;
                }
                sink.Rejected = 0;
                var fedInputs = new ReadOnlySpan<PlayerInput>(inputs, 0, inputCount);
                var fedCommands = new ReadOnlySpan<RunCommand>(commands, 0, commandCount);
                observer?.BeforeStep(world, fedInputs, fedCommands);
                world.Step(fedInputs, fedCommands, sink);
                steps++;
                result.TicksSimulated = world.Tick;
                if (sink.Rejected > 0)
                {
                    result.Status = ReplayStatus.RecordCorrupt;
                    return;
                }
                observer?.AfterStep(world);
                if (world.IsOver)
                {
                    if (world.Tick < stopTick)
                    {
                        result.Status = ReplayStatus.EndedEarly;
                        return;
                    }
                    break;
                }
            }

            // 멈춘 틱의 중간 지점(그 틱의 Step 전 = 지금)
            while (pending && next.Tick == stopTick)
            {
                if (next.Kind == RecordKind.Checkpoint && world.ComputeStateHash() != next.StateHash)
                {
                    result.Status = ReplayStatus.RecordCorrupt;
                    return;
                }
                pending = reader.Next(out next, out _);
            }

            if (tidy)
            {
                // 그 지점까지의 결과. 판은 끝나지 않았다(EndKind = None). 나머지 칸은 Outcome 모듈의 자리다(M6-SIM-01)
                result.Status = ReplayStatus.Completed;
                result.Outcome = new RunOutcome
                {
                    EndKind = RunEndKind.None,
                    EndTick = world.Tick,
                    FinalStateHash = world.ComputeStateHash(),
                    StartWave = header.Setup.StartWave,
                };
                return;
            }
            if (!world.IsOver)
            {
                result.Status = ReplayStatus.EndedLate;
                return;
            }
            var outcome = world.GetOutcome();
            if (outcome.FinalStateHash != footer.FinalStateHash)
            {
                result.Status = ReplayStatus.RecordCorrupt;
                return;
            }
            result.Status = ReplayStatus.Completed;
            result.Outcome = outcome;
        }

        private static void RunDuelInner(ContentDb? db, ReadOnlySpan<byte> duelRecord, ReplayLimits limits,
                                         IReplayObserver? observer, DuelReplayResult result)
        {
            if (db == null)
            {
                result.Status = ReplayStatus.SimFault;
                result.FaultDetail = "missing db";
                return;
            }
            if (!DuelRecordCodec.TryRead(duelRecord, limits, out DuelSetup? setup, out DuelRecordBody? body, out _))
            {
                result.Status = ReplayStatus.RecordCorrupt;
                return;
            }
            if (db.DataVersion != setup.DataVersion)
            {
                result.Status = ReplayStatus.PackMissing;
                return;
            }
            ReadOnlySpan<byte> recordBody = duelRecord.Slice(body.HeaderLength + InputRecordFormat.DuelInputDelayCount);

            SimWorld world = SimWorld.CreateDuel(db, SimConfig.ForDuel(db, setup.Options), setup);
            var sink = new ReplaySink();
            var inputs = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer * world.PlayerCount];
            var reader = new WzdrBodyReader(recordBody);       // 이미 확인한 본문이다. 두 번째로 읽으며 틱마다 나눠 넣는다
            bool pending = reader.Next(out RecordEntry next, out _);
            long steps = 0;
            uint stopTick = body.EndTick;

            while (world.Tick < stopTick)
            {
                uint t = world.Tick;
                int inputCount = 0;
                while (pending && next.Tick == t)
                {
                    inputs[inputCount++] = WzdrBodyReader.ToInput(in next);
                    pending = reader.Next(out next, out _);
                }
                if (steps >= limits.MaxStepCount)
                {
                    result.Status = ReplayStatus.Timeout;
                    return;
                }
                var fedInputs = new ReadOnlySpan<PlayerInput>(inputs, 0, inputCount);
                observer?.BeforeStep(world, fedInputs, ReadOnlySpan<RunCommand>.Empty);
                world.Step(fedInputs, ReadOnlySpan<RunCommand>.Empty, sink);
                steps++;
                result.TicksSimulated = world.Tick;
                observer?.AfterStep(world);
                if (world.IsOver)
                {
                    if (world.Tick < stopTick)
                    {
                        result.Status = ReplayStatus.EndedEarly;
                        return;
                    }
                    break;
                }
            }
            if (!world.IsOver)
            {
                result.Status = ReplayStatus.EndedLate;
                return;
            }
            var outcome = world.GetDuelOutcome();
            if (outcome.FinalStateHash != body.FinalStateHash)
            {
                result.Status = ReplayStatus.RecordCorrupt;
                return;
            }
            result.Status = ReplayStatus.Completed;
            result.Outcome = outcome;
        }

        // wave == upToWave 인 첫 중간 지점의 틱
        private static bool TryFindCheckpoint(ReadOnlySpan<byte> recordBody, int upToWave, out uint tick)
        {
            tick = 0;
            var reader = new WzrcBodyReader(recordBody);
            while (reader.Next(out RecordEntry e, out _))
            {
                if (e.Kind == RecordKind.Checkpoint && e.Wave == upToWave)
                {
                    tick = e.Tick;
                    return true;
                }
            }
            return false;
        }

        private static void Fault(ReplayResult result, Exception e)
        {
            result.Status = ReplayStatus.SimFault;
            result.Outcome = null;
            result.FaultDetail = Describe(e, result.TicksSimulated);
        }

        // 운영자용 글자: 예외의 종류와 틱
        private static string Describe(Exception e, uint ticksSimulated) =>
            e.GetType().FullName + " at tick " + ticksSimulated.ToString(CultureInfo.InvariantCulture) + ": " + e.Message;
    }
}
