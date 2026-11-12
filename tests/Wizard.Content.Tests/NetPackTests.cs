using System;
using System.Collections.Generic;
using Wizard.Content.Rules;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/rules/net.json → 팩 절 34 → ContentDb.Net 의 왕복(M2-DAT-01).
    // 기대값: spec 10 14.5(입력 지연 표), 14.7(되감기 한도 8), 14.8(순서 위반 30), 14.9(관전 2초 = 120틱),
    //   2.5·3.7·16(끊김 유예 20초 = 1200틱), online 4.12(자동 매치 왕복 120ms), CONTRACTS 2.2·6.4(관전 키프레임 60틱).
    // 명세에 값이 없는 것은 [총괄 제안]이고 아래 "Proposed_" 테스트가 따로 모은다(M2-NET-06 이 측정으로 고치면 그 테스트가 바뀐다).
    public sealed class NetPackTests
    {
        private static readonly Lazy<byte[]> RepoPack = new Lazy<byte[]>(() => BuildPack(TestRepo.Root));

        private static byte[] BuildPack(string root)
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return o.Pack!;
        }

        private static List<string> Problems(string root)
        {
            var d = new Diagnostics();
            Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            return new List<string>(d.Sorted());
        }

        private static NetRuleSet RepoNet() => ContentDb.Build(ContentPack.Load(RepoPack.Value)).Net;

        [Fact]
        public void InputDelayTable_FollowsSpec10_14_5()
        {
            InputDelayStep[] t = RepoNet().InputDelayTable;
            // spec 10 14.5·online 4.12: 60ms 이하 2틱, 90ms 이하 3틱, 120ms 이하 4틱
            Assert.Equal(3, t.Length);
            Assert.Equal((60, 2), (t[0].RttUpToMs, t[0].DelayTicks));
            Assert.Equal((90, 3), (t[1].RttUpToMs, t[1].DelayTicks));
            Assert.Equal((120, 4), (t[2].RttUpToMs, t[2].DelayTicks));
        }

        [Theory]
        [InlineData(0, 2)]
        [InlineData(60, 2)]      // spec 10 14.5 ChooseD: rttMs <= 60 ? 2
        [InlineData(61, 3)]
        [InlineData(90, 3)]
        [InlineData(91, 4)]
        [InlineData(120, 4)]
        [InlineData(121, 4)]     // 120 초과도 4틱 고정(마지막 줄의 D)
        [InlineData(150, 4)]
        public void InputDelayTable_GivesChooseDOfSpec10_14_5(int rttMs, int expected)
        {
            // 표를 읽는 규칙(첫 줄부터 rtt <= 경계인 줄, 없으면 마지막 줄의 D)이 spec 10 14.5 의 ChooseD 와 같은 값을 낸다.
            // 규칙의 구현은 M2-NET-03(ClockSync.ChooseInputDelay)이 가진다. 여기서는 데이터가 그 규칙으로 같은 답을 내는지만 본다
            Assert.Equal(expected, Lookup(RepoNet().InputDelayTable, rttMs));
        }

        [Fact]
        public void RollbackLimit_Is8Ticks()
        {
            Assert.Equal(8, RepoNet().RollbackLimitTicks);        // spec 10 14.7
        }

        [Fact]
        public void DisconnectGrace_Is20SecondsAs1200Ticks()
        {
            Assert.Equal(1200, RepoNet().DisconnectGraceTicks);   // spec 10 2.5 disconnect_wait "서버 시각 20s(1,200틱)"
        }

        [Fact]
        public void OrderViolationForfeit_Is30()
        {
            Assert.Equal(30, RepoNet().OrderViolationForfeit);    // spec 10 14.8 "순서 위반으로 버린 패킷이 30개를 넘으면"
        }

        [Fact]
        public void QueueRttLimit_Is120Ms()
        {
            Assert.Equal(120, RepoNet().QueueRttLimitMs);         // spec 10 14.5 "120ms 초과: 자동 매치 불가"
        }

        [Fact]
        public void Spectator_Delay120Ticks_Keyframe60Ticks()
        {
            NetRuleSet n = RepoNet();
            Assert.Equal(120, n.SpectatorDelayTicks);             // spec 10 14.9 "2초(120틱)"
            Assert.Equal(60, n.SpectatorKeyframeTicks);           // CONTRACTS 2.2 "// 120, 60"
        }

        [Fact]
        public void Proposed_ValuesNotInTheSpec_AreTheOverseersFirstValues()
        {
            // [총괄 제안]: data/rules/net.json 의 "_note" 가 근거를 적는다. M2-NET-06 의 측정으로 고친다
            NetRuleSet n = RepoNet();
            Assert.Equal(60, n.DisconnectSilenceTicks);           // 1.0초
            Assert.Equal(30, n.FullStateGapTicks);
            Assert.Equal(8, n.CatchupTicksPerAdvance);
            Assert.Equal(50, n.RttHoldCapMs);
        }

        [Fact]
        public void ProposedValues_StayInsideTheProtocolLimits()
        {
            // M2-NET-06 의 시나리오가 끊김·전체 상태 요청으로 잡히면 안 된다: 300ms(18틱) 수신 공백 → 전체 상태 요청 0, 끊김 아님
            NetRuleSet n = RepoNet();
            const int ticksPerSecond = 60;
            int gap300MsTicks = 300 * ticksPerSecond / 1000;
            Assert.True(n.FullStateGapTicks > gap300MsTicks);
            Assert.True(n.DisconnectSilenceTicks > gap300MsTicks);
            Assert.True(n.DisconnectGraceTicks > n.DisconnectSilenceTicks);
        }

        [Fact]
        public void SectionMissing_Throws()
        {
            ContentDb db = ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new PackSection[0])));
            Assert.Throws<ContentException>(() => db.Net);
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void SectionBytes_HaveTheDocumentedLayout()
        {
            // 절 바이트의 꼴(ContentDb.Net.cs 머리 주석): u16 N + (i32, i32) × N + i32 × 10 = 2 + 24 + 40
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(2 + 3 * 8 + 10 * 4, s.Length);
            Assert.Equal(3, s[0] | (s[1] << 8));
        }

        [Fact]
        public void Problems_SchemaViolations()
        {
            using (TempDataRepo repo = Repo(Net(extra: ", \"surprise\": 1")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(Net(rollback: "0")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/rollback_limit_ticks") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(Net(grace: "0")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/disconnect_grace_seconds") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(Net(table: "[]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/input_delay_table") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo(Net(table: "[{ \"rtt_up_to_ms\": 60 }]")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/input_delay_table/0") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void Problems_TableMustAscend()
        {
            // 경계가 같다 / 줄어든다 / D 가 줄어든다
            using (TempDataRepo repo = Repo(Net(table: Table((60, 2), (60, 3)))))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/input_delay_table/1/rtt_up_to_ms"));
            }
            using (TempDataRepo repo = Repo(Net(table: Table((90, 3), (60, 3)))))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/input_delay_table/1/rtt_up_to_ms"));
            }
            using (TempDataRepo repo = Repo(Net(table: Table((60, 3), (90, 2)))))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/input_delay_table/1/delay_ticks"));
            }
        }

        [Fact]
        public void Problems_SecondsThatRoundToZeroTicks()
        {
            using (TempDataRepo repo = Repo(Net(silence: "0.001")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/disconnect_silence_seconds") && p.Contains("0"));
            }
        }

        [Fact]
        public void Problems_NonIntegerCount()
        {
            using (TempDataRepo repo = Repo(Net(rollback: "8.5")))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/rules/net.json:/rollback_limit_ticks"));
            }
        }

        [Fact]
        public void SecondsConvertToTicks_HalfEven()
        {
            // 1.0초 → 60, 20초 → 1200, 2.0초 → 120 (spec 14 0.3: 가장 가까운 정수, 같으면 짝수). 0.0125초 = 0.75틱 → 1
            using (TempDataRepo repo = Repo(Net(silence: "0.0125", grace: "20.5", spectator: "0.025")))
            {
                var d = new Diagnostics();
                Pipeline.Output o = Pipeline.Run(repo.Root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
                Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
                NetRuleSet n = ContentDb.Build(ContentPack.Load(o.Pack!)).Net;
                Assert.Equal(1, n.DisconnectSilenceTicks);         // 0.75 → 1
                Assert.Equal(1230, n.DisconnectGraceTicks);        // 20.5 × 60
                Assert.Equal(2, n.SpectatorDelayTicks);            // 1.5 → 2 (같으면 짝수)
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Load(good);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            Assert.Throws<ContentException>(() => Load(new byte[0]));
            // 표가 비었다
            byte[] empty = (byte[])good.Clone();
            empty[0] = 0;
            empty[1] = 0;
            Assert.Throws<ContentException>(() => Load(empty));
            // 표 순서: 둘째 줄의 경계(오프셋 2 + 8)를 첫 줄(60)과 같게
            byte[] order = (byte[])good.Clone();
            order[10] = 60;
            Assert.Throws<ContentException>(() => Load(order));
            // 1 미만: 되감기 한도(표 뒤 첫 칸, 오프셋 2 + 24)를 0 으로
            byte[] zero = (byte[])good.Clone();
            zero[26] = 0;
            Assert.Throws<ContentException>(() => Load(zero));
        }

        // 표를 읽는 규칙의 참조 구현(테스트 전용). 계약: CONTRACTS 2.2 의 주석과 spec 10 14.5
        private static int Lookup(InputDelayStep[] table, int rttMs)
        {
            foreach (InputDelayStep s in table)
            {
                if (rttMs <= s.RttUpToMs)
                {
                    return s.DelayTicks;
                }
            }
            return table[table.Length - 1].DelayTicks;
        }

        private static string Table(params (int rtt, int delay)[] rows)
        {
            var parts = new List<string>();
            foreach ((int rtt, int delay) in rows)
            {
                parts.Add("{ \"rtt_up_to_ms\": " + rtt + ", \"delay_ticks\": " + delay + " }");
            }
            return "[" + string.Join(", ", parts) + "]";
        }

        private static string Net(string table = "", string rollback = "8", string silence = "1.0", string grace = "20",
            string spectator = "2.0", string extra = "")
        {
            if (table.Length == 0)
            {
                table = Table((60, 2), (90, 3), (120, 4));
            }
            return "{ \"input_delay_table\": " + table + ", \"rollback_limit_ticks\": " + rollback
                + ", \"disconnect_silence_seconds\": " + silence + ", \"disconnect_grace_seconds\": " + grace
                + ", \"full_state_gap_ticks\": 30, \"catchup_ticks_per_advance\": 8, \"rtt_hold_cap_ms\": 50"
                + ", \"queue_rtt_limit_ms\": 120, \"order_violation_forfeit\": 30, \"spectator_delay_seconds\": " + spectator
                + ", \"spectator_keyframe_ticks\": 60" + extra + " }\n";
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Net, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Net, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string net)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/net.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/net.schema.json")));
            repo.Write("data/rules/net.json", net);
            return repo;
        }
    }
}
