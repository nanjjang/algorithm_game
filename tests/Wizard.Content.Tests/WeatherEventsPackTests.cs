using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/weather_events.json → 팩 절 16 → ContentDb.WeatherEvents 의 왕복(M8-DAT-03, DECISIONS-TECH T-55).
    // 기대값은 world-characters 2.3 의 표(소나기: 낮·해질녘, 하루 1회 이하, 60초 / 안개: 해질녘, 가장자리 3칸 / 별똥 결정: 밤, 결정 +1 /
    // 느루의 훈수: 아무 때나, 두 줄 / 버린 물건: 날이 바뀔 때), spec 14 3.9(event.* 코드 1~5), spec 14 4.7(버린 물건 그림 6종)에서 온다.
    // 원문이 정하지 않은 값은 [임시]로 적었다(OPEN-QUESTIONS Q-19, Q-20): 소나기 10%, 안개 20%, 훈수 5%, 안개는 그 웨이브가 끝날 때까지.
    public sealed class WeatherEventsPackTests
    {
        private const long RawPerCell = 4294967296L;                // 2^32

        // 절 바이트의 꼴(ContentDb.WeatherEvents.cs 의 머리 주석): u16 개수 + 사건마다
        //   u8 id, u8 affects_rules, u8 on_day_change, u8 tod_mask, i32 chance_bp, i32 max_per_day, u8 is_instant, i32 duration_ticks, i64 edge_width_raw, i32 variant_count
        private const int Header = 2;
        private const int EntrySize = 1 + 1 + 1 + 1 + 4 + 4 + 1 + 4 + 8 + 4;
        private const int AtAffectsRules = 1;
        private const int AtOnDayChange = 2;
        private const int AtTodMask = 3;
        private const int AtChance = 4;
        private const int AtMaxPerDay = 8;
        private const int AtIsInstant = 12;
        private const int AtDuration = 13;
        private const int AtEdge = 17;
        private const int AtVariants = 25;

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

        private static ContentDb RepoDb() => ContentDb.Build(ContentPack.Load(RepoPack.Value));

        private static WeatherEventDef Get(WeatherEventId id)
        {
            foreach (WeatherEventDef d in RepoDb().WeatherEvents)
            {
                if (d.Id == id)
                {
                    return d;
                }
            }
            throw new Xunit.Sdk.XunitException("no event " + id);
        }

        private static int Bit(TimeOfDay tod) => 1 << (int)tod;

        [Fact]
        public void Spec14_3_9_FiveEventsInCodeOrder()
        {
            // spec 14 3.9: event.shower, event.fog, event.star_crystal, event.master_advice, event.discarded_item (코드 1~5)
            IReadOnlyList<WeatherEventDef> all = RepoDb().WeatherEvents;
            Assert.Equal(5, all.Count);
            Assert.Equal(WeatherEventId.Shower, all[0].Id);
            Assert.Equal(WeatherEventId.Fog, all[1].Id);
            Assert.Equal(WeatherEventId.StarCrystal, all[2].Id);
            Assert.Equal(WeatherEventId.MasterAdvice, all[3].Id);
            Assert.Equal(WeatherEventId.DiscardedItem, all[4].Id);
        }

        [Fact]
        public void Wc2_3_Shower()
        {
            // 낮·해질녘, 하루 1회 이하, 60초, 규칙 영향 없음(연출)
            WeatherEventDef d = Get(WeatherEventId.Shower);
            Assert.False(d.AffectsRules);
            Assert.False(d.OnDayChange);
            Assert.Equal(Bit(TimeOfDay.Day) | Bit(TimeOfDay.Dusk), d.TodMask);
            Assert.True(d.Allows(TimeOfDay.Day));
            Assert.True(d.Allows(TimeOfDay.Dusk));
            Assert.False(d.Allows(TimeOfDay.Night));
            Assert.False(d.Allows(TimeOfDay.BossWave));
            Assert.Equal(1, d.MaxPerDay);
            Assert.False(d.IsInstant);
            Assert.Equal(3600, d.DurationTicks);                // 60초 × 60틱
            Assert.Equal(1000, d.ChanceBp);                     // 10% [임시]
            Assert.Equal(0, d.EdgeWidth.Raw);
            Assert.Equal(1, d.VariantCount);
        }

        [Fact]
        public void Wc2_3_Fog()
        {
            // 해질녘, 가장자리 3칸이 뿌옇다, 규칙 영향 없음(연출)
            WeatherEventDef d = Get(WeatherEventId.Fog);
            Assert.False(d.AffectsRules);
            Assert.False(d.OnDayChange);
            Assert.Equal(Bit(TimeOfDay.Dusk), d.TodMask);
            Assert.False(d.Allows(TimeOfDay.Day));
            Assert.True(d.Allows(TimeOfDay.Dusk));
            Assert.False(d.Allows(TimeOfDay.Night));
            Assert.False(d.Allows(TimeOfDay.BossWave));
            Assert.Equal(3 * RawPerCell, d.EdgeWidth.Raw);      // 12,884,901,888
            Assert.Equal(0, d.MaxPerDay);                       // 원문에 상한이 없다
            Assert.False(d.IsInstant);
            Assert.Equal(0, d.DurationTicks);                   // 그 웨이브가 끝날 때까지 [임시]
            Assert.Equal(2000, d.ChanceBp);                     // 20% [임시]
            Assert.Equal(1, d.VariantCount);
        }

        [Fact]
        public void Wc2_3_StarCrystal_IsTheOnlyRuleEvent_AndItsNumbersLiveInStarfall()
        {
            // 밤, 결정 +1. 수치는 절 15 starfall 한 곳에만 있고 이 행은 자리만 든다
            ContentDb db = RepoDb();
            WeatherEventDef d = Get(WeatherEventId.StarCrystal);
            Assert.True(d.AffectsRules);
            Assert.False(d.OnDayChange);
            Assert.Equal(Bit(TimeOfDay.Night), d.TodMask);
            Assert.True(d.Allows(TimeOfDay.Night));
            Assert.True(d.Allows(db.Starfall.OnlyTod));         // 두 절의 시간대가 어긋나지 않는다
            Assert.Equal(Bit(db.Starfall.OnlyTod), d.TodMask);
            Assert.True(d.IsInstant);
            Assert.Equal(0, d.ChanceBp);
            Assert.Equal(0, d.MaxPerDay);
            Assert.Equal(0, d.DurationTicks);
            Assert.Equal(0, d.EdgeWidth.Raw);
            Assert.Equal(1, d.VariantCount);

            // "규칙 영향" 칸: 나머지 넷은 없음
            foreach (WeatherEventDef e in db.WeatherEvents)
            {
                Assert.Equal(e.Id == WeatherEventId.StarCrystal, e.AffectsRules);
            }
        }

        [Fact]
        public void Wc2_3_MasterAdvice()
        {
            // 아무 때나 드물게, 느루의 한 줄("왼쪽이 비었다." / "나 때는 맨손이었다."), 규칙 영향 없음
            WeatherEventDef d = Get(WeatherEventId.MasterAdvice);
            Assert.False(d.AffectsRules);
            Assert.False(d.OnDayChange);
            Assert.Equal(Bit(TimeOfDay.Day) | Bit(TimeOfDay.Dusk) | Bit(TimeOfDay.Night) | Bit(TimeOfDay.BossWave), d.TodMask);
            Assert.Equal(WeatherEventDef.AllTodsMask, d.TodMask);
            Assert.True(d.IsInstant);
            Assert.Equal(0, d.DurationTicks);
            Assert.Equal(0, d.MaxPerDay);
            Assert.Equal(500, d.ChanceBp);                      // 5% [임시]
            Assert.Equal(0, d.EdgeWidth.Raw);
            Assert.Equal(2, d.VariantCount);                    // 원문의 두 줄
        }

        [Fact]
        public void Wc2_3_MasterAdvice_LinesAreInTheNameTable()
        {
            // 변형마다 이름 표에 줄이 하나 있다(spec 14 2.2 의 접미사 .line.<n>). 글은 world-characters 2.3 의 두 줄
            var names = (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/names/names.ko.json")))!;
            WeatherEventDef d = Get(WeatherEventId.MasterAdvice);
            Assert.Equal("왼쪽이 비었다.", (string?)names["event.master_advice.line.1"]);
            Assert.Equal("나 때는 맨손이었다.", (string?)names["event.master_advice.line.2"]);
            int lines = 0;
            foreach (KeyValuePair<string, JsonNode?> kv in names)
            {
                if (kv.Key.StartsWith("event.master_advice.line.", StringComparison.Ordinal))
                {
                    lines++;
                }
            }
            Assert.Equal(d.VariantCount, lines);
        }

        [Fact]
        public void Wc2_3_DiscardedItem()
        {
            // 날이 바뀔 때 탑 창문에서 물건이 떨어져 새 소품이 된다. 규칙 영향 없음. 그림 6종(spec 14 4.7 "느루가 버린 물건 6")
            WeatherEventDef d = Get(WeatherEventId.DiscardedItem);
            Assert.False(d.AffectsRules);
            Assert.True(d.OnDayChange);
            Assert.Equal(0, d.TodMask);
            foreach (TimeOfDay tod in new[] { TimeOfDay.None, TimeOfDay.Day, TimeOfDay.Dusk, TimeOfDay.Night, TimeOfDay.BossWave })
            {
                Assert.False(d.Allows(tod));
            }
            Assert.Equal(10000, d.ChanceBp);                    // 날이 바뀌면 늘 떨어진다
            Assert.Equal(0, d.MaxPerDay);
            Assert.True(d.IsInstant);
            Assert.Equal(0, d.DurationTicks);
            Assert.Equal(0, d.EdgeWidth.Raw);
            Assert.Equal(6, d.VariantCount);
        }

        [Fact]
        public void Allows_IgnoresNoneAndUnknownBits()
        {
            var d = new WeatherEventDef { TodMask = WeatherEventDef.AllTodsMask | 1 };
            Assert.False(d.Allows(TimeOfDay.None));
            Assert.True(d.Allows(TimeOfDay.Day));
            Assert.True(d.Allows(TimeOfDay.BossWave));
            Assert.Equal(2 + 4 + 8 + 16, WeatherEventDef.AllTodsMask);   // TimeOfDay 코드 1~4 (spec 14 3.9)
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.WeatherEvents);
        }

        [Fact]
        public void SectionBytes_HaveTheDocumentedShape()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(Header + 5 * EntrySize, s.Length);
            Assert.Equal(5, s[0] | (s[1] << 8));
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i + 1, s[Header + i * EntrySize]);         // id 오름차순 1~5
            }
        }

        [Fact]
        public void SameData_SameSectionBytes()
        {
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(TestRepo.Root)));
        }

        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            JsonObject o = RepoJson();
            var rows = (JsonArray)o["events"]!;
            Reverse(rows);
            foreach (JsonNode? row in rows)
            {
                if (row!["tods"] is JsonArray t)
                {
                    Reverse(t);
                }
            }
            using TempDataRepo repo = Repo(o.ToJsonString());
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_UnknownId_Duplicate_Missing()
        {
            JsonObject o = RepoJson();
            o["events"]![0]!["id"] = "event.hail";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/0/id") && p.Contains("event.hail"));
            }
            o = RepoJson();
            o["events"]![1]!["id"] = "event.shower";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/1/id") && p.Contains("두 번"));
            }
            o = RepoJson();
            ((JsonArray)o["events"]!).RemoveAt(4);
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events") && p.Contains("event.discarded_item"));
            }
        }

        [Fact]
        public void Problems_WhenAndTods()
        {
            JsonObject o = RepoJson();
            ((JsonObject)o["events"]![0]!).Remove("tods");                  // 웨이브 사건인데 시간대가 없다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/0/tods"));
            }
            o = RepoJson();
            o["events"]![0]!["tods"] = new JsonArray("tod.day", "tod.noon");  // 모르는 시간대
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/0/tods/1") && p.Contains("tod.noon"));
            }
            o = RepoJson();
            o["events"]![4]!["tods"] = new JsonArray("tod.day");            // 날 바뀜 사건에 시간대
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/4/tods"));
            }
            o = RepoJson();
            o["events"]![4]!["max_per_day"] = 1;                            // 날 바뀜 사건에 하루 상한
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/4/max_per_day"));
            }
        }

        [Fact]
        public void Problems_SpanAndDuration()
        {
            JsonObject o = RepoJson();
            ((JsonObject)o["events"]![0]!).Remove("duration_seconds");      // timed 인데 길이가 없다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/0/span"));
            }
            o = RepoJson();
            o["events"]![1]!["duration_seconds"] = 30;                      // wave 인데 길이가 있다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/1/duration_seconds"));
            }
            o = RepoJson();
            o["events"]![0]!["duration_seconds"] = 0.001;                   // 0 틱이 된다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/0/duration_seconds") && p.Contains("0 틱"));
            }
        }

        [Fact]
        public void Problems_RuleEventsKeepTheirNumbersInStarfall()
        {
            JsonObject o = RepoJson();
            o["events"]![0]!["affects_rules"] = true;                       // 소나기가 규칙에 영향을 준다고 적음
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/0/affects_rules") && p.Contains("event.star_crystal"));
            }
            o = RepoJson();
            o["events"]![2]!["affects_rules"] = false;                      // 별똥 결정이 연출이라고 적음
            o["events"]![2]!["chance_percent"] = 50;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/2/affects_rules"));
            }
            foreach (string field in new[] { "chance_percent", "max_per_day", "edge_width", "variants" })
            {
                o = RepoJson();
                o["events"]![2]![field] = 2;                                // 별똥 결정의 줄에 수치
                using (TempDataRepo repo = Repo(o.ToJsonString()))
                {
                    Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/2/" + field) && p.Contains("starfall.json"));
                }
            }
            o = RepoJson();
            o["events"]![2]!["span"] = "wave";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/2/span"));
            }
            o = RepoJson();
            ((JsonObject)o["events"]![3]!).Remove("chance_percent");        // 연출 사건에 확률이 없다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json:/events/3/chance_percent"));
            }
        }

        [Fact]
        public void Problems_Schema()
        {
            var edits = new Action<JsonObject>[]
            {
                o => o["events"]![0]!["chance_percent"] = 101,
                o => o["events"]![0]!["chance_percent"] = -1,
                o => o["events"]![0]!["max_per_day"] = 0,
                o => o["events"]![3]!["variants"] = 0,
                o => o["events"]![1]!["edge_width"] = 0,
                o => o["events"]![0]!["when"] = "camp",
                o => o["events"]![0]!["span"] = "forever",
                o => o["events"]![0]!["tods"] = new JsonArray("tod.day", "tod.day"),
                o => o["events"]![0]!["tods"] = new JsonArray(),
                o => ((JsonObject)o["events"]![0]!).Remove("span"),
                o => ((JsonObject)o["events"]![0]!).Remove("affects_rules"),
                o => o["events"]![0]!["heat_multiplier"] = 1.2,         // 모르는 필드(속성에 유리한 날씨는 넣지 않는다, world-characters 2.3)
                o => o["events"]![0]!["id"] = "whim.shower",
            };
            foreach (Action<JsonObject> edit in edits)
            {
                JsonObject o = RepoJson();
                edit(o);
                using (TempDataRepo repo = Repo(o.ToJsonString()))
                {
                    Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/weather_events.json") && p.Contains("스키마"));
                }
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            Assert.Throws<ContentException>(() => Load(new byte[0]));
            Assert.Throws<ContentException>(() => Load(new byte[1]));

            int shower = Header;                                // id 1
            int fog = Header + EntrySize;                       // id 2
            int discarded = Header + 4 * EntrySize;             // id 5 (날 바뀜)

            // 모르는 id, None, 순서 위반·겹침
            foreach (byte id in new byte[] { 0, 6, 255 })
            {
                AssertBroken(good, b => b[shower] = id);
            }
            AssertBroken(good, b => b[fog] = 1);
            AssertBroken(good, b => { b[shower] = 2; b[fog] = 1; });

            // 0/1 이 아닌 bool
            AssertBroken(good, b => b[shower + AtAffectsRules] = 2);
            AssertBroken(good, b => b[shower + AtOnDayChange] = 2);
            AssertBroken(good, b => b[shower + AtIsInstant] = 2);

            // 시간대 비트: None 의 비트, 모르는 비트, 웨이브 사건인데 0, 날 바뀜 사건인데 비트가 있다
            AssertBroken(good, b => b[shower + AtTodMask] |= 1);
            AssertBroken(good, b => b[shower + AtTodMask] |= 32);
            AssertBroken(good, b => b[shower + AtTodMask] = 0);
            AssertBroken(good, b => b[discarded + AtTodMask] = 2);
            AssertBroken(good, b => b[shower + AtOnDayChange] = 1);

            // 범위 밖의 수
            AssertBroken(good, b => WriteLe(b, shower + AtChance, 10001));
            AssertBroken(good, b => WriteLe(b, shower + AtChance, -1));
            AssertBroken(good, b => WriteLe(b, shower + AtMaxPerDay, -1));
            AssertBroken(good, b => WriteLe(b, shower + AtDuration, -1));
            AssertBroken(good, b => WriteLe(b, discarded + AtDuration, 60));    // 한 번 일어나고 끝나는 사건에 길이
            AssertBroken(good, b => b[fog + AtEdge + 7] = 0x80);                // 음수 폭
            AssertBroken(good, b => WriteLe(b, shower + AtVariants, 0));

            Load(good);
        }

        private static void AssertBroken(byte[] good, Action<byte[]> edit)
        {
            byte[] bad = (byte[])good.Clone();
            edit(bad);
            Assert.Throws<ContentException>(() => Load(bad).WeatherEvents);
        }

        private static void WriteLe(byte[] b, int at, int v)
        {
            for (int i = 0; i < 4; i++)
            {
                b[at + i] = (byte)(v >> (8 * i));
            }
        }

        private static void Reverse(JsonArray a)
        {
            var items = new List<JsonNode?>();
            while (a.Count > 0)
            {
                JsonNode? n = a[a.Count - 1];
                a.RemoveAt(a.Count - 1);
                items.Add(n);
            }
            foreach (JsonNode? n in items)
            {
                a.Add(n);
            }
        }

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/weather_events.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.WeatherEvents, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.WeatherEvents, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string events)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/weather_events.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/weather_events.schema.json")));
            repo.Write("data/sim/weather_events.json", events + "\n");
            repo.Write("data/names/names.ko.json", "{\n  \"event.discarded_item\": \"버린 물건\",\n  \"event.fog\": \"안개\",\n  \"event.master_advice\": \"느루의 훈수\",\n  \"event.shower\": \"소나기\",\n  \"event.star_crystal\": \"별똥 결정\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}
