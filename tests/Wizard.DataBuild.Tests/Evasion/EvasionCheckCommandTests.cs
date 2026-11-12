using Wizard.DataBuild.Core;
using Wizard.DataBuild.Validate.Affinity;
using Wizard.DataBuild.Validate.Evasion;
using Wizard.TestKit;
using Xunit;

namespace Wizard.DataBuild.Tests.Evasion
{
    // 수락: R1~R4 를 어기는 공격 데이터가 있으면 databuild check 가 어느 적의 어느 공격이 어느 규칙을 어겼는지 적고 실패한다
    public sealed class EvasionCheckCommandTests
    {
        private const string Enemies = "data/sim/enemies.json";

        [Fact]
        public void RepoData_PassesEvasionAndAffinity()
        {
            using DataSet data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            var d = new Diagnostics();
            new EvasionValidator().Validate(data, d);
            new AffinityValidator().Validate(data, d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
        }

        [Fact]
        public void Validators_AreDiscoveredByDataBuild()
        {
            ModuleRegistry m = ModuleRegistry.Discover();
            Assert.Contains(m.Validators, v => v.Name == "evasion");
            Assert.Contains(m.Validators, v => v.Name == "affinity");
            Assert.Contains(EvasionValidator.Sources(), s => s is EnemiesEvasionSource);
            Assert.Contains(AffinityValidator.Find<IAffinitySource>(), s => s is EnemiesAffinitySource);
            Assert.Contains(AffinityValidator.Find<IAffinityRule>(), s => s is AffinityBalanceRule);
            Assert.Contains(AffinityValidator.Find<IAffinityRule>(), s => s is AffinityDistinctRule);
        }

        [Fact]
        public void Check_OnCopiedRepoData_IsOk()
        {
            using var repo = new RepoDataCopy();
            var (code, output, err) = repo.Run("check");
            Assert.True(code == Program.ExitOk, err);
            Assert.StartsWith("databuild check: ok", output);

            // 명세의 V = 3.76(달구)으로도 지금 데이터가 통과한다
            repo.SetAllStanceSpeeds("3.76");
            var (code2, _, err2) = repo.Run("check");
            Assert.True(code2 == Program.ExitOk, err2);
        }

        [Fact]
        public void Check_FailsOnR1_NamingEnemyAttackAndRule()
        {
            using var repo = new RepoDataCopy();
            repo.SetAllStanceSpeeds("3.76");
            repo.ReplaceOnce(Enemies, "\"telegraph_seconds\": 1.1,", "\"telegraph_seconds\": 1.0,");     // 선돌지기 내려찍기 옛값(TV-R1-28)
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("FAIL  data/sim/enemies.json:/enemies/2/attacks/0: enemy.golem_menhir 의 공격 slam: R1 위반:", err);
            Assert.Contains("2632000 < d × 1000 = 2650000", err);
        }

        [Fact]
        public void Build_WritesNothingWhenEvasionFails()
        {
            using var repo = new RepoDataCopy();
            repo.SetAllStanceSpeeds("3.76");
            repo.ReplaceOnce(Enemies, "\"telegraph_seconds\": 0.5,", "\"telegraph_seconds\": 0.4,");     // 늑대 돌진 0.4초: (400−300)×V = 376000 < 650000
            string outDir = System.IO.Path.Combine(repo.Root, "out");
            var (code, _, err) = repo.Run("build", "--out", outDir);
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("enemy.wolf_bramble 의 공격 dash: R1 위반:", err);
            Assert.False(System.IO.File.Exists(System.IO.Path.Combine(outDir, "base.wzpack")));
        }

        [Fact]
        public void Check_FailsOnR3Spacing()
        {
            using var repo = new RepoDataCopy();
            repo.ReplaceOnce(Enemies, "\"spread_degrees\": 20,", "\"spread_degrees\": 15,");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("data/sim/enemies.json:/enemies/3/attacks/0: enemy.shaman_cap 의 공격 triple: R3 위반:", err);
        }

        [Fact]
        public void Check_FailsOnR3SpeedAndR4WithoutAimLine()
        {
            using var repo = new RepoDataCopy();
            repo.ReplaceOnce(Enemies, "\"speed\": 6.5,", "\"speed\": 9.5,");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("enemy.shaman_cap 의 공격 triple: R3 위반: 흩뿌리거나 이어 쏘는 탄의 속도 9.500칸/초", err);
            Assert.Contains("enemy.shaman_cap 의 공격 triple: R4 위반: 탄속 9.500칸/초 > 9칸/초인데 조준선", err);
        }

        [Fact]
        public void FanOutsideFormula_NeedsOverride()
        {
            EvasionAttack a = EvasionR1Tests.Fan(3m, 0m, 48);
            EvasionViolation v = Assert.Single(EvasionCheck.CheckAttack(a, EvasionR1Tests.V, EvasionR1Tests.Hit));
            Assert.Equal("R1", v.Rule);
            Assert.Contains("escape_override_milli", v.Message);
        }

        [Fact]
        public void Check_UsesSlowestCharacterAsV()
        {
            // 기준 속도는 캐릭터 데이터에서 온다(하드코딩하지 않는다): 자세 속도를 낮추면 전에 통과하던 공격이 걸린다
            using var repo = new RepoDataCopy();
            Assert.Equal(Program.ExitOk, repo.Run("check").Code);
            // 늑대 돌진(d 0.65, 예고 0.5초)이 걸리는 속도: (500 − 300) × v < 650000 → v < 3.25
            repo.SetAllStanceSpeeds("3.2");
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("enemy.wolf_bramble 의 공격 dash: R1 위반:", err);
        }

        [Fact]
        public void Report_IsDeterministic()
        {
            using DataSet data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            var input = new EvasionInput();
            new EnemiesEvasionSource().Collect(data, input, new Diagnostics());
            var slow = new EvasionBasis(3000, 150, "test");      // 일부러 느린 V 로 위반을 만든다
            var a = new Diagnostics();
            var b = new Diagnostics();
            EvasionValidator.Report(input, slow, a);
            EvasionValidator.Report(input, slow, b);
            Assert.True(a.HasErrors);
            Assert.Equal(a.Sorted(), b.Sorted());
        }

        [Fact]
        public void Basis_PrefersCharactersOverBaseline()
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                "{\"baseline\":{\"stance_speed\":4.25,\"hit_radius\":0.15},\"characters\":[{\"stance_speed\":4.42,\"hit_radius\":0.15},{\"stance_speed\":3.76,\"hit_radius\":0.15},{\"stance_speed\":5.04,\"hit_radius\":0.16}]}");
            EvasionBasis b = EvasionBasis.FromJson(doc.RootElement)!;
            Assert.Equal(3760, b.SpeedMilli);
            Assert.Equal(160, b.HitMilli);
            Assert.Equal("characters", b.Source);
            using var only = System.Text.Json.JsonDocument.Parse("{\"baseline\":{\"stance_speed\":4.25,\"hit_radius\":0.15}}");
            EvasionBasis c = EvasionBasis.FromJson(only.RootElement)!;
            Assert.Equal(4250, c.SpeedMilli);
            Assert.Equal("baseline", c.Source);
        }

        [Fact]
        public void RepoData_HasEvasionBasis()
        {
            // 저장소 data/ 에는 V 를 구할 캐릭터 줄이 늘 있다(없으면 R1·R2 를 볼 수 없다)
            using DataSet data = DataLoader.Load(TestRepo.Root, new Diagnostics());
            Assert.True(EvasionBasis.HasCharactersFile(data));
            EvasionBasis? b = EvasionBasis.Read(data);
            Assert.NotNull(b);
            Assert.Equal(150, b!.HitMilli);
        }

        [Fact]
        public void WithoutCharacterFile_OnlyRulesIndependentOfVAreChecked()
        {
            using var repo = new RepoDataCopy();
            System.IO.File.Delete(System.IO.Path.Combine(repo.Root, "data/sim/characters.json"));
            repo.ReplaceOnce(Enemies, "\"telegraph_seconds\": 1.1,", "\"telegraph_seconds\": 0.4,");     // V 가 없으면 R1 은 볼 수 없다
            repo.ReplaceOnce(Enemies, "\"spread_degrees\": 20,", "\"spread_degrees\": 15,");              // R3 은 V 없이 본다
            var (code, _, err) = repo.Run("check");
            Assert.Equal(Program.ExitProblems, code);
            Assert.Contains("enemy.shaman_cap 의 공격 triple: R3 위반:", err);
            Assert.DoesNotContain("R1 위반", err);
        }

        [Fact]
        public void Basis_NeedsStanceSpeedAndHitRadius()
        {
            using var doc = System.Text.Json.JsonDocument.Parse("{\"baseline\":{\"stance_speed\":4.25}}");
            Assert.Null(EvasionBasis.FromJson(doc.RootElement));
            using var bad = System.Text.Json.JsonDocument.Parse("{\"baseline\":{\"stance_speed\":4.25,\"hit_radius\":0.15},\"characters\":[{\"stance_speed\":3.76}]}");
            Assert.Null(EvasionBasis.FromJson(bad.RootElement));
        }

        [Fact]
        public void ExemptFlag_OnlyWhereAllowed()
        {
            EvasionAttack a = EvasionR1Tests.Circle(9m, 1);
            a.Exempt = true;
            Assert.Equal("exempt", Assert.Single(EvasionCheck.CheckAttack(a, EvasionR1Tests.V, EvasionR1Tests.Hit)).Rule);
            a.ExemptAllowed = true;
            Assert.Empty(EvasionCheck.CheckAttack(a, EvasionR1Tests.V, EvasionR1Tests.Hit));
        }
    }
}
