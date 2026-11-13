using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Tests.Evasion;
using Wizard.DataBuild.Validate.Affinity;
using Wizard.DataBuild.Validate.Affinity.BossRules;
using Wizard.TestKit;
using Xunit;

namespace Wizard.DataBuild.Tests.AffinityBoss
{
    // 저장소 데이터(data/sim/enemies.json 의 일반 적 12종 + data/sim/bosses.json 의 보스 6종)의 약점·저항 배분.
    // 벡터: spec 14 7.2 TV-030~TV-036, spec 02 3.3 상성 표와 TV-A08~TV-A14. 줄은 상성 검사기의 원본(IAffinitySource)이 데이터에서 모은 그대로다.
    // spec 02 3.3 표의 옛 id 는 CONTRACTS 2.1 대응표로 읽는다(slime_moss → enemy.slime_moss, boss_kiln → boss.kiln_boar, boss_frost_ram → boss.frost_goat …).
    // 기대 개수의 순서는 화염, 번개, 비전, 냉기(element.ice), 암흑(Element 열거형 값 순서, spec 14 3.1).
    public sealed class BossAffinityVectorTests
    {
        private const Element F = Element.Fire, L = Element.Lightning, A = Element.Arcane, I = Element.Ice, D = Element.Dark;

        private static readonly Lazy<AffinityTable> RepoTable = new Lazy<AffinityTable>(Collect);

        private static AffinityTable Collect()
        {
            var d = new Diagnostics();
            using DataSet data = DataLoader.Load(TestRepo.Root, d);
            var t = new AffinityTable();
            foreach (IAffinitySource s in AffinityValidator.Find<IAffinitySource>())
            {
                s.Collect(data, t, d);
            }
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return t;
        }

        private static List<AffinityRow> Rows(AffinityGroup? group)
        {
            var list = new List<AffinityRow>();
            foreach (AffinityRow r in RepoTable.Value.Rows)
            {
                if (group == null || r.Group == group.Value)
                {
                    list.Add(r);
                }
            }
            return list;
        }

        private static int[] Count(List<AffinityRow> rows, bool weak)
        {
            var c = new int[5];
            foreach (AffinityRow r in rows)
            {
                c[(int)(weak ? r.Weak : r.Resist) - 1]++;
            }
            return c;
        }

        private static AffinityRow Row(string id)
        {
            AffinityRow? found = null;
            foreach (AffinityRow r in RepoTable.Value.Rows)
            {
                if (r.Id == id)
                {
                    Assert.Null(found);
                    found = r;
                }
            }
            Assert.True(found != null, id + " 의 상성 줄이 없다");
            return found!;
        }

        // spec 02 3.3 상성 표 18행(옛 id → CONTRACTS 2.1 의 id). 열: id, 묶음, 첫 등장 웨이브(보스는 0), 약점, 저항
        public static IEnumerable<object[]> Spec02Table33()
        {
            yield return new object[] { "enemy.slime_moss", AffinityGroup.Enemy, 1, F, I };
            yield return new object[] { "enemy.wolf_bramble", AffinityGroup.Enemy, 3, L, D };
            yield return new object[] { "enemy.golem_menhir", AffinityGroup.Enemy, 5, A, L };
            yield return new object[] { "enemy.shaman_cap", AffinityGroup.Enemy, 3, I, D };
            yield return new object[] { "enemy.bug_lantern", AffinityGroup.Enemy, 2, I, F };
            yield return new object[] { "enemy.hog_needle", AffinityGroup.Enemy, 4, D, I };
            yield return new object[] { "enemy.weasel_pot", AffinityGroup.Enemy, 6, L, F };
            yield return new object[] { "enemy.bat_dusk", AffinityGroup.Enemy, 7, F, D };
            yield return new object[] { "enemy.wisp_stray", AffinityGroup.Enemy, 8, A, L };
            yield return new object[] { "enemy.vine_thorn", AffinityGroup.Enemy, 11, F, A };
            yield return new object[] { "enemy.boar_tusk", AffinityGroup.Enemy, 13, I, A };
            yield return new object[] { "enemy.toad_drum", AffinityGroup.Enemy, 15, D, I };
            yield return new object[] { "boss.mirror_phantom", AffinityGroup.Boss, 0, D, A };   // boss_phantom
            yield return new object[] { "boss.kiln_boar", AffinityGroup.Boss, 0, I, F };        // boss_kiln
            yield return new object[] { "boss.spore_queen", AffinityGroup.Boss, 0, F, L };
            yield return new object[] { "boss.storm_owl", AffinityGroup.Boss, 0, A, L };
            yield return new object[] { "boss.frost_goat", AffinityGroup.Boss, 0, L, I };       // boss_frost_ram
            yield return new object[] { "boss.night_serpent", AffinityGroup.Boss, 0, L, D };
        }

        [Theory]
        [MemberData(nameof(Spec02Table33))]
        public void S02_TV_A08__AffinityTableRow_MatchesSpec02Table33(string id, AffinityGroup group, int firstWave, Element weak, Element resist)
        {
            AffinityRow r = Row(id);
            Assert.Equal(group, r.Group);
            Assert.Equal(firstWave, r.FirstWave);
            Assert.Equal(weak, r.Weak);
            Assert.Equal(resist, r.Resist);
        }

        [Fact]
        public void S02_TV_A08__AffinityTable_Has18Rows_AndNoMinion()
        {
            // 12 + 6. 졸개(갓버섯 새싹 enemy.spore_seedling, 101~)는 상성이 없어 줄이 아니다(spec 02 AF-4)
            Assert.Equal(18, RepoTable.Value.Rows.Count);
            Assert.Equal(12, Rows(AffinityGroup.Enemy).Count);
            Assert.Equal(6, Rows(AffinityGroup.Boss).Count);
            foreach (AffinityRow r in RepoTable.Value.Rows)
            {
                Assert.NotEqual("enemy.spore_seedling", r.Id);
            }
        }

        [Fact]
        public void S14_TV_030__WeakCounts_All18()
        {
            int[] c = Count(Rows(null), true);
            Assert.Equal(new[] { 4, 4, 3, 4, 3 }, c);
            Assert.Equal(18, Sum(c));
            Assert.Equal(1, AffinityBalance.Spread(c));
        }

        [Fact]
        public void S14_TV_031__ResistCounts_All18()
        {
            int[] c = Count(Rows(null), false);
            Assert.Equal(new[] { 3, 4, 3, 4, 4 }, c);
            Assert.Equal(18, Sum(c));
            Assert.Equal(1, AffinityBalance.Spread(c));
        }

        [Fact]
        public void S14_TV_032__WeakCounts_Enemies12()
        {
            int[] c = Count(Rows(AffinityGroup.Enemy), true);
            Assert.Equal(new[] { 3, 2, 2, 3, 2 }, c);
            Assert.Equal(1, AffinityBalance.Spread(c));
        }

        [Fact]
        public void S14_TV_033__WeakCounts_Bosses6()
        {
            int[] c = Count(Rows(AffinityGroup.Boss), true);
            Assert.Equal(new[] { 1, 2, 1, 1, 1 }, c);
            Assert.Equal(1, AffinityBalance.Spread(c));
        }

        [Fact]
        public void S14_TV_034__NoSpeciesWithWeakEqualsResist()
        {
            Assert.Equal(0, CountWeakEqualsResist());
        }

        [Fact]
        public void S14_TV_035__ElementalBosses_ResistOwnElement()
        {
            Assert.Equal(5, AssertElementalBossesResistOwn());
        }

        [Fact]
        public void S14_TV_036__Wave1To4Species_DoNotResistArcane()
        {
            AssertEarlySpeciesDoNotResistArcane();
        }

        [Fact]
        public void S02_TV_A09__WeakCounts_All18()
        {
            int[] c = Count(Rows(null), true);
            Assert.Equal(new[] { 4, 4, 3, 4, 3 }, c);
            Assert.Equal(1, AffinityBalance.Spread(c));
            Assert.True(AffinityBalance.Spread(c) <= 1);
        }

        [Fact]
        public void S02_TV_A10__ResistCounts_All18()
        {
            int[] c = Count(Rows(null), false);
            Assert.Equal(new[] { 3, 4, 3, 4, 4 }, c);
            Assert.Equal(1, AffinityBalance.Spread(c));
        }

        [Fact]
        public void S02_TV_A11__EnemiesOnly_And_BossesOnly()
        {
            int[] ew = Count(Rows(AffinityGroup.Enemy), true);
            int[] er = Count(Rows(AffinityGroup.Enemy), false);
            int[] bw = Count(Rows(AffinityGroup.Boss), true);
            int[] br = Count(Rows(AffinityGroup.Boss), false);
            Assert.Equal(new[] { 3, 2, 2, 3, 2 }, ew);
            Assert.Equal(new[] { 2, 2, 2, 3, 3 }, er);
            Assert.Equal(new[] { 1, 2, 1, 1, 1 }, bw);
            Assert.Equal(new[] { 1, 2, 1, 1, 1 }, br);
            foreach (int[] c in new[] { ew, er, bw, br })
            {
                Assert.Equal(1, AffinityBalance.Spread(c));
            }
        }

        [Fact]
        public void S02_TV_A12__NoSpeciesWithWeakEqualsResist()
        {
            Assert.Equal(0, CountWeakEqualsResist());
        }

        [Fact]
        public void S02_TV_A13__ElementalBosses_ResistOwnElement()
        {
            // kiln 화염, phantom 비전, storm_owl 번개, frost_ram(→ frost_goat) 냉기, night_serpent 암흑
            Assert.Equal(F, Row("boss.kiln_boar").Own);
            Assert.Equal(A, Row("boss.mirror_phantom").Own);
            Assert.Equal(L, Row("boss.storm_owl").Own);
            Assert.Equal(I, Row("boss.frost_goat").Own);
            Assert.Equal(D, Row("boss.night_serpent").Own);
            Assert.Equal(5, AssertElementalBossesResistOwn());
        }

        [Fact]
        public void S02_TV_A14__Wave1To4Species_DoNotResistArcane()
        {
            AssertEarlySpeciesDoNotResistArcane();
        }

        private static int Sum(int[] c)
        {
            int s = 0;
            foreach (int v in c)
            {
                s += v;
            }
            return s;
        }

        private static int CountWeakEqualsResist()
        {
            int n = 0;
            foreach (AffinityRow r in RepoTable.Value.Rows)
            {
                if (r.Weak == r.Resist)
                {
                    n++;
                }
            }
            return n;
        }

        // 속성 보스(천 갓 제외)의 수를 돌려주고 각자 자기 속성에 저항하는지 본다
        private static int AssertElementalBossesResistOwn()
        {
            int n = 0;
            foreach (AffinityRow r in Rows(AffinityGroup.Boss))
            {
                if (r.Own == Element.None)
                {
                    Assert.Equal("boss.spore_queen", r.Id);
                    continue;
                }
                Assert.True(r.Resist == r.Own, r.Id + " 의 저항 " + r.Resist + " ≠ 자기 속성 " + r.Own);
                n++;
            }
            return n;
        }

        // 웨이브 1~4 에 처음 나오는 종은 정확히 다섯(슬라임, 벌레, 늑대, 술사, 고슴도치)이고 어느 것도 비전에 저항하지 않는다
        private static void AssertEarlySpeciesDoNotResistArcane()
        {
            var early = new List<string>();
            foreach (AffinityRow r in Rows(AffinityGroup.Enemy))
            {
                if (r.FirstWave >= 1 && r.FirstWave <= EarlyEnemyResistRule.LastEarlyWave)
                {
                    early.Add(r.Id);
                    Assert.NotEqual(Element.Arcane, r.Resist);
                }
            }
            early.Sort(string.CompareOrdinal);
            Assert.Equal(new[] { "enemy.bug_lantern", "enemy.hog_needle", "enemy.shaman_cap", "enemy.slime_moss", "enemy.wolf_bramble" }, early);
        }

        // ---- 검사기에 더한 규칙(M3-BT-01 의 상성 검사기 + 이 작업의 BossRules) ----

        [Fact]
        public void RepoData_PassesAffinityAndEvasionChecks()
        {
            var d = new Diagnostics();
            using DataSet data = DataLoader.Load(TestRepo.Root, d);
            new AffinityValidator().Validate(data, d);
            new Wizard.DataBuild.Validate.Evasion.EvasionValidator().Validate(data, d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
        }

        [Fact]
        public void RepoBossData_PassesEvasionAtSlowestStance()
        {
            // spec 06 검산표의 기준은 가장 느린 자세 속도 달구 3.76(V_frost 2.63). 캐릭터 데이터의 V 와 상관없이 그 V 로도 보스 공격이 R1~R4·R2 를 넘는다
            using var repo = Slowest();
            var (code, outText, err) = repo.Run("check");
            Assert.True(code == 0, err);
            Assert.Contains("databuild check: ok", outText);
        }

        // 저장소 data/ 복사본에서 모든 자세 속도를 달구의 3.76 으로(spec 06 3.5 등 검산표의 기준 V)
        private static RepoDataCopy Slowest()
        {
            var repo = new RepoDataCopy();
            repo.SetAllStanceSpeeds("3.76");
            return repo;
        }

        [Fact]
        public void BossResistNotOwnElement_Fails()
        {
            var t = new AffinityTable(0, 1);
            t.Rows.Add(new AffinityRow { File = "data/sim/bosses.json", Pointer = "/bosses/0", Id = "boss.kiln_boar", Group = AffinityGroup.Boss, Weak = I, Resist = L, Own = F });
            var d = new Diagnostics();
            new BossResistOwnElementRule().Check(t, d);
            Assert.Contains(d.Sorted(), e => e.Contains("boss.kiln_boar: 속성 보스의 저항은 자기 속성 element.fire 이다. 지금 저항은 element.lightning"));
        }

        [Fact]
        public void NeutralBoss_IsNotHeldToOwnElement()
        {
            var t = new AffinityTable(0, 1);
            t.Rows.Add(new AffinityRow { Id = "boss.spore_queen", Group = AffinityGroup.Boss, Weak = F, Resist = L, Own = Element.None });
            var d = new Diagnostics();
            new BossResistOwnElementRule().Check(t, d);
            Assert.False(d.HasErrors);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(4, true)]
        [InlineData(5, false)]       // 웨이브 5 부터는 비전 저항이 있어도 된다
        [InlineData(0, false)]       // 첫 웨이브를 모르는 줄(졸개 등)은 보지 않는다
        public void EarlyEnemyResistingArcane_Fails(int firstWave, bool fails)
        {
            var t = new AffinityTable(1, 0);
            t.Rows.Add(new AffinityRow { Id = "enemy.bug_lantern", Group = AffinityGroup.Enemy, Weak = I, Resist = A, FirstWave = firstWave });
            var d = new Diagnostics();
            new EarlyEnemyResistRule().Check(t, d);
            Assert.Equal(fails, d.HasErrors);
        }

        [Fact]
        public void Check_FailsWhenBossDataResistsAnotherElement()
        {
            // 가마등의 저항을 화염 → 비전으로: 자기 속성 규칙과 분배(보스 저항 비전 2) 둘 다 실패
            using var repo = new RepoDataCopy();
            repo.ReplaceOnce("data/sim/bosses.json", "\"resist\": \"element.fire\"", "\"resist\": \"element.arcane\"");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.kiln_boar: 속성 보스의 저항은 자기 속성 element.fire 이다. 지금 저항은 element.arcane", err);
            Assert.Contains("상성 분배(보스 저항, 6/6종)", err);
        }

        [Fact]
        public void Check_FailsWhenEarlyEnemyResistsArcane()
        {
            // 꽈리 벌레(웨이브 2)의 저항을 화염 → 비전으로
            using var repo = new RepoDataCopy();
            SetInRow(repo, "data/sim/enemies.json", "enemy.bug_lantern", "\"resist\": \"element.fire\"", "\"resist\": \"element.arcane\"");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("enemy.bug_lantern: 웨이브 2 에 처음 나오는 적이 element.arcane 에 저항한다", err);
        }

        // ---- 보스 공격의 회피 검산 R1~R4(spec 06 2.13, 3.5·4.6·5.5·6.5·7.8·8.5 검산값) ----

        [Fact]
        public void Evasion_StompAt08Seconds_FailsR1()
        {
            // spec 06 3.5: 발구르기는 원래 0.8초였는데 달구(3.76)에게 0.82초가 걸려 0.9초로 늘렸다
            using var repo = Slowest();
            repo.ReplaceOnce("data/sim/bosses.json", "\"key\": \"shock\",\n                \"kind\": \"zone\",\n                \"shape\": \"circle\",\n                \"radius\": 2.0,\n                \"telegraph_seconds\": 0.9",
                "\"key\": \"shock\",\n                \"kind\": \"zone\",\n                \"shape\": \"circle\",\n                \"radius\": 2.0,\n                \"telegraph_seconds\": 0.8");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.kiln_boar 의 공격 shock: R1 위반", err);
        }

        [Fact]
        public void Evasion_FeatherFanAt15Degrees_FailsR3()
        {
            // spec 06 6.4: 15도면 3칸 지점 0.78칸이라 규칙에 못 미친다
            using var repo = Slowest();
            repo.ReplaceOnce("data/sim/bosses.json", "\"speed\": 7, \"count\": 5, \"spread_degrees\": 20", "\"speed\": 7, \"count\": 5, \"spread_degrees\": 15");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.storm_owl 의 공격 fan: R3 위반", err);
        }

        [Fact]
        public void Evasion_DarkSpitWithoutLock_FailsR4()
        {
            // spec 06 8.4: 속도 9 의 조준 탄은 조준선 + 발사 0.4초 전 고정(R4). 고정을 0.3초로 줄이면 실패
            using var repo = Slowest();
            repo.ReplaceOnce("data/sim/bosses.json", "\"lock_seconds\": 0.4", "\"lock_seconds\": 0.3");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.night_serpent 의 공격 spit: R4 위반", err);
        }

        [Fact]
        public void Evasion_FrostGoatOnFrostAt27_FailsR2()
        {
            // spec 06 7.6: 서리밭 위 흰뿔 2.4 < V_frost 2.63. 2.7 이면 실패
            using var repo = Slowest();
            repo.ReplaceOnce("data/sim/bosses.json", "\"move_speed_on_frost\": 2.4", "\"move_speed_on_frost\": 2.7");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.frost_goat: R2 위반: 서리밭 위 걷는 속도 2.700칸/초", err);
        }

        [Fact]
        public void Evasion_BreathMinIntervalBelowR3_Fails()
        {
            // spec 06 2.12: 회전 줄기(속도 5.5, 초당 45도)의 R3 하한은 11틱(10.03 → 11). 10틱이면 실패
            using var repo = Slowest();
            repo.ReplaceOnce("data/sim/bosses.json", "\"rapid_fire\": true,\n              \"min_interval_ticks\": 11", "\"rapid_fire\": true,\n              \"min_interval_ticks\": 10");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.frost_goat 의 패턴 frost_breath: R3 위반: 연사형 간격의 하한 10틱", err);
            Assert.Contains("가장 작은 정수 틱은 11", err);
        }

        [Fact]
        public void Evasion_CrossGlide_IsChecked()
        {
            // spec 06 6.4·6.5: 십자 활강의 교차점 벗어나는 거리 1.63 → 0.9초면 통과. 예고 0.6초로 줄이면 교차점 줄이 실패한다
            using var repo = Slowest();
            repo.ReplaceOnce("data/sim/bosses.json", "\"key\": \"glide\",\n                \"kind\": \"dash\",\n                \"shape\": \"band\",\n                \"width\": 2.0,\n                \"length\": null,\n                \"telegraph_seconds\": 0.9",
                "\"key\": \"glide\",\n                \"kind\": \"dash\",\n                \"shape\": \"band\",\n                \"width\": 2.0,\n                \"length\": null,\n                \"telegraph_seconds\": 0.6");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("boss.storm_owl 의 공격 glide [2단계] 교차점(띠 2줄): R1 위반", err);
        }

        // 그 줄(id 가 꼭 한 번 나오는 줄)과 다음 "id" 줄 사이에서만 from 을 to 로 바꾼다
        private static void SetInRow(RepoDataCopy repo, string rel, string id, string from, string to)
        {
            string text = repo.Read(rel);
            string idLine = "\"id\": \"" + id + "\"";
            int at = text.IndexOf(idLine, StringComparison.Ordinal);
            Assert.True(at >= 0, rel + " 에 " + id + " 가 없다");
            Assert.True(text.IndexOf(idLine, at + idLine.Length, StringComparison.Ordinal) < 0, rel + " 에 " + id + " 가 둘 이상이다");
            int next = text.IndexOf("\"id\": \"", at + idLine.Length, StringComparison.Ordinal);
            int r = text.IndexOf(from, at, StringComparison.Ordinal);
            Assert.True(r >= 0 && (next < 0 || r < next), id + " 의 줄에 " + from + " 가 없다");
            repo.Write(rel, text.Substring(0, r) + to + text.Substring(r + from.Length));
        }
    }
}
