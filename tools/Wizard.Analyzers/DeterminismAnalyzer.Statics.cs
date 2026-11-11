using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wizard.Analyzers
{
    public sealed partial class DeterminismAnalyzer
    {
        // 가변 정적 상태(WZ0010). 서버는 여러 세계를 여러 스레드에서 동시에 돌린다(T-42)
        private sealed partial class Checker
        {
            // 정적 배열을 둘 수 있는 생성 표(TableGen 의 출력, ARCHITECTURE 3.4). 경로는 저장소 기준이다.
            // 표를 더하는 일은 이 목록을 고치는 분석기 작업과 같이 한다.
            private static readonly string[] GeneratedTableFiles =
            {
                "/src/Wizard.Core/Generated/SinTable.g.cs",
                "/src/Wizard.Core/Generated/PowTables.g.cs",
            };

            public void OnField(SymbolAnalysisContext ctx)
            {
                var f = (IFieldSymbol)ctx.Symbol;
                if (!_core || !f.IsStatic || f.IsConst || f.IsImplicitlyDeclared || f.AssociatedSymbol != null)
                {
                    return;   // 자동 속성·이벤트의 뒤 필드는 OnProperty·OnEvent 가 본다
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
                bool autoProperty = p.ContainingType.GetMembers().OfType<IFieldSymbol>()
                    .Any(f => SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, p));
                if (p.SetMethod != null || (autoProperty && !IsImmutableValue(p.Type, p.Locations)))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.MutableStatic, p.Locations.FirstOrDefault() ?? Location.None, _asm, p.Name));
                }
            }

            public void OnEvent(SymbolAnalysisContext ctx)
            {
                var e = (IEventSymbol)ctx.Symbol;
                if (_core && e.IsStatic && !e.IsImplicitlyDeclared)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(Rules.MutableStatic, e.Locations.FirstOrDefault() ?? Location.None, _asm, e.Name));
                }
            }

            // 불변 값: 원시형, 열거형, 문자열, 인스턴스 필드가 전부 불변 값인 readonly struct. 배열은 생성 표 허용 목록의 파일에서만.
            // readonly struct 라도 배열·가변 객체를 감싸면(Memory<T>, ArraySegment<T>, KeyValuePair<int, int[]>, 배열 필드를 가진 구조체)
            // 정적 임시 버퍼가 되므로 막는다(T-42). 참조 어셈블리의 구조체는 감춘 참조 필드를 object _dummy 로 드러내므로 같이 걸린다.
            private bool IsImmutableValue(ITypeSymbol t, ImmutableArray<Location> locations)
            {
                if (IsImmutableScalar(t))
                {
                    return true;
                }
                if (t.TypeKind == TypeKind.Struct && t.IsReadOnly)
                {
                    return IsDeeplyImmutableStruct((INamedTypeSymbol)t, 0);
                }
                if (t is IArrayTypeSymbol && _asm == "Wizard.Core" && !locations.IsEmpty)
                {
                    foreach (Location loc in locations)
                    {
                        if (!IsGeneratedTableFile(loc.SourceTree?.FilePath))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            private static bool IsImmutableScalar(ITypeSymbol t) =>
                t.TypeKind == TypeKind.Enum || t.SpecialType == SpecialType.System_String || (t.SpecialType != SpecialType.None && t.IsValueType);

            // 생성된(제네릭 인자를 채운) 타입의 필드를 본다. 자동 속성의 뒤 필드도 인스턴스 필드다.
            private static bool IsDeeplyImmutableStruct(INamedTypeSymbol s, int depth)
            {
                if (depth > 8)
                {
                    return false;
                }
                foreach (ISymbol member in s.GetMembers())
                {
                    if (!(member is IFieldSymbol f) || f.IsStatic || f.IsConst)
                    {
                        continue;
                    }
                    ITypeSymbol ft = f.Type;
                    if (IsImmutableScalar(ft))
                    {
                        continue;
                    }
                    if (ft.TypeKind == TypeKind.Struct && ft.IsReadOnly && !SymbolEqualityComparer.Default.Equals(ft, s)
                        && IsDeeplyImmutableStruct((INamedTypeSymbol)ft, depth + 1))
                    {
                        continue;
                    }
                    return false;
                }
                return true;
            }

            private static bool IsGeneratedTableFile(string? path)
            {
                string p = SymbolUtil.NormalizePath(path);
                foreach (string allowed in GeneratedTableFiles)
                {
                    if (p.EndsWith(allowed, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
