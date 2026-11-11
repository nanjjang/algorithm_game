using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wizard.Analyzers
{
    public sealed partial class DeterminismAnalyzer
    {
        // 문법 검사: 공유 패키지의 C# 9 제한(WZ0021), 코어의 checked·unsafe·lock·async(WZ0015·0012·0008), 네임스페이스 = 폴더(WZ0024, CLAUDE.md 6, T-33)
        private sealed partial class Checker
        {
            // ---------- C# 9 를 넘는 문법과 Unity 가 받지 못하는 문법(ARCHITECTURE 3.3) ----------
            private void CheckLanguageVersion(CompilationAnalysisContext ctx)
            {
                foreach (SyntaxTree tree in ctx.Compilation.SyntaxTrees)
                {
                    if (tree.Options is CSharpParseOptions po && po.LanguageVersion > LanguageVersion.CSharp9)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, Location.None, _asm, "LangVersion " + po.LanguageVersion.ToDisplayString()));
                        return;
                    }
                }
            }

            public void OnRecord(SyntaxNodeAnalysisContext ctx)
            {
                var node = (RecordDeclarationSyntax)ctx.Node;
                string what = node.IsKind(SyntaxKind.RecordStructDeclaration) ? "record struct" : "record";
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, node.Keyword.GetLocation(), _asm, what));
            }

            public void OnInitAccessor(SyntaxNodeAnalysisContext ctx) =>
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, ctx.Node.GetLocation(), _asm, "init"));

            public void OnFileScopedNamespace(SyntaxNodeAnalysisContext ctx) =>
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, ctx.Node.GetFirstToken().GetLocation(), _asm, "file-scoped namespace"));

            public void OnUsingDirective(SyntaxNodeAnalysisContext ctx)
            {
                var node = (UsingDirectiveSyntax)ctx.Node;
                if (node.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, node.GlobalKeyword.GetLocation(), _asm, "global using"));
                }
            }

            public void OnListPattern(SyntaxNodeAnalysisContext ctx) =>
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, ctx.Node.GetLocation(), _asm, "list pattern"));

            // 원시 문자열과 UTF-8 문자열(C# 11)
            private void CheckNewerLiterals(SyntaxTreeAnalysisContext ctx)
            {
                foreach (SyntaxToken token in ctx.Tree.GetRoot(ctx.CancellationToken).DescendantTokens())
                {
                    switch (token.Kind())
                    {
                        case SyntaxKind.SingleLineRawStringLiteralToken:
                        case SyntaxKind.MultiLineRawStringLiteralToken:
                        case SyntaxKind.InterpolatedSingleLineRawStringStartToken:
                        case SyntaxKind.InterpolatedMultiLineRawStringStartToken:
                            ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, token.GetLocation(), _asm, "raw string literal"));
                            break;
                        case SyntaxKind.Utf8StringLiteralToken:
                        case SyntaxKind.Utf8SingleLineRawStringLiteralToken:
                        case SyntaxKind.Utf8MultiLineRawStringLiteralToken:
                            ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, token.GetLocation(), _asm, "UTF-8 string literal"));
                            break;
                    }
                }
            }

            // ---------- 코어: checked(WZ0015), unsafe(WZ0012), lock·async·volatile(WZ0008) ----------
            public void OnChecked(SyntaxNodeAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                bool isChecked = ctx.Node is CheckedExpressionSyntax e
                    ? e.Keyword.IsKind(SyntaxKind.CheckedKeyword)
                    : ((CheckedStatementSyntax)ctx.Node).Keyword.IsKind(SyntaxKind.CheckedKeyword);
                if (isChecked)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CheckedContext, ctx.Node.GetLocation(), _asm));
                }
            }

            public void OnUnsafe(SyntaxNodeAnalysisContext ctx)
            {
                if (_core)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnsafeCode, ctx.Node.GetLocation(), _asm));
                }
            }

            public void OnLock(SyntaxNodeAnalysisContext ctx)
            {
                if (_core)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.Threading, ((LockStatementSyntax)ctx.Node).LockKeyword.GetLocation(), _asm, "lock"));
                }
            }

            public void OnModifiers(SyntaxNodeAnalysisContext ctx)
            {
                SyntaxTokenList mods = ctx.Node switch
                {
                    MemberDeclarationSyntax m => m.Modifiers,
                    LocalFunctionStatementSyntax l => l.Modifiers,
                    AnonymousFunctionExpressionSyntax f => f.Modifiers,
                    _ => default,
                };
                foreach (SyntaxToken t in mods)
                {
                    if (t.IsKind(SyntaxKind.RequiredKeyword))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, t.GetLocation(), _asm, "required"));
                    }
                    if (!_core)
                    {
                        continue;
                    }
                    if (t.IsKind(SyntaxKind.UnsafeKeyword))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnsafeCode, t.GetLocation(), _asm));
                    }
                    else if (t.IsKind(SyntaxKind.AsyncKeyword) || t.IsKind(SyntaxKind.VolatileKeyword))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Threading, t.GetLocation(), _asm, t.Text));
                    }
                }
            }

            // ---------- 네임스페이스 = 폴더(WZ0024, CLAUDE.md 6, T-33) ----------
            // 파일 하나로 판정되는 규칙(앞머리, 바닥, 전역 금지)은 여기서 바로 알리고,
            // 한 폴더 한 네임스페이스는 선언을 모아 두었다가 컴파일 끝에서 본다.
            private readonly ConcurrentQueue<NamespaceUse> _namespaceUses = new ConcurrentQueue<NamespaceUse>();

            private sealed class NamespaceUse
            {
                public NamespaceUse(string folderKey, string ns, string path, Location location)
                {
                    FolderKey = folderKey;
                    Namespace = ns;
                    Path = path;
                    Location = location;
                }

                public string FolderKey { get; }
                public string Namespace { get; }
                public string Path { get; }
                public Location Location { get; }
            }

            private void CheckNamespaceFolder(SyntaxTreeAnalysisContext ctx)
            {
                string path = ctx.Tree.FilePath;
                string[]? folders = NamespaceFolder.FolderSegments(_asm, path);
                if (folders == null)
                {
                    return;   // 프로젝트 폴더 밖(obj 의 생성 파일, CI 탐침)
                }
                string expected = NamespaceFolder.Expected(_asm, folders);
                string shallowest = NamespaceFolder.Shallowest(_asm, folders);
                string range = expected == shallowest
                    ? "expected '" + expected + "'"
                    : "expected '" + expected + "' or a prefix of it no shorter than '" + shallowest + "'";
                var root = (CompilationUnitSyntax)ctx.Tree.GetRoot(ctx.CancellationToken);
                if (root.Members.Any(m => m is BaseTypeDeclarationSyntax || m is DelegateDeclarationSyntax))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.NamespaceFolder, root.Members.First(m => !(m is BaseNamespaceDeclarationSyntax)).GetLocation(),
                        path, "<global>", range));
                }
                string folderKey = NamespaceFolder.FolderKey(_asm, folders);
                foreach (BaseNamespaceDeclarationSyntax ns in root.DescendantNodes(n => !(n is BaseTypeDeclarationSyntax)).OfType<BaseNamespaceDeclarationSyntax>())
                {
                    if (!ns.Members.Any(m => m is BaseTypeDeclarationSyntax || m is DelegateDeclarationSyntax))
                    {
                        continue;   // 타입을 직접 담지 않은 바깥 네임스페이스 블록은 안쪽 블록이 대신 본다
                    }
                    string full = FullName(ns);
                    bool onlyPartial = DeclaresOnlyPartialClass(ns, NamespaceFolder.PartialClassOf(_asm, path));
                    if (!NamespaceFolder.IsAllowed(_asm, path, full, onlyPartial))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.NamespaceFolder, ns.Name.GetLocation(), path, full, range));
                    }
                    else if (!NamespaceFolder.IsPartialClassException(_asm, path, full, onlyPartial))
                    {
                        _namespaceUses.Enqueue(new NamespaceUse(folderKey, full, SymbolUtil.NormalizePath(path), ns.Name.GetLocation()));
                    }
                }
            }

            // 네임스페이스 블록이 직접 담은 타입이 "partial class <name>" 뿐인가(부분 클래스 파일의 예외 조건)
            private static bool DeclaresOnlyPartialClass(BaseNamespaceDeclarationSyntax ns, string? name)
            {
                if (name == null)
                {
                    return false;
                }
                foreach (MemberDeclarationSyntax member in ns.Members)
                {
                    if (!(member is ClassDeclarationSyntax c) || c.Identifier.ValueText != name || !c.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        return false;
                    }
                }
                return true;
            }

            // 한 폴더 한 네임스페이스. 폴더에 네임스페이스가 둘 이상이면 선언 수가 가장 많은 것을 그 폴더의 네임스페이스로 보고
            // (같으면 서수 비교로 앞선 이름) 나머지 선언을 알린다. 결과는 파일을 읽는 순서와 무관하다.
            private void CheckOneNamespacePerFolder(CompilationAnalysisContext ctx)
            {
                var byFolder = new Dictionary<string, List<NamespaceUse>>(StringComparer.Ordinal);
                foreach (NamespaceUse use in _namespaceUses)
                {
                    if (!byFolder.TryGetValue(use.FolderKey, out List<NamespaceUse>? list))
                    {
                        list = new List<NamespaceUse>();
                        byFolder.Add(use.FolderKey, list);
                    }
                    list.Add(use);
                }
                foreach (KeyValuePair<string, List<NamespaceUse>> folder in byFolder.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (NamespaceUse use in folder.Value)
                    {
                        counts.TryGetValue(use.Namespace, out int n);
                        counts[use.Namespace] = n + 1;
                    }
                    if (counts.Count < 2)
                    {
                        continue;
                    }
                    string main = counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).First().Key;
                    foreach (NamespaceUse use in folder.Value.Where(u => u.Namespace != main)
                                 .OrderBy(u => u.Path, StringComparer.Ordinal).ThenBy(u => u.Location.SourceSpan.Start))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.NamespaceFolder, use.Location, use.Path, use.Namespace,
                            "other files in this folder use '" + main + "'; one folder has one namespace"));
                    }
                }
            }

            private static string FullName(BaseNamespaceDeclarationSyntax ns)
            {
                var parts = new List<string>();
                for (SyntaxNode? cur = ns; cur != null; cur = cur.Parent)
                {
                    if (cur is BaseNamespaceDeclarationSyntax b)
                    {
                        parts.Insert(0, b.Name.ToString().Replace(" ", string.Empty));
                    }
                }
                return string.Join(".", parts);
            }
        }
    }
}
