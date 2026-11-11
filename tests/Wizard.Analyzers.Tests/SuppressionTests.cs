using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using static Wizard.Analyzers.Tests.AnalyzerHarness;

namespace Wizard.Analyzers.Tests
{
    // WZ0016: 억제는 코어 세 어셈블리에서 그 자체가 오류다. 어디서든 WZ 진단을 끄거나 오류 밖으로 낮추면 오류다(T-42).
    // 억제가 WZ0016 자신을 가리는 경우도 본다.
    public sealed class SuppressionTests
    {
        private const string Core = "Wizard.Core";
        private const string Path = "/repo/src/Probe/P.cs";

        private static string M(string body) => "namespace Wizard.Probe { public sealed class P { " + body + " } }";

        private static (string, string)[] One(string code) => new[] { (code, Path) };

        [Fact]
        public void Pragma_InCore_IsAnErrorWhateverItDisables()
        {
            Assert.Contains("WZ0016", Ids(Core, "#pragma warning disable WZ0001\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids("Wizard.Sim", "#pragma warning disable CS0168\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids("Wizard.Content", "#pragma warning disable\n" + M("int x;")));
            Assert.DoesNotContain("WZ0016", Ids(Core, "#pragma warning restore CS0168\n" + M("int x;")));
            Assert.DoesNotContain("WZ0016", Ids(Core, M("int x;")));
        }

        [Fact]
        public void Pragma_CannotHideItsOwnDiagnostic()
        {
            Assert.Contains("WZ0016", Ids(Core, "#pragma warning disable WZ0016\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids("Wizard.Rules", "#pragma warning disable WZ0016, WZ0001\n" + M("int x;")));
        }

        [Fact]
        public void Pragma_OutsideCore_OnlyWizardDiagnosticsAreErrors()
        {
            Assert.Contains("WZ0016", Ids("Wizard.Rules", "#pragma warning disable WZ0001\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids("Wizard.Client", "#pragma warning disable\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids("Wizard.Server.Api", "#pragma warning disable WZ0023\n" + M("int x;")));
            Assert.DoesNotContain("WZ0016", Ids("Wizard.Client", "#pragma warning disable CS0168\n" + M("int x;")));
            Assert.DoesNotContain("WZ0016", Ids("Wizard.Server.Api", "#pragma warning disable CS0168\n" + M("int x;")));
            Assert.DoesNotContain("WZ0016", Ids("Wizard.Core.Tests", "#pragma warning disable WZ0001\n" + M("int x;")));
        }

        [Fact]
        public void SuppressMessage_InCore_IsAnError()
        {
            Assert.Contains("WZ0016", Ids(Core, M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0001\")] int F() => 1;")));
            Assert.Contains("WZ0016", Ids(Core, M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"IDE0001\")] int F() => 1;")));
            Assert.Contains("WZ0016", Ids("Wizard.Sim", "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0005\")]\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids("Wizard.Sim", "[module: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0005\")]\n" + M("int x;")));
            Assert.Contains("WZ0016", Ids(Core, M("int F([System.Diagnostics.CodeAnalysis.SuppressMessage(\"D\", \"WZ0001\")] int a) => a;")));
            // 직접 정의한 UnconditionalSuppressMessage 도 컴파일러가 억제로 읽는다
            Assert.Contains("WZ0016", Ids(Core,
                "namespace System.Diagnostics.CodeAnalysis { sealed class UnconditionalSuppressMessageAttribute : System.Attribute { " +
                "public UnconditionalSuppressMessageAttribute(string c, string id) { } } }\n" +
                M("[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"D\", \"WZ0001\")] int F() => 1;")));
            Assert.DoesNotContain("WZ0016", Ids(Core, M("[System.Obsolete] int F() => 1;")));
        }

        [Fact]
        public void SuppressMessage_CannotHideItsOwnDiagnostic()
        {
            // 기호에 붙은 특성: 그 기호 안의 위치는 특성이 가리므로 위치 없는 진단이 남아야 한다
            Assert.Contains("WZ0016", Ids(Core, M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0016\")] int F() => 1;")));
            // 대상 없는 전역 특성: 위치 없는 진단을 가리므로 특성 자리의 진단이 남아야 한다
            Assert.Contains("WZ0016", Ids(Core, "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0016\")]\n" + M("int x;")));
            // 둘 다
            Assert.Contains("WZ0016", Ids(Core,
                "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0016\")]\n" +
                M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0016\")] int F() => 1;")));
            // #pragma 와 전역 특성을 같이: 지시문 자리의 진단이 남는다
            Assert.Contains("WZ0016", Ids(Core,
                "#pragma warning disable WZ0016\n[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0016\")]\n" + M("int x;")));
        }

        [Fact]
        public void SuppressMessage_OutsideCore_OnlyWizardDiagnosticsAreErrors()
        {
            Assert.Contains("WZ0016", Ids("Wizard.Rules", M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0001:FloatingPoint\")] int F() => 1;")));
            Assert.Contains("WZ0016", Ids("Wizard.Server.App", M("[System.Diagnostics.CodeAnalysis.SuppressMessage(checkId: \"WZ0023\", category: \"Layering\")] int F() => 1;")));
            Assert.DoesNotContain("WZ0016", Ids("Wizard.Client", M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Design\", \"CA1000\")] int F() => 1;")));
        }

        [Fact]
        public void CompilerOptions_NoWarn_WarningsNotAsErrors_Ruleset()
        {
            Assert.Contains("WZ0016", Ids(Core, One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Suppress }));   // NoWarn
            Assert.Contains("WZ0016", Ids(Core, One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Default }));    // WarningsNotAsErrors
            Assert.Contains("WZ0016", Ids(Core, One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["WZ0014"] = ReportDiagnostic.Warn }));
            Assert.Contains("WZ0016", Ids("Wizard.Netcode", One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["WZ0019"] = ReportDiagnostic.Hidden }));
            Assert.Contains("WZ0016", Ids("Wizard.Server.Duel", One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["WZ0023"] = ReportDiagnostic.Suppress }));
            // WZ0016 자신을 꺼도 위치 없는 진단은 남는다
            Assert.Contains("WZ0016", Ids(Core, One(M("int x;")), null,
                new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Suppress, ["WZ0016"] = ReportDiagnostic.Error }));
            Assert.DoesNotContain("WZ0016", Ids(Core, One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["WZ0014"] = ReportDiagnostic.Error }));
            Assert.DoesNotContain("WZ0016", Ids(Core, One(M("int x;")), null, new Dictionary<string, ReportDiagnostic> { ["CS8618"] = ReportDiagnostic.Suppress }));
        }

        [Fact]
        public void EditorConfig_PerFileAndGlobalSeverity()
        {
            var perFile = new Setup
            {
                TreeSeverities = new Dictionary<string, Dictionary<string, ReportDiagnostic>> { [Path] = new Dictionary<string, ReportDiagnostic> { ["WZ0005"] = ReportDiagnostic.Suppress } },
            };
            Assert.Contains("WZ0016", Ids(Core, One(M("int x;")), perFile));

            // 파일별로 WZ0016 자신을 꺼도(위치 없는 진단이라 닿지 않는다)
            var selfOff = new Setup
            {
                TreeSeverities = new Dictionary<string, Dictionary<string, ReportDiagnostic>>
                {
                    [Path] = new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Suppress, ["WZ0016"] = ReportDiagnostic.Suppress },
                },
            };
            Assert.Contains("WZ0016", Ids(Core, One(M("float f;")), selfOff));

            var global = new Setup { GlobalSeverities = new Dictionary<string, ReportDiagnostic> { ["WZ0010"] = ReportDiagnostic.Warn } };
            Assert.Contains("WZ0016", Ids("Wizard.Sim", One(M("int x;")), global));

            var errorIsFine = new Setup
            {
                TreeSeverities = new Dictionary<string, Dictionary<string, ReportDiagnostic>> { [Path] = new Dictionary<string, ReportDiagnostic> { ["WZ0005"] = ReportDiagnostic.Error } },
                GlobalSeverities = new Dictionary<string, ReportDiagnostic> { ["CA1000"] = ReportDiagnostic.Suppress },
            };
            Assert.DoesNotContain("WZ0016", Ids(Core, One(M("int x;")), errorIsFine));
        }

        [Fact]
        public void EditorConfig_BulkSeverity()
        {
            var category = new Setup
            {
                TreeKeys = new Dictionary<string, Dictionary<string, string>> { [Path] = new Dictionary<string, string> { ["dotnet_analyzer_diagnostic.category-Determinism.severity"] = "none" } },
            };
            Assert.Contains("WZ0016", Ids(Core, One(M("int x;")), category));
            var all = new Setup { GlobalKeys = new Dictionary<string, string> { ["dotnet_analyzer_diagnostic.severity"] = "warning" } };
            Assert.Contains("WZ0016", Ids("Wizard.Server.Api", One(M("int x;")), all));
            var fine = new Setup
            {
                GlobalKeys = new Dictionary<string, string> { ["dotnet_analyzer_diagnostic.severity"] = "error", ["dotnet_analyzer_diagnostic.category-Style.severity"] = "none" },
            };
            Assert.DoesNotContain("WZ0016", Ids(Core, One(M("int x;")), fine));
        }

        // 진단은 NotConfigurable·Compiler 꼬리표라 어느 억제로도 실제로 꺼지지 않는다(끄려는 시도는 WZ0016 으로 따로 잡는다)
        [Fact]
        public void SuppressedDiagnosticsStillFire()
        {
            const string dbl = "double D;";
            Assert.Contains("WZ0001", Ids(Core, "#pragma warning disable WZ0001\n" + M(dbl)));
            Assert.Contains("WZ0001", Ids(Core, "#pragma warning disable\n" + M(dbl)));
            Assert.Contains("WZ0001", Ids(Core, M("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0001\")] double F() => 1.5;")));
            Assert.Contains("WZ0001", Ids(Core, "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Determinism\", \"WZ0001\")]\n" + M(dbl)));
            Assert.Contains("WZ0001", Ids(Core, One(M(dbl)), null, new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Suppress }));
            Assert.Contains("WZ0001", Ids(Core, One(M(dbl)), new Setup
            {
                TreeSeverities = new Dictionary<string, Dictionary<string, ReportDiagnostic>> { [Path] = new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Suppress } },
                GlobalSeverities = new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Suppress },
            }));
            Assert.Contains("WZ0023", Ids("Wizard.Server.Api", One(M("int x;")), new Setup
            {
                References = new[] { EmptyAssembly("Wizard.Server.Payments.Fakes") },
                SpecificOptions = new Dictionary<string, ReportDiagnostic> { ["WZ0023"] = ReportDiagnostic.Suppress },
            }));
            // 내려 보낸 심각도도 오류 그대로다
            Diagnostic d = Run(Core, One(M(dbl)), new Setup { SpecificOptions = new Dictionary<string, ReportDiagnostic> { ["WZ0001"] = ReportDiagnostic.Warn } })
                .First(x => x.Id == "WZ0001");
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        }
    }
}
