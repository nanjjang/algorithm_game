using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Wizard.Content;
using Wizard.ReplayCli;
using Wizard.Sim.Tests.World;
using Wizard.TestKit;
using Wizard.TestKit.Shared.Fuzz;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Wizard.Sim.Tests.Determinism
{
    // 결정론 묶음(M1-TE-01, PIPELINE 7.3, ARCHITECTURE 11·12)의 도우미. 기록은 ReplayCli 의 봇이 판을 하는 쪽과 같은 길로 만들고
    // (InputRecorder 가 순번을 매긴 열 = 세계에 넣은 열), 재실행은 ReplayCli 의 RecordFeed·Replayer 로 한다. 팩은 저장소 data/ 다.
    internal static class DetKit
    {
        public const uint Minute = 3600;                  // 60틱 × 60초
        public const uint TenMinutes = 36000;

        public static ContentDb Db => WorldData.RepoDb;

        public static RecordFeed Feed(byte[] record) => RecordFeed.Load(record, GoldenRunner.Limits);

        public static byte[] PracticeRecord(ulong botSeed, uint ticks, string profile = "play", ulong[]? hashes = null) =>
            BotRecorder.RecordRun(Db, WorldKind.Practice, BotRecorder.PracticeHeader(Db), botSeed, ticks, BotProfile.Named(profile), hashes);

        public static byte[] EndlessRecord(ulong seed, ulong botSeed, uint ticks, string profile = "play", ulong[]? hashes = null) =>
            BotRecorder.RecordRun(Db, WorldKind.Endless, BotRecorder.EndlessHeader(Db, seed), botSeed, ticks, BotProfile.Named(profile), hashes);

        public static byte[] DuelRecord(ulong seed, ulong botSeed, uint ticks, string profile = "play", ulong[]? hashes = null) =>
            BotRecorder.RecordDuel(Db, BotRecorder.DuelSetupFor(Db, seed), botSeed, ticks, BotProfile.Named(profile), hashes);

        public static byte[] Record(WorldKind kind, ulong worldSeed, ulong botSeed, uint ticks, string profile, ulong[]? hashes = null)
        {
            switch (kind)
            {
                case WorldKind.Practice:
                    return PracticeRecord(botSeed, ticks, profile, hashes);
                case WorldKind.Endless:
                    return EndlessRecord(worldSeed, botSeed, ticks, profile, hashes);
                default:
                    return DuelRecord(worldSeed, botSeed, ticks, profile, hashes);
            }
        }

        public static SimWorld World(WorldKind kind, RecordFeed feed) => ReplayWorlds.Create(Db, kind, feed);

        public static ulong[] Hashes(WorldKind kind, RecordFeed feed) => Replayer.TickHashes(World(kind, feed), feed);

        // 같은 사례 씨앗 열(Fuzzer.CaseSeed)을 여러 스레드로 나눠 돌리고, 실패한 첫 사례를 TestKit 퍼저로 다시 돌려(줄이기 포함) 보고한다.
        // 사례마다 세계를 따로 만든다(세계끼리 나누는 것은 불변인 ContentDb 뿐). 결과는 스레드 수와 무관하다: 사례 씨앗이 정해져 있고
        // 보고하는 것은 색인이 가장 작은 실패 사례다.
        public static void ParallelFuzz<T>(ITestOutputHelper output, ulong seed, int cases, FuzzGen<T> generate, FuzzProperty<T> property,
                                           IFuzzShrinker<T>? shrinker, Func<T, string> format)
        {
            var seeds = new ulong[cases];
            for (int i = 0; i < cases; i++)
            {
                seeds[i] = Fuzzer.CaseSeed(seed, i);
            }
            var failures = new ConcurrentDictionary<int, string>();
            Parallel.For(0, cases, i =>
            {
                string? message;
                try
                {
                    message = property(Fuzzer.Generate(seeds[i], generate));
                }
                catch (Exception e)
                {
                    message = "threw " + e.GetType().FullName + ": " + e.Message;
                }
                if (message != null)
                {
                    failures[i] = message;
                }
            });
            if (failures.IsEmpty)
            {
                output.WriteLine("fuzz seed 0x" + seed.ToString("x16") + ": " + cases + " cases passed");
                return;
            }
            int first = int.MaxValue;
            foreach (int k in failures.Keys)
            {
                first = Math.Min(first, k);
            }
            output.WriteLine(failures.Count + " of " + cases + " cases failed; first is case " + first + " (case seed 0x" + seeds[first].ToString("x16") + ")");
            FuzzAssert.Holds(output, FuzzOptions.Replay(seeds[first]), generate, property, shrinker, format);
            throw new XunitException("case " + first + " failed in parallel but passed when replayed alone (state leaks between worlds?): " +
                                     failures[first]);
        }
    }
}
