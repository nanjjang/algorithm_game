using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Wizard.ReplayCli;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Determinism
{
    // ReplayCli(M1-TE-01 수락 조건): .wzrc 를 돌려 해시 목록을 내고, 두 실행을 비교해 처음 갈라진 틱과 상태 절을 알려 준다.
    // 그리고 이 도구가 기록을 펼치는 길이 코어의 재실행(ReplayRunner)이 세계에 넣는 열과 같은지 본다.
    public sealed class ReplayCliTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "wz-replaycli-" + Guid.NewGuid().ToString("N"));

        public ReplayCliTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, true);
        }

        private string Write(string name, byte[] bytes)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static (int Code, string Out, string Err) Cli(params string[] args)
        {
            var all = new List<string>(args) { "--root", TestRepo.Root };
            var o = new StringWriter();
            var e = new StringWriter();
            int code = Wizard.ReplayCli.Cli.Run(all.ToArray(), o, e);
            return (code, o.ToString(), e.ToString());
        }

        // 연습 기록을 다시 기록한다. extraAt 틱에는 키 누름을 하나 더 넣는다(그 틱의 Step 뒤부터 상태가 갈라진다)
        private static byte[] ReRecordWithExtraKey(RecordFeed feed, uint extraAt)
        {
            var recorder = new InputRecorder(BotRecorder.PracticeHeader(DetKit.Db));
            SimWorld world = DetKit.World(WorldKind.Practice, feed);
            var sink = new CountingSink();
            var commandsOut = new RunCommand[4];
            var inputsOut = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            for (uint t = 0; t < feed.EndTick; t++)
            {
                var drafts = new List<RunCommandDraft>();
                foreach (RunCommand c in feed.CommandsAt(t))
                {
                    drafts.Add(new RunCommandDraft(c.Op, c.A, c.B));
                }
                var keys = new List<QuantizedKey>();
                foreach (PlayerInput p in feed.InputsAt(t))
                {
                    keys.Add(new QuantizedKey(t, p.Event.Offset, p.Event.Key, p.Event.Down));
                }
                if (t == extraAt)
                {
                    // 눌려 있지 않고 이 틱에 나오지 않는 키의 누름(뗌은 누른 시각을 남기지 않아 상태가 같을 수 있다)
                    foreach (LogicalKey k in BotProfile.PlayKeys)
                    {
                        if (!world.PlayerAt(0).Keys.IsDown(k) && keys.TrueForAll(q => q.Key != k))
                        {
                            keys.Insert(0, new QuantizedKey(t, 0, k, true));   // 아홉째가 되면 기록기가 오프셋 순 마지막을 버린다
                            break;
                        }
                    }
                }
                recorder.Append(t, DetRig.P0, drafts.ToArray(), keys.ToArray(), commandsOut, out int nc, inputsOut, out int ni);
                world.Step(new ReadOnlySpan<PlayerInput>(inputsOut, 0, ni), new ReadOnlySpan<RunCommand>(commandsOut, 0, nc), sink);
            }
            return recorder.Finish(world.Tick, world.ComputeStateHash()).ToArray();
        }

        [Fact]
        public void HashesCommandListsTheTickHashesOfTheRecord()
        {
            byte[] record = DetKit.PracticeRecord(0xC11_0001UL, 600, "dense");
            string path = Write("p.wzrc", record);
            (int code, string output, string err) = Cli("hashes", path, "--kind", "practice", "--every", "100");
            Assert.True(code == Wizard.ReplayCli.Cli.Ok, err);
            ulong[] expected = DetKit.Hashes(WorldKind.Practice, DetKit.Feed(record));
            var lines = output.TrimEnd('\n').Split('\n');
            Assert.Equal(7, lines.Length);                                                  // 0, 100, …, 600
            foreach (string line in lines)
            {
                string[] parts = line.Split(' ');
                uint tick = uint.Parse(parts[0], CultureInfo.InvariantCulture);
                Assert.Equal(expected[tick].ToString("x16"), parts[1]);
            }
            Assert.StartsWith("600 ", lines[6]);
        }

        [Fact]
        public void DiffOfTheSameRecordTwiceIsSame()
        {
            string path = Write("d.wzdr", DetKit.DuelRecord(0xC11_0002UL, 0xC11_0003UL, 300, "dense"));
            (int code, string output, _) = Cli("diff", path, path);
            Assert.Equal(Wizard.ReplayCli.Cli.Ok, code);
            Assert.StartsWith("same: 301 tick boundaries compared", output);
        }

        [Fact]
        public void DiffFindsTheFirstDivergingTickAndTheStateSections()
        {
            RecordFeed feed = DetKit.Feed(DetKit.PracticeRecord(0xC11_0004UL, 600, "play"));
            byte[] a = DetKit.PracticeRecord(0xC11_0004UL, 600, "play");
            byte[] b = ReRecordWithExtraKey(feed, 300);

            DivergenceReport report = Divergence.Compare(DetKit.World(WorldKind.Practice, feed), feed,
                                                         DetKit.World(WorldKind.Practice, DetKit.Feed(b)), DetKit.Feed(b));
            Assert.True(report.Diverged, report.ToString());
            Assert.Equal(301U, report.Tick);                                                // 틱 300 의 Step 뒤
            Assert.Contains(nameof(StateSection.Player), report.SectionNames);
            Assert.Contains("the records fed different inputs or commands at tick 300", report.Note);

            string pa = Write("a.wzrc", a), pb = Write("b.wzrc", b);
            (int code, string output, _) = Cli("diff", pa, pb, "--kind", "practice");
            Assert.Equal(Wizard.ReplayCli.Cli.Mismatch, code);
            Assert.Contains("diverged at tick 301 (after the Step of tick 300)", output);
            Assert.Contains("Player(1): first difference at byte", output);

            // 다른 기기에서 돌린 실행끼리: trace 를 내고 trace 끼리 비교한다
            string ta = Path.Combine(_dir, "a.trace"), tb = Path.Combine(_dir, "b.trace"), ta2 = Path.Combine(_dir, "a2.trace");
            Assert.Equal(0, Cli("trace", pa, "--kind", "practice", "--out", ta).Code);
            Assert.Equal(0, Cli("trace", pa, "--kind", "practice", "--out", ta2).Code);
            Assert.Equal(0, Cli("trace", pb, "--kind", "practice", "--out", tb).Code);
            Assert.Equal(File.ReadAllBytes(ta), File.ReadAllBytes(ta2));                     // 같은 실행이면 같은 바이트
            (int same, string sameOut, _) = Cli("diff", ta, ta2);
            Assert.Equal(Wizard.ReplayCli.Cli.Ok, same);
            Assert.StartsWith("same: 601", sameOut);
            (int differ, string differOut, _) = Cli("diff", ta, tb);
            Assert.Equal(Wizard.ReplayCli.Cli.Mismatch, differ);
            Assert.Contains("diverged at tick 301", differOut);
            Assert.Contains("sections: Player", differOut);
        }

        [Fact]
        public void BotCommandWritesARecordTheCoreAccepts()
        {
            string path = Path.Combine(_dir, "bot.wzrc");
            (int code, string output, string err) = Cli("bot", "--kind", "endless", "--ticks", "240", "--bot-seed", "0x77", "--seed", "0x99",
                                                        "--profile", "dense", "--out", path);
            Assert.True(code == 0, err);
            Assert.Contains("240 ticks", output);
            RecordFeed feed = DetKit.Feed(File.ReadAllBytes(path));
            Assert.Equal(240U, feed.EndTick);
            Assert.Equal(0x99UL, feed.SetupSeed);
            Assert.Equal(feed.FinalStateHash, DetKit.Hashes(WorldKind.Endless, feed)[240]);
        }

        [Fact]
        public void UsageErrorsExitWithTwo()
        {
            Assert.Equal(Wizard.ReplayCli.Cli.Usage, Wizard.ReplayCli.Cli.Run(Array.Empty<string>(), new StringWriter(), new StringWriter()));
            Assert.Equal(Wizard.ReplayCli.Cli.Usage, Cli("nope").Code);
            Assert.Equal(Wizard.ReplayCli.Cli.Usage, Cli("golden", "--id", "m1/does-not-exist").Code);
            Assert.Equal(Wizard.ReplayCli.Cli.Usage, Cli("hashes", Write("bad.wzrc", new byte[] { 1, 2, 3 })).Code);
            Assert.Equal(Wizard.ReplayCli.Cli.Usage, Cli("hashes", Write("x.wzrc", DetKit.PracticeRecord(1, 10)), "--bogus", "1").Code);
        }

        // 이 도구가 펼친 열 = 코어의 ReplayRunner 가 세계에 넣는 열(틱, 슬롯, 오프셋, 키, 눌림, 순번, 명령까지)
        private sealed class FeedRecorder : IReplayObserver
        {
            public readonly List<string> Fed = new List<string>();

            public void BeforeStep(SimWorld world, ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands)
            {
                foreach (RunCommand c in commands)
                {
                    Fed.Add(world.Tick + "|c|" + c.Tick + ":" + c.Seq + ":" + c.Slot.Value + ":" + (int)c.Op + ":" + c.A + ":" + c.B);
                }
                foreach (PlayerInput p in inputs)
                {
                    Fed.Add(world.Tick + "|k|" + p.Slot.Value + ":" + p.Event.Tick + ":" + p.Event.Offset + ":" + (int)p.Event.Key + ":" + p.Event.Down + ":" + p.Event.Seq);
                }
            }

            public void AfterStep(SimWorld world)
            {
            }
        }

        private static List<string> Expand(RecordFeed feed)
        {
            var list = new List<string>();
            for (uint t = 0; t < feed.EndTick; t++)
            {
                foreach (RunCommand c in feed.CommandsAt(t))
                {
                    list.Add(t + "|c|" + c.Tick + ":" + c.Seq + ":" + c.Slot.Value + ":" + (int)c.Op + ":" + c.A + ":" + c.B);
                }
                foreach (PlayerInput p in feed.InputsAt(t))
                {
                    list.Add(t + "|k|" + p.Slot.Value + ":" + p.Event.Tick + ":" + p.Event.Offset + ":" + (int)p.Event.Key + ":" + p.Event.Down + ":" + p.Event.Seq);
                }
            }
            return list;
        }

        [Fact]
        public void FeedMatchesWhatReplayRunnerFeedsTheWorld()
        {
            // 무한 웨이브(명령이 없는 세계라 연습 기록의 명령은 무효가 된다. 명령은 연습 쪽에서 아래와 같이 본다)
            byte[] run = DetKit.EndlessRecord(0xFEED_0001UL, 0xFEED_0002UL, 900, "dense");
            RecordFeed feed = DetKit.Feed(run);
            Assert.True(EndlessSetup.TryReadCanonicalPrefix(run, out EndlessSetup? setup, out int headerLength));
            var header = new InputRecordHeader { FormatVersion = EndlessSetup.CanonicalFormatVersion, Setup = setup! };
            var observer = new FeedRecorder();
            ReplayResult r = ReplayRunner.RunCore(DetKit.Db, header, run.AsSpan(headerLength), GoldenRunner.Limits, 0, observer);
            Assert.Equal(ReplayStatus.EndedLate, r.Status);                                 // M1 에는 판을 끝내는 모듈이 없다
            Assert.Equal(900U, r.TicksSimulated);
            Assert.Equal(Expand(feed), observer.Fed);

            byte[] duel = DetKit.DuelRecord(0xFEED_0003UL, 0xFEED_0004UL, 600, "dense");
            var duelObserver = new FeedRecorder();
            DuelReplayResult d = ReplayRunner.RunDuelCore(DetKit.Db, duel, GoldenRunner.Limits, duelObserver);
            Assert.Equal(600U, d.TicksSimulated);
            Assert.Equal(Expand(DetKit.Feed(duel)), duelObserver.Fed);

            // 명령: 연습 기록의 명령 레코드와 순번(명령과 키가 같은 공간)
            RecordFeed practice = DetKit.Feed(DetKit.PracticeRecord(0xFEED_0005UL, DetKit.Minute, "loadout"));
            Assert.True(practice.CommandCount > 10);
            uint last = 0;
            for (uint t = 0; t < practice.EndTick; t++)
            {
                foreach (RunCommand c in practice.CommandsAt(t))
                {
                    Assert.Equal(t, c.Tick);
                    Assert.True(c.Seq == last + 1, "commands come first in a tick and share the sequence space with keys");
                    last = c.Seq;
                }
                foreach (PlayerInput p in practice.InputsAt(t))
                {
                    Assert.Equal(last + 1, p.Event.Seq);
                    last = p.Event.Seq;
                }
            }
        }
    }
}
