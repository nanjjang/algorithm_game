using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Wizard.Analyzers
{
    public sealed partial class DeterminismAnalyzer
    {
        // 이름으로 쓰기만 해도 막는 타입·멤버(선언, 매개변수, 호출, 특성 모두)
        private sealed partial class Checker
        {
            private static readonly string[] RandomTypes = { "System.Random", "System.Security.Cryptography.RandomNumberGenerator" };

            private static readonly string[] ClockTypes =
            {
                "System.DateTime", "System.DateTimeOffset", "System.Diagnostics.Stopwatch", "System.TimeProvider", "System.TimeZoneInfo",
            };

            // System.Threading·System.Threading.Tasks·System.Timers 네임스페이스 전부에 더해
            private static readonly string[] ThreadingTypes = { "System.Linq.ParallelEnumerable", "System.Linq.ParallelQuery", "System.Linq.ParallelQuery`1" };

            private static readonly string[] ThreadLocalTypes = { "System.Threading.ThreadLocal`1", "System.ThreadStaticAttribute" };

            private static readonly string[] ReinterpretTypes =
            {
                "System.BitConverter", "System.Runtime.InteropServices.MemoryMarshal", "System.Runtime.CompilerServices.Unsafe",
                "System.Runtime.InteropServices.Marshal", "System.Buffer", "System.Runtime.InteropServices.FieldOffsetAttribute",
            };

            // System.Collections.Comparer(Default, DefaultInvariant)와 대소문자 무시 비교자는 문화권으로 비교한다
            private static readonly string[] CultureTypes =
            {
                "System.Globalization.CompareInfo", "System.Globalization.TextInfo", "System.Collections.Comparer",
                "System.Collections.CaseInsensitiveComparer", "System.Collections.CaseInsensitiveHashCodeProvider",
            };

            // 공유 패키지에서 쓸 수 없는 타입(ARCHITECTURE 3.3). 네임스페이스 System.Text.Json 전부와 제네릭 수학 인터페이스도 막는다
            private static readonly string[] SharedForbiddenTypes =
            {
                "System.Int128", "System.UInt128", "System.Numerics.BitOperations", "System.Security.Cryptography.ChaCha20Poly1305",
            };

            // CONTRACTS 1.3, T-18 의 해시 밖 타입(src/Wizard.Core/Errors/)
            private static readonly string[] CoreErrorTypes = { "Wizard.Core.ErrorCode", "Wizard.Core.Error", "Wizard.Core.Result`1", "Wizard.Core.Unit" };

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
                string typeName = SymbolUtil.MetadataName(type);

                if (_floatBanned && sym is INamedTypeSymbol nt && SymbolUtil.IsFloatingLike(nt))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.FloatingPoint, loc, _asm, nt.ToDisplayString()));
                }
                if (_clockBanned)
                {
                    CheckClock(ctx, sym, typeName, loc);
                }
                if (_core && type != null)
                {
                    CheckCoreName(ctx, sym, type, typeName, loc);
                }
                if (_asm == "Wizard.Netcode" && type != null
                    && (SymbolUtil.InNamespace(type, "System.Net.Sockets") || SymbolUtil.InNamespace(type, "System.Net.WebSockets")))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.Sockets, loc, _asm, typeName));
                }
                if (_asm == "Wizard.Sim" || _asm == "Wizard.Rules")
                {
                    CheckPaidValue(ctx, sym, loc);
                }
                if (_shared && type != null)
                {
                    CheckSharedApi(ctx, sym, type, typeName, loc);
                }
            }

            private void CheckClock(SyntaxNodeAnalysisContext ctx, ISymbol sym, string typeName, Location loc)
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

            private void CheckCoreName(SyntaxNodeAnalysisContext ctx, ISymbol sym, INamedTypeSymbol type, string typeName, Location loc)
            {
                if (Array.IndexOf(RandomTypes, typeName) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.SystemRandom, loc, _asm));
                }
                if (typeName == "System.Guid" && sym.Name == "NewGuid")
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.NewGuid, loc, _asm));
                }
                if (Array.IndexOf(ThreadLocalTypes, typeName) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.ThreadStatic, loc, _asm));
                }
                else if (SymbolUtil.InNamespace(type, "System.Threading") || SymbolUtil.InNamespace(type, "System.Timers")
                         || Array.IndexOf(ThreadingTypes, typeName) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.Threading, loc, _asm, typeName));
                }
                if (typeName == "System.HashCode" && !InsideGetHashCode(ctx.ContainingSymbol))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashCodeOrder, loc, _asm));
                }
                if (Array.IndexOf(ReinterpretTypes, typeName) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.MemoryReinterpret, loc, _asm, typeName));
                }
                if (Array.IndexOf(CultureTypes, typeName) >= 0
                    || (typeName == "System.Globalization.CultureInfo" && sym.Name.StartsWith("Current", StringComparison.Ordinal))
                    || (typeName == "System.Globalization.CultureInfo" && sym.Name.StartsWith("DefaultThreadCurrent", StringComparison.Ordinal))
                    || (typeName == "System.StringComparer" && (sym.Name.StartsWith("CurrentCulture", StringComparison.Ordinal)
                                                                 || sym.Name.StartsWith("InvariantCulture", StringComparison.Ordinal))))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, loc, _asm, typeName + "." + sym.Name));
                }
                string? comparer = DefaultComparerMember(sym, ctx.SemanticModel.Compilation);
                if (comparer != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, loc, _asm, comparer));
                }
                if (typeName == "System.Math" && sym.Name == "DivRem" && !InDivisionHelper(ctx.ContainingSymbol))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.RawDivision, loc, _asm, "Math.DivRem"));
                }
                CheckOutsideCoreHash(ctx, sym, type, loc);
            }

            // WZ0017: Sim 과 해시 안의 Content 는 Content.Rules, ErrorCode, Error, Result, Unit 을 참조하지 않는다(T-18).
            // 이름이 가리키는 타입과 멤버를 담는 타입뿐 아니라 멤버 자신의 타입(속성·필드·이벤트의 타입, 메서드의 반환·매개변수,
            // 인덱서의 매개변수)도 본다. 그래서 해시 안의 타입(ContentDb)에 붙은 규칙 쪽 접근자(CONTRACTS 2.3, db.Net,
            // db.Titles, db.Item(..))와 Result·ErrorCode 를 돌려주는 메서드를 부르는 것도 막는다.
            private void CheckOutsideCoreHash(SyntaxNodeAnalysisContext ctx, ISymbol sym, INamedTypeSymbol type, Location loc)
            {
                if (!OutsideCoreHashApplies(ctx.Node.SyntaxTree))
                {
                    return;
                }
                INamedTypeSymbol? hit = IsOutsideCoreHash(type) ? type : FindInSignature(MemberOf(ctx, sym));
                if (hit != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.OutsideCoreHash, loc, _asm, hit.ToDisplayString()));
                }
            }

            // 이름이 나오지 않는 멤버 사용: 인덱서(db[c]), 사용자 정의 연산자·변환
            public void OnOutsideCoreHashOperation(OperationAnalysisContext ctx)
            {
                if (!OutsideCoreHashApplies(ctx.Operation.Syntax.SyntaxTree))
                {
                    return;
                }
                ISymbol? member = ctx.Operation switch
                {
                    IPropertyReferenceOperation p when p.Property.IsIndexer => p.Property,
                    IBinaryOperation b => b.OperatorMethod,
                    IUnaryOperation u => u.OperatorMethod,
                    ICompoundAssignmentOperation c => c.OperatorMethod,
                    IConversionOperation v => v.OperatorMethod,
                    IIncrementOrDecrementOperation i => i.OperatorMethod,
                    _ => null,
                };
                INamedTypeSymbol? hit = member == null ? null : FindInSignature(member);
                if (hit != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.OutsideCoreHash, ctx.Operation.Syntax.GetLocation(), _asm, hit.ToDisplayString()));
                }
            }

            private bool OutsideCoreHashApplies(SyntaxTree tree) =>
                _asm == "Wizard.Sim" || (_asm == "Wizard.Content" && !IsContentRulesFile(tree));

            private static INamedTypeSymbol? FindInSignature(ISymbol member)
            {
                foreach (ITypeSymbol t in SignatureTypes(member))
                {
                    INamedTypeSymbol? hit = FindOutsideCoreHash(t, 0);
                    if (hit != null)
                    {
                        return hit;
                    }
                }
                return null;
            }

            private static bool IsOutsideCoreHash(INamedTypeSymbol t) =>
                SymbolUtil.InNamespace(t, "Wizard.Content.Rules") || Array.IndexOf(CoreErrorTypes, SymbolUtil.MetadataName(t)) >= 0;

            // 배열 원소, 제네릭 인자(바깥 타입의 인자 포함), 포인터, 타입 매개변수의 제약까지 내려가 해시 밖 타입을 찾는다
            private static INamedTypeSymbol? FindOutsideCoreHash(ITypeSymbol? t, int depth)
            {
                if (t == null || depth > 8)
                {
                    return null;
                }
                switch (t)
                {
                    case IArrayTypeSymbol arr:
                        return FindOutsideCoreHash(arr.ElementType, depth + 1);
                    case IPointerTypeSymbol ptr:
                        return FindOutsideCoreHash(ptr.PointedAtType, depth + 1);
                    case ITypeParameterSymbol tp:
                        foreach (ITypeSymbol c in tp.ConstraintTypes)
                        {
                            INamedTypeSymbol? h = FindOutsideCoreHash(c, depth + 1);
                            if (h != null)
                            {
                                return h;
                            }
                        }
                        return null;
                    case INamedTypeSymbol n:
                        if (IsOutsideCoreHash(n))
                        {
                            return n;
                        }
                        for (INamedTypeSymbol? cur = n; cur != null; cur = cur.ContainingType)
                        {
                            foreach (ITypeSymbol a in cur.TypeArguments)
                            {
                                INamedTypeSymbol? h = FindOutsideCoreHash(a, depth + 1);
                                if (h != null)
                                {
                                    return h;
                                }
                            }
                        }
                        return null;
                    default:
                        return null;
                }
            }

            // new T(..) 의 T 이름은 타입을 가리키므로, 그 자리에서는 불리는 생성자를 멤버로 본다
            private static ISymbol MemberOf(SyntaxNodeAnalysisContext ctx, ISymbol sym)
            {
                if (sym is INamedTypeSymbol && ctx.Node.Parent is ObjectCreationExpressionSyntax oc && oc.Type == ctx.Node)
                {
                    ISymbol? ctor = ctx.SemanticModel.GetSymbolInfo(oc, ctx.CancellationToken).Symbol;
                    if (ctor != null)
                    {
                        return ctor;
                    }
                }
                return sym;
            }

            private static ITypeSymbol[] SignatureTypes(ISymbol member) => member switch
            {
                IMethodSymbol m => m.Parameters.Select(p => p.Type).Concat(new[] { m.ReturnType }).Concat(m.TypeArguments).ToArray(),
                IPropertySymbol p => p.Parameters.Select(x => x.Type).Concat(new[] { p.Type }).ToArray(),
                IFieldSymbol f => new[] { f.Type },
                IEventSymbol e => new[] { e.Type },
                ILocalSymbol l => new[] { l.Type },
                IParameterSymbol p => new[] { p.Type },
                _ => Array.Empty<ITypeSymbol>(),
            };

            // src/Wizard.Content/Rules/ 아래의 파일(해시 밖)
            private static bool IsContentRulesFile(SyntaxTree tree)
            {
                string[]? folders = NamespaceFolder.FolderSegments("Wizard.Content", tree.FilePath);
                return folders != null && folders.Length > 0 && folders[0] == "Rules";
            }

            // WZ0020: Sim 전부, Rules 의 강화·매치·리그 네임스페이스(spec 12 10.7, ARCHITECTURE 3.2)
            private void CheckPaidValue(SyntaxNodeAnalysisContext ctx, ISymbol sym, Location loc)
            {
                if (_asm == "Wizard.Rules")
                {
                    string ns = ctx.ContainingSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                    if (!(IsUnder(ns, "Wizard.Rules.Enhance") || IsUnder(ns, "Wizard.Rules.Match") || IsUnder(ns, "Wizard.Rules.League")))
                    {
                        return;
                    }
                }
                if (!(sym is IFieldSymbol) && !(sym is IPropertySymbol))
                {
                    return;
                }
                string owner = sym.ContainingType?.Name ?? string.Empty;
                if ((owner == "CurrencyCode" && sym.Name == "Starlight") || (owner == "CurrencyBucket" && sym.Name == "Paid")
                    || (owner == "ItemOrigin" && sym.Name == "Paid") || (owner == "Entitlement" && sym.Name == "Source"))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.PaidValue, loc, _asm, owner + "." + sym.Name));
                }
            }

            private static bool IsUnder(string ns, string root) => ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal);

            // WZ0022: Unity 가 컴파일하지 못하거나 런타임에 없는 API(ARCHITECTURE 3.3)
            private void CheckSharedApi(SyntaxNodeAnalysisContext ctx, ISymbol sym, INamedTypeSymbol type, string typeName, Location loc)
            {
                if (Array.IndexOf(SharedForbiddenTypes, typeName) >= 0 || SymbolUtil.InNamespace(type, "System.Text.Json"))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenApi, loc, _asm, typeName));
                }
                else if (type.TypeKind == TypeKind.Interface && type.IsGenericType && SymbolUtil.NamespaceOf(type) == "System.Numerics")
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenApi, loc, _asm, typeName + " (generic math)"));
                }
                else if (typeName == "System.Math" && sym is IMethodSymbol m && m.Name == "BigMul" && !IsIntBigMul(m))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.ForbiddenApi, loc, _asm, "Math.BigMul"));
                }
            }

            // Math.BigMul(int, int) 은 .NET Standard 2.1 에 있다. 64비트 판만 막는다
            private static bool IsIntBigMul(IMethodSymbol m) =>
                m.Parameters.Length == 2 && m.Parameters[0].Type.SpecialType == SpecialType.System_Int32
                                         && m.Parameters[1].Type.SpecialType == SpecialType.System_Int32;

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
                if (SymbolUtil.IsFloatingLike(t))
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
                ITypeSymbol[] types = ctx.Symbol switch
                {
                    IMethodSymbol m => m.Parameters.Select(p => p.Type).Concat(new[] { m.ReturnType }).ToArray(),
                    IPropertySymbol p => p.Parameters.Select(x => x.Type).Concat(new[] { p.Type }).ToArray(),
                    IFieldSymbol f => new[] { f.Type },
                    IEventSymbol e => new[] { e.Type },
                    _ => Array.Empty<ITypeSymbol>(),
                };
                foreach (ITypeSymbol t in types)
                {
                    if (SymbolUtil.ContainsFloating(t))
                    {
                        Location loc = ctx.Symbol.Locations.FirstOrDefault() ?? Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.FloatingPoint, loc, _asm, t.ToDisplayString()));
                        return;
                    }
                }
            }

            // ---------- 도우미 ----------
            // GetHashCode 를 구현하는 메서드(재정의, IEqualityComparer<T>.GetHashCode) 안인가
            private static bool InsideGetHashCode(ISymbol? s)
            {
                for (ISymbol? cur = s; cur != null; cur = cur.ContainingSymbol)
                {
                    if (cur is IMethodSymbol m && m.MethodKind != MethodKind.AnonymousFunction && m.MethodKind != MethodKind.LocalFunction)
                    {
                        return m.Name == "GetHashCode";
                    }
                    if (cur is INamedTypeSymbol)
                    {
                        break;
                    }
                }
                return false;
            }
        }
    }
}
