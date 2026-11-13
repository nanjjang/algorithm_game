using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/modifiers.json → 팩 절 9 → ContentDb.Modifiers 의 왕복(M8-DAT-03).
    // 기대값은 spec 07 2.5 표(변덕 8행: 효과의 코어 파라미터, 점수 배율, 조건), spec 07 9절([임시] 세부), spec 14 3.9(코드 1~8, 점수 배율 100배 정수),
    // spec 07 17절 TV-105·TV-107·TV-108·TV-109 의 데이터 쪽 값에서 온다. id 는 spec 14 3.9 의 whim.*(CONTRACTS 2.1 대응표).
    public sealed class ModifiersPackTests
    {
        private const long RawPerCell = 4294967296L;                // 2^32

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

        private static IReadOnlyList<ModifierDef> Modifiers() => ContentDb.Build(ContentPack.Load(RepoPack.Value)).Modifiers;

        private static ModifierDef Get(ModifierId id)
        {
            foreach (ModifierDef d in Modifiers())
            {
                if (d.Id == id)
                {
                    return d;
                }
            }
            throw new InvalidOperationException("no modifier " + id);
        }

        [Fact]
        public void Spec07_2_5_EightModifiers_InModifierIdOrder()
        {
            // spec 07 9절 "데이터 순서 고정": 후보 순서는 ModifierId(spec 14 3.9 코드 1~8) 오름차순
            IReadOnlyList<ModifierDef> m = Modifiers();
            Assert.Equal(8, m.Count);
            var ids = new ModifierId[m.Count];
            for (int i = 0; i < m.Count; i++)
            {
                ids[i] = m[i].Id;
            }
            Assert.Equal(new[]
            {
                ModifierId.Rush, ModifierId.ThickHide, ModifierId.BigHerd, ModifierId.ChiefMarch,
                ModifierId.LastBreath, ModifierId.SlowBarrage, ModifierId.LongNight, ModifierId.NarrowField,
            }, ids);
        }

        [Fact]
        public void Spec14_3_9_ScorePct_ByCode()
        {
            // spec 14 3.9 점수 배율(100배 정수): 115, 115, 115, 125, 120, 120, 120, 125 (spec 07 2.5, monsters 4.5 ×1.15 …)
            IReadOnlyList<ModifierDef> m = Modifiers();
            var pct = new int[m.Count];
            for (int i = 0; i < m.Count; i++)
            {
                pct[i] = m[i].ScorePct;
            }
            Assert.Equal(new[] { 115, 115, 115, 125, 120, 120, 120, 125 }, pct);
        }

        [Fact]
        public void Spec07_2_5_OnlyLongNightIsNightOnly()
        {
            // spec 07 2.5 조건 "밤 웨이브에만"은 긴 밤 하나. 나머지는 모든 시간대(TV-102: 낮 w=22 의 후보에서 long_night 만 빠진다)
            foreach (ModifierDef d in Modifiers())
            {
                Assert.Equal(d.Id == ModifierId.LongNight ? TimeOfDay.Night : TimeOfDay.None, d.OnlyTod);
            }
        }

        [Fact]
        public void Spec07_2_5_Crowding()
        {
            // 몰려옴: bundle_interval_ticks 420 → 240 (7초 → 4초)
            ModifierDef d = Get(ModifierId.Rush);
            Assert.Equal(240, d.BundleIntervalTicks);
            AssertNeutralExcept(d, "bundle");
        }

        [Fact]
        public void Spec07_2_5_ThickHide()
        {
            // 두꺼운 가죽: 적 체력 +30% (hp_pct = 130)
            ModifierDef d = Get(ModifierId.ThickHide);
            Assert.Equal(130, d.EnemyHpPct);
            AssertNeutralExcept(d, "hp");
        }

        [Fact]
        public void Spec07_2_5_BigPack()
        {
            // 큰 무리: 예산 +25%, 추가분은 비용 1~2짜리로만 (extra_budget_pct = 25, extra_cost_max = 2)
            ModifierDef d = Get(ModifierId.BigHerd);
            Assert.Equal(25, d.ExtraBudgetPct);
            Assert.Equal(2, d.ExtraBudgetCostMax);
            AssertNeutralExcept(d, "budget");
        }

        [Fact]
        public void Spec07_2_5_ChiefParade()
        {
            // 우두머리 행차: 엘리트 +2 (extra_elites = 2)
            ModifierDef d = Get(ModifierId.ChiefMarch);
            Assert.Equal(2, d.ExtraElites);
            AssertNeutralExcept(d, "elites");
        }

        [Fact]
        public void Spec07_TV105_LastBreath_CircleValues()
        {
            // TV-105: 반경 1.2칸(Q 5,153,960,755 = round(1.2 × 2^32 = 5,153,960,755.2)), 예고 48틱(0.8초), 피해 1.
            // TV-106·spec 07 2.5: R5 에 걸리면 늦게 생긴 원부터 0.2초(12틱)씩 미룬다
            ModifierDef d = Get(ModifierId.LastBreath);
            Assert.Equal(5153960755L, d.DeathCircleRadius.Raw);
            Assert.Equal(48, d.DeathCircleTelegraphTicks);
            Assert.Equal(1, d.DeathCircleDamage);
            Assert.Equal(12, d.DeathCircleStaggerTicks);
            AssertNeutralExcept(d, "circle");
        }

        [Fact]
        public void Spec07_2_5_SlowBarrage()
        {
            // 느린 탄막: 적 탄 수 +50% (proj_count_pct = 150), 탄 속도 −20% (proj_speed_pct = 80)
            ModifierDef d = Get(ModifierId.SlowBarrage);
            Assert.Equal(150, d.EnemyShotCountPct);
            Assert.Equal(80, d.EnemyShotSpeedPct);
            AssertNeutralExcept(d, "shots");
        }

        [Fact]
        public void Spec07_2_5_LongNight()
        {
            // 긴 밤: 박쥐·도깨비불 가중치 다시 ×2(night 배율에 200% 곱 → 400), wisp_stray 종 상한 6, 밤 웨이브에만
            ModifierDef d = Get(ModifierId.LongNight);
            Assert.Equal(new[] { EnemyTypeId.BatDusk, EnemyTypeId.WispStray }, d.WeightSpecies);
            Assert.Equal(new[] { 200, 200 }, d.WeightPct);
            Assert.Equal(new[] { EnemyTypeId.WispStray }, d.CapSpecies);
            Assert.Equal(new[] { 6 }, d.Cap);
            Assert.Equal(TimeOfDay.Night, d.OnlyTod);
            AssertNeutralExcept(d, "night");
        }

        [Fact]
        public void Spec07_TV107_NarrowField_ThornBorder()
        {
            // TV-107: 180틱 예고 뒤 웨이브 0틱, 가장자리 2칸 가시밭(Q32.32 = 8,589,934,592, spec 07 9절).
            // spec 07 9절 [임시]: 같은 가시밭에서 1.5초(90틱)에 1회, 피해 1
            ModifierDef d = Get(ModifierId.NarrowField);
            Assert.Equal(2 * RawPerCell, d.ThornBorderWidth.Raw);
            Assert.Equal(8589934592L, d.ThornBorderWidth.Raw);
            Assert.Equal(180, d.ThornTelegraphTicks);
            Assert.Equal(90, d.ThornContactIntervalTicks);
            Assert.Equal(1, d.ThornDamage);
            AssertNeutralExcept(d, "thorn");
        }

        [Fact]
        public void Spec07_TV108_TwoModifiers_ScoreMultipliesInTurn()
        {
            // TV-108: 몰려옴 + 좁은 들, 원점수 10,000 → × 115/100 = 11,500 → × 125/100 = 14,375 (spec 07 12.2: 변덕마다 100으로 나눠 내림)
            long score = 10000;
            score = score * Get(ModifierId.Rush).ScorePct / 100;
            Assert.Equal(11500, score);
            score = score * Get(ModifierId.NarrowField).ScorePct / 100;
            Assert.Equal(14375, score);
        }

        [Fact]
        public void Spec07_2_5_NoModifierShortensTelegraphsOrSpeedsEnemies()
        {
            // spec 07 2.5 "예고 시간을 줄이거나 적을 빠르게 하는 변덕은 만들지 않는다": 탄 속도 배율은 100 이하, 위험 구역은 예고가 있다
            foreach (ModifierDef d in Modifiers())
            {
                Assert.True(d.EnemyShotSpeedPct <= ModifierDef.NeutralPct, d.Id + " speeds enemy shots up");
                Assert.True(d.DeathCircleRadius.Raw == 0 || d.DeathCircleTelegraphTicks > 0, d.Id + " circle without telegraph");
                Assert.True(d.ThornBorderWidth.Raw == 0 || d.ThornTelegraphTicks > 0, d.Id + " thorns without telegraph");
            }
        }

        [Fact]
        public void Spec07_TV109_ForbiddenField_FailsTheBuild_WithTheRepoSchema()
        {
            // TV-109: ModifierDef 에 예고 시간 감소 필드 → 데이터 로드 실패. 저장소의 스키마는 객체를 닫아 두어 스키마 단계에서 막는다
            JsonObject o = RepoJson();
            o["modifiers"]![0]!["telegraph_multiplier"] = 0.5;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                List<string> p = Problems(repo.Root);
                Assert.Contains(p, x => x.Contains("data/sim/modifiers.json:/modifiers/0") && x.Contains("스키마") && x.Contains("telegraph_multiplier"));
            }
            o = RepoJson();
            o["modifiers"]![5]!["enemy_shots"]!["move_speed_multiplier"] = 1.2;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), x => x.Contains("data/sim/modifiers.json:/modifiers/5/enemy_shots") && x.Contains("스키마"));
            }
        }

        [Fact]
        public void Spec07_TV109_ForbiddenField_FailsTheBuild_EvenWithAnOpenSchema()
        {
            // 스키마가 열려 있어도(누가 additionalProperties 를 풀어도) 변환기가 금지 필드로 거부한다
            string open = OpenSchema();
            JsonObject o = RepoJson();
            o["modifiers"]![0]!["telegraph_seconds"] = 0.3;
            o["modifiers"]![1]!["enemy_move_speed_multiplier"] = 1.1;
            o["modifiers"]![4]!["death_circle"]!["telegraph_scale"] = 0.5;
            o["modifiers"]![2]!["confetti"] = 1;
            using (TempDataRepo repo = Repo(o.ToJsonString(), open))
            {
                List<string> p = Problems(repo.Root);
                Assert.Contains(p, x => x.Contains("data/sim/modifiers.json:/modifiers/0/telegraph_seconds") && x.Contains("금지 필드"));
                Assert.Contains(p, x => x.Contains("data/sim/modifiers.json:/modifiers/1/enemy_move_speed_multiplier") && x.Contains("금지 필드"));
                Assert.Contains(p, x => x.Contains("data/sim/modifiers.json:/modifiers/4/death_circle/telegraph_scale") && x.Contains("금지 필드"));
                Assert.Contains(p, x => x.Contains("data/sim/modifiers.json:/modifiers/2/confetti") && x.Contains("모르는 필드"));
            }
            // 열린 스키마로도 저장소의 데이터 그대로는 통과한다(같은 절 바이트)
            using (TempDataRepo repo = Repo(RepoJson().ToJsonString(), open))
            {
                Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
            }
        }

        [Fact]
        public void Spec07_2_5_FasterEnemyShots_FailTheBuild()
        {
            // 탄 속도 배율 > 1 은 "적을 빠르게 하는 변덕": 저장소 스키마(maximum 1)와 변환기 둘 다 거부한다
            JsonObject o = RepoJson();
            o["modifiers"]![5]!["enemy_shots"]!["speed_multiplier"] = 1.2;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), x => x.Contains("data/sim/modifiers.json:/modifiers/5/enemy_shots/speed_multiplier") && x.Contains("스키마"));
            }
            // 스키마의 상한을 풀어도 변환기가 막는다
            JsonNode open = JsonNode.Parse(OpenSchema())!;
            ((JsonObject)open["$defs"]!["modifier"]!["properties"]!["enemy_shots"]!["properties"]!["speed_multiplier"]!).Remove("maximum");
            using (TempDataRepo repo = Repo(o.ToJsonString(), open.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), x => x.Contains("data/sim/modifiers.json:/modifiers/5/enemy_shots/speed_multiplier") && x.Contains("금지"));
            }
        }

        [Fact]
        public void ModifiersList_IsReadOnly()
        {
            IReadOnlyList<ModifierDef> m = Modifiers();
            Assert.False(m is ModifierDef[]);
            Assert.True(((ICollection<ModifierDef>)m).IsReadOnly);
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.Modifiers);
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
            var rows = (JsonArray)o["modifiers"]!;
            Reverse(rows);
            foreach (JsonNode? row in rows)
            {
                if (row!["weights"] is JsonArray w)
                {
                    Reverse(w);
                }
            }
            using TempDataRepo repo = Repo(o.ToJsonString());
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_UnknownId_Duplicate_Missing_BossTod_NoEffect_Species_Schema()
        {
            JsonObject o = RepoJson();
            o["modifiers"]![0]!["id"] = "whim.fog";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/0/id") && p.Contains("whim.fog"));
            }
            o = RepoJson();
            o["modifiers"]![7]!["id"] = "whim.crowding";             // 몰려옴이 둘, 좁은 들이 없다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                List<string> p = Problems(repo.Root);
                Assert.Contains(p, x => x.Contains("data/sim/modifiers.json:/modifiers/7/id") && x.Contains("두 번"));
            }
            o = RepoJson();
            ((JsonArray)o["modifiers"]!).RemoveAt(7);
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers") && p.Contains("whim.narrow_field") && p.Contains("없다"));
            }
            o = RepoJson();
            o["modifiers"]![6]!["only_tod"] = "tod.boss";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/6/only_tod") && p.Contains("보스"));
            }
            o = RepoJson();
            o["modifiers"]![6]!["only_tod"] = "tod.noon";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/6/only_tod") && p.Contains("tod.noon"));
            }
            o = RepoJson();
            ((JsonObject)o["modifiers"]![3]!).Remove("extra_elites");
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/3") && p.Contains("효과가 하나도 없는"));
            }
            o = RepoJson();
            o["modifiers"]![6]!["weights"]![1]!["species"] = "enemy.bat_dusk";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/6/weights/1/species") && p.Contains("두 번"));
            }
            o = RepoJson();
            o["modifiers"]![6]!["species_caps"]![0]!["species"] = "enemy.wyvern";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/6/species_caps/0/species") && p.Contains("enemy.wyvern"));
            }
            o = RepoJson();
            o["modifiers"]![0]!["score_multiplier"] = 1.155;          // × 100 이 정수가 아니다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/0/score_multiplier") && p.Contains("정수"));
            }
            o = RepoJson();
            o["modifiers"]![4]!["death_circle"]!["telegraph_seconds"] = 0;   // 예고 없는 원
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/4/death_circle/telegraph_seconds") && p.Contains("스키마"));
            }
            o = RepoJson();
            o["modifiers"]![4]!["death_circle"]!["damage"] = 4;      // 3칸 상한(spec 07 6.3)
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/4/death_circle/damage") && p.Contains("스키마"));
            }
            o = RepoJson();
            ((JsonObject)o["modifiers"]![2]!["extra_budget"]!).Remove("cost_max");
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/modifiers.json:/modifiers/2/extra_budget") && p.Contains("스키마"));
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

            // 첫 변덕(u16 개수 뒤)의 id: 모르는 값, None, 그리고 순서(좁은 들 = 8 을 맨 앞에)
            byte[] bad = (byte[])good.Clone();
            bad[2] = 77;
            Assert.Throws<ContentException>(() => Load(bad));
            bad[2] = 0;
            Assert.Throws<ContentException>(() => Load(bad));
            bad[2] = (byte)ModifierId.NarrowField;
            Assert.Throws<ContentException>(() => Load(bad));

            // 첫 변덕의 only_tod(u16 개수, u8 id, i32 score 뒤): 모르는 값, 보스
            const int tod = 2 + 1 + 4;
            bad = (byte[])good.Clone();
            bad[tod] = 9;
            Assert.Throws<ContentException>(() => Load(bad));
            bad[tod] = (byte)TimeOfDay.BossWave;
            Assert.Throws<ContentException>(() => Load(bad));

            // 첫 변덕의 탄 속도 배율(only_tod 뒤 i32 × 5, i64, i32 × 3, i32 count 다음): 100 을 넘으면 금지(spec 07 2.5)
            const int speed = tod + 1 + 4 * 5 + 8 + 4 * 3 + 4;
            Assert.Equal(100, ReadLe32(good, speed));
            bad = (byte[])good.Clone();
            WriteLe(bad, speed, 101);
            Assert.Throws<ContentException>(() => Load(bad));

            Load(good);
        }

        [Fact]
        public void BrokenSectionBytes_CircleWithoutTelegraph_Throws()
        {
            // 첫 변덕(몰려옴)에 예고 0 인 원을 심는다: 반경 Raw(only_tod 뒤 i32 × 5 다음 i64)를 1칸으로
            byte[] bad = Section(RepoPack.Value);
            const int radius = 2 + 1 + 4 + 1 + 4 * 5;
            WriteLe64(bad, radius, RawPerCell);
            Assert.Throws<ContentException>(() => Load(bad));
        }

        // 저장소 스키마의 객체를 전부 연 것(additionalProperties 를 지운다). 변환기의 금지 필드 검사를 보려고 쓴다
        private static string OpenSchema()
        {
            JsonNode schema = JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/schema/modifiers.schema.json")))!;
            Open(schema);
            return schema.ToJsonString();
        }

        private static void Open(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                o.Remove("additionalProperties");
                foreach (KeyValuePair<string, JsonNode?> kv in o)
                {
                    Open(kv.Value);
                }
            }
            else if (n is JsonArray a)
            {
                foreach (JsonNode? x in a)
                {
                    Open(x);
                }
            }
        }

        // 그 변덕이 쓰지 않는 효과 칸은 "효과 없음" 값이다(ModifierDef 주석)
        private static void AssertNeutralExcept(ModifierDef d, string effect)
        {
            if (effect != "bundle")
            {
                Assert.Equal(0, d.BundleIntervalTicks);
            }
            if (effect != "hp")
            {
                Assert.Equal(100, d.EnemyHpPct);
            }
            if (effect != "budget")
            {
                Assert.Equal(0, d.ExtraBudgetPct);
                Assert.Equal(0, d.ExtraBudgetCostMax);
            }
            if (effect != "elites")
            {
                Assert.Equal(0, d.ExtraElites);
            }
            if (effect != "circle")
            {
                Assert.Equal(0L, d.DeathCircleRadius.Raw);
                Assert.Equal(0, d.DeathCircleTelegraphTicks);
                Assert.Equal(0, d.DeathCircleDamage);
                Assert.Equal(0, d.DeathCircleStaggerTicks);
            }
            if (effect != "shots")
            {
                Assert.Equal(100, d.EnemyShotCountPct);
                Assert.Equal(100, d.EnemyShotSpeedPct);
            }
            if (effect != "night")
            {
                Assert.Empty(d.WeightSpecies);
                Assert.Empty(d.WeightPct);
                Assert.Empty(d.CapSpecies);
                Assert.Empty(d.Cap);
            }
            if (effect != "thorn")
            {
                Assert.Equal(0L, d.ThornBorderWidth.Raw);
                Assert.Equal(0, d.ThornTelegraphTicks);
                Assert.Equal(0, d.ThornContactIntervalTicks);
                Assert.Equal(0, d.ThornDamage);
            }
        }

        private static int ReadLe32(byte[] b, int at) => b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);

        private static void WriteLe(byte[] b, int at, int v)
        {
            for (int i = 0; i < 4; i++)
            {
                b[at + i] = (byte)(v >> (8 * i));
            }
        }

        private static void WriteLe64(byte[] b, int at, long v)
        {
            for (int i = 0; i < 8; i++)
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

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/modifiers.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.Modifiers, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Modifiers, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string modifiers, string? schema = null)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/modifiers.schema.json", schema ?? System.IO.File.ReadAllText(TestRepo.Path("data/schema/modifiers.schema.json")));
            repo.Write("data/sim/modifiers.json", modifiers + "\n");
            repo.Write("data/names/names.ko.json", "{\n  \"whim.big_pack\": \"큰 무리\",\n  \"whim.chief_parade\": \"우두머리 행차\",\n  \"whim.crowding\": \"몰려옴\",\n  \"whim.fog\": \"안개\",\n  \"whim.last_breath\": \"마지막 숨\",\n  \"whim.long_night\": \"긴 밤\",\n  \"whim.narrow_field\": \"좁은 들\",\n  \"whim.slow_barrage\": \"느린 탄막\",\n  \"whim.thick_hide\": \"두꺼운 가죽\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}
