using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Wizard.Analyzers
{
    public sealed partial class DeterminismAnalyzer
    {
        // 연산 검사: 불안정 정렬(공유 전부), 코어 세 어셈블리의 LINQ 정렬·문화권·GetHashCode·해시 순서 순회·원시 나눗셈
        private sealed partial class Checker
        {
            // 원시 나눗셈을 허용하는 도우미(CONTRACTS 1.1·1.2)
            private static readonly string[] DivisionHelperTypes = { "Wizard.Core.FixMath", "Wizard.Core.Wide" };

            // 순회 순서가 정해지지 않은 컬렉션(T-11). 인터페이스로 받아도 실체는 해시 표다
            private static readonly string[] HashOrderedTypes =
            {
                "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1",
                "System.Collections.Generic.Dictionary`2+KeyCollection", "System.Collections.Generic.Dictionary`2+ValueCollection",
                "System.Collections.Hashtable", "System.Collections.Concurrent.ConcurrentDictionary`2", "System.Collections.Concurrent.ConcurrentBag`1",
                "System.Collections.Generic.IDictionary`2", "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.ISet`1",
                "System.Collections.Generic.IReadOnlySet`1", "System.Collections.IDictionary",
            };

            private static readonly string[] DictionaryInterfaces =
            {
                "System.Collections.Generic.IDictionary`2", "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.IDictionary",
            };

            // 이 타입으로 바뀌면 받는 쪽이 순회한다(LINQ, 생성자, string.Join …)
            private static readonly string[] EnumerableInterfaces =
            {
                "System.Collections.IEnumerable", "System.Collections.ICollection", "System.Collections.Generic.IEnumerable`1",
                "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IReadOnlyCollection`1",
            };

            private static readonly string[] OrderingMethods = { "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "Order", "OrderDescending" };

            private static readonly string[] SortOwners =
            {
                "System.Array", "System.Collections.Generic.List`1", "System.MemoryExtensions", "System.Collections.ArrayList",
                "System.Collections.Immutable.ImmutableArray`1", "System.Collections.Immutable.ImmutableArray", "System.Collections.Immutable.ImmutableList`1",
            };

            // 불안정 정렬(Array.Sort, List<T>.Sort …)은 공유 어셈블리 전부에서 막는다(ARCHITECTURE 3.3 표의 마지막 행).
            // LINQ OrderBy 와 나머지 금지 목록은 코어 세 어셈블리만이다(T-11, ARCHITECTURE 11).
            // 코어 밖에서는 3.3 표의 '대신' 칸("완전 순서 비교자")이 기계로 확인되는 꼴 하나만 허용한다:
            // 문자열 원소 하나의 열(keys·items 짝이 아님)을 StringComparer.Ordinal 로 정렬한다. 서수로 같은 두 문자열은 글자가 같아
            // 정렬이 불안정해도 결과가 런타임마다 같다. 비교자 없는 Sort(), 람다 비교자, OrdinalIgnoreCase 는 막는다. 코어는 예외 없이 막는다(T-11).
            private bool IsUnstableSort(IMethodSymbol m, string owner, IInvocationOperation? inv) =>
                m.Name == "Sort" && Array.IndexOf(SortOwners, owner) >= 0 && (_core || inv == null || !IsOrdinalStringSort(m, inv));

            private static bool IsOrdinalStringSort(IMethodSymbol m, IInvocationOperation inv)
            {
                string owner = SymbolUtil.MetadataName(m.ContainingType);
                ITypeSymbol? element = null;
                if (owner == "System.Collections.Generic.List`1")
                {
                    element = m.ContainingType.TypeArguments[0];
                }
                else if (owner == "System.Array" && m.IsGenericMethod && m.TypeArguments.Length == 1)
                {
                    element = m.TypeArguments[0];
                }
                if (element == null || element.SpecialType != SpecialType.System_String || m.Parameters.Any(p => p.Name == "items"))
                {
                    return false;
                }
                return inv.Arguments.Any(a => a.Parameter != null && IsComparerType(a.Parameter.Type)
                    && SymbolUtil.Unconvert(a.Value) is IPropertyReferenceOperation p && p.Property.Name == "Ordinal"
                    && SymbolUtil.MetadataName(p.Property.ContainingType) == "System.StringComparer");
            }

            // 메서드 그룹으로 넘기는 것(Action<int[]> f = Array.Sort; Comparer<string>.Create(string.Compare))도 부르는 것과 같다
            public void OnMethodReference(OperationAnalysisContext ctx)
            {
                var r = (IMethodReferenceOperation)ctx.Operation;
                IMethodSymbol m = r.Method;
                string owner = SymbolUtil.MetadataName(m.ContainingType);
                Location loc = r.Syntax.GetLocation();
                if (IsUnstableSort(m, owner, null))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnstableSort, loc, _asm, owner + "." + m.Name));
                }
                if (!_core)
                {
                    return;
                }
                string? culture = CultureProblem(m, owner, null) ?? DefaultComparerCall(m, owner, null, ctx.Compilation);
                if (culture != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, loc, _asm, culture));
                }
            }

            public void OnInvocation(OperationAnalysisContext ctx)
            {
                var inv = (IInvocationOperation)ctx.Operation;
                IMethodSymbol m = inv.TargetMethod;
                string owner = SymbolUtil.MetadataName(m.ContainingType);
                Location loc = inv.Syntax.GetLocation();

                if (IsUnstableSort(m, owner, inv))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnstableSort, loc, _asm, owner + "." + m.Name));
                }
                if (!_core)
                {
                    return;
                }
                if ((owner == "System.Linq.Enumerable" || owner == "System.Linq.Queryable") && Array.IndexOf(OrderingMethods, m.Name) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.UnstableSort, loc, _asm, owner + "." + m.Name));
                }

                if (m.Name == "GetHashCode" && !InsideGetHashCode(ctx.ContainingSymbol))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashCodeOrder, loc, _asm));
                }

                // 해시 표의 열거자·복사(순회 순서가 그대로 나온다)
                if ((m.Name == "GetEnumerator" || m.Name == "CopyTo") && inv.Instance != null && IsHashOrdered(SymbolUtil.Unconvert(inv.Instance).Type))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashIteration, loc, _asm, SymbolUtil.Unconvert(inv.Instance).Type!.ToDisplayString()));
                }

                string? culture = CultureProblem(m, owner, inv) ?? DefaultComparerCall(m, owner, inv, ctx.Compilation);
                if (culture != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, loc, _asm, culture));
                }
            }

            // inv 가 null 이면 메서드 그룹이다(인자를 모른다)
            private static string? CultureProblem(IMethodSymbol m, string owner, IInvocationOperation? inv)
            {
                bool hasComparison = m.Parameters.Any(p => p.Type.Name == "StringComparison");
                bool hasProvider = m.Parameters.Any(p => p.Type.Name == "IFormatProvider" || p.Type.Name == "CultureInfo");
                bool firstIsString = m.Parameters.Length > 0 && m.Parameters[0].Type.SpecialType == SpecialType.System_String;
                switch (owner)
                {
                    case "System.String":
                        return StringCultureProblem(m, inv, hasComparison, hasProvider, firstIsString);
                    case "System.Char":
                        return m.Name == "ToUpper" || m.Name == "ToLower" ? "char." + m.Name : null;
                    case "System.Convert":
                        if (m.Name.StartsWith("To", StringComparison.Ordinal) && !hasProvider && m.Parameters.Length > 0
                            && (firstIsString || (m.Name == "ToString" && SymbolUtil.IsCultureFormattedNumber(m.Parameters[0].Type))))
                        {
                            return "Convert." + m.Name;
                        }
                        return null;
                    case "System.StringComparer":
                        // StringComparer.Create(culture, …), FromComparison(CurrentCulture*·InvariantCulture*)
                        if (m.Name == "Create")
                        {
                            return "StringComparer.Create";
                        }
                        if (m.Name == "FromComparison" && (inv == null || HasCultureComparisonArgument(inv)))
                        {
                            return "StringComparer.FromComparison";
                        }
                        return null;
                    case "System.Text.StringBuilder":
                        if (m.Name == "AppendFormat" && !hasProvider)
                        {
                            return "StringBuilder.AppendFormat";
                        }
                        if ((m.Name == "Append" || m.Name == "Insert") && m.Parameters.Any(p => SymbolUtil.IsCultureFormattedNumber(p.Type)))
                        {
                            return "StringBuilder." + m.Name + "(number)";
                        }
                        return null;
                }
                if (SymbolUtil.IsCultureFormattedNumber(m.ContainingType) && (m.Name == "ToString" || m.Name == "Parse" || m.Name == "TryParse") && !hasProvider)
                {
                    return m.ContainingType.Name + "." + m.Name;
                }
                return null;
            }

            // StringComparison 인자가 문화권 값(CurrentCulture* = 0,1, InvariantCulture* = 2,3)이거나 상수가 아니다
            private static bool HasCultureComparisonArgument(IInvocationOperation inv)
            {
                foreach (IArgumentOperation arg in inv.Arguments)
                {
                    if (arg.Parameter?.Type.Name == "StringComparison"
                        && (!arg.Value.ConstantValue.HasValue || (arg.Value.ConstantValue.Value is int v && v < 4)))
                    {
                        return true;
                    }
                }
                return false;
            }

            private static string? StringCultureProblem(IMethodSymbol m, IInvocationOperation? inv, bool hasComparison, bool hasProvider, bool firstIsString)
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
                        if (!hasComparison && firstIsString)
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
                // 문화권 값이거나, 상수가 아니라 어느 쪽인지 정해지지 않은 비교 방식(메서드 그룹이면 부르는 쪽이 정한다)
                if (hasComparison && (inv == null || HasCultureComparisonArgument(inv)))
                {
                    return "StringComparison (culture)";
                }
                return null;
            }

            // ---------- 원시 나눗셈(WZ0014), 수 → 문자열 이어 붙이기(WZ0007) ----------
            public void OnBinary(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                BinaryOperatorKind kind;
                IOperation left;
                IOperation right;
                IMethodSymbol? userOp;
                if (ctx.Operation is IBinaryOperation b)
                {
                    kind = b.OperatorKind;
                    left = b.LeftOperand;
                    right = b.RightOperand;
                    userOp = b.OperatorMethod;
                }
                else if (ctx.Operation is ICompoundAssignmentOperation c)
                {
                    kind = c.OperatorKind;
                    left = c.Target;
                    right = c.Value;
                    userOp = c.OperatorMethod;
                }
                else
                {
                    return;
                }

                if (kind == BinaryOperatorKind.Add && ctx.Operation.Type?.SpecialType == SpecialType.System_String)
                {
                    foreach (IOperation side in new[] { left, right })
                    {
                        if (SymbolUtil.IsCultureFormattedNumber(SymbolUtil.Unconvert(side).Type))
                        {
                            ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, side.Syntax.GetLocation(), _asm, "string + number"));
                        }
                    }
                    return;
                }

                if ((kind != BinaryOperatorKind.Divide && kind != BinaryOperatorKind.Remainder) || userOp != null)
                {
                    return;
                }
                if (!SymbolUtil.IsIntegral(left.Type) && !SymbolUtil.IsIntegral(right.Type))
                {
                    return;
                }
                // 상수 제수는 허용한다. 다만 -1 은 MinValue ÷ -1 이 런타임마다 다르다(T-06)
                Optional<object?> constant = right.ConstantValue;
                bool minusOne = constant.HasValue && constant.Value != null && IsMinusOne(constant.Value);
                if (constant.HasValue && !minusOne)
                {
                    return;
                }
                if (InDivisionHelper(ctx.ContainingSymbol))
                {
                    return;
                }
                string op = kind == BinaryOperatorKind.Divide ? "/" : "%";
                ctx.ReportDiagnostic(Diagnostic.Create(Rules.RawDivision, ctx.Operation.Syntax.GetLocation(), _asm, minusOne ? op + " -1" : op));
            }

            private static bool IsMinusOne(object v)
            {
                switch (v)
                {
                    case int i:
                        return i == -1;
                    case long l:
                        return l == -1;
                    case short s:
                        return s == -1;
                    case sbyte sb:
                        return sb == -1;
                    default:
                        return false;
                }
            }

            private static bool InDivisionHelper(ISymbol? s)
            {
                INamedTypeSymbol? container = s as INamedTypeSymbol ?? s?.ContainingType;
                for (INamedTypeSymbol? t = container; t != null; t = t.ContainingType)
                {
                    if (Array.IndexOf(DivisionHelperTypes, SymbolUtil.MetadataName(t)) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            }

            // ---------- 해시 순서 순회(WZ0005) ----------
            public void OnForEach(OperationAnalysisContext ctx)
            {
                if (!_core || !(ctx.Operation is IForEachLoopOperation loop))
                {
                    return;
                }
                ITypeSymbol? t = SymbolUtil.Unconvert(loop.Collection).Type;
                if (IsHashOrdered(t))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashIteration, loop.Collection.Syntax.GetLocation(), _asm, t!.ToDisplayString()));
                }
            }

            // 해시 표를 IEnumerable 계열로 넘기면 받는 쪽(LINQ, 생성자, string.Join …)이 순회한다
            public void OnConversion(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                var conv = (IConversionOperation)ctx.Operation;
                ITypeSymbol? from = conv.Operand.Type;
                if (IsHashOrdered(from) && conv.Type is INamedTypeSymbol to && Array.IndexOf(EnumerableInterfaces, SymbolUtil.MetadataName(to)) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashIteration, conv.Syntax.GetLocation(), _asm, from!.ToDisplayString()));
                }
                string? comparable = ComparableConversion(conv, ctx.Compilation);
                if (comparable != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, conv.Syntax.GetLocation(), _asm, comparable));
                }
            }

            // 사전 인터페이스의 Keys·Values 는 IEnumerable 이라 타입만으로는 뒤를 쫓을 수 없다. 꺼내는 곳에서 막는다
            public void OnPropertyReference(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                var p = (IPropertyReferenceOperation)ctx.Operation;
                if ((p.Property.Name == "Keys" || p.Property.Name == "Values")
                    && Array.IndexOf(DictionaryInterfaces, SymbolUtil.MetadataName(p.Property.ContainingType)) >= 0)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.HashIteration, p.Syntax.GetLocation(), _asm, p.Property.ContainingType.ToDisplayString()));
                }
            }

            // 보간 문자열의 수는 현재 문화권으로 형식화된다
            public void OnInterpolation(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                var hole = (IInterpolationOperation)ctx.Operation;
                if (SymbolUtil.IsCultureFormattedNumber(SymbolUtil.Unconvert(hole.Expression).Type))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, hole.Syntax.GetLocation(), _asm, "interpolated number"));
                }
            }

            // 해시 순서 타입 그 자체, 그것을 상속한 클래스(class Map : Dictionary<,>), 그것을 넓힌 인터페이스,
            // 그것으로 제약한 타입 매개변수(where T : Dictionary<,>). 클래스는 기반 타입만 따라 올라간다.
            // 클래스가 구현한 인터페이스까지 보면 SortedDictionary(IDictionary`2)·SortedSet(ISet`1)처럼 순서가 정해진 것도 걸린다.
            private static bool IsHashOrdered(ITypeSymbol? t) => IsHashOrdered(t, 0);

            private static bool IsHashOrdered(ITypeSymbol? t, int depth)
            {
                if (t == null || depth > 8)
                {
                    return false;
                }
                if (t is ITypeParameterSymbol tp)
                {
                    foreach (ITypeSymbol c in tp.ConstraintTypes)
                    {
                        if (IsHashOrdered(c, depth + 1))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                if (!(t is INamedTypeSymbol n))
                {
                    return false;
                }
                if (n.TypeKind == TypeKind.Interface)
                {
                    return IsHashOrderedName(n) || n.AllInterfaces.Any(IsHashOrderedName);
                }
                for (INamedTypeSymbol? cur = n; cur != null; cur = cur.BaseType)
                {
                    if (IsHashOrderedName(cur))
                    {
                        return true;
                    }
                }
                return false;
            }

            private static bool IsHashOrderedName(INamedTypeSymbol n) => Array.IndexOf(HashOrderedTypes, SymbolUtil.MetadataName(n)) >= 0;
        }
    }
}
