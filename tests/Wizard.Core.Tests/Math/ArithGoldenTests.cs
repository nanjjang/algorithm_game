using System.Collections.Generic;
using System.Linq;
using Wizard.TestKit;
using Wizard.TestKit.Shared.Arith;
using Wizard.TestKit.Shared.Golden;
using Xunit;

namespace Wizard.Core.Tests.Math
{
    // CONTRACTS 1.1 "골든에 0 제수와 MinValue 경계 벡터를 둔다", DECISIONS-TECH T-30.
    // tests/golden/arith/ 의 묶음을 지금 런타임·아키텍처에서 돌려 기대(값 또는 예외 분류)와 비교한다.
    // dotnet arm64 는 `dotnet test`, x64 는 `dotnet test -a x64`(Rosetta 2)로 같은 테스트가 돈다.
    // 기대값은 계약의 정의를 임의 정밀도 정수로 옮긴 독립 계산으로 적었다(구현 출력에서 베끼지 않았다).
    public sealed class ArithGoldenTests
    {
        private static readonly string[] Bundles = { "fix64-ops", "fixmath-div", "fixmath-misc", "fixmath-muldiv", "saturate", "wide" };

        [Fact]
        public void ArithBundles_AllPresent()
        {
            GoldenCatalog catalog = TestRepo.LoadGolden();
            Assert.Equal(Bundles, catalog.ArithBundles.Select(b => b.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void ArithBundles_SatisfyExpectationsOnThisCombo()
        {
            GoldenCatalog catalog = TestRepo.LoadGolden();
            string combo = DotnetCombo.Current().Name;
            var failures = new List<string>();
            int total = 0;
            foreach (ArithBundle b in catalog.ArithBundles)
            {
                List<ArithCaseResult> results = ArithRunner.Run(b, new CoreArithOps());
                for (int i = 0; i < b.Cases.Count; i++)
                {
                    total++;
                    if (!results[i].Satisfies(b.Cases[i]))
                    {
                        failures.Add(combo + " " + results[i].Key + ": got " + results[i].Describe());
                    }
                }
            }
            Assert.True(total > 0, "no arith cases");
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        // 계약이 이름을 든 경계: 0 제수(예외 분류), MinValue ÷ −1, 128÷64 몫 넘침, 포화 경계가 묶음에 다 있다
        [Fact]
        public void ArithBundles_CoverTheContractBoundaries()
        {
            GoldenCatalog catalog = TestRepo.LoadGolden();
            List<ArithCase> all = catalog.ArithBundles.SelectMany(b => b.Cases).ToList();
            string[] divisionOps = { "FixMath.DivInt", "FixMath.ModInt", "FixMath.DivLong", "FixMath.ModLong", "FixMath.MulDiv", "Fix64.Div", "Fix64.DivInt", "Wide.Div128By64" };
            foreach (string op in divisionOps)
            {
                Assert.Contains(all, c => c.Op == op && c.Fault == ArithFaults.SimInvariant);
            }
            Assert.Contains(all, c => c.Op == "FixMath.DivInt" && c.Args[0].ToInt64() == int.MinValue && c.Args[1].ToInt64() == -1);
            Assert.Contains(all, c => c.Op == "FixMath.ModInt" && c.Args[0].ToInt64() == int.MinValue && c.Args[1].ToInt64() == -1);
            Assert.Contains(all, c => c.Op == "FixMath.DivLong" && c.Args[0].ToInt64() == long.MinValue && c.Args[1].ToInt64() == -1);
            Assert.Contains(all, c => c.Op == "FixMath.ModLong" && c.Args[0].ToInt64() == long.MinValue && c.Args[1].ToInt64() == -1);
            Assert.Contains(all, c => c.Op == "Wide.Div128By64" && c.Id == "div128-2pow64-1");
            Assert.Contains(all, c => c.Op == "FixMath.SaturateToInt" && c.Args[0].ToInt64() == (long)int.MaxValue + 1);
            Assert.Contains(all, c => c.Op == "FixMath.SaturateToInt" && c.Args[0].ToInt64() == (long)int.MinValue - 1);
            Assert.All(all, c => Assert.Contains(c.Op, CoreArithOps.Ops));
        }

        [Fact]
        public void UnknownOp_IsReportedAsFault()
        {
            var c = new ArithCase("x", "Nope.Op", new ArithValue[0], new[] { ArithValue.FromInt64(0) }, null);
            Assert.Equal(ArithFaults.UnknownOp, ArithRunner.RunOne("t", c, new CoreArithOps()).Fault);
        }
    }
}
