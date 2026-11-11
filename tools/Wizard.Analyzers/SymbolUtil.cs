using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Wizard.Analyzers
{
    // 기호 이름과 형식 분류 도우미. 이름 비교는 전부 서수 비교다.
    internal static class SymbolUtil
    {
        // "System.Collections.Generic.Dictionary`2+KeyCollection" 꼴(원래 정의 기준)
        public static string MetadataName(INamedTypeSymbol? t)
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

        public static string NamespaceOf(INamedTypeSymbol t) =>
            t.ContainingNamespace == null || t.ContainingNamespace.IsGlobalNamespace ? string.Empty : t.ContainingNamespace.ToDisplayString();

        public static bool InNamespace(INamedTypeSymbol t, string ns)
        {
            string actual = NamespaceOf(t);
            return actual == ns || actual.StartsWith(ns + ".", StringComparison.Ordinal);
        }

        public static bool IsFloating(ITypeSymbol? t) =>
            t != null && (t.SpecialType == SpecialType.System_Single || t.SpecialType == SpecialType.System_Double
                          || t.SpecialType == SpecialType.System_Decimal);

        // 실수 성분을 가진 수학 타입(System.Numerics 의 벡터·행렬, Half)
        private static readonly string[] FloatingStructs =
        {
            "System.Half", "System.Numerics.Vector2", "System.Numerics.Vector3", "System.Numerics.Vector4", "System.Numerics.Matrix3x2",
            "System.Numerics.Matrix4x4", "System.Numerics.Quaternion", "System.Numerics.Plane", "System.Numerics.Complex",
        };

        public static bool IsFloatingLike(ITypeSymbol? t) =>
            IsFloating(t) || (t is INamedTypeSymbol n && Array.IndexOf(FloatingStructs, MetadataName(n)) >= 0);

        // 배열 원소, 제네릭 인자, 포인터까지 내려가 실수 성분을 찾는다
        public static bool ContainsFloating(ITypeSymbol t)
        {
            if (IsFloatingLike(t))
            {
                return true;
            }
            switch (t)
            {
                case IArrayTypeSymbol arr:
                    return ContainsFloating(arr.ElementType);
                case IPointerTypeSymbol ptr:
                    return ContainsFloating(ptr.PointedAtType);
                case INamedTypeSymbol n when n.IsGenericType:
                    foreach (ITypeSymbol a in n.TypeArguments)
                    {
                        if (ContainsFloating(a))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        public static bool IsIntegral(ITypeSymbol? t)
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

        // 문화권 형식에 따라 글자가 달라지는 수 타입(char 와 열거형은 문화권과 무관하다)
        public static bool IsCultureFormattedNumber(ITypeSymbol? t)
        {
            if (t == null || t.TypeKind == TypeKind.Enum || t.SpecialType == SpecialType.System_Char)
            {
                return false;
            }
            return IsIntegral(t) || IsFloatingLike(t);
        }

        public static IOperation Unconvert(IOperation op)
        {
            while (op is IConversionOperation c)
            {
                op = c.Operand;
            }
            return op;
        }

        // 저장소 기준 경로를 '/' 로 맞춘다
        public static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/');
    }
}
