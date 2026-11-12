using System.Collections.Generic;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Validate.Affinity;
using Xunit;

namespace Wizard.DataBuild.Tests.Evasion
{
    // 상성 분배 검사(DECISIONS 2.5, spec 05 1.3 A7·5.2). 표는 spec 05 2.5(일반 적 12종)와 5.2(보스 6종)에서 옮겼다.
    // 이 벡터(spec 05 11.13 TV-AFF-*)는 M3-ENM-01 의 몫이라 벡터 이름을 쓰지 않는다. 같은 값으로 검사기를 시험한다.
    public sealed class AffinityBalanceTests
    {
        private const Element F = Element.Fire, L = Element.Lightning, A = Element.Arcane, I = Element.Ice, D = Element.Dark;

        private static AffinityRow Enemy(string id, Element weak, Element resist, int firstWave) =>
            new AffinityRow { File = "data/sim/enemies.json", Pointer = "/" + id, Id = "enemy." + id, Group = AffinityGroup.Enemy, Weak = weak, Resist = resist, FirstWave = firstWave };

        private static AffinityRow Boss(string id, Element weak, Element resist, Element own) =>
            new AffinityRow { File = "data/sim/bosses.json", Pointer = "/" + id, Id = "boss." + id, Group = AffinityGroup.Boss, Weak = weak, Resist = resist, Own = own };

        private static List<AffinityRow> Enemies12() => new List<AffinityRow>
        {
            Enemy("slime_moss", F, I, 1), Enemy("wolf_bramble", L, D, 3), Enemy("golem_menhir", A, L, 5), Enemy("shaman_cap", I, D, 3),
            Enemy("bug_lantern", I, F, 2), Enemy("hog_needle", D, I, 4), Enemy("weasel_pot", L, F, 6), Enemy("bat_dusk", F, D, 7),
            Enemy("wisp_stray", A, L, 8), Enemy("vine_thorn", F, A, 11), Enemy("boar_tusk", I, A, 13), Enemy("toad_drum", D, I, 15),
        };

        private static List<AffinityRow> Bosses6() => new List<AffinityRow>
        {
            Boss("mirror_phantom", D, A, A), Boss("kiln_boar", I, F, F), Boss("spore_queen", F, L, Element.None),
            Boss("storm_owl", A, L, L), Boss("frost_goat", L, I, I), Boss("night_serpent", L, D, D),
        };

        private static int[] Count(IEnumerable<AffinityRow> rows, bool weak)
        {
            var c = new int[5];
            foreach (AffinityRow r in rows)
            {
                c[(int)(weak ? r.Weak : r.Resist) - 1]++;
            }
            return c;
        }

        private static Diagnostics Run(AffinityTable t)
        {
            var d = new Diagnostics();
            AffinityValidator.Check(t, d);
            return d;
        }

        [Fact]
        public void Roster_ComesFromIdEnums()
        {
            Assert.Equal(12, AffinityTable.CountEnemySpecies());   // spec 14 3.6: 1~12 (졸개 101 은 빼고)
            Assert.Equal(6, AffinityTable.CountBosses());
            Assert.Equal(new[] { F, L, A, I, D }, AffinityTable.Elements());
        }

        [Fact]
        public void FullTable_18_IsBalanced()
        {
            var t = new AffinityTable();
            t.Rows.AddRange(Enemies12());
            t.Rows.AddRange(Bosses6());
            Assert.Equal(new[] { 4, 4, 3, 4, 3 }, Count(t.Rows, true));
            Assert.Equal(new[] { 3, 4, 3, 4, 4 }, Count(t.Rows, false));
            Assert.Equal(new[] { 3, 2, 2, 3, 2 }, Count(Enemies12(), true));
            Assert.Equal(new[] { 2, 2, 2, 3, 3 }, Count(Enemies12(), false));
            Assert.Equal(new[] { 1, 2, 1, 1, 1 }, Count(Bosses6(), true));
            Diagnostics d = Run(t);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
        }

        [Fact]
        public void EnemiesOnly_12_IsBalanced()
        {
            var t = new AffinityTable();
            t.Rows.AddRange(Enemies12());
            Assert.False(Run(t).HasErrors);
        }

        [Fact]
        public void ExtraFireWeakSpecies_Fails()
        {
            // 가상: 화염 약점 적 1종 추가 → 화염 약점 5, 비전 3 → 차 2
            var t = new AffinityTable(13, 6);
            t.Rows.AddRange(Enemies12());
            t.Rows.AddRange(Bosses6());
            t.Rows.Add(Enemy("extra", F, L, 9));
            Assert.Equal(new[] { 5, 4, 3, 4, 3 }, Count(t.Rows, true));
            Diagnostics d = Run(t);
            Assert.Contains(d.Sorted(), e => e.Contains("상성 분배(일반 적 + 보스 약점, 19/19종)") && e.Contains("차이 2 > 1"));
        }

        [Fact]
        public void WeakEqualsResist_Fails()
        {
            var t = new AffinityTable();
            t.Rows.Add(Enemy("slime_moss", F, F, 1));
            Diagnostics d = Run(t);
            Assert.Contains(d.Sorted(), e => e.StartsWith("data/sim/enemies.json:/slime_moss: enemy.slime_moss: 약점과 저항이 같은 속성 element.fire"));
        }

        [Fact]
        public void PartialTable_FeasibleIsOk_InfeasibleFails()
        {
            // 지금 데이터의 원형 4종: 남은 8종으로 맞출 수 있다
            var ok = new AffinityTable();
            ok.Rows.AddRange(Enemies12().GetRange(0, 4));
            Assert.False(Run(ok).HasErrors);

            // 저항이 모두 암흑인 4종: 다 찼을 때 속성마다 2~3 이어야 하는데 이미 4
            var bad = new AffinityTable();
            bad.Rows.Add(Enemy("slime_moss", F, D, 1));
            bad.Rows.Add(Enemy("wolf_bramble", L, D, 3));
            bad.Rows.Add(Enemy("golem_menhir", A, D, 5));
            bad.Rows.Add(Enemy("shaman_cap", I, D, 3));
            Diagnostics d = Run(bad);
            Assert.Contains(d.Sorted(), e => e.Contains("상성 분배(일반 적 저항, 4/12종)") && e.Contains("남은 8종을 어떻게 더해도"));
        }

        [Theory]
        [InlineData(new[] { 0, 0, 0, 0, 0 }, 12, true)]
        [InlineData(new[] { 3, 2, 2, 3, 2 }, 12, true)]
        [InlineData(new[] { 4, 2, 2, 2, 2 }, 12, false)]       // 다 찼고 차이 2
        [InlineData(new[] { 3, 3, 3, 0, 0 }, 12, false)]       // 9줄, 남은 3종으로 냉기·암흑을 2씩(4) 채울 수 없다
        [InlineData(new[] { 3, 3, 2, 0, 0 }, 12, true)]        // 8줄, 남은 4종 = 냉기 2 + 암흑 2
        [InlineData(new[] { 4, 0, 0, 0, 0 }, 12, false)]       // ⌈12/5⌉ = 3 을 넘는다
        [InlineData(new[] { 4, 4, 3, 4, 3 }, 18, true)]
        [InlineData(new[] { 5, 4, 3, 4, 3 }, 19, false)]
        public void CanBalance_Cases(int[] counts, int roster, bool expected)
        {
            Assert.Equal(expected, AffinityBalance.CanBalance(counts, roster));
        }

        [Fact]
        public void Check_FailsOnInfeasibleRepoData()
        {
            using var repo = new RepoDataCopy();
            repo.ReplaceOnce("data/sim/enemies.json", "\"resist\": \"element.ice\",", "\"resist\": \"element.dark\",");        // 이끼 슬라임
            repo.ReplaceOnce("data/sim/enemies.json", "\"resist\": \"element.lightning\",", "\"resist\": \"element.dark\",");  // 선돌지기
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("FAIL  data/sim/enemies.json: 상성 분배(일반 적 저항, 4/12종): element.fire 0, element.lightning 0, element.arcane 0, element.ice 0, element.dark 4", err);
        }
    }
}
