using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Duel;

namespace Wizard.Perf
{
    // 시나리오의 이름(결과 파일의 열쇠 일부), 세계 만들기, 각본의 씨앗
    internal static class ScenarioInfo
    {
        // 각본 씨앗(고정. 바꾸면 기준선을 다시 재야 한다)
        private const ulong PracticeScriptSeed = 0x5045_5246_0000_0001UL;
        private const ulong DuelScriptSeed = 0x5045_5246_0000_0002UL;
        private const ulong EndlessScriptSeed = 0x5045_5246_0000_0003UL;
        private const ulong WorldSeed = 0x5045_5246_5345_4544UL;

        public static readonly Guid ReplayRunId = new Guid("7065726f-0000-4000-8000-000000000001");

        public static string Id(PerfScenario s)
        {
            switch (s)
            {
                case PerfScenario.PracticeProto:
                    return "practice_proto";
                case PerfScenario.Duel2p:
                    return "duel_2p";
                case PerfScenario.Endless1p:
                    return "endless_1p";
                default:
                    throw new ArgumentOutOfRangeException(nameof(s));
            }
        }

        public static int Players(PerfScenario s) => s == PerfScenario.Duel2p ? 2 : 1;

        public static ulong ScriptSeed(PerfScenario s)
        {
            switch (s)
            {
                case PerfScenario.PracticeProto:
                    return PracticeScriptSeed;
                case PerfScenario.Duel2p:
                    return DuelScriptSeed;
                default:
                    return EndlessScriptSeed;
            }
        }

        // 0단계 명령(조립 바꾸기)은 시제품이 켜진 연습 세계에서만 유효하다. 결투에는 0단계 명령이 없다(CONTRACTS 3.2)
        public static bool UsesCommands(PerfScenario s) => s == PerfScenario.PracticeProto;

        public static SimWorld Create(PerfScenario s)
        {
            ContentDb db = PerfRepo.Db;
            switch (s)
            {
                case PerfScenario.PracticeProto:
                    return SimWorld.CreatePractice(db, SimConfig.ForPractice(db, SimConfig.ForEndless(db)), new PracticeSetup());
                case PerfScenario.Duel2p:
                    return SimWorld.CreateDuel(db, SimConfig.ForDuel(db, new DuelRoomOptions()), new DuelSetup
                    {
                        Seed = WorldSeed,
                        Players = new[]
                        {
                            new DuelPlayerSetup { Slot = new PlayerSlot(0) },
                            new DuelPlayerSetup { Slot = new PlayerSlot(1) },
                        },
                    });
                case PerfScenario.Endless1p:
                    return SimWorld.CreateEndless(db, SimConfig.ForEndless(db), EndlessSetup(db));
                default:
                    throw new ArgumentOutOfRangeException(nameof(s));
            }
        }

        // 재실행 기록의 머리말이 될 시작 조건(정규 바이트가 있어야 한다: CoreVersion 32자, Wand 칸)
        public static EndlessSetup EndlessSetup(ContentDb db) => new EndlessSetup
        {
            RunId = ReplayRunId,
            Seed = WorldSeed,
            Kind = RunKind.Normal,
            StartWave = 1,
            Wand = new WandSnapshot { Character = CharacterId.Baseline },
            CoreVersion = SimVersion.CoreVersion,
            DataVersion = db.DataVersion,
        };
    }
}
