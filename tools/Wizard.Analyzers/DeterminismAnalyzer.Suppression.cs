using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wizard.Analyzers
{
    public sealed partial class DeterminismAnalyzer
    {
        // 진단 끄기 금지(WZ0016, T-42).
        // 코어 세 어셈블리: #pragma warning disable 과 SuppressMessage 는 무엇을 끄든 그 자체가 오류다.
        // 그 밖의 공유·서버 프로젝트: WZ 진단을 끄는 것(번호가 없는 #pragma 포함)만 오류다.
        // 옵션(NoWarn, WarningsNotAsErrors, .editorconfig·전역 설정의 심각도)으로 WZ 의 심각도를 오류 밖으로 바꾸는 것도 오류다.
        //
        // WZ 진단은 전부 NotConfigurable·Compiler 꼬리표라(Rules.Error) 위의 어느 방법으로도 실제로 꺼지지 않는다. 여기서는 끄려는 시도 자체를
        // 오류로 알린다. 옵션으로 찾은 것은 위치 없는 진단으로 낸다.
        // 분석기를 통째로 빼는 것(RunAnalyzers=false, 분석기 참조 삭제)은 분석기 안에서 볼 수 없다. tools/ci/determinism-guard.sh 의
        // 탐침 빌드와 정적 검사가 잡는다.
        private sealed partial class Checker
        {
            private static readonly string[] SuppressAttributes =
            {
                "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute", "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
            };

            private static readonly string[] Categories = { "Determinism", "Layering", "Language", "Payments" };

            private void CheckPragmas(SyntaxTreeAnalysisContext ctx)
            {
                SyntaxNode root = ctx.Tree.GetRoot(ctx.CancellationToken);
                foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
                {
                    if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)
                        || !(trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax pragma)
                        || !pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                    {
                        continue;
                    }
                    bool disablesWizard = pragma.ErrorCodes.Count == 0;
                    foreach (ExpressionSyntax code in pragma.ErrorCodes)
                    {
                        if (code.ToString().Trim().StartsWith("WZ", StringComparison.OrdinalIgnoreCase))
                        {
                            disablesWizard = true;
                        }
                    }
                    if (_core || disablesWizard)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, pragma.GetLocation(), _asm, "#pragma warning disable"));
                    }
                }
            }

            public void OnAttribute(SyntaxNodeAnalysisContext ctx)
            {
                var attr = (AttributeSyntax)ctx.Node;
                ISymbol? sym = ctx.SemanticModel.GetSymbolInfo(attr, ctx.CancellationToken).Symbol;
                INamedTypeSymbol? type = sym?.ContainingType;
                if (type == null || Array.IndexOf(SuppressAttributes, SymbolUtil.MetadataName(type)) < 0)
                {
                    return;
                }
                if (!_core && !TargetsWizardDiagnostic(ctx.SemanticModel, attr, ctx))
                {
                    return;
                }
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, attr.GetLocation(), _asm, type.Name));
            }

            // SuppressMessage(category, checkId) 의 checkId 가 WZ 로 시작하는가
            private static bool TargetsWizardDiagnostic(SemanticModel model, AttributeSyntax attr, SyntaxNodeAnalysisContext ctx)
            {
                if (attr.ArgumentList == null)
                {
                    return false;
                }
                int positional = 0;
                foreach (AttributeArgumentSyntax arg in attr.ArgumentList.Arguments)
                {
                    if (arg.NameEquals != null)
                    {
                        continue;
                    }
                    bool isCheckId = arg.NameColon != null ? arg.NameColon.Name.Identifier.ValueText == "checkId" : positional == 1;
                    positional++;
                    if (!isCheckId)
                    {
                        continue;
                    }
                    Optional<object?> value = model.GetConstantValue(arg.Expression, ctx.CancellationToken);
                    return !value.HasValue || !(value.Value is string id) || id.Trim().StartsWith("WZ", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }

            private void CheckSuppressingOptions(CompilationAnalysisContext ctx)
            {
                CompilationOptions options = ctx.Compilation.Options;
                var reported = new HashSet<string>(StringComparer.Ordinal);

                // NoWarn → Suppress, WarningsNotAsErrors → Default, 규칙 집합 → Warn/Info/Hidden/Suppress
                foreach (KeyValuePair<string, ReportDiagnostic> kv in options.SpecificDiagnosticOptions)
                {
                    if (kv.Key.StartsWith("WZ", StringComparison.OrdinalIgnoreCase) && kv.Value != ReportDiagnostic.Error && reported.Add(kv.Key))
                    {
                        Report(ctx, kv.Key + " set to " + kv.Value + " by compiler options (NoWarn, WarningsNotAsErrors or ruleset)");
                    }
                }

                // .editorconfig 의 dotnet_diagnostic.WZxxxx.severity 와 전역 설정(.globalconfig)
                SyntaxTreeOptionsProvider? trees = options.SyntaxTreeOptionsProvider;
                if (trees != null)
                {
                    foreach (DiagnosticDescriptor d in Rules.All)
                    {
                        if (trees.TryGetGlobalDiagnosticValue(d.Id, ctx.CancellationToken, out ReportDiagnostic global) && global != ReportDiagnostic.Error
                            && reported.Add(d.Id))
                        {
                            Report(ctx, d.Id + " set to " + global + " by a global analyzer config");
                        }
                        foreach (SyntaxTree tree in ctx.Compilation.SyntaxTrees)
                        {
                            if (trees.TryGetDiagnosticValue(tree, d.Id, ctx.CancellationToken, out ReportDiagnostic local) && local != ReportDiagnostic.Error
                                && reported.Add(d.Id))
                            {
                                Report(ctx, d.Id + " set to " + local + " by .editorconfig for " + tree.FilePath);
                            }
                        }
                    }
                }

                // 묶음 설정: dotnet_analyzer_diagnostic.severity, dotnet_analyzer_diagnostic.category-<분류>.severity
                AnalyzerConfigOptionsProvider provider = ctx.Options.AnalyzerConfigOptionsProvider;
                CheckBulkSeverity(ctx, provider.GlobalOptions, "a global analyzer config", reported);
                foreach (SyntaxTree tree in ctx.Compilation.SyntaxTrees)
                {
                    CheckBulkSeverity(ctx, provider.GetOptions(tree), ".editorconfig for " + tree.FilePath, reported);
                }
            }

            private void CheckBulkSeverity(CompilationAnalysisContext ctx, AnalyzerConfigOptions options, string source, HashSet<string> reported)
            {
                var keys = new List<string> { "dotnet_analyzer_diagnostic.severity" };
                foreach (string category in Categories)
                {
                    keys.Add("dotnet_analyzer_diagnostic.category-" + category + ".severity");
                }
                foreach (string key in keys)
                {
                    if (options.TryGetValue(key, out string? value) && !string.Equals((value ?? string.Empty).Trim(), "error", StringComparison.OrdinalIgnoreCase)
                        && reported.Add(key))
                    {
                        Report(ctx, key + " = " + value + " in " + source);
                    }
                }
            }

            private void Report(CompilationAnalysisContext ctx, string what) =>
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, Location.None, _asm, what));
        }
    }
}
