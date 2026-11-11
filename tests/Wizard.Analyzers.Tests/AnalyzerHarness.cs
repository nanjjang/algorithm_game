using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wizard.Analyzers.Tests
{
    // 코드 조각을 지정한 어셈블리 이름으로 컴파일해 분석기 진단만 모은다(컴파일 오류는 무시한다).
    internal static class AnalyzerHarness
    {
        private static readonly Lazy<MetadataReference[]> Platform = new Lazy<MetadataReference[]>(() =>
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.Ordinal) && !Path.GetFileName(p).StartsWith("Wizard.", StringComparison.Ordinal))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToArray());

        public static string[] Ids(string assembly, string code, string path = "/repo/src/Probe/Probe.cs") =>
            Run(assembly, new[] { (code, path) }, Array.Empty<MetadataReference>(), null).Select(d => d.Id).Distinct().OrderBy(x => x).ToArray();

        public static string[] Ids(string assembly, (string Code, string Path)[] files, MetadataReference[]? extra = null,
            Dictionary<string, ReportDiagnostic>? options = null) =>
            Run(assembly, files, extra ?? Array.Empty<MetadataReference>(), options).Select(d => d.Id).Distinct().OrderBy(x => x).ToArray();

        public static ImmutableArray<Diagnostic> Run(string assembly, (string Code, string Path)[] files, MetadataReference[] extra,
            Dictionary<string, ReportDiagnostic>? options)
        {
            var parse = new CSharpParseOptions(LanguageVersion.CSharp9);
            var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Code, parse, f.Path)).ToArray();
            var compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable);
            if (options != null)
            {
                compileOptions = compileOptions.WithSpecificDiagnosticOptions(options);
            }
            CSharpCompilation compilation = CSharpCompilation.Create(assembly, trees, Platform.Value.Concat(extra), compileOptions);
            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismAnalyzer());
            var withAnalyzers = compilation.WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), null, concurrentAnalysis: false, logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false));
            return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        }

        // 이름만 있는 빈 어셈블리 참조(계층 검사용)
        public static MetadataReference EmptyAssembly(string name)
        {
            CSharpCompilation c = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText("public class Marker {}") }, Platform.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var ms = new MemoryStream();
            var result = c.Emit(ms);
            if (!result.Success)
            {
                throw new InvalidOperationException("failed to emit " + name);
            }
            return MetadataReference.CreateFromImage(ms.ToArray());
        }
    }
}
