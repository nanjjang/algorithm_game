using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using static Wizard.Analyzers.Tests.AnalyzerHarness;

namespace Wizard.Analyzers.Tests
{
    // WZ0024 "네임스페이스 = 폴더"(CLAUDE.md 6절). 뜻은 NamespaceFolder.cs 머리 주석(CONTRACTS 머리 주석과 맞춘 해석): 폴더가 네임스페이스를 정한다.
    //   앞머리 — 네임스페이스는 어셈블리 이름 + 폴더 경로의 앞쪽 k 마디
    //   한 폴더 한 네임스페이스
    //   바닥 — Wizard.Content/Rules, Wizard.Rules/{Enhance,Match,League} 는 그 폴더 이름까지 꼭 넣는다
    //   예외 둘 — SimWorld.<모듈>.cs(Wizard.Sim), ContentDb.<절>.cs(Wizard.Content). 뒤의 두 규칙에서 빠진다
    public sealed class NamespaceFolderTests
    {
        private const string SimWorldPart = "namespace Wizard.Sim { public sealed partial class SimWorld { } }";
        private const string ContentDbPart = "namespace Wizard.Content { public sealed partial class ContentDb { } }";

        private static string N(string ns, string type = "T") => "namespace " + ns + " { public sealed class " + type + " { } }";

        private static string[] Wz0024Paths(string assembly, params (string Code, string Path)[] files) =>
            Run(assembly, files, new Setup()).Where(d => d.Id == "WZ0024")
                .Select(d => d.Location.GetLineSpan().Path).OrderBy(p => p, System.StringComparer.Ordinal).ToArray();

        // 수락 조건의 세 꼴
        [Fact]
        public void ThreeAllowedForms()
        {
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", SimWorldPart, "/repo/src/Wizard.Sim/Movement/SimWorld.Movement.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", ContentDbPart, "/repo/src/Wizard.Content/Pack/Sections/ContentDb.Enemies.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Core", N("Wizard.Core"), "/repo/src/Wizard.Core/Errors/ErrorCode.cs"));
        }

        [Fact]
        public void NamespaceEqualToFolderIsAllowed()
        {
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Movement"), "/repo/src/Wizard.Sim/Movement/Mover.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Core", N("Wizard.Core"), "/repo/src/Wizard.Core/Fix64.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Server.App", N("Wizard.Server.App.Mail"), "/repo/server/Wizard.Server.App/Mail/IMailSender.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.TestKit.Shared", N("Wizard.TestKit.Shared.Golden"), "/repo/tests/Wizard.TestKit/Shared/Golden/G.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", "namespace Wizard { namespace Sim.Movement { public sealed class T { } } }", "/repo/src/Wizard.Sim/Movement/M.cs"));
        }

        // 앞머리: 폴더는 소유 경로로 더 잘게 나뉘고 네임스페이스는 모듈까지만 간다(CONTRACTS 의 머리 주석과 뼈대의 꼴)
        [Fact]
        public void FolderMayBeDeeperThanNamespace()
        {
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", N("Wizard.Content"), "/repo/src/Wizard.Content/Ids/BlockId.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", N("Wizard.Content"), "/repo/src/Wizard.Content/Pack/Core/PackReader.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", N("Wizard.Content.Rules"), "/repo/src/Wizard.Content/Rules/Defs/EnhanceTable.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim"), "/repo/src/Wizard.Sim/Runtime/Contracts/DuelSetup.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Input"), "/repo/src/Wizard.Sim/Input/Types/InputEvent.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Spell"), "/repo/src/Wizard.Sim/Spell/Compile/Compiler.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Core", N("Wizard.Core"), "/repo/src/Wizard.Core/Generated/SinTable.g.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules"), "/repo/src/Wizard.Rules/Common/RuleCheck.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules.Enhance"), "/repo/src/Wizard.Rules/Enhance/Core/Roll.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Client", N("Wizard.Client"), "/repo/src/Wizard.Client/Backend/Contracts/IBackend.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Client", N("Wizard.Client.Save"), "/repo/src/Wizard.Client/Save/Core/ISaveStore.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Server.Data", N("Wizard.Server.Data"), "/repo/server/Wizard.Server.Data/Core/ILedger.cs"));
        }

        [Fact]
        public void WrongNamespacesAreErrors()
        {
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Rules"), "/repo/src/Wizard.Sim/Movement/Mover.cs"));               // 다른 어셈블리
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Movement.Deep"), "/repo/src/Wizard.Sim/Movement/Mover.cs"));   // 폴더보다 깊다
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Combat"), "/repo/src/Wizard.Sim/Movement/Mover.cs"));          // 옆 폴더
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Types"), "/repo/src/Wizard.Sim/Input/Types/K.cs"));            // 가운데 마디를 건너뛴다
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Input.Extra"), "/repo/src/Wizard.Sim/Input/Types/K.cs"));      // 앞머리가 아니다
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Simulation"), "/repo/src/Wizard.Sim/Mover.cs"));                   // 접두만 같다
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard"), "/repo/src/Wizard.Sim/Movement/Mover.cs"));                      // 어셈블리보다 위
            Assert.Contains("WZ0024", Ids("Wizard.Core", "public sealed class Global { }", "/repo/src/Wizard.Core/Global.cs"));         // 전역 네임스페이스
            Assert.Contains("WZ0024", Ids("Wizard.Server.Data", N("Wizard.Server.App"), "/repo/server/Wizard.Server.Data/Core/X.cs"));
            // 예외의 이름을 빌려도 다른 어셈블리·다른 네임스페이스면 예외가 아니다
            Assert.Contains("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim.Other"), "/repo/src/Wizard.Sim/Movement/SimWorld.Movement.cs"));
            Assert.Contains("WZ0024", Ids("Wizard.Sim", ContentDbPart, "/repo/src/Wizard.Sim/Movement/ContentDb.Enemies.cs"));
        }

        // 바닥: 분석기 규칙(WZ0017, WZ0020)이 네임스페이스로 가르는 폴더에서는 얕은 네임스페이스로 빠져나가지 못한다
        [Fact]
        public void FloorFoldersMustKeepTheirSegment()
        {
            Assert.Contains("WZ0024", Ids("Wizard.Content", N("Wizard.Content"), "/repo/src/Wizard.Content/Rules/Defs/EnhanceTable.cs"));
            Assert.Contains("WZ0024", Ids("Wizard.Content", N("Wizard.Content"), "/repo/src/Wizard.Content/Rules/Loose.cs"));
            Assert.Contains("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules"), "/repo/src/Wizard.Rules/Enhance/Core/Roll.cs"));
            Assert.Contains("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules"), "/repo/src/Wizard.Rules/League/Bands.cs"));
            Assert.Contains("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules"), "/repo/src/Wizard.Rules/Match/Queue/Q.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules.Enhance.Core"), "/repo/src/Wizard.Rules/Enhance/Core/Roll.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules.Match"), "/repo/src/Wizard.Rules/Match/Queue/Q.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", N("Wizard.Content.Rules"), "/repo/src/Wizard.Content/Rules/Sections/EnhanceSection.cs"));
            // 바닥은 지정된 폴더에만 있다
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Rules", N("Wizard.Rules"), "/repo/src/Wizard.Rules/Economy/Prices.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Sim", N("Wizard.Sim"), "/repo/src/Wizard.Sim/Rules/X.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", N("Wizard.Content"), "/repo/src/Wizard.Content/Defs/Rules/X.cs"));
        }

        // 바닥을 피하는 길이 유료 재화 검사(WZ0020)도 피하지 못한다: 얕은 네임스페이스 자체가 오류다
        [Fact]
        public void ShallowNamespaceInEnhanceFolderDoesNotEscapeUnnoticed()
        {
            const string defs = "namespace Wizard.Content { public enum CurrencyCode : byte { Gold = 0, Starlight = 2 } }";
            string[] ids = Ids("Wizard.Rules", new[]
            {
                ("namespace Wizard.Rules { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/repo/src/Wizard.Rules/Enhance/Core/E.cs"),
                (defs, "/r/defs.cs"),
            });
            Assert.Contains("WZ0024", ids);
            string[] ok = Ids("Wizard.Rules", new[]
            {
                ("namespace Wizard.Rules.Enhance { class E { object F() => Wizard.Content.CurrencyCode.Starlight; } }", "/repo/src/Wizard.Rules/Enhance/Core/E.cs"),
                (defs, "/r/defs.cs"),
            });
            Assert.DoesNotContain("WZ0024", ok);
            Assert.Contains("WZ0020", ok);
        }

        // 예외 둘은 바닥에서도 빠진다. 다만 그 블록이 그 부분 클래스만 담을 때다
        [Fact]
        public void PartialClassFilesAreExemptOnlyWhenTheyHoldJustThatClass()
        {
            const string path = "/repo/src/Wizard.Content/Rules/ContentDb.Rules.cs";
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Content", ContentDbPart, path));
            Assert.Contains("WZ0024", Ids("Wizard.Content",
                "namespace Wizard.Content { public sealed partial class ContentDb { } public sealed class Smuggled { } }", path));
            Assert.Contains("WZ0024", Ids("Wizard.Content", "namespace Wizard.Content { public sealed class ContentDb { } }", path));   // partial 이 아니다
            Assert.Contains("WZ0024", Ids("Wizard.Content", N("Wizard.Content", "Other"), path));
            // 이름 꼴이 다르면 예외가 아니다
            Assert.Contains("WZ0024", Ids("Wizard.Content", ContentDbPart, "/repo/src/Wizard.Content/Rules/ContentDb.cs"));
            Assert.Contains("WZ0024", Ids("Wizard.Content", ContentDbPart, "/repo/src/Wizard.Content/Rules/ContentDb.Rules.Extra.cs"));
        }

        [Fact]
        public void OneFolderHasOneNamespace()
        {
            // 같은 폴더의 두 파일이 서로 다른 앞머리를 쓰면 적은 쪽이 오류다
            Assert.Equal(new[] { "/repo/src/Wizard.Sim/Spell/Exec/C.cs" }, Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Spell", "A"), "/repo/src/Wizard.Sim/Spell/Exec/A.cs"),
                (N("Wizard.Sim.Spell", "B"), "/repo/src/Wizard.Sim/Spell/Exec/B.cs"),
                (N("Wizard.Sim", "C"), "/repo/src/Wizard.Sim/Spell/Exec/C.cs")));
            // 수가 같으면 서수 비교로 앞선 네임스페이스가 그 폴더의 것이다
            Assert.Equal(new[] { "/repo/src/Wizard.Sim/Spell/Exec/A.cs" }, Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Spell", "A"), "/repo/src/Wizard.Sim/Spell/Exec/A.cs"),
                (N("Wizard.Sim", "C"), "/repo/src/Wizard.Sim/Spell/Exec/C.cs")));
            // 폴더가 다르면 깊이가 달라도 된다
            Assert.Empty(Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Spell", "A"), "/repo/src/Wizard.Sim/Spell/Exec/A.cs"),
                (N("Wizard.Sim.Spell.Compile", "B"), "/repo/src/Wizard.Sim/Spell/Compile/B.cs"),
                (N("Wizard.Sim", "C"), "/repo/src/Wizard.Sim/Runtime/Contracts/C.cs"),
                (N("Wizard.Sim", "D"), "/repo/src/Wizard.Sim/D.cs")));
            // 프로젝트 폴더 밖의 파일은 세지 않는다
            Assert.Empty(Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Spell", "A"), "/repo/src/Wizard.Sim/Spell/A.cs"),
                (N("Wizard.Probe", "P"), "/repo/tools/ci/probes/SimProbe.cs")));
        }

        [Fact]
        public void OneFolderResultDoesNotDependOnFileOrder()
        {
            (string, string) a = (N("Wizard.Sim.Spell", "A"), "/repo/src/Wizard.Sim/Spell/A.cs");
            (string, string) b = (N("Wizard.Sim.Spell", "B"), "/repo/src/Wizard.Sim/Spell/B.cs");
            (string, string) c = (N("Wizard.Sim", "C"), "/repo/src/Wizard.Sim/Spell/C.cs");
            string[] expected = { "/repo/src/Wizard.Sim/Spell/C.cs" };
            Assert.Equal(expected, Wz0024Paths("Wizard.Sim", a, b, c));
            Assert.Equal(expected, Wz0024Paths("Wizard.Sim", c, b, a));
            Assert.Equal(expected, Wz0024Paths("Wizard.Sim", b, c, a));
        }

        // SimWorld.<모듈>.cs 는 모듈 폴더의 네임스페이스와 달라도 된다(한 폴더 한 네임스페이스의 예외)
        [Fact]
        public void PartialClassFilesDoNotCountTowardTheFolderNamespace()
        {
            Assert.Empty(Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Heat", "HeatState"), "/repo/src/Wizard.Sim/Heat/HeatState.cs"),
                (SimWorldPart, "/repo/src/Wizard.Sim/Heat/SimWorld.Heat.cs")));
            Assert.Empty(Wz0024Paths("Wizard.Content",
                (N("Wizard.Content.Rules", "EnhanceSection"), "/repo/src/Wizard.Content/Rules/Sections/EnhanceSection.cs"),
                (ContentDbPart, "/repo/src/Wizard.Content/Rules/Sections/ContentDb.Enhance.cs")));
            // 같은 내용이라도 파일 이름이 그 꼴이 아니면 보통 파일로 센다
            Assert.Equal(new[] { "/repo/src/Wizard.Sim/Heat/SimWorld.cs" }, Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Heat", "HeatState"), "/repo/src/Wizard.Sim/Heat/HeatState.cs"),
                (N("Wizard.Sim.Heat", "HeatRules"), "/repo/src/Wizard.Sim/Heat/HeatRules.cs"),
                (SimWorldPart, "/repo/src/Wizard.Sim/Heat/SimWorld.cs")));
            // 부분 클래스 옆에 다른 타입을 끼우면 예외가 아니다
            Assert.Equal(new[] { "/repo/src/Wizard.Sim/Heat/SimWorld.Heat.cs" }, Wz0024Paths("Wizard.Sim",
                (N("Wizard.Sim.Heat", "HeatState"), "/repo/src/Wizard.Sim/Heat/HeatState.cs"),
                (N("Wizard.Sim.Heat", "HeatRules"), "/repo/src/Wizard.Sim/Heat/HeatRules.cs"),
                ("namespace Wizard.Sim { public sealed partial class SimWorld { } public struct Smuggled { } }", "/repo/src/Wizard.Sim/Heat/SimWorld.Heat.cs")));
        }

        [Fact]
        public void FilesOutsideProjectFoldersAreNotChecked()
        {
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Core", N("Wizard.Probe"), "/repo/tools/ci/probes/CoreProbe.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Core", "[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")]",
                "/repo/artifacts/obj/Wizard.Core/debug/Wizard.Core.AssemblyInfo.cs"));
            Assert.DoesNotContain("WZ0024", Ids("Wizard.Core.Tests", N("Elsewhere"), "/repo/tests/Wizard.Core.Tests/X.cs"));
        }

        [Fact]
        public void MessageNamesTheAllowedRange()
        {
            Diagnostic d = Run("Wizard.Sim", new[] { (N("Wizard.Sim.Combat"), "/repo/src/Wizard.Sim/Input/Types/K.cs") }, new Setup())
                .Single(x => x.Id == "WZ0024");
            string text = d.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains("'Wizard.Sim.Combat'", text);
            Assert.Contains("'Wizard.Sim.Input.Types'", text);
            Assert.Contains("no shorter than 'Wizard.Sim'", text);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        }

        [Fact]
        public void RuleFunctions()
        {
            string[] deep = { "Spell", "Compile" };
            Assert.True(NamespaceFolder.IsFolderPrefix("Wizard.Sim", deep, "Wizard.Sim", 0));
            Assert.True(NamespaceFolder.IsFolderPrefix("Wizard.Sim", deep, "Wizard.Sim.Spell", 0));
            Assert.True(NamespaceFolder.IsFolderPrefix("Wizard.Sim", deep, "Wizard.Sim.Spell.Compile", 0));
            Assert.False(NamespaceFolder.IsFolderPrefix("Wizard.Sim", deep, "Wizard.Sim.Compile", 0));
            Assert.False(NamespaceFolder.IsFolderPrefix("Wizard.Sim", deep, "Wizard.Sim", 1));
            Assert.False(NamespaceFolder.IsFolderPrefix("Wizard.Sim", deep, "Wizard.Sim.Spell.Compile.X", 0));
            Assert.Equal("Wizard.Sim.Spell.Compile", NamespaceFolder.Expected("Wizard.Sim", deep));
            Assert.Equal("Wizard.Sim", NamespaceFolder.Shallowest("Wizard.Sim", deep));
            Assert.Equal("Wizard.Content.Rules", NamespaceFolder.Shallowest("Wizard.Content", new[] { "Rules", "Defs" }));
            Assert.Equal(1, NamespaceFolder.FloorDepth("Wizard.Rules", new[] { "Enhance", "Core" }));
            Assert.Equal(0, NamespaceFolder.FloorDepth("Wizard.Rules", new[] { "Economy" }));
            Assert.Equal(0, NamespaceFolder.FloorDepth("Wizard.Rules", new string[0]));
            Assert.Equal("SimWorld", NamespaceFolder.PartialClassOf("Wizard.Sim", "/repo/src/Wizard.Sim/Heat/SimWorld.Heat.cs"));
            Assert.Equal("ContentDb", NamespaceFolder.PartialClassOf("Wizard.Content", "C:\\repo\\src\\Wizard.Content\\Rules\\ContentDb.Rules.cs"));
            Assert.Null(NamespaceFolder.PartialClassOf("Wizard.Sim", "/repo/src/Wizard.Sim/Heat/SimWorld.cs"));
            Assert.Null(NamespaceFolder.PartialClassOf("Wizard.Sim", "/repo/src/Wizard.Sim/Heat/SimWorld.A.B.cs"));
            Assert.Null(NamespaceFolder.PartialClassOf("Wizard.Rules", "/repo/src/Wizard.Rules/Enhance/SimWorld.Enhance.cs"));
            Assert.Equal("Wizard.Sim/Spell/Compile", NamespaceFolder.FolderKey("Wizard.Sim", deep));
        }

        [Fact]
        public void FolderSegmentsUseTheLastProjectMarker()
        {
            Assert.Equal(new[] { "Movement" }, NamespaceFolder.FolderSegments("Wizard.Sim", "/x/src/Wizard.Sim/y/src/Wizard.Sim/Movement/M.cs"));
            Assert.Equal(new[] { "A", "B" }, NamespaceFolder.FolderSegments("Wizard.Core", "C:\\repo\\src\\Wizard.Core\\A\\B\\C.cs"));
            Assert.Empty(NamespaceFolder.FolderSegments("Wizard.Core", "/repo/src/Wizard.Core/C.cs")!);
            Assert.Null(NamespaceFolder.FolderSegments("Wizard.Core", "/repo/src/Wizard.CoreX/C.cs"));
        }
    }
}
