using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Wizard.Analyzers
{
    public sealed partial class DeterminismAnalyzer
    {
        // WZ0007 의 한 갈래: 기본 비교자로 문자열을 비교하는 길(코어 세 어셈블리).
        // string 의 기본 순서(Comparer<string>.Default, IComparable<string>, System.Collections.Comparer.Default)는 현재 문화권을 따른다.
        // 같은 문자열 집합이 ICU 와 InvariantGlobalization 에서 다른 순서로 나온다("block.fireball", "boss.kiln_boar" 같은 id 꼴도 갈린다).
        // 비교자를 명시한 꼴(new SortedSet<string>(StringComparer.Ordinal), list.BinarySearch(k, StringComparer.Ordinal))은 허용한다.
        private sealed partial class Checker
        {
            // 비교자를 주지 않으면 Comparer<TKey>.Default 로 키를 정렬하는 컬렉션
            private static readonly string[] SortedCollections =
            {
                "System.Collections.Generic.SortedSet`1", "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.SortedList`2",
                "System.Collections.SortedList",
            };

            // Empty 가 기본 비교자를 쓰는 불변 정렬 컬렉션
            private static readonly string[] ImmutableSortedCollections =
            {
                "System.Collections.Immutable.ImmutableSortedSet`1", "System.Collections.Immutable.ImmutableSortedDictionary`2",
            };

            // 비교자를 주지 않으면 기본 비교자를 쓰는 만드는 메서드(정적 클래스, 확장 메서드)
            private static readonly string[] ImmutableSortedFactories =
            {
                "System.Collections.Immutable.ImmutableSortedSet", "System.Collections.Immutable.ImmutableSortedDictionary",
            };

            private static readonly string[] BinarySearchOwners =
            {
                "System.Array", "System.Collections.Generic.List`1", "System.Collections.ArrayList", "System.Collections.Immutable.ImmutableArray`1",
                "System.Collections.Immutable.ImmutableArray", "System.Collections.Immutable.ImmutableList`1",
            };

            private static readonly string[] MinMaxMethods = { "Min", "Max", "MinBy", "MaxBy" };

            // 값을 이 인터페이스로 바꾸면 받는 쪽이 문자열의 기본 순서로 비교한다
            private static readonly string[] ComparableInterfaces =
            {
                "System.IComparable", "System.IComparable`1", "System.Collections.IStructuralComparable",
            };

            private static readonly string[] TupleTypes =
            {
                "System.ValueTuple`1", "System.ValueTuple`2", "System.ValueTuple`3", "System.ValueTuple`4", "System.ValueTuple`5",
                "System.ValueTuple`6", "System.ValueTuple`7", "System.ValueTuple`8", "System.Tuple`1", "System.Tuple`2", "System.Tuple`3",
                "System.Tuple`4", "System.Tuple`5", "System.Tuple`6", "System.Tuple`7", "System.Tuple`8", "System.Nullable`1",
            };

            // ---------- 부르는 곳 ----------
            // inv 가 null 이면 메서드 그룹이다(인자를 모르므로 매개변수 목록만 본다)
            private static string? DefaultComparerCall(IMethodSymbol m, string owner, IInvocationOperation? inv, Compilation comp)
            {
                // BinarySearch(비교자 없음): 원소를 기본 비교자로 비교한다
                if (m.Name == "BinarySearch" && Array.IndexOf(BinarySearchOwners, owner) >= 0 && !GivesComparer(m, inv))
                {
                    ITypeSymbol? element = BinarySearchElement(m, inv);
                    if (element == null || CouldOrderStrings(element, comp))
                    {
                        return owner + ".BinarySearch (default comparer)";
                    }
                }

                // LINQ Min·Max·MinBy·MaxBy(비교자 없음): 비교하는 값이 문자열일 수 있다
                if ((owner == "System.Linq.Enumerable" || owner == "System.Linq.Queryable") && Array.IndexOf(MinMaxMethods, m.Name) >= 0
                    && !GivesComparer(m, inv))
                {
                    ITypeSymbol? compared = MinMaxCompared(m);
                    if (compared != null && CouldOrderStrings(compared, comp))
                    {
                        return "Enumerable." + m.Name + " (default comparer)";
                    }
                }

                // ImmutableSortedSet.Create…, ToImmutableSortedSet(), ToImmutableSortedDictionary(…)(비교자 없음)
                if (Array.IndexOf(ImmutableSortedFactories, owner) >= 0 && m.IsGenericMethod && !GivesComparer(m, inv)
                    && CouldOrderStrings(m.TypeArguments[KeyTypeArgument(m)], comp))
                {
                    return owner + "." + m.Name + " (default comparer)";
                }

                // CompareTo: 튜플(원소마다 기본 비교자), IComparable<string>, 문자열을 담은 IComparable·IStructuralComparable
                if (m.Name == "CompareTo" && owner != "System.String")
                {
                    INamedTypeSymbol type = m.ContainingType;
                    string def = SymbolUtil.MetadataName(type);
                    bool tuple = Array.IndexOf(TupleTypes, def) >= 0;
                    bool generic = def == "System.IComparable`1";
                    bool plain = def == "System.IComparable" || def == "System.Collections.IStructuralComparable";
                    // IComparable<T>·IComparable 로 제약한 타입 매개변수의 CompareTo 는 그 T 를 문자열로 채우는 자리에서 잡는다
                    ITypeSymbol? receiver = inv?.Instance == null ? null : SymbolUtil.Unconvert(inv.Instance).Type;
                    if ((tuple && CouldOrderStrings(type, comp))
                        || (generic && !(type.TypeArguments[0] is ITypeParameterSymbol) && CouldOrderStrings(type.TypeArguments[0], comp))
                        || (plain && !(receiver is ITypeParameterSymbol) && (receiver == null || CouldOrderStrings(receiver, comp))))
                    {
                        return type.ToDisplayString() + ".CompareTo";
                    }
                }

                // T : IComparable<T> 로 제약한 메서드에 문자열을 넣는다(사용자 정의 정렬·이진 탐색 도우미가 string.CompareTo 를 부른다)
                if (m.IsGenericMethod)
                {
                    string? bound = ComparableBoundToString(m.TypeParameters, m.TypeArguments, comp);
                    if (bound != null)
                    {
                        return m.ToDisplayString() + " with " + bound;
                    }
                }
                return null;
            }

            private static ITypeSymbol? BinarySearchElement(IMethodSymbol m, IInvocationOperation? inv)
            {
                if (m.IsGenericMethod)
                {
                    return m.TypeArguments[0];
                }
                if (m.ContainingType.IsGenericType)
                {
                    return m.ContainingType.TypeArguments[0];
                }
                // Array.BinarySearch(Array, object), ArrayList.BinarySearch(object): 넘긴 배열의 원소 타입, 모르면 object
                if (inv != null && inv.Arguments.Length > 0 && SymbolUtil.Unconvert(inv.Arguments[0].Value).Type is IArrayTypeSymbol arr)
                {
                    return arr.ElementType;
                }
                return null;
            }

            // Min·Max: 선택자가 있으면 선택자의 결과, 없으면 원소. MinBy·MaxBy: 키
            private static ITypeSymbol? MinMaxCompared(IMethodSymbol m)
            {
                if (m.Name == "MinBy" || m.Name == "MaxBy")
                {
                    return m.TypeArguments.Length > 1 ? m.TypeArguments[1] : null;
                }
                foreach (IParameterSymbol p in m.Parameters)
                {
                    INamedTypeSymbol? f = SelectorDelegate(p.Type);
                    if (f != null && f.TypeArguments.Length > 0)
                    {
                        return f.TypeArguments[f.TypeArguments.Length - 1];
                    }
                }
                return m.TypeArguments.Length > 0 ? m.TypeArguments[0] : null;
            }

            // Func<…> 또는 Expression<Func<…>>
            private static INamedTypeSymbol? SelectorDelegate(ITypeSymbol t)
            {
                if (!(t is INamedTypeSymbol n))
                {
                    return null;
                }
                if (SymbolUtil.MetadataName(n) == "System.Linq.Expressions.Expression`1")
                {
                    return SelectorDelegate(n.TypeArguments[0]);
                }
                return n.TypeKind == TypeKind.Delegate ? n : null;
            }

            // ImmutableSortedDictionary 의 키는 첫 번째 타입 인자다. ToImmutableSortedDictionary<TSource, TKey, TValue>(…) 는 두 번째다
            private static int KeyTypeArgument(IMethodSymbol m) =>
                m.Name == "ToImmutableSortedDictionary" && m.TypeArguments.Length == 3 ? 1 : 0;

            // 매개변수에 IComparer 가 있고, 그 인자가 null(= 기본 비교자)이 아니다
            private static bool GivesComparer(IMethodSymbol m, IInvocationOperation? inv)
            {
                if (!m.Parameters.Any(p => IsComparerType(p.Type)))
                {
                    return false;
                }
                if (inv == null)
                {
                    return true;
                }
                return !inv.Arguments.Any(a => a.Parameter != null && IsComparerType(a.Parameter.Type) && IsNullValue(a.Value));
            }

            private static bool IsComparerType(ITypeSymbol t)
            {
                string name = t is INamedTypeSymbol n ? SymbolUtil.MetadataName(n) : string.Empty;
                return name == "System.Collections.Generic.IComparer`1" || name == "System.Collections.IComparer";
            }

            private static bool IsNullValue(IOperation value)
            {
                IOperation v = SymbolUtil.Unconvert(value);
                return v is IDefaultValueOperation || (v.ConstantValue.HasValue && v.ConstantValue.Value == null);
            }

            // ---------- 만드는 곳: new SortedSet<string>(), new SortedDictionary<string, V>(…) ----------
            public void OnObjectCreation(OperationAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                var oc = (IObjectCreationOperation)ctx.Operation;
                if (oc.Constructor == null || !(oc.Type is INamedTypeSymbol type))
                {
                    return;
                }
                string def = SymbolUtil.MetadataName(type);
                if (Array.IndexOf(SortedCollections, def) < 0)
                {
                    return;
                }
                ITypeSymbol key = type.IsGenericType ? type.TypeArguments[0] : type;   // 비제네릭 SortedList 의 키는 object
                if (type.IsGenericType ? CouldOrderStrings(key, ctx.Compilation) : true)
                {
                    if (!GivesComparer(oc.Constructor, oc.Arguments))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, oc.Syntax.GetLocation(), _asm,
                            type.ToDisplayString() + " (default comparer)"));
                    }
                }
            }

            private static bool GivesComparer(IMethodSymbol ctor, System.Collections.Immutable.ImmutableArray<IArgumentOperation> args)
            {
                if (!ctor.Parameters.Any(p => IsComparerType(p.Type)))
                {
                    return false;
                }
                return !args.Any(a => a.Parameter != null && IsComparerType(a.Parameter.Type) && IsNullValue(a.Value));
            }

            // ---------- 멤버 이름: Comparer<string>.Default(속성), ImmutableSortedSet<string>.Empty(필드) ----------
            private static string? DefaultComparerMember(ISymbol p, Compilation comp)
            {
                if (!(p is IPropertySymbol) && !(p is IFieldSymbol))
                {
                    return null;
                }
                INamedTypeSymbol type = p.ContainingType;
                string def = SymbolUtil.MetadataName(type);
                if (p.Name == "Default" && def == "System.Collections.Generic.Comparer`1" && CouldOrderStrings(type.TypeArguments[0], comp))
                {
                    return type.ToDisplayString() + ".Default";
                }
                if (p.Name == "Empty" && Array.IndexOf(ImmutableSortedCollections, def) >= 0 && CouldOrderStrings(type.TypeArguments[0], comp))
                {
                    return type.ToDisplayString() + ".Empty";
                }
                return null;
            }

            // ---------- 바꾸기: (IComparable<string>)a, 문자열 튜플 → IComparable ----------
            private static string? ComparableConversion(IConversionOperation conv, Compilation comp)
            {
                ITypeSymbol? from = conv.Operand.Type;
                if (from == null || from is ITypeParameterSymbol || from.TypeKind == TypeKind.Interface
                    || from.SpecialType == SpecialType.System_Object || conv.OperatorMethod != null)
                {
                    return null;
                }
                if (conv.Type is INamedTypeSymbol to && Array.IndexOf(ComparableInterfaces, SymbolUtil.MetadataName(to)) >= 0
                    && CouldOrderStrings(from, comp))
                {
                    return from.ToDisplayString() + " as " + to.ToDisplayString();
                }
                return null;
            }

            // ---------- 타입 이름: Helper<string>(class Helper<T> where T : IComparable<T>) ----------
            public void OnGenericName(SyntaxNodeAnalysisContext ctx)
            {
                if (!_core)
                {
                    return;
                }
                var node = (GenericNameSyntax)ctx.Node;
                if (!(ctx.SemanticModel.GetSymbolInfo(node, ctx.CancellationToken).Symbol is INamedTypeSymbol type))
                {
                    return;
                }
                string? bound = ComparableBoundToString(type.TypeParameters, type.TypeArguments, ctx.Compilation);
                if (bound != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.CultureString, node.GetLocation(), _asm, type.ToDisplayString() + " with " + bound));
                }
            }

            // IComparable·IComparable<T> 로 제약한 타입 매개변수에 문자열(또는 문자열을 담은 튜플)이 들어갔는가.
            // 타입 매개변수를 그대로 넘기는 것은 그것을 실제 타입으로 채우는 바깥 자리에서 잡힌다(제약을 만족하려면 같은 제약이 필요하다).
            private static string? ComparableBoundToString(System.Collections.Immutable.ImmutableArray<ITypeParameterSymbol> parameters,
                System.Collections.Immutable.ImmutableArray<ITypeSymbol> arguments, Compilation comp)
            {
                for (int i = 0; i < parameters.Length && i < arguments.Length; i++)
                {
                    ITypeSymbol arg = arguments[i];
                    if (arg is ITypeParameterSymbol || arg.SpecialType == SpecialType.System_Object)
                    {
                        continue;
                    }
                    bool comparable = parameters[i].ConstraintTypes.Any(c =>
                        c is INamedTypeSymbol n && Array.IndexOf(ComparableInterfaces, SymbolUtil.MetadataName(n)) >= 0);
                    if (comparable && CouldOrderStrings(arg, comp))
                    {
                        return parameters[i].Name + " = " + arg.ToDisplayString();
                    }
                }
                return null;
            }

            // ---------- 문자열을 기본 순서로 비교할 수 있는 타입 ----------
            // string, object·dynamic, string 이 구현한 인터페이스(IComparable, IEnumerable<char> …), string 이 들어갈 수 있는 타입 매개변수,
            // 그런 원소를 담은 튜플·Nullable. Comparer<T>.Default 는 이 타입들에서 실체가 문자열이면 string.CompareTo(문화권)를 부른다.
            private static bool CouldOrderStrings(ITypeSymbol? t, Compilation comp) => CouldOrderStrings(t, comp, 0);

            private static bool CouldOrderStrings(ITypeSymbol? t, Compilation comp, int depth)
            {
                if (t == null || depth > 8)
                {
                    return false;
                }
                if (t.SpecialType == SpecialType.System_String || t.SpecialType == SpecialType.System_Object || t.TypeKind == TypeKind.Dynamic)
                {
                    return true;
                }
                if (t is ITypeParameterSymbol tp)
                {
                    return StringSatisfies(tp, comp, depth);
                }
                if (!(t is INamedTypeSymbol n))
                {
                    return false;
                }
                if (n.TypeKind == TypeKind.Interface)
                {
                    return StringImplements(n, comp);
                }
                if (Array.IndexOf(TupleTypes, SymbolUtil.MetadataName(n)) >= 0)
                {
                    return n.TypeArguments.Any(a => CouldOrderStrings(a, comp, depth + 1));
                }
                return false;
            }

            private static bool StringImplements(INamedTypeSymbol iface, Compilation comp)
            {
                INamedTypeSymbol str = comp.GetSpecialType(SpecialType.System_String);
                return str.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface));
            }

            // 타입 매개변수에 string 이 들어갈 수 있는가(struct·unmanaged 제약, string 이 아닌 클래스 제약, string 이 구현하지 않은 인터페이스 제약이면 아니다)
            private static bool StringSatisfies(ITypeParameterSymbol tp, Compilation comp, int depth)
            {
                if (tp.HasValueTypeConstraint || tp.HasUnmanagedTypeConstraint)
                {
                    return false;
                }
                foreach (ITypeSymbol c in tp.ConstraintTypes)
                {
                    switch (c)
                    {
                        case ITypeParameterSymbol inner:
                            if (!StringSatisfies(inner, comp, depth + 1))
                            {
                                return false;
                            }
                            break;
                        case INamedTypeSymbol n when n.TypeKind == TypeKind.Interface:
                            // IComparable<T>(T 자신)처럼 자기 자신을 인자로 쓰는 제약은 string 이 IComparable<string> 으로 만족한다
                            if (!StringImplements(n, comp) && !IsSelfComparable(n, tp))
                            {
                                return false;
                            }
                            break;
                        default:
                            if (c.SpecialType != SpecialType.System_Object)
                            {
                                return false;
                            }
                            break;
                    }
                }
                return true;
            }

            private static bool IsSelfComparable(INamedTypeSymbol iface, ITypeParameterSymbol tp) =>
                iface.IsGenericType && iface.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], tp)
                && (SymbolUtil.MetadataName(iface) == "System.IComparable`1" || SymbolUtil.MetadataName(iface) == "System.IEquatable`1");
        }
    }
}
