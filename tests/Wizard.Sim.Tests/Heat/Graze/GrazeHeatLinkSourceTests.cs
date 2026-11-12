using System.IO;
using System.Text.RegularExpressions;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Heat.Graze
{
    // 작업 카드의 수락 조건 "열 보상의 계산은 Heat 한 곳에만 있다. HeatApplyGraze 는 이 훅의 몸통이 되거나 이 훅만 부른다
    // (다른 모듈이 부르는 문으로 남기지 않는다)"를 소스로 본다. 이음 훅을 부르는 곳이 RaiseGrazeAwarded 하나라는 것은
    // Runtime/HookTableTests 가 본다(T-50). 주석의 언급은 세지 않는다.
    public sealed class GrazeHeatLinkSourceTests
    {
        private const string HeatModuleFile = "src/Wizard.Sim/Heat/SimWorld.Heat.cs";

        private static string Code(string path) => Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");

        private static string HeatModuleCode => Code(TestRepo.Path(HeatModuleFile));

        [Fact]
        public void OnlyTheHeatLinkHookCallsHeatApplyGraze()
        {
            // src 전체(공유 코드)에서 HeatApplyGraze 를 부르는 곳은 Heat 모듈의 이음 훅 하나다
            int calls = 0;
            foreach (string file in Directory.GetFiles(TestRepo.Path("src"), "*.cs", SearchOption.AllDirectories))
            {
                string code = Code(file);
                int n = Regex.Matches(code, @"(?<!internal void )\bHeatApplyGraze\(").Count;
                if (n > 0)
                {
                    Assert.Equal(Path.GetFullPath(TestRepo.Path(HeatModuleFile)), Path.GetFullPath(file));
                }
                calls += n;
            }
            Assert.Equal(1, calls);

            // 그 호출은 이음 훅의 몸통 안에 있다(훅의 시그니처부터 다음 partial 메서드 전까지)
            string heat = HeatModuleCode;
            Match hook = Regex.Match(heat, @"partial void OnGrazeAwarded_Heat\(PlayerSlot slot, ISimEventSink sink\)\s*\{");
            Assert.True(hook.Success, "OnGrazeAwarded_Heat is not implemented in " + HeatModuleFile);
            int next = heat.IndexOf("partial void ", hook.Index + hook.Length, System.StringComparison.Ordinal);
            string body = next < 0 ? heat.Substring(hook.Index) : heat.Substring(hook.Index, next - hook.Index);
            Assert.Matches(@"\bHeatApplyGraze\(slot, sink\);", body);
        }

        [Fact]
        public void GrazeHeatRulesLiveOnlyInTheHeatModule()
        {
            // 열 보상의 상태 기계(HeatMachine.ApplyGraze·ApplyReducedGraze)를 부르는 곳은 HeatApplyGraze 하나다
            int calls = 0;
            foreach (string file in Directory.GetFiles(TestRepo.Path("src"), "*.cs", SearchOption.AllDirectories))
            {
                int n = Regex.Matches(Code(file), @"\bHeatMachine\.Apply(Reduced)?Graze\(").Count;
                if (n > 0)
                {
                    Assert.Equal(Path.GetFullPath(TestRepo.Path(HeatModuleFile)), Path.GetFullPath(file));
                }
                calls += n;
            }
            Assert.Equal(2, calls);

            // Graze 모듈은 열을 모른다: Heat 의 상태와 연산을 부르지 않는다
            foreach (string file in Directory.GetFiles(TestRepo.Path("src/Wizard.Sim/Graze"), "*.cs"))
            {
                string code = Code(file);
                Assert.DoesNotMatch(@"\bHeat[A-Z]\w*\(", code);
                Assert.DoesNotMatch(@"\b_heat\b", code);
            }
        }
    }
}
