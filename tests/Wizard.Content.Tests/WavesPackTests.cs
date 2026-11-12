using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Wizard.Content.Tests.Pack;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/waves.json → 팩 절 7 → ContentDb.WaveCurve 의 왕복(M3-DAT-02).
    // 기대값은 spec 07 2.1 표(필드마다 한 줄), 5절(예산·묶음·예고 상한·엘리트·종 상한·지체 방지), 6절(체력·피해 성장), 7.2(고정 각본)에서 온다.
    // 아래 "명세 식 재현" 테스트는 spec 07 의 정수 식을 데이터 값으로 돌려 17절 표의 숫자(명세에 적힌 값)가 나오는지 본다.
    // 그 식의 구현과 벡터 테스트의 주인은 웨이브 디렉터(M3-WAV-01, WaveMath)이고, 여기서는 데이터가 명세의 곡선을 그대로 싣는지만 본다.
    public sealed class WavesPackTests
    {
        private const int Inf = WaveCurveDef.Unbounded;

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

        private static WaveCurveDef Curve() => ContentDb.Build(ContentPack.Load(RepoPack.Value)).WaveCurve;

        // ── spec 07 2.1 표, 한 줄씩 ──

        [Fact]
        public void Spec07_2_1_DayLengthAndTodRanges()
        {
            WaveCurveDef c = Curve();
            Assert.Equal(10, c.DayLength);                                                  // day_length 10 [D2.3]
            // tod_ranges (1,4,day), (5,7,dusk), (8,9,night), (10,10,boss)
            Assert.Equal(new[] { 1, 5, 8, 10 }, c.TodRangeFrom);
            Assert.Equal(new[] { 4, 7, 9, 10 }, c.TodRangeTo);
            Assert.Equal(new[] { TimeOfDay.Day, TimeOfDay.Dusk, TimeOfDay.Night, TimeOfDay.BossWave }, c.TodRangeTod);
        }

        [Fact]
        public void Spec07_2_1_HpCurve()
        {
            WaveCurveDef c = Curve();
            Assert.Equal(12, c.HpLinearUntil);                  // hp_linear_until 12
            Assert.Equal(120_000L, c.HpLinearStepMicro);        // hp_linear_step_micro 120,000 (= 0.12)
            Assert.Equal(2_320_000L, c.HpExpBaseMicro);         // hp_exp_base_micro 2,320,000 (= 2.32)
            Assert.Equal(106, c.HpExpRatePct);                  // hp_exp_rate_pct 106
        }

        [Fact]
        public void Spec07_2_1_BudgetAndBundles()
        {
            WaveCurveDef c = Curve();
            // budget_linear: B = 10 + 6w (w ≤ 12)
            Assert.Equal(10, c.BudgetLinearBase);
            Assert.Equal(6, c.BudgetLinearPerWave);
            Assert.Equal(12, c.BudgetLinearUntil);
            // budget_late: B = 82 + 4(w − 12), 상한 200
            Assert.Equal(82, c.BudgetLateBase);
            Assert.Equal(4, c.BudgetLatePerWave);
            Assert.Equal(200, c.BudgetCap);
            // bundle_rule: w ≤ 12: 4 / w ≥ 13: 4 + floor((w − 12) ÷ 8), 최대 8
            Assert.Equal(4, c.BundleBase);
            Assert.Equal(12, c.BundleGrowAfter);
            Assert.Equal(8, c.BundleGrowEvery);
            Assert.Equal(8, c.BundleMax);
            Assert.Equal(420, c.BundleIntervalTicks);           // bundle_interval_ticks 420 (7초)
            Assert.Equal(3, c.BundleEarlyReleaseAlive);         // bundle_early_release_alive 3
            Assert.Equal(40, c.MaxAlive);                       // max_alive 40
        }

        [Fact]
        public void Spec07_2_1_TelegraphCapAndElites()
        {
            WaveCurveDef c = Curve();
            // telegraph_cap (1,4,2), (5,8,3), (9,12,4), (13,29,5), (30,∞,6)
            Assert.Equal(new[] { 1, 5, 9, 13, 30 }, c.TelegraphCapFrom);
            Assert.Equal(new[] { 4, 8, 12, 29, Inf }, c.TelegraphCapTo);
            Assert.Equal(new[] { 2, 3, 4, 5, 6 }, c.TelegraphCap);
            // elite_count (12,19,1), (20,29,2), (30,39,3), (40,∞,4)
            Assert.Equal(new[] { 12, 20, 30, 40 }, c.EliteCountFrom);
            Assert.Equal(new[] { 19, 29, 39, Inf }, c.EliteCountTo);
            Assert.Equal(new[] { 1, 2, 3, 4 }, c.EliteCount);
            Assert.Equal(3, c.EliteCostMul);                    // elite_cost_mul 3
        }

        [Fact]
        public void Spec07_2_1_SpeciesCapStallAndModifiers()
        {
            WaveCurveDef c = Curve();
            // species_cap: golem_menhir 3, vine_thorn 4, toad_drum 2, boar_tusk 3, hog_needle 4, wisp_stray 4 (EnemyTypeId 오름차순으로 든다)
            Assert.Equal(new[] { EnemyTypeId.GolemMenhir, EnemyTypeId.HogNeedle, EnemyTypeId.WispStray, EnemyTypeId.VineThorn, EnemyTypeId.BoarTusk, EnemyTypeId.ToadDrum }, c.SpeciesCapSpecies);
            Assert.Equal(new[] { 3, 4, 4, 4, 3, 2 }, c.SpeciesCap);
            Assert.Equal(7200, c.StallTicks);                   // stall_ticks 7,200 (120초)
            Assert.Equal(50, c.StallKillScorePct);              // stall_kill_score_pct 50
            // modifier_count: 21 ≤ w ≤ 40: 1 / w ≥ 41: 2 / 그 밖 0
            Assert.Equal(new[] { 21, 41 }, c.ModifierCountFrom);
            Assert.Equal(new[] { 40, Inf }, c.ModifierCountTo);
            Assert.Equal(new[] { 1, 2 }, c.ModifierCount);
        }

        [Fact]
        public void Spec07_6_EliteBossHpAndDamageGrowth()
        {
            WaveCurveDef c = Curve();
            Assert.Equal(3, c.EliteHpMul);                      // 6.2 elite ? 3 : 1
            Assert.Equal(10, c.BossHpRefWave);                  // 6.2 BossHp = base × HMicro(w) ÷ HMicro(10)
            Assert.Equal(1, c.ContactDamage);                   // 6.3 접촉·탄 1
            Assert.Equal(1, c.TelegraphDamagePlus);             // 6.3 +1
            Assert.Equal(31, c.EliteBossPlusFromWave);          // 6.3 웨이브 31~60 엘리트·보스 +1
            Assert.Equal(61, c.AllPlusFromWave);                // 6.3 웨이브 61 이상 일반 적도 +1
            Assert.Equal(3, c.AttackDamageCap);                 // 6.3 3칸 상한 [확정: G11]
            Assert.Equal(EnemyTypeId.SlimeMoss, c.FillSpecies); // 7.4 5번: 남은 예산은 slime_moss
        }

        // 수락 조건: 하루 = 10웨이브(낮 1~4, 해질녘 5~7, 밤 8~9, 보스 10)
        [Fact]
        public void DayIsTenWaves_Day1to4_Dusk5to7_Night8to9_Boss10()
        {
            WaveCurveDef c = Curve();
            Assert.Equal(10, c.DayLength);
            var expected = new[] { TimeOfDay.Day, TimeOfDay.Day, TimeOfDay.Day, TimeOfDay.Day, TimeOfDay.Dusk, TimeOfDay.Dusk, TimeOfDay.Dusk, TimeOfDay.Night, TimeOfDay.Night, TimeOfDay.BossWave };
            for (int local = 1; local <= 10; local++)
            {
                Assert.Equal(expected[local - 1], TodOf(c, local));
            }
            // spec 07 17.1 TV-002·TV-003, spec 14 7.3 TV-047~TV-049 의 웨이브들(누적 웨이브 → 하루 속 순번 → 데이터의 구간)
            Assert.Equal(TimeOfDay.Day, TodOf(c, 23));
            Assert.Equal(TimeOfDay.Dusk, TodOf(c, 27));
            Assert.Equal(TimeOfDay.BossWave, TodOf(c, 30));
            Assert.Equal(TimeOfDay.Night, TodOf(c, 99));
        }

        // ── 명세 식 재현(spec 07 6.1·5절의 정수 식 + 데이터 → 17절 표의 값) ──

        [Fact]
        public void Spec07_6_1_HMicro_FromData_GivesTheTableValues()
        {
            WaveCurveDef c = Curve();
            // 17.3 TV-022·TV-023
            var waves = new[] { 1, 6, 10, 12, 13, 20, 30, 40, 50, 60, 80, 100 };
            var micro = new[] { 1_000_000L, 1_600_000L, 2_080_000L, 2_320_000L, 2_459_200L, 3_697_724L, 6_622_055L, 11_859_087L, 21_237_812L, 38_033_679L, 121_979_144L, 391_203_621L };
            for (int i = 0; i < waves.Length; i++)
            {
                Assert.Equal(micro[i], HMicro(c, waves[i]));
            }
        }

        [Fact]
        public void Spec07_5_BudgetBundlesTelegraphElitesModifiers_FromData_GiveTheTableValues()
        {
            WaveCurveDef c = Curve();
            // 17.5 TV-060·TV-061
            AssertEach(new[] { 1, 2, 9, 12, 13, 20, 30, 41, 42, 100 }, new[] { 16, 22, 64, 82, 86, 114, 154, 198, 200, 200 }, w => Budget(c, w));
            // 17.5 TV-062·TV-063
            AssertEach(new[] { 1, 12, 13, 19, 20, 27, 28, 36, 44, 100 }, new[] { 4, 4, 4, 4, 5, 5, 6, 7, 8, 8 }, w => BundleCount(c, w));
            // 17.5 TV-064
            AssertEach(new[] { 4, 5, 8, 9, 12, 13, 29, 30, 99 }, new[] { 2, 3, 3, 4, 4, 5, 5, 6, 6 }, w => Lookup(c.TelegraphCapFrom, c.TelegraphCapTo, c.TelegraphCap, w));
            // 17.5 TV-065 (보스 웨이브 0 은 규칙이라 일반 웨이브만)
            AssertEach(new[] { 11, 12, 19, 21, 29, 31, 39, 41, 99 }, new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4 }, w => Lookup(c.EliteCountFrom, c.EliteCountTo, c.EliteCount, w));
            // 17.7 TV-101 (40 은 보스 웨이브라 규칙이 0 으로 만든다)
            AssertEach(new[] { 19, 21, 39, 41, 55 }, new[] { 0, 1, 1, 2, 2 }, w => Lookup(c.ModifierCountFrom, c.ModifierCountTo, c.ModifierCount, w));
        }

        // ── spec 07 7.2 고정 각본 ──

        [Fact]
        public void Spec07_7_2_Script_EveryRow()
        {
            WaveCurveDef c = Curve();
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 15 }, c.ScriptWaves);
            foreach (FrameDef p in c.ScriptPlans)
            {
                Assert.Equal(FrameId.Mixed, p.Id);              // 7.1 "1~12는 frame.mixed 고정 표기", 13·15 혼합 강제(C-04)
            }
            // 1: slime × B (나머지 전부 fill)
            AssertPlan(c, 1, new EnemyTypeId[0], 0, new EnemyTypeId[0], new int[0], new int[0], false);
            // 2: bug × k (k∈{2,3}), 나머지 slime
            AssertPlan(c, 2, new EnemyTypeId[0], 0, new[] { EnemyTypeId.BugLantern }, new[] { 2 }, new[] { 3 }, false);
            // 3: shaman × 1, 나머지 wolf 최대, 남는 예산 slime
            AssertPlan(c, 3, new[] { EnemyTypeId.WolfBramble }, 100, new[] { EnemyTypeId.ShamanCap }, new[] { 1 }, new[] { 1 }, false);
            // 4: shaman 1, hog 1, 나머지 wolf 최대, 남는 예산 slime
            AssertPlan(c, 4, new[] { EnemyTypeId.WolfBramble }, 100, new[] { EnemyTypeId.ShamanCap, EnemyTypeId.HogNeedle }, new[] { 1, 1 }, new[] { 1, 1 }, false);
            // 5: golem 1, 나머지 혼합
            AssertPlan(c, 5, new EnemyTypeId[0], 0, new[] { EnemyTypeId.GolemMenhir }, new[] { 1 }, new[] { 1 }, true);
            // 6: golem 1, shaman 1, weasel 1, 나머지 혼합
            AssertPlan(c, 6, new EnemyTypeId[0], 0, new[] { EnemyTypeId.GolemMenhir, EnemyTypeId.ShamanCap, EnemyTypeId.WeaselPot }, new[] { 1, 1, 1 }, new[] { 1, 1, 1 }, true);
            // 7: bat 무리 1(6~8), 나머지 혼합
            AssertPlan(c, 7, new EnemyTypeId[0], 0, new[] { EnemyTypeId.BatDusk }, new[] { 1 }, new[] { 1 }, true);
            // 8: wisp 2, 나머지 혼합
            AssertPlan(c, 8, new EnemyTypeId[0], 0, new[] { EnemyTypeId.WispStray }, new[] { 2 }, new[] { 2 }, true);
            // 9: wolf 60%, bat 무리 1, 나머지 혼합
            AssertPlan(c, 9, new[] { EnemyTypeId.WolfBramble }, 60, new[] { EnemyTypeId.BatDusk }, new[] { 1 }, new[] { 1 }, true);
            // 11: vine 1, 나머지 혼합
            AssertPlan(c, 11, new EnemyTypeId[0], 0, new[] { EnemyTypeId.VineThorn }, new[] { 1 }, new[] { 1 }, true);
            // 12: 엘리트 1(elite_count 표), 나머지 혼합
            AssertPlan(c, 12, new EnemyTypeId[0], 0, new EnemyTypeId[0], new int[0], new int[0], true);
            // 13: 혼합 강제, boar ≥ 1 / 15: 혼합 강제, toad ≥ 1 (18절 C-04)
            AssertPlan(c, 13, new EnemyTypeId[0], 0, new[] { EnemyTypeId.BoarTusk }, new[] { 1 }, new[] { 1 }, true);
            AssertPlan(c, 15, new EnemyTypeId[0], 0, new[] { EnemyTypeId.ToadDrum }, new[] { 1 }, new[] { 1 }, true);
        }

        [Fact]
        public void Spec07_7_2_ScriptBudgets_Waves3And4_MatchTheSpecCounts()
        {
            // 17.6 TV-082·TV-083 의 숫자가 각본 데이터와 맞는지(종 비용은 spec 07 2.2: shaman 4, hog 4, wolf 3, slime 1).
            // 늑대 최대 = floor(남은 예산 ÷ 3) (7.2 끝 줄), 남는 예산 slime
            WaveCurveDef c = Curve();
            int b3 = Budget(c, 3);
            Assert.Equal(28, b3);
            int wolves3 = (b3 - 4) / 3;
            Assert.Equal((8, 0), (wolves3, b3 - 4 - 3 * wolves3));
            int b4 = Budget(c, 4);
            Assert.Equal(34, b4);
            int wolves4 = (b4 - 4 - 4) / 3;
            Assert.Equal((8, 2), (wolves4, b4 - 8 - 3 * wolves4));
        }

        [Fact]
        public void PackWithoutTheSection_Throws()
        {
            ContentDb db = Load(null);
            Assert.Throws<ContentException>(() => db.WaveCurve);
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
            foreach (string array in new[] { "tod_ranges", "telegraph_cap", "elite_count", "species_cap", "modifier_count", "script" })
            {
                Reverse((JsonArray)o[array]!);
            }
            foreach (JsonNode? row in (JsonArray)o["script"]!)
            {
                Reverse((JsonArray)row!["fixed"]!);
                Reverse((JsonArray)row!["group"]!);
            }
            using TempDataRepo repo = Repo(o.ToJsonString());
            Assert.Equal(Section(RepoPack.Value), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_Schema()
        {
            JsonObject o = RepoJson();
            o["max_alive_typo"] = 40;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json") && p.Contains("스키마"));
            }
            o = RepoJson();
            o.Remove("stall_seconds");
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void Problems_TodRanges_GapOrBossNotLast()
        {
            JsonObject o = RepoJson();
            o["tod_ranges"]![1]!["from"] = 6;                    // 5 가 빈다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/tod_ranges/1/from"));
            }
            o = RepoJson();
            o["tod_ranges"]![2]!["tod"] = "tod.boss";            // 8~9 가 보스
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/tod_ranges/2/tod") && p.Contains("tod.boss"));
            }
            o = RepoJson();
            o["tod_ranges"]![3]!["tod"] = "tod.noon";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/tod_ranges/3/tod") && p.Contains("tod.noon"));
            }
        }

        [Fact]
        public void Problems_Steps_GapOverlapOrOpenEndInTheMiddle()
        {
            JsonObject o = RepoJson();
            o["telegraph_cap"]![2]!["from"] = 10;                // 9 가 빈다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/telegraph_cap/2/from"));
            }
            o = RepoJson();
            o["elite_count"]![1]!["from"] = 19;                  // 19 가 겹친다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/elite_count/1/from"));
            }
            o = RepoJson();
            o["modifier_count"]![0]!["to"] = null;              // 끝 없음이 가운데
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/modifier_count/0/to"));
            }
            o = RepoJson();
            o["modifier_count"]![1]!["to"] = 60;                // 61 뒤가 빈다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/modifier_count/1/to"));
            }
        }

        [Fact]
        public void Problems_InexactRatio_UnknownOrDuplicateSpecies()
        {
            JsonObject o = RepoJson();
            o["hp_linear_step"] = 0.1234567m;                    // × 1,000,000 이 정수가 아니다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/hp_linear_step") && p.Contains("정수"));
            }
            o = RepoJson();
            o["hp_exp_rate"] = 1.065m;                           // × 100 이 정수가 아니다
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/hp_exp_rate"));
            }
            o = RepoJson();
            o["species_cap"]![0]!["species"] = "enemy.dragon";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/species_cap/0/species") && p.Contains("enemy.dragon"));
            }
            o = RepoJson();
            o["species_cap"]![1]!["species"] = "enemy.golem_menhir";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/species_cap/1/species") && p.Contains("두 번"));
            }
        }

        [Fact]
        public void Problems_Script_BossWave_Duplicate_Share_MinMax()
        {
            JsonObject o = RepoJson();
            o["script"]![10]!["wave"] = 20;                      // 보스 웨이브
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/script/10/wave") && p.Contains("보스"));
            }
            o = RepoJson();
            o["script"]![1]!["wave"] = 1;
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/script/1/wave") && p.Contains("두 번"));
            }
            o = RepoJson();
            o["script"]![2]!["group_share"] = 0;                 // 주 묶음이 있는데 몫 0
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/script/2/group_share"));
            }
            o = RepoJson();
            o["script"]![1]!["fixed"]![0]!["min"] = 4;           // min 4 > max 3
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json:/script/1/fixed/0/min"));
            }
            o = RepoJson();
            o["script"]![0]!["rest"] = "boss";
            using (TempDataRepo repo = Repo(o.ToJsonString()))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/waves.json") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()).WaveCurve);
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            // 첫 구간의 tod 바이트(i32 day_length, u16 n, i32 from, i32 to 뒤): 모르는 값, None
            byte[] badTod = (byte[])good.Clone();
            badTod[4 + 2 + 4 + 4] = 9;
            Assert.Throws<ContentException>(() => Load(badTod));
            badTod[4 + 2 + 4 + 4] = 0;
            Assert.Throws<ContentException>(() => Load(badTod));
            // 첫 구간의 to 를 둘째 구간의 from 이상으로: 겹침
            byte[] overlap = (byte[])good.Clone();
            overlap[4 + 2 + 4] = 5;
            Assert.Throws<ContentException>(() => Load(overlap));
            Load(good);
        }

        // ── 도우미 ──

        // 누적 웨이브 → 하루 속 순번(spec 07 3.1 LocalWave) → 데이터의 구간
        private static TimeOfDay TodOf(WaveCurveDef c, int wave)
        {
            int local = (wave - 1) % c.DayLength + 1;
            for (int i = 0; i < c.TodRangeFrom.Length; i++)
            {
                if (local >= c.TodRangeFrom[i] && local <= c.TodRangeTo[i])
                {
                    return c.TodRangeTod[i];
                }
            }
            return TimeOfDay.None;
        }

        // spec 07 6.1 HMicro (매 단계 내림)
        private static long HMicro(WaveCurveDef c, int w)
        {
            if (w <= c.HpLinearUntil)
            {
                return 1_000_000L + c.HpLinearStepMicro * (w - 1);
            }
            long h = c.HpExpBaseMicro;
            for (int i = 0; i < w - c.HpLinearUntil; i++)
            {
                h = h * c.HpExpRatePct / 100;
            }
            return h;
        }

        // spec 07 5.1
        private static int Budget(WaveCurveDef c, int w) =>
            w <= c.BudgetLinearUntil ? c.BudgetLinearBase + c.BudgetLinearPerWave * w : Math.Min(c.BudgetCap, c.BudgetLateBase + c.BudgetLatePerWave * (w - c.BudgetLinearUntil));

        // spec 07 5.2
        private static int BundleCount(WaveCurveDef c, int w) =>
            w <= c.BundleGrowAfter ? c.BundleBase : Math.Min(c.BundleMax, c.BundleBase + (w - c.BundleGrowAfter) / c.BundleGrowEvery);

        private static int Lookup(int[] from, int[] to, int[] value, int w)
        {
            for (int i = 0; i < from.Length; i++)
            {
                if (w >= from[i] && w <= to[i])
                {
                    return value[i];
                }
            }
            return 0;
        }

        private static void AssertEach(int[] waves, int[] expected, Func<int, int> f)
        {
            var actual = new int[waves.Length];
            for (int i = 0; i < waves.Length; i++)
            {
                actual[i] = f(waves[i]);
            }
            Assert.Equal(expected, actual);
        }

        private static void AssertPlan(WaveCurveDef c, int wave, EnemyTypeId[] group, int share, EnemyTypeId[] fixedSpecies, int[] min, int[] max, bool restMixed)
        {
            int i = Array.IndexOf(c.ScriptWaves, wave);
            Assert.True(i >= 0, "웨이브 " + wave + " 의 각본이 없다");
            FrameDef p = c.ScriptPlans[i];
            Assert.Equal(group, p.GroupSpecies);
            Assert.Equal(share, p.GroupSharePct);
            Assert.Equal(fixedSpecies, p.FixedSpecies);
            Assert.Equal(min, p.FixedMin);
            Assert.Equal(max, p.FixedMax);
            Assert.Equal(restMixed, p.RestMixed);
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

        private static JsonObject RepoJson() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(TestRepo.Path("data/sim/waves.json")))!;

        private static ContentDb Load(byte[]? section)
        {
            var sections = section == null ? new PackSection[0] : new[] { new PackSection((ushort)ContentSection.Waves, section) };
            return ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength], sections)));
        }

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Waves, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        private static TempDataRepo Repo(string waves)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/waves.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/waves.schema.json")));
            repo.Write("data/sim/waves.json", waves + "\n");
            return repo;
        }
    }
}
