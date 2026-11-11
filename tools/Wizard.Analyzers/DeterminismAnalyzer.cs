using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Wizard.Analyzers
{
    // 결정론 금지 목록(ARCHITECTURE 11, T-42), 공유 패키지의 언어·API 제한(3.3), 계층 참조(3.2), 결제 값 참조(spec 12 10.7).
    // 생성 코드(*.g.cs)도 검사한다. 파일 이름으로 금지 목록을 피할 수 없다.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Rules.FloatingPoint, Rules.SystemRandom, Rules.Clock, Rules.NewGuid, Rules.HashIteration, Rules.UnstableSort,
            Rules.CultureString, Rules.Threading, Rules.HashCodeOrder, Rules.MutableStatic, Rules.ThreadStatic, Rules.UnsafeCode,
            Rules.MemoryReinterpret, Rules.RawDivision, Rules.CheckedContext, Rules.Suppression, Rules.OutsideCoreHash,
            Rules.Sockets, Rules.PaidValue, Rules.ForbiddenSyntax, Rules.ForbiddenApi, Rules.LayerReference);

        // 원시 나눗셈을 허용하는 도우미(CONTRACTS 1.1·1.2)
        private static readonly string[] DivisionHelperTypes = { "Wizard.Core.FixMath", "Wizard.Core.Wide" };

        private static readonly string[] ClockTypes = { "System.DateTime", "System.DateTimeOffset", "System.Diagnostics.Stopwatch", "System.TimeProvider" };

        private static readonly string[] ThreadingTypes =
        {
            "System.Threading.Thread", "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.Parallel",
            "System.Threading.ThreadPool", "System.Threading.Timer", "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1",
        };

        private static readonly string[] ReinterpretTypes =
        {
            "System.BitConverter", "System.Runtime.InteropServices.MemoryMarshal", "System.Runtime.CompilerServices.Unsafe",
            "System.Runtime.InteropServices.Marshal", "System.Buffer",
        };

        private static readonly string[] HashOrderedTypes =
        {
            "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1",
            "System.Collections.Generic.Dictionary`2+KeyCollection", "System.Collections.Generic.Dictionary`2+ValueCollection",
            "System.Collections.Hashtable", "System.Collections.Concurrent.ConcurrentDictionary`2",
        };

        private static readonly string[] SharedForbiddenTypes =
        {
            "System.Int128", "System.UInt128", "System.Numerics.BitOperations", "System.Security.Cryptography.ChaCha20Poly1305",
        };

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext start)
        {
            string? asm = start.Compilation.AssemblyName;
            bool core = AssemblyRoles.IsCore(asm);
            bool shared = AssemblyRoles.IsShared(asm);
            bool server = AssemblyRoles.IsServer(asm);
            if (!core && !shared && !server)
            {
                return;
            }
            var a = new Checker(asm!, core, shared);

            if (shared || server)
            {
                start.RegisterCompilationEndAction(a.CheckReferences);
            }
            if (!shared)
            {
                return;   // 서버는 참조 검사만 받는다
            }
            start.RegisterSyntaxNodeAction(a.OnPredefinedType, SyntaxKind.PredefinedType);
            start.RegisterSyntaxNodeAction(a.OnName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
            start.RegisterSyntaxNodeAction(a.OnRecord, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
            start.RegisterSyntaxNodeAction(a.OnInitAccessor, SyntaxKind.InitAccessorDeclaration);
            start.RegisterSyntaxNodeAction(a.OnChecked, SyntaxKind.CheckedExpression, SyntaxKind.CheckedStatement);
            start.RegisterSyntaxNodeAction(a.OnUnsafe, SyntaxKind.UnsafeStatement, SyntaxKind.FixedStatement, SyntaxKind.PointerType,
                SyntaxKind.FunctionPointerType);
            start.RegisterSyntaxNodeAction(a.OnModifiers, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, SyntaxKind.MethodDeclaration,
                SyntaxKind.FieldDeclaration, SyntaxKind.PropertyDeclaration, SyntaxKind.ConstructorDeclaration, SyntaxKind.LocalFunctionStatement,
                SyntaxKind.OperatorDeclaration, SyntaxKind.IndexerDeclaration);
            start.RegisterSyntaxNodeAction(a.OnPragma, SyntaxKind.PragmaWarningDirectiveTrivia);
            start.RegisterOperationAction(a.OnTypedOperation, OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.FieldReference,
                OperationKind.Conversion, OperationKind.Literal, OperationKind.LocalReference, OperationKind.ParameterReference,
                OperationKind.Binary, OperationKind.ObjectCreation);
            start.RegisterOperationAction(a.OnInvocation, OperationKind.Invocation);
            start.RegisterOperationAction(a.OnBinary, OperationKind.Binary, OperationKind.CompoundAssignment);
            start.RegisterOperationAction(a.OnForEach, OperationKind.Loop);
            start.RegisterSymbolAction(a.OnField, SymbolKind.Field);
            start.RegisterSymbolAction(a.OnProperty, SymbolKind.Property);
            start.RegisterSymbolAction(a.OnMemberSignature, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field);
            start.RegisterSymbolAction(a.OnAttributes, SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Field, SymbolKind.Property);
            start.RegisterCompilationEndAction(a.OnAssemblyAttributes);
        }

        private sealed class Checker
        {
            private readonly string _asm;
            private readonly bool _core;
            private readonly bool _shared;
            private readonly bool _floatBanned;
            private readonly bool _clockBanned;

            public Checker(string asm, bool core, bool shared)
            {
                _asm = asm;
                _core = core;
                _shared = shared;
                _floatBanned = AssemblyRoles.BansFloatingPoint(asm);
                _clockBanned = AssemblyRoles.BansClock(asm);
            }

            // ---------- 참조(WZ0023) ----------
            public void CheckReferences(CompilationAnalysisContext ctx)
            {
                foreach (AssemblyIdentity id in ctx.Compilation.ReferencedAssemblyNames)
                {
                    if (AssemblyRoles.IsForbiddenReference(_asm, id.Name))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.LayerReference, Location.None, _asm, id.Name));
                    }
                }
            }

            // ---------- 실수형(WZ0001) ----------
            public void OnPredefinedType(SyntaxNodeAnalysisContext ctx)
            {
                if (!_floatBanned)
                {
                    return;
                }
                var node = (PredefinedTypeSyntax)ctx.Node;
                SyntaxKind k = node.Keyword.Kind();
                if (k == SyntaxKind.FloatKeyword || k == SyntaxKind.DoubleKeyword || k == SyntaxKind.DecimalKeyword)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.FloatingPoint, node.GetLocation(), _asm, node.Keyword.Text));
                }
            }

            public void OnTypedOperation(OperationAnalysisContext ctx)
            {
                if (!_floatBanned)
                {
                    return;
                }
                ITypeSymbol? t = ctx.Operation.Type;
                if (IsFloating(t))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.FloatingPoint, ctx.Operation.Syntax.GetLocation(), _asm, t!.ToDisplayString()));
                }
            }

            public void OnMemberSignature(SymbolAnalysisContext ctx)
            {
                if (!_floatBanned || ctx.Symbol.IsImplicitlyDeclared)
                {
                    return;
                }
                IEnumerable<ITypeSymbol> types = ctx.Symbol switch
                {
                    IMethodSymbol m => m.Parameters.Select(p => p.Type).Concat(new[] { m.ReturnType }),
                    IPropertySymbol p => new[] { p.Type },
                    IFieldSymbol f => new[] { f.Type },
                    _ => Array.Empty<ITypeSymbol>(),
                };
                foreach (ITypeSymbol t in types)
                {
                    if (ContainsFloating(t))
                    {
                        Location loc = ctx.Symbol.Locations.FirstOrDefault() ?? Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.FloatingPoint, loc, _asm, t.ToDisplayString()));
                        return;
                    }
                }
            }

            // ---------- 이름 참조: 금지 타입·멤버 ----------
            public void OnName(SyntaxNodeAnalysisContext ctx)
            {
                SymbolInfo info = ctx.SemanticModel.GetSymbolInfo(ctx.Node, ctx.CancellationToken);
                ISymbol? sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                if (sym == null)
                {
                    return;
                }
                Location loc = ctx.Node.GetLocation();
                INamedTypeSymbol? type = sym as INamedTypeSymbol ?? sym.ContainingType;
                string typeName = type == null ? string.Empty : MetadataName(type);

                if (_floatBanned && sym is INamedTypeSymbol nt && IsFloating(nt))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.FloatingPoint, loc, _asm, nt.ToDisplayString()));
                }
                if (_core && typeName == "System.Random")
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.SystemRandom, loc, _asm));
                }
                if (_clockBanned)
                {
                    if (Array.IndexOf(ClockTypes, typeName) >= 0)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Clock, loc, _asm, typeName));
                    }
                    else if (typeName == "System.Environment" && (sym.Name == "TickCount" || sym.Name == "TickCount64"))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Clock, loc, _asm, "Environment." + sym.Name));
                    }
                }
                if (_core)
                {
                    if (typeName == "System.Guid" && sym.Name == "NewGuid")
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.NewGuid, loc, _asm));
                    }
                    if (Array.IndexOf(ThreadingTypes, typeName) >= 0)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Threading, loc, _asm, typeName));
                    }
                    if (typeName == "System.Threading.ThreadLocal`1" || typeName == "System.ThreadStaticAttribute")
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.ThreadStatic, loc, _asm));
                    }
                    if (Array.IndexOf(ReinterpretTypes, typeName) >= 0)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.MemoryReinterpret, loc, _asm, typeName));
                    }
                    if (typeName == "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute")
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, loc, _asm, "SuppressMessage"));
                    }
                    CheckOutsideCoreHash(ctx, sym, type, loc);
                    CheckPaidValue(ctx, sym, loc);
                }
                if (_asm == "Wizard.Netcode" && type != null && NamespaceOf(type).StartsWith("System.Net.Sockets", StringComparison.Ordinal))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.Sockets, loc, _asm, typeName));
                }
                if (_asm == "Wizard.Rules")
                {
                    CheckPaidValue(ctx, sym, loc);
                }
                if (_shared && type != null)
                {
                    if (Array.IndexOf(SharedForbiddenTypes, typeName) >= 0 || NamespaceOf(type).StartsWith("System.Text.Json", StringComparison.Ordinal))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenApi, loc, _asm, typeName));
                    }
                    else if (typeName == "System.Math" && sym.Name == "BigMul")
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenApi, loc, _asm, "Math.BigMul"));
                    }
                }
            }

            // WZ0017: Sim 과 해시 안의 Content 는 Content.Rules, ErrorCode, Error, Result 를 참조하지 않는다(T-18)
            private void CheckOutsideCoreHash(SyntaxNodeAnalysisContext ctx, ISymbol sym, INamedTypeSymbol? type, Location loc)
            {
                bool applies = _asm == "Wizard.Sim" || (_asm == "Wizard.Content" && !InFolder(ctx.Node.SyntaxTree, "/Rules/"));
                if (!applies || type == null)
                {
                    return;
                }
                string ns = NamespaceOf(type);
                string name = MetadataName(type);
                if (ns == "Wizard.Content.Rules" || ns.StartsWith("Wizard.Content.Rules.", StringComparison.Ordinal)
                    || name == "Wizard.Core.ErrorCode" || name == "Wizard.Core.Error" || name == "Wizard.Core.Result`1")
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.OutsideCoreHash, loc, _asm, type.ToDisplayString()));
                }
            }

            // WZ0020: Sim 전부, Rules 의 강화·매치·리그 네임스페이스(spec 12 10.7)
            private void CheckPaidValue(SyntaxNodeAnalysisContext ctx, ISymbol sym, Location loc)
            {
                if (_asm == "Wizard.Rules")
                {
                    string ns = ctx.ContainingSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                    if (!(ns.StartsWith("Wizard.Rules.Enhance", StringComparison.Ordinal) || ns.StartsWith("Wizard.Rules.League", StringComparison.Ordinal)
                          || ns.StartsWith("Wizard.Rules.Match", StringComparison.Ordinal)))
                    {
                        return;
                    }
                }
                else if (_asm != "Wizard.Sim")
                {
                    return;
                }
                if (sym is IFieldSymbol f && f.ContainingType != null)
                {
                    string owner = f.ContainingType.Name;
                    if ((owner == "CurrencyCode" && f.Name == "Starlight") || (owner == "CurrencyBucket" && f.Name == "Paid")
                        || (owner == "ItemOrigin" && f.Name == "Paid"))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.PaidValue, loc, _asm, owner + "." + f.Name));
                    }
                }
            }

            // ---------- 호출: 정렬, 문화권, GetHashCode, 해시 순서 LINQ ----------
            public void OnInvocation(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                var inv = (IInvocationOperation)ctx.Operation;
                IMethodSymbol m = inv.TargetMethod;
                string owner = MetadataName(m.ContainingType);
                Location loc = inv.Syntax.GetLocation();

                if ((owner == "System.Array" && m.Name == "Sort") || (owner == "System.Collections.Generic.List`1" && m.Name == "Sort")
                    || (owner == "System.Linq.Enumerable" && (m.Name == "OrderBy" || m.Name == "OrderByDescending" || m.Name == "ThenBy"
                        || m.Name == "ThenByDescending" || m.Name == "Order" || m.Name == "OrderDescending"))
                    || (owner == "System.MemoryExtensions" && m.Name == "Sort"))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnstableSort, loc, _asm, owner + "." + m.Name));
                }

                if (m.Name == "GetHashCode" && m.Parameters.Length == 0 && !InsideGetHashCodeOverride(ctx.ContainingSymbol))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashCodeOrder, loc, _asm));
                }

                if (owner == "System.Linq.Enumerable" && inv.Arguments.Length > 0)
                {
                    ITypeSymbol? source = Unconvert(inv.Arguments[0].Value).Type;
                    if (IsHashOrdered(source))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashIteration, loc, _asm, source!.ToDisplayString()));
                    }
                }

                string? culture = CultureProblem(m, inv);
                if (culture != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, loc, _asm, culture));
                }
            }

            private static string? CultureProblem(IMethodSymbol m, IInvocationOperation inv)
            {
                string owner = MetadataName(m.ContainingType);
                bool hasComparison = m.Parameters.Any(p => p.Type.Name == "StringComparison");
                bool hasProvider = m.Parameters.Any(p => p.Type.Name == "IFormatProvider" || p.Type.Name == "CultureInfo");
                if (owner == "System.String")
                {
                    switch (m.Name)
                    {
                        case "Compare":
                        case "CompareTo":
                            if (!hasComparison)
                            {
                                return "string." + m.Name;
                            }
                            break;
                        case "ToUpper":
                        case "ToLower":
                            return "string." + m.Name;
                        case "StartsWith":
                        case "EndsWith":
                        case "IndexOf":
                        case "LastIndexOf":
                            if (!hasComparison && m.Parameters.Length > 0 && m.Parameters[0].Type.SpecialType == SpecialType.System_String)
                            {
                                return "string." + m.Name + "(string)";
                            }
                            break;
                        case "Format":
                            if (!hasProvider)
                            {
                                return "string.Format";
                            }
                            break;
                    }
                    if (hasComparison)
                    {
                        foreach (IArgumentOperation arg in inv.Arguments)
                        {
                            if (arg.Parameter?.Type.Name == "StringComparison" && arg.Value.ConstantValue.HasValue
                                && arg.Value.ConstantValue.Value is int v && v < 4)
                            {
                                return "StringComparison (culture)";   // CurrentCulture* = 0,1, InvariantCulture* = 2,3
                            }
                        }
                    }
                    return null;
                }
                if (IsNumeric(m.ContainingType) && (m.Name == "ToString" || m.Name == "Parse" || m.Name == "TryParse") && !hasProvider)
                {
                    return m.ContainingType.Name + "." + m.Name;
                }
                if (owner == "System.Convert" && m.Name.StartsWith("To", StringComparison.Ordinal) && !hasProvider
                    && m.Parameters.Length > 0 && m.Parameters[0].Type.SpecialType == SpecialType.System_String)
                {
                    return "Convert." + m.Name + "(string)";
                }
                return null;
            }

            // ---------- 원시 나눗셈(WZ0014) ----------
            public void OnBinary(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                BinaryOperatorKind kind;
                IOperation right;
                IMethodSymbol? userOp;
                ITypeSymbol? leftType;
                if (ctx.Operation is IBinaryOperation b)
                {
                    kind = b.OperatorKind;
                    right = b.RightOperand;
                    userOp = b.OperatorMethod;
                    leftType = b.LeftOperand.Type;
                }
                else if (ctx.Operation is ICompoundAssignmentOperation c)
                {
                    kind = c.OperatorKind;
                    right = c.Value;
                    userOp = c.OperatorMethod;
                    leftType = c.Target.Type;
                }
                else
                {
                    return;
                }
                if ((kind != BinaryOperatorKind.Divide && kind != BinaryOperatorKind.Remainder) || userOp != null)
                {
                    return;
                }
                if (!IsIntegral(leftType) || right.ConstantValue.HasValue)
                {
                    return;
                }
                INamedTypeSymbol? container = ctx.ContainingSymbol?.ContainingType;
                if (container != null && Array.IndexOf(DivisionHelperTypes, MetadataName(container)) >= 0)
                {
                    return;
                }
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.RawDivision, ctx.Operation.Syntax.GetLocation(), _asm,
                    kind == BinaryOperatorKind.Divide ? "/" : "%"));
            }

            // ---------- 해시 순서 순회(WZ0005) ----------
            public void OnForEach(OperationAnalysisContext ctx)
            {
                if (!_core || !(ctx.Operation is IForEachLoopOperation loop))
                {
                    return;
                }
                ITypeSymbol? t = Unconvert(loop.Collection).Type;
                if (IsHashOrdered(t))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashIteration, loop.Collection.Syntax.GetLocation(), _asm, t!.ToDisplayString()));
                }
            }

            // ---------- 가변 정적(WZ0010) ----------
            public void OnField(SymbolAnalysisContext ctx)
            {
                var f = (IFieldSymbol)ctx.Symbol;
                if (!_core || !f.IsStatic || f.IsConst || f.IsImplicitlyDeclared)
                {
                    return;
                }
                if (f.AssociatedSymbol is IPropertySymbol)
                {
                    return;   // 자동 속성의 뒤 필드는 OnProperty 가 본다
                }
                if (f.IsReadOnly && IsImmutableValue(f.Type, f.Locations))
                {
                    return;
                }
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.MutableStatic, f.Locations.FirstOrDefault() ?? Location.None, _asm, f.Name));
            }

            public void OnProperty(SymbolAnalysisContext ctx)
            {
                var p = (IPropertySymbol)ctx.Symbol;
                if (!_core || !p.IsStatic || p.IsImplicitlyDeclared)
                {
                    return;
                }
                if (p.SetMethod != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.MutableStatic, p.Locations.FirstOrDefault() ?? Location.None, _asm, p.Name));
                }
            }

            // ---------- 특성: ThreadStatic, SuppressMessage ----------
            public void OnAttributes(SymbolAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                foreach (AttributeData attr in ctx.Symbol.GetAttributes())
                {
                    string name = attr.AttributeClass == null ? string.Empty : MetadataName(attr.AttributeClass);
                    Location loc = attr.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken).GetLocation() ?? Location.None;
                    if (name == "System.ThreadStaticAttribute")
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.ThreadStatic, loc, _asm));
                    }
                }
            }

            public void OnAssemblyAttributes(CompilationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                foreach (AttributeData attr in ctx.Compilation.Assembly.GetAttributes().Concat(ctx.Compilation.SourceModule.GetAttributes()))
                {
                    if (attr.AttributeClass != null && MetadataName(attr.AttributeClass) == "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute")
                    {
                        Location loc = attr.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken).GetLocation() ?? Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, loc, _asm, "SuppressMessage"));
                    }
                }
                foreach (KeyValuePair<string, ReportDiagnostic> kv in ctx.Compilation.Options.SpecificDiagnosticOptions)
                {
                    if (kv.Key.StartsWith("WZ", StringComparison.Ordinal) && kv.Value != ReportDiagnostic.Error && kv.Value != ReportDiagnostic.Default)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, Location.None, _asm, kv.Key + " severity changed"));
                    }
                }
            }

            // ---------- 문법 ----------
            public void OnRecord(SyntaxNodeAnalysisContext ctx)
            {
                var node = (RecordDeclarationSyntax)ctx.Node;
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, node.Keyword.GetLocation(), _asm, "record"));
            }

            public void OnInitAccessor(SyntaxNodeAnalysisContext ctx)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenSyntax, ctx.Node.GetLocation(), _asm, "init"));
            }

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

            public void OnModifiers(SyntaxNodeAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                SyntaxTokenList mods = ctx.Node switch
                {
                    MemberDeclarationSyntax m => m.Modifiers,
                    LocalFunctionStatementSyntax l => l.Modifiers,
                    _ => default,
                };
                foreach (SyntaxToken t in mods)
                {
                    if (t.IsKind(SyntaxKind.UnsafeKeyword))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnsafeCode, t.GetLocation(), _asm));
                    }
                }
            }

            public void OnPragma(SyntaxNodeAnalysisContext ctx)
            {
                if (_core)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.Suppression, ctx.Node.GetLocation(), _asm, "#pragma warning"));
                }
            }

            // ---------- 도우미 ----------
            private static bool InsideGetHashCodeOverride(ISymbol? s)
            {
                for (ISymbol? cur = s; cur != null; cur = cur.ContainingSymbol)
                {
                    if (cur is IMethodSymbol m && m.Name == "GetHashCode" && m.IsOverride && m.Parameters.Length == 0)
                    {
                        return true;
                    }
                    if (cur is INamedTypeSymbol)
                    {
                        break;
                    }
                }
                return false;
            }

            // 불변 값: 원시형, 열거형, 문자열, readonly struct. 배열은 생성 표(Generated/*.g.cs)에서만.
            private static bool IsImmutableValue(ITypeSymbol t, ImmutableArray<Location> locations)
            {
                if (t.TypeKind == TypeKind.Enum || t.SpecialType == SpecialType.System_String)
                {
                    return true;
                }
                if (t.SpecialType != SpecialType.None && t.IsValueType)
                {
                    return true;
                }
                if (t.TypeKind == TypeKind.Struct && t.IsReadOnly)
                {
                    return true;
                }
                if (t is IArrayTypeSymbol)
                {
                    foreach (Location loc in locations)
                    {
                        string path = (loc.SourceTree?.FilePath ?? string.Empty).Replace('\\', '/');
                        if (!(path.Contains("/Generated/") && path.EndsWith(".g.cs", StringComparison.Ordinal)))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            private static bool IsFloating(ITypeSymbol? t) =>
                t != null && (t.SpecialType == SpecialType.System_Single || t.SpecialType == SpecialType.System_Double
                              || t.SpecialType == SpecialType.System_Decimal);

            private static bool ContainsFloating(ITypeSymbol t)
            {
                if (IsFloating(t))
                {
                    return true;
                }
                if (t is IArrayTypeSymbol arr)
                {
                    return ContainsFloating(arr.ElementType);
                }
                if (t is INamedTypeSymbol n && n.IsGenericType)
                {
                    return n.TypeArguments.Any(ContainsFloating);
                }
                return false;
            }

            private static bool IsIntegral(ITypeSymbol? t)
            {
                if (t == null)
                {
                    return false;
                }
                if (t is INamedTypeSymbol n && n.EnumUnderlyingType != null)
                {
                    return true;
                }
                switch (t.SpecialType)
                {
                    case SpecialType.System_SByte:
                    case SpecialType.System_Byte:
                    case SpecialType.System_Int16:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_Int32:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Char:
                    case SpecialType.System_IntPtr:
                    case SpecialType.System_UIntPtr:
                        return true;
                    default:
                        return false;
                }
            }

            private static bool IsNumeric(INamedTypeSymbol? t) =>
                t != null && (IsIntegral(t) || IsFloating(t));

            private static bool IsHashOrdered(ITypeSymbol? t)
            {
                if (!(t is INamedTypeSymbol n))
                {
                    return false;
                }
                return Array.IndexOf(HashOrderedTypes, MetadataName(n)) >= 0;
            }

            private static IOperation Unconvert(IOperation op)
            {
                while (op is IConversionOperation c && c.IsImplicit)
                {
                    op = c.Operand;
                }
                return op;
            }

            private static bool InFolder(SyntaxTree tree, string folder) =>
                (tree.FilePath ?? string.Empty).Replace('\\', '/').Contains(folder);

            private static string NamespaceOf(INamedTypeSymbol t) =>
                t.ContainingNamespace == null || t.ContainingNamespace.IsGlobalNamespace ? string.Empty : t.ContainingNamespace.ToDisplayString();

            // "System.Collections.Generic.Dictionary`2+KeyCollection" 꼴
            private static string MetadataName(INamedTypeSymbol? t)
            {
                if (t == null)
                {
                    return string.Empty;
                }
                INamedTypeSymbol def = t.OriginalDefinition;
                if (def.ContainingType != null)
                {
                    return MetadataName(def.ContainingType) + "+" + def.MetadataName;
                }
                string ns = NamespaceOf(def);
                return ns.Length == 0 ? def.MetadataName : ns + "." + def.MetadataName;
            }
        }
    }
}
