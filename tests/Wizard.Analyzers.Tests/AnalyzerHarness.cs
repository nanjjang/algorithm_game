using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wizard.Analyzers.Tests
{
    // 코드 조각을 지정한 어셈블리 이름으로 컴파일해 분석기 진단만 모은다(컴파일 오류는 무시한다).
    // 분석기가 예외를 던지면(AD0001) 그 자리에서 테스트가 실패한다.
    internal static class AnalyzerHarness
    {
        private static readonly Lazy<MetadataReference[]> Platform = new Lazy<MetadataReference[]>(() =>
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.Ordinal) && !Path.GetFileName(p).StartsWith("Wizard.", StringComparison.Ordinal))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToArray());

        public sealed class Setup
        {
            public MetadataReference[] References = Array.Empty<MetadataReference>();
            public Dictionary<string, ReportDiagnostic>? SpecificOptions;
            public LanguageVersion Language = LanguageVersion.CSharp9;
            public bool AllowUnsafe;
            public bool CheckOverflow;
            // .editorconfig 의 파일별 심각도: 경로 → (진단 id → 심각도)
            public Dictionary<string, Dictionary<string, ReportDiagnostic>>? TreeSeverities;
            public Dictionary<string, ReportDiagnostic>? GlobalSeverities;
            // .editorconfig 의 그 밖의 키: 경로 → (키 → 값), 전역 키
            public Dictionary<string, Dictionary<string, string>>? TreeKeys;
            public Dictionary<string, string>? GlobalKeys;
        }

        public static string[] Ids(string assembly, string code, string path = "/repo/src/Probe/Probe.cs") =>
            Ids(assembly, new[] { (code, path) });

        public static string[] Ids(string assembly, (string Code, string Path)[] files, MetadataReference[]? extra = null,
            Dictionary<string, ReportDiagnostic>? options = null) =>
            Ids(assembly, files, new Setup { References = extra ?? Array.Empty<MetadataReference>(), SpecificOptions = options });

        public static string[] Ids(string assembly, (string Code, string Path)[] files, Setup setup) =>
            Run(assembly, files, setup).Select(d => d.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

        public static ImmutableArray<Diagnostic> Run(string assembly, (string Code, string Path)[] files, Setup setup)
        {
            var parse = new CSharpParseOptions(setup.Language);
            SyntaxTree[] trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Code, parse, f.Path)).ToArray();
            var compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: setup.AllowUnsafe,
                checkOverflow: setup.CheckOverflow, nullableContextOptions: NullableContextOptions.Enable);
            if (setup.SpecificOptions != null)
            {
                compileOptions = compileOptions.WithSpecificDiagnosticOptions(setup.SpecificOptions);
            }
            if (setup.TreeSeverities != null || setup.GlobalSeverities != null)
            {
                compileOptions = compileOptions.WithSyntaxTreeOptionsProvider(new TreeOptions(setup.TreeSeverities, setup.GlobalSeverities));
            }
            CSharpCompilation compilation = CSharpCompilation.Create(assembly, trees, Platform.Value.Concat(setup.References), compileOptions);
            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismAnalyzer());
            var errors = new ConcurrentQueue<Exception>();
            var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new ConfigOptions(setup.TreeKeys, setup.GlobalKeys));
            CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(
                analyzerOptions, (e, _, _) => errors.Enqueue(e), concurrentAnalysis: false, logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false));
            ImmutableArray<Diagnostic> result = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
            if (!errors.IsEmpty || result.Any(d => d.Id == "AD0001"))
            {
                throw new InvalidOperationException("analyzer threw: " + string.Join("; ", errors.Select(e => e.ToString())));
            }
            return result;
        }

        // 이름만 있는 빈 어셈블리 참조(계층 검사용)
        public static MetadataReference EmptyAssembly(string name) => CompiledAssembly(name, "public class Marker {}");

        // 코드 조각을 컴파일한 어셈블리 참조. 실제 빌드에서 Wizard.Sim 은 Wizard.Content 를 소스가 아니라 메타데이터로 본다.
        public static MetadataReference CompiledAssembly(string name, string code, params MetadataReference[] references)
        {
            CSharpCompilation c = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp9)) },
                Platform.Value.Concat(references), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            using var ms = new MemoryStream();
            var result = c.Emit(ms);
            if (!result.Success)
            {
                throw new InvalidOperationException("failed to emit " + name + ": "
                    + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }
            return MetadataReference.CreateFromImage(ms.ToArray());
        }

        // 컴파일러가 .editorconfig 의 dotnet_diagnostic.<id>.severity 를 넘기는 길과 같은 자리
        private sealed class TreeOptions : SyntaxTreeOptionsProvider
        {
            private readonly Dictionary<string, Dictionary<string, ReportDiagnostic>> _trees;
            private readonly Dictionary<string, ReportDiagnostic> _global;

            public TreeOptions(Dictionary<string, Dictionary<string, ReportDiagnostic>>? trees, Dictionary<string, ReportDiagnostic>? global)
            {
                _trees = trees ?? new Dictionary<string, Dictionary<string, ReportDiagnostic>>();
                _global = global ?? new Dictionary<string, ReportDiagnostic>();
            }

            public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.Unknown;

            public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
            {
                severity = ReportDiagnostic.Default;
                return _trees.TryGetValue(tree.FilePath, out var map) && map.TryGetValue(diagnosticId, out severity);
            }

            public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity) =>
                _global.TryGetValue(diagnosticId, out severity);
        }

        private sealed class ConfigOptions : AnalyzerConfigOptionsProvider
        {
            private readonly Dictionary<string, Dictionary<string, string>> _trees;

            public ConfigOptions(Dictionary<string, Dictionary<string, string>>? trees, Dictionary<string, string>? global)
            {
                _trees = trees ?? new Dictionary<string, Dictionary<string, string>>();
                GlobalOptions = new Options(global);
            }

            public override AnalyzerConfigOptions GlobalOptions { get; }

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
                new Options(_trees.TryGetValue(tree.FilePath, out var map) ? map : null);

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(null);
        }

        private sealed class Options : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _map;

            public Options(Dictionary<string, string>? map) =>
                _map = new Dictionary<string, string>(map ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => _map.TryGetValue(key, out value);
        }
    }
}
