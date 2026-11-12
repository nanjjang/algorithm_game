using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Input;

namespace Wizard.ReplayCli
{
    // 봇 입력으로 세계를 돌리며 기록을 만든다(골든의 입력 기록, 퍼저의 입력). 기록하는 길은 판을 하는 쪽과 같다:
    //   .wzrc  InputRecorder.Append 가 순번을 매긴 열을 그대로 Step 에 넣는다. 명령은 한 틱에 하나, CanApply 가 참인 것만(RunSession 과 같다)
    //   .wzdr  사람마다 SeqCounter 로 매긴 열을 InputOrder 로 섞어 Step 에 넣고 같은 열을 DuelRecordWriter 에 싣는다
    // 꼬리말에는 기록을 만든 실행의 최종 해시가 들어간다(재실행이 같은 해시를 내야 한다).
    // hashes 를 주면(길이 ticks + 1) 칸 t 에 기록하는 실행의 world.Tick == t 일 때의 상태 해시를 담는다(끊지 않고 진행한 실행의 해시).
    public static class BotRecorder
    {
        private const int DuelPlayers = 2;
        private const int MaxHpPoints = 600;            // 결투 머리말의 칸(담기기만 하면 된다. 이 팩의 플레이어 모듈은 캐릭터의 칸 수를 쓴다)
        private const string MatchIdText = "6d310000-0000-4000-8000-000000000001";

        public static InputRecordHeader EndlessHeader(ContentDb db, ulong seed) => new InputRecordHeader
        {
            FormatVersion = EndlessSetup.CanonicalFormatVersion,
            Setup = new EndlessSetup
            {
                RunId = RunIdFor(seed),
                Seed = seed,
                Kind = RunKind.Normal,
                StartWave = 1,
                PhantomFirst = false,
                SeasonTagOn = false,
                Wand = BaselineWand(),
                UnlockedArts = 0,
                CoreVersion = SimVersion.CoreVersion,
                DataVersion = db.DataVersion,
            },
        };

        // 연습 세계는 머리말을 읽지 않는다. 담기만 하면 되는 자리 값(PracticeSession 의 자리 머리말과 같은 뜻)
        public static InputRecordHeader PracticeHeader(ContentDb db) => EndlessHeader(db, 0UL);

        public static DuelSetup DuelSetupFor(ContentDb db, ulong seed) => new DuelSetup
        {
            MatchId = new Guid(MatchIdText),
            Seed = seed,
            CoreVersion = SimVersion.CoreVersion,
            DataVersion = db.DataVersion,
            Options = new Wizard.Sim.Duel.DuelRoomOptions
            {
                Rounds = 3,
                RoundSeconds = 90,
                HpMultPermille = 1000,
                EnhanceCap = 13,
                AllowMutations = true,
                FloorColor = TimeOfDay.Day,
            },
            Players = new[] { DuelPlayer(0), DuelPlayer(1) },
        };

        // 무한 웨이브(kind = Endless, header 의 씨앗) 또는 연습(kind = Practice)의 .wzrc
        public static byte[] RecordRun(ContentDb db, WorldKind kind, InputRecordHeader header, ulong botSeed, uint ticks, BotProfile profile,
                                       ulong[]? hashes = null)
        {
            RequireHashes(hashes, ticks);
            if (kind != WorldKind.Endless && kind != WorldKind.Practice)
            {
                throw new ArgumentException("RecordRun makes .wzrc records (endless or practice)", nameof(kind));
            }
            var recorder = new InputRecorder(header);
            // 세계는 이 기록을 다시 돌릴 때와 같은 길로 만든다(머리말의 바이트를 다시 읽어 RecordFeed 와 같은 설정을 쓴다)
            SimWorld world = kind == WorldKind.Endless
                ? SimWorld.CreateEndless(db, SimConfig.ForEndless(db), header.Setup)
                : SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());
            var script = new BotScript(botSeed, profile);
            var keys = new QuantizedKey[BotScript.MaxKeysOut];
            var draft = new RunCommandDraft[1];
            var commandsOut = new RunCommand[1];
            var inputsOut = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            var sink = new CountingSink();
            var slot = new PlayerSlot(0);
            Hash(hashes, world);
            for (uint t = 0; t < ticks; t++)
            {
                int n = script.Keys(t, 0, keys);
                int c = 0;
                if (script.Command(out RunCommandDraft d))
                {
                    var probe = new RunCommand(t, 0, slot, d.Op, d.A, d.B);   // 확인은 순번을 보지 않는다
                    if (world.CanApply(in probe))
                    {
                        draft[0] = d;
                        c = 1;
                    }
                }
                recorder.Append(t, slot, new ReadOnlySpan<RunCommandDraft>(draft, 0, c), new ReadOnlySpan<QuantizedKey>(keys, 0, n),
                                commandsOut, out int fedCommands, inputsOut, out int fedInputs);
                world.Step(new ReadOnlySpan<PlayerInput>(inputsOut, 0, fedInputs), new ReadOnlySpan<RunCommand>(commandsOut, 0, fedCommands), sink);
                if (sink.RejectedCommands != 0)
                {
                    throw new InvalidOperationException("a command that passed CanApply was rejected at tick " + t);
                }
                Hash(hashes, world);
            }
            return recorder.Finish(world.Tick, world.ComputeStateHash()).ToArray();
        }

        public static byte[] RecordDuel(ContentDb db, DuelSetup setup, ulong botSeed, uint ticks, BotProfile profile, ulong[]? hashes = null)
        {
            RequireHashes(hashes, ticks);
            SimWorld world = SimWorld.CreateDuel(db, SimConfig.ForDuel(db, setup.Options), setup);
            var writer = new DuelRecordWriter(setup);
            var script = new BotScript(botSeed, profile);
            var keys = new QuantizedKey[BotScript.MaxKeysOut];
            var merged = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer * DuelPlayers];
            var perSlot = new PlayerInput[BotScript.MaxKeysOut];
            var seqs = new SeqCounter[DuelPlayers];
            var sink = new CountingSink();
            Hash(hashes, world);
            for (uint t = 0; t < ticks; t++)
            {
                int m = 0;
                for (int p = 0; p < DuelPlayers; p++)
                {
                    var slot = new PlayerSlot((byte)p);
                    int n = script.Keys(t, p, keys);
                    for (int i = 0; i < n; i++)
                    {
                        QuantizedKey k = keys[i];
                        perSlot[i] = new PlayerInput(slot, new InputEvent(t, k.Offset, k.Key, k.Down, 0));
                    }
                    // 한 사람의 순번은 (오프셋, 분류) 순으로 매기고, 아홉째부터는 번호를 매기기 전에 버린다(CONTRACTS 3.1)
                    InputOrder.Sort(perSlot.AsSpan(0, n));
                    int keep = Math.Min(n, InputOrder.MaxEventsPerTickPerPlayer);
                    for (int i = 0; i < keep; i++)
                    {
                        InputEvent e = perSlot[i].Event;
                        merged[m++] = new PlayerInput(slot, new InputEvent(t, e.Offset, e.Key, e.Down, seqs[p].Next()));
                    }
                }
                InputOrder.Sort(merged.AsSpan(0, m));
                for (int i = 0; i < m; i++)
                {
                    writer.Append(in merged[i]);
                }
                world.Step(new ReadOnlySpan<PlayerInput>(merged, 0, m), ReadOnlySpan<RunCommand>.Empty, sink);
                Hash(hashes, world);
            }
            return writer.Finish(world.Tick, world.ComputeStateHash()).ToArray();
        }

        private static void RequireHashes(ulong[]? hashes, uint ticks)
        {
            if (hashes != null && hashes.Length != ticks + 1L)
            {
                throw new ArgumentException("hashes must have ticks + 1 slots", nameof(hashes));
            }
        }

        private static void Hash(ulong[]? hashes, SimWorld world)
        {
            if (hashes != null)
            {
                hashes[world.Tick] = world.ComputeStateHash();
            }
        }

        private static WandSnapshot BaselineWand() => new WandSnapshot
        {
            Character = CharacterId.Baseline,
            Stage = 0,
            SlotsExtra = 0,
            Mutations = Array.Empty<MutationId>(),
            Core = BlockId.MagicBolt,
            Links = Array.Empty<BlockId>(),
        };

        private static DuelPlayerSetup DuelPlayer(byte slot) => new DuelPlayerSetup
        {
            Slot = new PlayerSlot(slot),
            AppliedWand = BaselineWand(),
            ActualStage = 0,
            MaxHpPoints = MaxHpPoints,
            UnlockedArts = 0,
        };

        // 씨앗에서 정한 run_id(uuid 의 바이트는 기록의 머리말에만 들어가고 세계는 읽지 않는다)
        private static Guid RunIdFor(ulong seed)
        {
            var b = new byte[16];
            for (int i = 0; i < 8; i++)
            {
                b[i] = (byte)(seed >> (8 * i));
            }
            b[8] = 0x80;
            return new Guid(b);
        }
    }
}
