using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wizard.Content;
using Wizard.Sim;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.ReplayCli
{
    // 골든 항목을 돌려 해시 목록을 낸다(tests/golden/README.md, tests/golden/m1/README.md).
    //
    // 항목의 "setup"(m1 부터의 규약. 모르는 열쇠는 오류다. 오타가 조용히 무시되지 않게):
    //   players        정수. 세계의 플레이어 수와 같아야 한다(연습·무한 웨이브 1, 결투 2)
    //   hashEveryTicks 정수 > 0. 해시를 남기는 틱 간격
    // 해시 목록: world.Tick == hashEveryTicks × k (< 기록의 끝)마다 한 칸, 그리고 기록의 끝(label "final"). 칸의 해시는 그때의 상태 해시다
    // (그 틱 수만큼 Step 한 뒤. .wzrc 의 0x04·꼬리말과 같은 규약).
    // 세계: kind 가 endless 면 기록의 머리말로, practice 면 연습 세계(머리말을 읽지 않는다, 씨앗 0), duel 이면 .wzdr 의 머리말로 만든다.
    // 항목의 seed 는 그 세계의 씨앗과 같아야 한다.
    public static class GoldenRunner
    {
        public const string PlayersKey = "players";
        public const string HashEveryKey = "hashEveryTicks";
        public const string FinalLabel = "final";

        private static readonly string[] KnownKeys = { HashEveryKey, PlayersKey };

        // 기록의 end_tick 상한(골든은 우리가 만든 기록이라 넉넉한 값이면 된다. 10시간)
        public static readonly ReplayLimits Limits = new ReplayLimits(2_160_000U, 2_160_000L);

        public static IReadOnlyList<GoldenHash> Run(ContentDb db, GoldenEntry entry, Action<SimWorld, RecordFeed>? afterStep = null) =>
            Run(db, entry.Id, entry.Kind, entry.Seed, entry.Setup, entry.ReadRecord(), afterStep);

        // afterStep: 틱마다 Step 뒤(해시를 남기기 전)에 부른다. 시험이 그 자리에서 스냅숏 저장 → 진행 → 복원을 끼워 넣는다
        // (README: 스냅숏 저장·복원 뒤 이어 돌리기도 같은 기대 목록과 비교한다). 돌아올 때 world.Tick 은 부르기 전과 같아야 한다
        public static IReadOnlyList<GoldenHash> Run(ContentDb db, string id, string kindText, ulong seed, GoldenSetup setup, byte[] record,
                                                    Action<SimWorld, RecordFeed>? afterStep = null)
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
            WorldKind kind = ReplayWorlds.ParseKind(kindText);
            RecordFeed feed = RecordFeed.Load(record, Limits);
            if ((kind == WorldKind.Duel) != (feed.Format == RecordFormat.Wzdr))
            {
                throw new FormatException(id + ": kind " + kindText + " does not match the record format " + feed.Format);
            }
            ulong worldSeed = ReplayWorlds.WorldSeed(kind, feed);
            if (worldSeed != seed)
            {
                throw new FormatException(id + ": seed " + seed + " is not the world seed " + worldSeed + " of the record");
            }
            SimWorld world = ReplayWorlds.Create(db, kind, feed);
            if (players != world.PlayerCount)
            {
                throw new FormatException(id + ": players " + players + " but the world has " + world.PlayerCount);
            }

            var list = new List<GoldenHash>();
            var sink = new CountingSink();
            uint step = (uint)every;
            while (world.Tick < feed.EndTick && !world.IsOver)
            {
                Replayer.StepOnce(world, feed, sink);
                if (afterStep != null)
                {
                    uint at = world.Tick;
                    afterStep(world, feed);
                    if (world.Tick != at)
                    {
                        throw new InvalidOperationException("afterStep must leave the world at the tick it was given");
                    }
                }
                if (world.Tick % step == 0 && world.Tick < feed.EndTick && !world.IsOver)
                {
                    list.Add(new GoldenHash(world.Tick, world.ComputeStateHash(), null));
                }
            }
            list.Add(new GoldenHash(world.Tick, world.ComputeStateHash(), FinalLabel));
            return list;
        }

        // 항목 폴더 하나를 읽는다. expected.json 이 없어도 된다(새 골든을 만들 때)
        public static GoldenEntry LoadForUpdate(string entryDirectory, string id)
        {
            string goldenJson = File.ReadAllText(Path.Combine(entryDirectory, GoldenEntry.EntryFileName), Encoding.UTF8);
            string expectedPath = Path.Combine(entryDirectory, GoldenEntry.ExpectedFileName);
            string expected = File.Exists(expectedPath)
                ? File.ReadAllText(expectedPath, Encoding.UTF8)
                : GoldenEntry.FormatExpected(id, new[] { new GoldenHash(0, 0, null) });     // 자리 값. 읽은 뒤 버린다
            return GoldenEntry.Parse(id, entryDirectory, goldenJson, expected, true);
        }

        // 골든 뿌리 아래 m<N>/<이름>/golden.json 이 있는 폴더(id 서수 오름차순)
        public static List<(string Id, string Directory)> EntryFolders(string goldenRoot)
        {
            var list = new List<(string, string)>();
            foreach (string milestone in Directory.GetDirectories(goldenRoot))
            {
                string m = Path.GetFileName(milestone);
                if (m.Length < 2 || m[0] != 'm')
                {
                    continue;
                }
                foreach (string dir in Directory.GetDirectories(milestone))
                {
                    if (File.Exists(Path.Combine(dir, GoldenEntry.EntryFileName)))
                    {
                        list.Add((m + "/" + Path.GetFileName(dir), dir));
                    }
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            return list;
        }
    }
}
