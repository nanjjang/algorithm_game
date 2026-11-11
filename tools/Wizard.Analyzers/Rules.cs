using Microsoft.CodeAnalysis;

namespace Wizard.Analyzers
{
    // 진단 목록. 번호는 추가만 한다. ARCHITECTURE 3.2·3.3·11, DECISIONS-TECH T-11·T-18·T-33·T-42, spec 12 10.7.
    public static class Rules
    {
        private const string Determinism = "Determinism";
        private const string Layering = "Layering";
        private const string Language = "Language";
        private const string Payments = "Payments";

        // 억제할 수 없게 만든다(T-42 "억제도 오류"). 끄려는 시도 자체는 WZ0016 으로 따로 알린다.
        //   NotConfigurable — #pragma, NoWarn, WarningsNotAsErrors, .editorconfig·전역 설정의 심각도(묶음 설정 포함)가 듣지 않는다.
        //   Compiler        — SuppressMessage·UnconditionalSuppressMessage 가 듣지 않는다(컴파일러 진단은 특성으로 억제되지 않는다).
        // 두 꼬리표가 없으면 [assembly: SuppressMessage(…, "WZ0016")] 하나로 억제 검사까지 꺼진다. SDK 10.0.201 빌드에서 확인했다(M0-BT-02).
        private static DiagnosticDescriptor Error(string id, string title, string message, string category) =>
            new DiagnosticDescriptor(id, title, message, category, DiagnosticSeverity.Error, isEnabledByDefault: true,
                customTags: new[] { WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.Compiler });

        public static readonly DiagnosticDescriptor FloatingPoint = Error("WZ0001",
            "Floating-point type in integer-only assembly",
            "'{0}' uses floating-point type '{1}'; use Fix64 or integer units (Ticks, Hu, Centi, Permille, Bp)", Determinism);

        public static readonly DiagnosticDescriptor SystemRandom = Error("WZ0002",
            "System.Random in deterministic core",
            "'{0}' must not use System.Random; use Pcg32 from RngStreams.Derive", Determinism);

        public static readonly DiagnosticDescriptor Clock = Error("WZ0003",
            "Wall clock in deterministic or clockless code",
            "'{0}' must not read the clock ('{1}'); use tick numbers or pass time in as an argument", Determinism);

        public static readonly DiagnosticDescriptor NewGuid = Error("WZ0004",
            "Guid.NewGuid in deterministic core",
            "'{0}' must not call Guid.NewGuid; ids come from the world counter or the server", Determinism);

        public static readonly DiagnosticDescriptor HashIteration = Error("WZ0005",
            "Iteration over hash-ordered collection",
            "'{0}' iterates '{1}' whose order is not defined; use arrays, SortedIntList or fixed pools (Dictionary is lookup-only)", Determinism);

        public static readonly DiagnosticDescriptor UnstableSort = Error("WZ0006",
            "Unstable or unspecified sort",
            "'{0}' must not call '{1}'; use DetSort with a total-order comparer", Determinism);

        public static readonly DiagnosticDescriptor CultureString = Error("WZ0007",
            "Culture-dependent string operation in deterministic core",
            "'{0}' calls culture-dependent '{1}'; use ordinal comparison, invariant culture or integer ids", Determinism);

        public static readonly DiagnosticDescriptor Threading = Error("WZ0008",
            "Threading in deterministic core",
            "'{0}' must not use '{1}'; a world runs on one thread", Determinism);

        public static readonly DiagnosticDescriptor HashCodeOrder = Error("WZ0009",
            "GetHashCode in deterministic core logic",
            "'{0}' calls GetHashCode outside a GetHashCode override; hash codes differ between runtimes", Determinism);

        public static readonly DiagnosticDescriptor MutableStatic = Error("WZ0010",
            "Mutable static state in deterministic core",
            "Static member '{1}' in '{0}' is mutable; only const and immutable static readonly values are allowed (generated tables excepted)", Determinism);

        public static readonly DiagnosticDescriptor ThreadStatic = Error("WZ0011",
            "ThreadStatic in deterministic core",
            "'{0}' must not use [ThreadStatic] or ThreadLocal; buffers belong to the world or the caller", Determinism);

        public static readonly DiagnosticDescriptor UnsafeCode = Error("WZ0012",
            "Unsafe code in deterministic core",
            "'{0}' must not use unsafe code, pointers or fixed", Determinism);

        public static readonly DiagnosticDescriptor MemoryReinterpret = Error("WZ0013",
            "Memory reinterpretation in deterministic core",
            "'{0}' must not use '{1}'; use ByteWriter, ByteReader or StateWriter", Determinism);

        public static readonly DiagnosticDescriptor RawDivision = Error("WZ0014",
            "Raw division by a non-constant",
            "'{0}' divides by a non-constant with raw '{1}'; use FixMath.DivInt/DivLong/ModInt/ModLong/MulDiv or Wide", Determinism);

        public static readonly DiagnosticDescriptor CheckedContext = Error("WZ0015",
            "checked arithmetic in deterministic core",
            "'{0}' must not use 'checked'; arithmetic wraps in every build", Determinism);

        public static readonly DiagnosticDescriptor Suppression = Error("WZ0016",
            "Diagnostic suppression",
            "'{0}' must not suppress or downgrade diagnostics ({1})", Determinism);

        public static readonly DiagnosticDescriptor OutsideCoreHash = Error("WZ0017",
            "Reference to code outside the core version hash",
            "'{0}' must not reference '{1}' (Wizard.Content.Rules, ErrorCode, Error, Result, Unit are outside the core version hash)", Layering);

        public static readonly DiagnosticDescriptor Sockets = Error("WZ0019",
            "Socket in netcode core",
            "'{0}' must not use sockets ('{1}'); Wizard.Netcode takes packets and time as arguments", Layering);

        public static readonly DiagnosticDescriptor PaidValue = Error("WZ0020",
            "Paid currency value in enhance, match or league code",
            "'{0}' must not read paid value '{1}'", Payments);

        public static readonly DiagnosticDescriptor ForbiddenSyntax = Error("WZ0021",
            "C# feature not available to Unity",
            "'{0}' uses '{1}', which shared packages must not use (ARCHITECTURE 3.3)", Language);

        public static readonly DiagnosticDescriptor ForbiddenApi = Error("WZ0022",
            "API not available in shared packages",
            "'{0}' uses '{1}', which is not available to Unity shared packages (ARCHITECTURE 3.3)", Language);

        public static readonly DiagnosticDescriptor LayerReference = Error("WZ0023",
            "Forbidden assembly reference",
            "'{0}' must not reference '{1}' (ARCHITECTURE 3.2)", Layering);

        public static readonly DiagnosticDescriptor NamespaceFolder = Error("WZ0024",
            "Namespace does not match folder",
            "Namespace '{1}' does not match the folder of '{0}' ({2}; CLAUDE.md 6, CONTRACTS 0.1)", Layering);

        // 분석기가 내는 진단 전부(심각도를 낮추는 설정을 찾을 때 쓴다)
        public static readonly DiagnosticDescriptor[] All =
        {
            FloatingPoint, SystemRandom, Clock, NewGuid, HashIteration, UnstableSort, CultureString, Threading, HashCodeOrder, MutableStatic,
            ThreadStatic, UnsafeCode, MemoryReinterpret, RawDivision, CheckedContext, Suppression, OutsideCoreHash, Sockets, PaidValue,
            ForbiddenSyntax, ForbiddenApi, LayerReference, NamespaceFolder,
        };
    }
}
