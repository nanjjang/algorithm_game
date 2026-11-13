#nullable enable
using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.App.Headless
{
    // 골든 항목 하나를 이 런타임에서 돌려 해시 목록을 낸다. 규칙은 tests/golden/m1/README.md 그대로다
    // (dotnet 쪽은 tools/Wizard.ReplayCli/GoldenRunner.cs. 같은 규칙이어야 같은 해시가 나온다):
    //   세계   practice: SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup()). 씨앗 0
    //          endless:  SimWorld.CreateEndless(db, SimConfig.ForEndless(db), 머리말의 EndlessSetup)
    //          duel:     SimWorld.CreateDuel(db, SimConfig.ForDuel(db, setup.Options), 머리말의 DuelSetup)
    //          기록 머리말의 data_version 이 팩과 다르면 돌리지 않는다. 코어 버전은 보지 않는다
    //   입력   틱 t 의 Step 에는 기록의 틱 t 열만. 기록의 end_tick 까지(판이 먼저 끝나면 거기까지)
    //   setup  players(세계의 플레이어 수와 같아야 한다), hashEveryTicks(> 0). 모르는 열쇠는 오류
    //   해시   world.Tick 이 hashEveryTicks 의 배수이고 end_tick 보다 작을 때마다 한 칸, 그리고 끝에 한 칸(label "final")
    // 예외(형식 오류, SimInvariantException …)는 밖으로 던진다. 결과 파일의 error 칸은 부르는 쪽(GoldenSuite)이 채운다.
    public static class GoldenReplay
    {
        public const string PlayersKey = "players";
        public const string HashEveryKey = "hashEveryTicks";
        public const string FinalLabel = "final";

        // 기록의 end_tick 상한. 골든은 우리가 만든 기록이라 넉넉한 값이면 된다(10시간. ReplayCli 의 GoldenRunner.Limits 와 같은 값)
        public static readonly ReplayLimits Limits = new ReplayLimits(2160000U, 2160000L);

        private static readonly string[] KnownKeys = { HashEveryKey, PlayersKey };

        public static GoldenPlan Prepare(ContentDb db, GoldenEntry entry)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
            return Prepare(db, entry.Id, entry.Kind, entry.Seed, entry.Setup, entry.ReadRecord());
        }

        public static GoldenPlan Prepare(ContentDb db, string id, string kind, ulong seed, GoldenSetup setup, byte[] recordBytes)
        {
            foreach (string key in setup.Keys)
            {
                if (Array.IndexOf(KnownKeys, key) < 0)
                {
                    throw new FormatException(id + ": unknown setup key '" + key + "' (known: " + string.Join(", ", KnownKeys) + ")");
                }
            }
            long every = setup.GetInt64(HashEveryKey);
            long players = setup.GetInt64(PlayersKey);
            if (every <= 0 || every > uint.MaxValue)
            {
                throw new FormatException(id + ": " + HashEveryKey + " must be a positive tick count");
            }
            if (kind != "practice" && kind != "endless" && kind != "duel")
            {
                throw new FormatException(id + ": kind '" + kind + "' is not endless, practice or duel");
            }
            GoldenRecord record = GoldenRecord.Load(recordBytes, Limits);
            if ((kind == "duel") != record.IsDuel)
            {
                throw new FormatException(id + ": kind " + kind + " does not match the record format (" + (record.IsDuel ? ".wzdr" : ".wzrc") + ")");
            }
            int dataVersion = record.IsDuel ? record.Duel!.DataVersion : record.Endless!.DataVersion;
            if (dataVersion != db.DataVersion)
            {
                throw new FormatException(id + ": record data_version " + dataVersion + " is not the loaded pack " + db.DataVersion);
            }
            ulong worldSeed = kind == "practice" ? 0UL : record.SetupSeed;
            if (worldSeed != seed)
            {
                throw new FormatException(id + ": seed " + seed + " is not the world seed " + worldSeed + " of the record");
            }
            var plan = new GoldenPlan(id, kind, record, (uint)every, (int)players);
            SimWorld probe = CreateWorld(db, plan);
            if (players != probe.PlayerCount)
            {
                throw new FormatException(id + ": players " + players + " but the world has " + probe.PlayerCount);
            }
            return plan;
        }

        public static SimWorld CreateWorld(ContentDb db, GoldenPlan p)
        {
            switch (p.Kind)
            {
                case "practice":
                    return SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());
                case "endless":
                    return SimWorld.CreateEndless(db, SimConfig.ForEndless(db), p.Record.Endless!);
                default:
                    DuelSetup duel = p.Record.Duel!;
                    return SimWorld.CreateDuel(db, SimConfig.ForDuel(db, duel.Options), duel);
            }
        }

        // 끊지 않고 처음부터 끝까지
        public static List<GoldenHash> Run(ContentDb db, GoldenPlan p) => RunCore(db, p, 0, 0);

        // 같은 기록을 다시 돌리되 판의 가운데쯤(rewindAt)에서 스냅숏 저장 → 9틱 진행 → 복원,
        // 4분의 3 지점(transferAt)에서 WriteState → 새 세계 ReadState → 그 세계의 스냅숏을 원래 세계에 싣는다(T-30, PIPELINE 7.3).
        // 지점은 dotnet 의 GoldenTests 와 같다: end = 기대 목록의 마지막 틱, rewindAt = end/2 + 3, transferAt = end/4*3 + 1.
        // 해시 목록은 Run 과 같아야 한다.
        public static List<GoldenHash> RunWithRewindAndTransfer(ContentDb db, GoldenPlan p, uint end)
        {
            uint rewindAt = end / 2 + 3;
            uint transferAt = end / 4 * 3 + 1;
            return RunCore(db, p, rewindAt, transferAt);
        }

        private static List<GoldenHash> RunCore(ContentDb db, GoldenPlan p, uint rewindAt, uint transferAt)
        {
            SimWorld world = CreateWorld(db, p);
            GoldenRecord record = p.Record;
            var sink = new DiscardSink();
            var list = new List<GoldenHash>();
            while (world.Tick < record.EndTick && !world.IsOver)
            {
                StepOnce(world, record, sink);
                if (rewindAt != 0 && world.Tick == rewindAt)
                {
                    SimSnapshot snap = world.CreateSnapshotBuffer();
                    world.SaveSnapshot(snap);
                    const uint RewindTicks = 9;
                    while (world.Tick < rewindAt + RewindTicks && world.Tick < record.EndTick && !world.IsOver)
                    {
                        StepOnce(world, record, sink);
                    }
                    world.LoadSnapshot(snap);
                }
                else if (transferAt != 0 && world.Tick == transferAt)
                {
                    var bytes = new ByteWriter();
                    world.WriteState(bytes);
                    SimWorld fresh = CreateWorld(db, p);
                    fresh.ReadState(bytes.Written);
                    SimSnapshot snap = fresh.CreateSnapshotBuffer();
                    fresh.SaveSnapshot(snap);
                    world.LoadSnapshot(snap);
                }
                if (world.Tick % p.HashEvery == 0 && world.Tick < record.EndTick && !world.IsOver)
                {
                    list.Add(new GoldenHash(world.Tick, world.ComputeStateHash(), null));
                }
            }
            list.Add(new GoldenHash(world.Tick, world.ComputeStateHash(), FinalLabel));
            return list;
        }

        private static void StepOnce(SimWorld world, GoldenRecord record, ISimEventSink sink)
        {
            uint t = world.Tick;
            world.Step(record.InputsAt(t), record.CommandsAt(t), sink);
        }

        // 표시 이벤트는 버린다(재실행은 상태 해시만 본다)
        private sealed class DiscardSink : ISimEventSink
        {
            public void Emit(in SimEvent e)
            {
            }
        }
    }
}
